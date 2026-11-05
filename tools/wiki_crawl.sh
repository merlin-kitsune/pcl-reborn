#!/usr/bin/env bash
# wiki_crawl.sh — 抓取 MC 百科（mcmod.cn）模组页，提取 CurseForge / Modrinth slug 与中文名。
#
# 用法：
#   bash tools/wiki_crawl.sh --ids FILE --out FILE [--delay MIN MAX] [--limit N] [--resume]
#                            [--backoff SEC] [--base-url URL]
#
#   --ids FILE        每行一个 class id（# 注释与空行会被忽略）
#   --out FILE        TSV 输出，追加写入：id<TAB>cfSlug<TAB>mrSlug<TAB>中文名
#                     （没有的项目留空，但 4 列 tab 分隔始终保留）
#   --delay MIN MAX   每页之间的随机等待秒数（默认 4 6）
#   --limit N         最多抓取 N 个 id
#   --resume          跳过 --out 里已经存在的 id（断点续抓）
#   --backoff SEC     被 WAF 拦截后的退避秒数（默认 600）
#   --base-url URL    站点前缀（默认 https://www.mcmod.cn）
#
# 退出码：0 正常（个别页面最终失败不算异常）；2 被拦截中止；1 参数/环境错误
set -u -o pipefail

UA='Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36'
BASE_URL='https://www.mcmod.cn'
DELAY_MIN=4
DELAY_MAX=6
LIMIT=0
RESUME=0
BACKOFF=600
PAGE_RETRIES=2          # 单页失败后最多再试 2 次（共 3 次请求）
MAX_BLOCK_STREAK=5      # 连续被拦截这么多次就收手，别把 IP 刷进黑名单
MIN_PAGE_BYTES=3000     # 正常页面远大于此；小于它且命中拦截特征 = 被 WAF 拦
IDS_FILE=''
OUT_FILE=''

log()  { printf '%s\n' "wiki_crawl: $*" >&2; }
warn() { printf '%s\n' "wiki_crawl: [warn] $*" >&2; }
die()  { printf '%s\n' "wiki_crawl: [error] $*" >&2; exit 1; }

usage() {
  cat >&2 <<'EOF'
用法: bash tools/wiki_crawl.sh --ids FILE --out FILE [--delay MIN MAX] [--limit N] [--resume]

  --ids FILE        每行一个 class id
  --out FILE        TSV 输出（id<TAB>cfSlug<TAB>mrSlug<TAB>中文名），追加写入
  --delay MIN MAX   每页随机等待秒数（默认 4 6）
  --limit N         最多抓取 N 个 id
  --resume          跳过 --out 中已存在的 id
  --backoff SEC     被 WAF 拦截后的退避秒数（默认 600）
  --base-url URL    站点前缀（默认 https://www.mcmod.cn）

退出码: 0 正常 / 2 被拦截中止 / 1 参数错误
EOF
}

is_uint() { case ${1-} in ''|*[!0-9]*) return 1 ;; *) return 0 ;; esac; }

while [ $# -gt 0 ]; do
  case $1 in
    --ids)      [ $# -ge 2 ] || die '--ids 缺少参数'; IDS_FILE=$2; shift 2 ;;
    --out)      [ $# -ge 2 ] || die '--out 缺少参数'; OUT_FILE=$2; shift 2 ;;
    --delay)    [ $# -ge 3 ] || die '--delay 需要两个参数: MIN MAX'; DELAY_MIN=$2; DELAY_MAX=$3; shift 3 ;;
    --limit)    [ $# -ge 2 ] || die '--limit 缺少参数'; LIMIT=$2; shift 2 ;;
    --resume)   RESUME=1; shift ;;
    --backoff)  [ $# -ge 2 ] || die '--backoff 缺少参数'; BACKOFF=$2; shift 2 ;;
    --base-url) [ $# -ge 2 ] || die '--base-url 缺少参数'; BASE_URL=$2; shift 2 ;;
    -h|--help)  usage; exit 0 ;;
    *)          usage; die "未知参数: $1" ;;
  esac
done

[ -n "$IDS_FILE" ] || { usage; die '缺少 --ids'; }
[ -n "$OUT_FILE" ] || { usage; die '缺少 --out'; }
[ -f "$IDS_FILE" ] && [ -r "$IDS_FILE" ] || die "ids 文件不可读: $IDS_FILE"
is_uint "$LIMIT" || die "--limit 必须是整数: $LIMIT"
is_uint "$BACKOFF" || die "--backoff 必须是整数: $BACKOFF"
is_uint "$DELAY_MIN" || die "--delay MIN 必须是整数: $DELAY_MIN"
is_uint "$DELAY_MAX" || die "--delay MAX 必须是整数: $DELAY_MAX"
[ "$DELAY_MIN" -le "$DELAY_MAX" ] || die '--delay MIN 不能大于 MAX'
command -v curl >/dev/null 2>&1 || die '缺少 curl'
command -v base64 >/dev/null 2>&1 || die '缺少 base64'

