#!/usr/bin/env bash
# wiki_merge.sh — 合并多个抓取分片（以及 wiki-data 分支上的旧文件）成一份稳定的 modules.tsv。
#
# 用法：
#   bash tools/wiki_merge.sh --base FILE_OR_NONE --shard A.tsv B.tsv ... --out wiki/modules.tsv
#
#   --base FILE|NONE  已有的合并结果（首行可能是 # 注释，会被跳过）；NONE 表示没有
#   --shard FILES...  一个或多个分片（可重复给 --shard）
#   --out FILE        合并结果，首行固定写 "# id<TAB>cf<TAB>mr<TAB>name"
#
# 去重规则（同一 id）：
#   1) 优先保留"有 slug（cf 或 mr 非空）"的记录；
#   2) 同等情况保留中文名更长的（按 UTF-8 字符数，不是字节数）；
#   3) 再相同则保留先出现的。
# 输出按 id 数值升序。
#
# 退出码：0 成功；1 参数错误
set -u -o pipefail

BASE=''
OUT_FILE=''
SHARDS=()
log()  { printf '%s\n' "wiki_merge: $*" >&2; }
warn() { printf '%s\n' "wiki_merge: [warn] $*" >&2; }
die()  { printf '%s\n' "wiki_merge: [error] $*" >&2; exit 1; }

usage() {
  cat >&2 <<'EOF'
用法: bash tools/wiki_merge.sh --base FILE_OR_NONE --shard A.tsv [B.tsv ...] --out FILE

  --base FILE|NONE  旧数据（可含 # 注释行）；NONE 表示无
  --shard FILES...  一个或多个分片 TSV（可重复）
  --out FILE        输出路径（首行为 "# id<TAB>cf<TAB>mr<TAB>name"）
EOF
}

while [ $# -gt 0 ]; do
  case $1 in
    --base)  [ $# -ge 2 ] || die '--base 缺少参数'; BASE=$2; shift 2 ;;
    --shard)
      shift
      got=0
      while [ $# -gt 0 ]; do
        case $1 in
          --*) break ;;
        esac
        SHARDS+=("$1"); got=1; shift
      done
      [ "$got" -eq 1 ] || die '--shard 后面至少要跟一个文件'
      ;;
    --out)   [ $# -ge 2 ] || die '--out 缺少参数'; OUT_FILE=$2; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *)       usage; die "未知参数: $1" ;;
  esac
done

[ -n "$OUT_FILE" ] || { usage; die '缺少 --out'; }
[ ${#SHARDS[@]} -gt 0 ] || { usage; die '缺少 --shard'; }
command -v awk >/dev/null 2>&1 || die '缺少 awk'

out_dir=$(dirname -- "$OUT_FILE")
[ -d "$out_dir" ] || mkdir -p -- "$out_dir" || die "无法创建输出目录: $out_dir"

tmp_dir=$(mktemp -d 2>/dev/null) || die 'mktemp 失败'
trap 'rm -rf -- "$tmp_dir"' EXIT
trap 'rm -rf -- "$tmp_dir"; exit 130' INT TERM

cand="$tmp_dir/candidates.tsv"
: > "$cand"

# 收集输入行：跳过 # 注释与空行；顺便去掉 CRLF 的 CR
collect() {  # file label
  local f=$1
  if [ ! -f "$f" ]; then
    warn "$2 不存在，跳过: $f"
    return 0
  fi
  tr -d '\r' < "$f" | grep -v '^[[:space:]]*#' | grep -v '^[[:space:]]*$' >> "$cand" || true
}

if [ -n "$BASE" ] && [ "$BASE" != 'NONE' ] && [ "$BASE" != 'none' ]; then
  collect "$BASE" '--base'
elif [ -n "$BASE" ]; then
  log 'base = NONE，不合并旧数据'
fi

for f in "${SHARDS[@]}"; do
  collect "$f" '--shard'
done

input_rows=$(wc -l < "$cand" | tr -d ' ')
if [ "$input_rows" -eq 0 ]; then
  warn '没有可用的输入行，只写出表头'
  printf '# id\tcf\tmr\tname\n' > "$OUT_FILE"
  exit 0
fi

body="$tmp_dir/body.tsv"
LC_ALL=C awk -F'\t' -v OFS='\t' '
  # UTF-8 字符数：把"续字节"(0x80-0xBF) 删掉，剩下的前缀字节数就是字符数。
  # 用八进制字节区间而不是 length() 直接量，这样在 C locale / mawk / 多字节
  # locale 下结果一致（中文按 1 个字符算，不会因为 3 字节而被高估）。
  function clen(s,   t) { t = s; gsub(/[\200-\277]/, "", t); return length(t) }
  {
    sub(/\r$/, "")                       # 双保险：清掉行尾 CR
    id = $1
    gsub(/^[ \t]+|[ \t]+$/, "", id)
    if (id !~ /^[0-9]+$/) next           # 非法 id 直接丢
    cf = $2; gsub(/^[ \t]+|[ \t]+$/, "", cf)
    mr = $3; gsub(/^[ \t]+|[ \t]+$/, "", mr)
    name = $4
    for (i = 5; i <= NF; i++) name = name "\t" $i   # 名字里带 tab 也不丢
    gsub(/^[ \t]+|[ \t]+$/, "", name)

    has = (cf != "" || mr != "") ? 1 : 0
    len = clen(name)
    if (!(id in best_has)) {
      order[++n] = id
      best_has[id] = has; best_len[id] = len; rec[id] = id "\t" cf "\t" mr "\t" name
      next
    }
    better = 0
    if (has > best_has[id]) better = 1
    else if (has == best_has[id] && len > best_len[id]) better = 1
    if (better) {
      best_has[id] = has; best_len[id] = len; rec[id] = id "\t" cf "\t" mr "\t" name
    }
  }
  END {
    for (i = 1; i <= n; i++) print rec[order[i]]
    printf "wiki_merge: 输入行 %d -> 唯一 id %d\n", NR, n > "/dev/stderr"
  }
' "$cand" > "$body" || die 'awk 合并失败'

{
  printf '# id\tcf\tmr\tname\n'
  sort -n -k1,1 "$body"
} > "$OUT_FILE" || die "无法写入 $OUT_FILE"

out_rows=$(wc -l < "$OUT_FILE" | tr -d ' ')
log "输入 $input_rows 行 -> 输出文件 $out_rows 行（含表头）: $OUT_FILE"
exit 0
