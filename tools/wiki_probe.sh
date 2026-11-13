#!/usr/bin/env bash
# wiki_probe.sh — 校验平台 slug 是否真实存在，并取回平台侧标题（供中文名一致性校验用）。
#
# 用法：
#   bash tools/wiki_probe.sh --list FILE --cache FILE --out FILE [--jobs N] [--recheck-days N]
#   bash tools/wiki_probe.sh --one PLATFORM SLUG
#
# 平台：
#   mr = Modrinth：GET https://api.modrinth.com/v2/project/<slug>（免费、无需 key）
#   cf = CurseForge：GET https://api.cfwidget.com/minecraft/mc-mods/<slug>（网页探针；
#        curseforge.com 本站有 Cloudflare，机房 IP 一律 403，所以不直连本站）
#
# 缓存格式（TSV，5 列）：
#   platform  slug  status  title  checked
#   status = 200（存在）/ 404（不存在）/ err（本轮失败，下轮重试）
#   checked = UTC 日期 yyyy-mm-dd，决定多久复查一次
#
# 退出码：0 正常；1 参数/环境错误
set -u -o pipefail

LIST=''
CACHE='/dev/null'
OUT=''
JOBS=4
RECHECK_DAYS=30
ONE_PLATFORM=''
ONE_SLUG=''
PLAN=''

MR_DELAY=0.8      # Modrinth 限速 300 次/分；4 并发下每次歇 0.8s 约等于 5 次/秒
CF_DELAY=0.15     # cfwidget 无公开限速，保守留一点间隔

log()  { printf 'wiki_probe: %s\n' "$*" >&2; }
die()  { printf 'wiki_probe: [error] %s\n' "$*" >&2; exit 1; }

usage() {
  cat >&2 <<'EOF'
用法: bash tools/wiki_probe.sh --list FILE --cache FILE --out FILE [--jobs N] [--recheck-days N]
      bash tools/wiki_probe.sh --one PLATFORM SLUG

  --list FILE          每行一个 "platform<TAB>slug"（platform 只能是 mr 或 cf）
  --cache FILE         已有缓存（可以不存在），复查期内的不再重复探针
  --out FILE           本次新探针到的缓存行（追加口径，由调用方合并；用 --plan 时可省）
  --jobs N             并发数（默认 4）
  --recheck-days N     status=200/404 的缓存多少天后复查（默认 30）
  --plan FILE          只算出待探针清单写到 FILE，不做任何网络请求
  --one PLATFORM SLUG  只探针一个，输出一行缓存（供 xargs 调用）
EOF
}

while [ $# -gt 0 ]; do
  case $1 in
    --list)         [ $# -ge 2 ] || die '--list 缺少参数'; LIST=$2; shift 2 ;;
    --cache)        [ $# -ge 2 ] || die '--cache 缺少参数'; CACHE=$2; shift 2 ;;
    --out)          [ $# -ge 2 ] || die '--out 缺少参数'; OUT=$2; shift 2 ;;
    --jobs)         [ $# -ge 2 ] || die '--jobs 缺少参数'; JOBS=$2; shift 2 ;;
    --recheck-days) [ $# -ge 2 ] || die '--recheck-days 缺少参数'; RECHECK_DAYS=$2; shift 2 ;;
    --plan)         [ $# -ge 2 ] || die '--plan 缺少参数'; PLAN=$2; shift 2 ;;
    --one)          [ $# -ge 3 ] || die '--one 需要两个参数: PLATFORM SLUG'; ONE_PLATFORM=$2; ONE_SLUG=$3; shift 3 ;;
    -h|--help)      usage; exit 0 ;;
    *)              usage; die "未知参数: $1" ;;
  esac
done

# 从 JSON 里取顶层 title。描述字段里的引号是转义的，不会命中这个模式。
json_title() {
  grep -o '"title":"[^"]*"' "$1" 2>/dev/null | head -n 1 | sed -e 's/^"title":"//' -e 's/"$//'
}

