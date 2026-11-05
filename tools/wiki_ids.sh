#!/usr/bin/env bash
# wiki_ids.sh — 从 MC 百科（mcmod.cn）sitemap 里取全部 class id。
#
# 用法：
#   bash tools/wiki_ids.sh --out FILE [--delay MIN MAX] [--base-url URL]
#
# 流程：sitemap.xml -> 其中的子 sitemap（sitemap1.xml / sitemap2.xml ...）
#       -> 抽出全部 /class/<id>.html -> 去重按数值升序输出，每行一个 id。
#
# 退出码：0 成功；1 参数/抓取失败
set -u -o pipefail

UA='Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36'
BASE_URL='https://www.mcmod.cn'
OUT_FILE=''
DELAY_MIN=2
DELAY_MAX=4

log()  { printf '%s\n' "wiki_ids: $*" >&2; }
warn() { printf '%s\n' "wiki_ids: [warn] $*" >&2; }
die()  { printf '%s\n' "wiki_ids: [error] $*" >&2; exit 1; }

usage() {
  cat >&2 <<'EOF'
用法: bash tools/wiki_ids.sh --out FILE [--delay MIN MAX] [--base-url URL]

  --out FILE       输出文件，每行一个 class id（升序去重）
  --delay MIN MAX  子 sitemap 之间的随机等待秒数（默认 2 4）
  --base-url URL   站点前缀（默认 https://www.mcmod.cn）
EOF
}

is_uint() { case ${1-} in ''|*[!0-9]*) return 1 ;; *) return 0 ;; esac; }

while [ $# -gt 0 ]; do
  case $1 in
    --out)      [ $# -ge 2 ] || die '--out 缺少参数'; OUT_FILE=$2; shift 2 ;;
    --delay)    [ $# -ge 3 ] || die '--delay 需要两个参数: MIN MAX'; DELAY_MIN=$2; DELAY_MAX=$3; shift 3 ;;
    --base-url) [ $# -ge 2 ] || die '--base-url 缺少参数'; BASE_URL=$2; shift 2 ;;
    -h|--help)  usage; exit 0 ;;
    *)          usage; die "未知参数: $1" ;;
  esac
done

[ -n "$OUT_FILE" ] || { usage; die '缺少 --out'; }
is_uint "$DELAY_MIN" || die '--delay MIN 必须是整数'
is_uint "$DELAY_MAX" || die '--delay MAX 必须是整数'
[ "$DELAY_MIN" -le "$DELAY_MAX" ] || die '--delay MIN 不能大于 MAX'
command -v curl >/dev/null 2>&1 || die '缺少 curl'

out_dir=$(dirname -- "$OUT_FILE")
[ -d "$out_dir" ] || mkdir -p -- "$out_dir" || die "无法创建输出目录: $out_dir"

tmp_dir=$(mktemp -d 2>/dev/null) || die 'mktemp 失败'
trap 'rm -rf -- "$tmp_dir"' EXIT
trap 'rm -rf -- "$tmp_dir"; exit 130' INT TERM

rand_between() { printf '%s' $(( $1 + RANDOM % ($2 - $1 + 1) )); }

# 抓一个 URL 到文件，失败重试 3 次
fetch_to() {  # url dest
  local url=$1 dest=$2 attempt code
  for attempt in 1 2 3; do
    code=$(curl -sS --compressed --max-time 90 -A "$UA" -o "$dest" -w '%{http_code}' "$url" 2>/dev/null) || code='000'
    if [ "$code" = '200' ] && [ -s "$dest" ]; then
      return 0
    fi
    warn "抓取失败（第 $attempt 次，http=$code）: $url"
    sleep 5
  done
  return 1
}

extract_ids() {  # 从 sitemap 里抽 /class/<id>.html 的 id
  grep -oE '/class/[0-9]+\.html' "$1" 2>/dev/null \
    | sed -E 's#^/class/##; s#\.html$##'
}

index_file="$tmp_dir/sitemap.xml"
ids_raw="$tmp_dir/ids.raw"
: > "$ids_raw"

fetch_to "$BASE_URL/sitemap.xml" "$index_file" || die "无法获取 $BASE_URL/sitemap.xml"

# 有些站点 sitemap.xml 本身就是扁平的，先把它里面直接列出的 class 链接收下
extract_ids "$index_file" >> "$ids_raw"

# 再逐个抓子 sitemap（sitemap1.xml / sitemap2.xml ...）
sub_urls=$(grep -oE '<loc>[^<]*</loc>' "$index_file" 2>/dev/null \
  | sed -e 's#<loc>##' -e 's#</loc>##' \
  | grep -Ei 'sitemap[^/]*\.xml([?#].*)?$' || true)

sub_count=0
for sub in $sub_urls; do
  case $sub in
    http*) ;;
    /*)    sub="$BASE_URL$sub" ;;
    *)     continue ;;
  esac
  sub_name=$(printf '%s' "$sub" | sed -E 's#[?#].*$##; s#.*/##')
  [ -n "$sub_name" ] || sub_name="sub$sub_count.xml"
  sub_file="$tmp_dir/$sub_name"
  if fetch_to "$sub" "$sub_file"; then
    extract_ids "$sub_file" >> "$ids_raw"
    sub_count=$(( sub_count + 1 ))
    log "已解析 $sub"
  else
    warn "跳过抓取失败的子 sitemap: $sub"
  fi
  sleep "$(rand_between "$DELAY_MIN" "$DELAY_MAX")"
done

total_raw=$(wc -l < "$ids_raw" | tr -d ' ')
sort -n -u "$ids_raw" > "$OUT_FILE" || die "无法写入 $OUT_FILE"
total=$(wc -l < "$OUT_FILE" | tr -d ' ')

log "子 sitemap: $sub_count 个，原始条目 $total_raw，去重后 id $total"
[ "$total" -gt 0 ] || die '没有抽到任何 class id（sitemap 结构可能变了）'
exit 0