out_dir=$(dirname -- "$OUT_FILE")
[ -d "$out_dir" ] || mkdir -p -- "$out_dir" || die "无法创建输出目录: $out_dir"
[ -e "$OUT_FILE" ] || : > "$OUT_FILE" || die "无法创建输出文件: $OUT_FILE"

tmp_dir=$(mktemp -d 2>/dev/null) || die 'mktemp 失败'
trap 'rm -rf -- "$tmp_dir"' EXIT
trap 'rm -rf -- "$tmp_dir"; exit 130' INT TERM

rand_between() {  # lo hi -> 含端点的随机整数
  printf '%s' $(( $1 + RANDOM % ($2 - $1 + 1) ))
}

# 修复 padding 后做 base64url/base64 解码；失败返回非 0（输出为空）
b64_decode() {
  local s=${1%%=*} rem
  rem=$(( ${#s} % 4 ))
  case $rem in
    0) ;;
    2) s="${s}==" ;;
    3) s="${s}=" ;;
    *) return 1 ;;
  esac
  printf '%s' "$s" | tr '_-' '/+' | base64 -d 2>/dev/null
}

# 从解码后的链接里按"路径形状"取 slug；形状不认识的返回非 0（宁可为空也不写脏数据：
# 例如原版 Minecraft 页面的平台链接指向栏目首页 https://www.curseforge.com/minecraft/mc-mods，
# 直接取最后一段会得到无意义的 "mc-mods"）
cf_slug_of() {  # https://www.curseforge.com/minecraft/mc-mods/industrial-craft[/files] -> industrial-craft
  local u=$1
  u=${u%%[?#]*}
  case $u in
    *curseforge.com/minecraft/mc-mods/*) u=${u#*curseforge.com/minecraft/mc-mods/} ;;
    *) return 1 ;;
  esac
  u=${u%%/*}
  [ -n "$u" ] || return 1
  printf '%s' "$u"
}

mr_slug_of() {  # https://modrinth.com/mod/industrial-craft[/versions] -> industrial-craft
  local u=$1
  u=${u%%[?#]*}
  case $u in
    *modrinth.com/mod/*) u=${u#*modrinth.com/mod/} ;;
    *) return 1 ;;
  esac
  u=${u%%/*}
  [ -n "$u" ] || return 1
  printf '%s' "$u"
}

# 从 <title> 里取中文名
parse_title() {
  local raw
  raw=$(grep -oE '<title>[^<]*</title>' "$1" 2>/dev/null | head -n 1)
  [ -n "$raw" ] || return 0
  raw=${raw#<title>}
  raw=${raw%</title>}
  # HTML 实体（只处理标题里可能出现的几种）
  raw=$(printf '%s' "$raw" \
    | sed -e "s/&#39;/'/g" -e "s/&#039;/'/g" -e "s/&#34;/\"/g" \
          -e 's/&quot;/"/g' -e 's/&lt;/</g' -e 's/&gt;/>/g' \
          -e 's/&nbsp;/ /g' -e 's/&amp;/\&/g')
  # 去掉 " - MC百科|最大的Minecraft中文MOD百科" 这类后缀
  raw=$(printf '%s' "$raw" | sed -E 's/[[:space:]]*-[[:space:]]*MC百科.*$//')
  # 去掉开头连续的 "[IC2]" 这类方括号前缀
  raw=$(printf '%s' "$raw" | sed -E 's/^([[:space:]]*\[[^]]*\])+//')
  # NBSP(0xC2 0xA0) 与全角空格(0xE3 0x80 0x80) 归一成半角空格
  raw=$(printf '%s' "$raw" | sed -e 's/\xC2\xA0/ /g' -e 's/\xE3\x80\x80/ /g')
  # 两端 trim
  raw=$(printf '%s' "$raw" | sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//')
  printf '%s' "$raw"
}

# 按 <li> 块作用域解析平台链接，输出 "cfSlug<TAB>mrSlug"
# 全页扫描会把正文里提到的无关模组也收进来，所以必须先切块再取链接。
extract_platform() {
  local html=$1
  local cf='' mr=''
  local block links val decoded kind slug
  while IFS= read -r block; do
    case $block in
      *link.mcmod.cn/target/*) ;;
      *) continue ;;
    esac
    links=$(printf '%s' "$block" | grep -oE 'link\.mcmod\.cn/target/[A-Za-z0-9_+/=%-]+' | sed -e 's|.*/target/||')
    [ -n "$links" ] || continue
    for val in $links; do
      case $val in
        *%*) val=$(printf '%b' "${val//%/\\x}") ;;   # 万一是 URL 编码过的 base64
      esac
      decoded=$(b64_decode "$val" 2>/dev/null) || decoded=''
      [ -n "$decoded" ] || continue
      # 平台种类直接由解码后的域名判定（同一个 <li> 里还有 title="CurseForge"/"Modrinth"
      # 的 span，但域名是更硬的证据，两者冲突时信域名）
      if [ -z "$cf" ]; then
        slug=$(cf_slug_of "$decoded" 2>/dev/null) || slug=''
        [ -n "$slug" ] && cf=$slug
      fi
      if [ -z "$mr" ]; then
        slug=$(mr_slug_of "$decoded" 2>/dev/null) || slug=''
        [ -n "$slug" ] && mr=$slug
      fi
    done
  done < <(tr '\n' ' ' < "$html" | sed -E 's/<li([[:space:]>])/\n<li\1/g')
  printf '%s\t%s' "$cf" "$mr"
}

done_ids="$tmp_dir/done_ids.txt"
: > "$done_ids"
if [ "$RESUME" -eq 1 ] && [ -s "$OUT_FILE" ]; then
  awk -F'\t' '!/^#/ && NF { gsub(/\r/, "", $1); gsub(/^ +| +$/, "", $1); if ($1 ~ /^[0-9]+$/) print $1 }' \
    "$OUT_FILE" | sort -u > "$done_ids"
  log "resume: 输出里已有 $(wc -l < "$done_ids" | tr -d ' ') 个 id 会被跳过"
fi

attempted=0
written=0
skipped=0
failed=0
block_streak=0

while IFS= read -r raw_line || [ -n "$raw_line" ]; do
  id=$(printf '%s' "$raw_line" | tr -d '\r' | sed -E 's/#.*$//; s/^[[:space:]]+//; s/[[:space:]]+$//')
  [ -n "$id" ] || continue
  is_uint "$id" || { warn "忽略非法 id: $raw_line"; continue; }

  if [ "$RESUME" -eq 1 ] && [ -s "$done_ids" ] && grep -qxF -- "$id" "$done_ids"; then
    skipped=$(( skipped + 1 ))
    continue
  fi
  if [ "$LIMIT" -gt 0 ] && [ "$attempted" -ge "$LIMIT" ]; then
    log "已到 --limit $LIMIT，停止"
    break
  fi
  attempted=$(( attempted + 1 ))

  page="$tmp_dir/page.html"
  : > "$page"
  ok=0
  fails=0
  while :; do
    http=$(curl -sS --compressed --max-time 60 -A "$UA" -o "$page" -w '%{http_code}' \
             "$BASE_URL/class/$id.html" 2>/dev/null) || http='000'
    size=$(wc -c < "$page" 2>/dev/null | tr -d ' ')
    size=${size:-0}
    blocked=0
    if [ "$size" -lt "$MIN_PAGE_BYTES" ] && grep -qE 'security reasons|已被系统拦截' "$page" 2>/dev/null; then
      blocked=1
    fi
    if [ "$blocked" -eq 1 ]; then
      block_streak=$(( block_streak + 1 ))
      warn "id=$id 被 WAF 拦截（连续第 $block_streak 次，http=$http size=$size），退避 ${BACKOFF}s"
      if [ "$block_streak" -ge "$MAX_BLOCK_STREAK" ]; then
        printf '%s\n' "wiki_crawl: [error] WAF 拦截连续 $MAX_BLOCK_STREAK 次，中止抓取" >&2
        exit 2
      fi
      sleep "$BACKOFF"
      continue
    fi
    if [ "$http" = '200' ] && [ "$size" -ge "$MIN_PAGE_BYTES" ]; then
      ok=1
      block_streak=0
      break
    fi
    fails=$(( fails + 1 ))
    warn "id=$id 请求失败（http=$http size=$size，第 $fails 次）"
    if [ "$fails" -gt "$PAGE_RETRIES" ]; then
      break
    fi
    sleep "$(rand_between "$DELAY_MIN" "$DELAY_MAX")"
  done

  if [ "$ok" -ne 1 ]; then
    warn "id=$id 最终失败，不写行"
    failed=$(( failed + 1 ))
    sleep "$(rand_between "$DELAY_MIN" "$DELAY_MAX")"
    continue
  fi

  name=$(parse_title "$page")
  if [ -z "$name" ]; then
    warn "id=$id 未解析出中文名，不写行"
    failed=$(( failed + 1 ))
    sleep "$(rand_between "$DELAY_MIN" "$DELAY_MAX")"
    continue
  fi

  plat=$(extract_platform "$page")
  cf=${plat%%$'\t'*}
  mr=${plat#*$'\t'}
  [ "$mr" = "$plat" ] && mr=''

  printf '%s\t%s\t%s\t%s\n' "$id" "$cf" "$mr" "$name" >> "$OUT_FILE"
  written=$(( written + 1 ))
  log "ok id=$id cf=$cf mr=$mr name=$name"
  sleep "$(rand_between "$DELAY_MIN" "$DELAY_MAX")"
done < "$IDS_FILE"

log "完成：尝试 $attempted，写入 $written，resume 跳过 $skipped，失败 $failed"
exit 0