probe_one() {
  local platform=$1 slug=$2 body http title status
  body=$(mktemp) || return 1
  if [ "$platform" = 'mr' ]; then
    http=$(curl -sS --max-time 30 --retry 1 -H 'User-Agent: pcl-reborn-wiki/1.0' -o "$body" -w '%{http_code}' \
      "https://api.modrinth.com/v2/project/$slug" 2>/dev/null) || http='000'
    sleep "$MR_DELAY"
  else
    http=$(curl -sS --max-time 30 --retry 1 -H 'User-Agent: pcl-reborn-wiki/1.0' -o "$body" -w '%{http_code}' \
      "https://api.cfwidget.com/minecraft/mc-mods/$slug" 2>/dev/null) || http='000'
    sleep "$CF_DELAY"
  fi
  case $http in
    200) title=$(json_title "$body"); status=200 ;;
    404) title=''; status=404 ;;
    *)   title=''; status=err ;;
  esac
  rm -f -- "$body"
  # 标题里的 tab / 换行必须洗掉，否则污染 TSV
  title=$(printf '%s' "$title" | tr '\t\r\n' '   ')
  printf '%s\t%s\t%s\t%s\t%s\n' "$platform" "$slug" "$status" "$title" "$(date -u +%F)"
}

if [ -n "$ONE_PLATFORM" ]; then
  case $ONE_PLATFORM in mr|cf) ;; *) die "--one 的平台只能是 mr 或 cf: $ONE_PLATFORM" ;; esac
  [ -n "$ONE_SLUG" ] || die '--one 缺少 slug'
  probe_one "$ONE_PLATFORM" "$ONE_SLUG"
  exit 0
fi

[ -n "$LIST" ] || { usage; die '缺少 --list'; }
[ -n "$OUT" ] || [ -n "$PLAN" ] || { usage; die '缺少 --out'; }
[ -f "$LIST" ] && [ -r "$LIST" ] || die "list 不可读: $LIST"
[ -f "$CACHE" ] && [ -r "$CACHE" ] || CACHE='/dev/null'

command -v curl >/dev/null 2>&1 || die '缺少 curl'

cutoff=$(date -u -d "-$RECHECK_DAYS days" +%F 2>/dev/null) || cutoff='0000-01-01'

work=$(mktemp -d 2>/dev/null) || die 'mktemp 失败'
trap 'rm -rf -- "$work"' EXIT

# 列表去重、去注释；缓存里仍在复查期内且已有 200/404 结论的 (platform, slug) 跳过
# 加标记合并列表与缓存，避免用路径做 FILENAME 比较（Windows 路径的反斜杠会被 awk 转义）
{
  if [ -n "$CACHE" ] && [ -s "$CACHE" ]; then sed 's/^/K\t/' "$CACHE"; fi
  sed 's/^/L\t/' "$LIST"
} | awk -F'\t' -v cutoff="$cutoff" '
  $1 == "K" {
    if (NF >= 6 && $6 > cutoff && ($4 == "200" || $4 == "404")) skip[$2 SUBSEP $3] = 1
    next
  }
  $1 == "L" {
    if ($2 ~ /^#/ || $3 == "") next
    s = $3
    sub(/\r$/, "", s)
    k = $2 SUBSEP s
    if (k in skip) next
    if (k in seen) next
    seen[k] = 1
    print $2 "\t" s
  }
' > "$work/todo.txt"

if [ -n "$PLAN" ]; then
  cp -- "$work/todo.txt" "$PLAN"
  log "已写出待探针清单 $(wc -l < "$PLAN" | tr -d ' ') 行: $PLAN"
  exit 0
fi

total=$(wc -l < "$work/todo.txt" | tr -d ' ')
log "待探针 $total 个 slug（并发 $JOBS，复查期 $RECHECK_DAYS 天）"
if [ "$total" -le 0 ]; then
  : > "$OUT"
  exit 0
fi

: > "$OUT"
self=$(cd "$(dirname -- "$0")" && pwd)/$(basename -- "$0")
xargs -P "$JOBS" -n 2 bash "$self" --one < "$work/todo.txt" >> "$OUT"

ok=$(awk -F'\t' '$3 == "200"' "$OUT" | wc -l | tr -d ' ')
missing=$(awk -F'\t' '$3 == "404"' "$OUT" | wc -l | tr -d ' ')
failed=$(awk -F'\t' '$3 == "err"' "$OUT" | wc -l | tr -d ' ')
log "本轮结果：存在 $ok，不存在 $missing，失败 $failed"
exit 0
