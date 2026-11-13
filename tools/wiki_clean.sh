#!/usr/bin/env bash
# wiki_clean.sh — 用探针缓存清理抓取结果（纯离线、结果确定）。
#
# 用法：
#   bash tools/wiki_clean.sh --raw FILE --cache FILE --out FILE [--overrides FILE] [--report FILE]
#
# 规则（按顺序执行）：
#   1) 丢弃 cf 与 mr 都为空的条目（游戏本体、启动器/加载器、只在 MC 百科有页面的模组）；
#   2) 平台链接必须真实存在：Modrinth 返回 404 的摘除；CF 侧 404 只记未验证、保留
#      （cfwidget 对关闭第三方 API 的项目也回 404，实测有网页存在却 404 的项目）；
#      err/缺失 = 未验证，保留；
#   3) 名称一致性：标题括号里的英文名与平台侧标题归一化后必须互相包含，
#      不一致说明 MC 百科上的平台链接张冠李戴（例：按键显示 (Keystrokes) 挂的是 essential-mod），摘除；
#   4) 同一 slug 被多个条目认领时只留一个：名称一致优先，其次 id 更小；其余摘除该链接；
#   5) 摘完链接仍没有任何 slug 的条目丢弃。
#
# overrides：每行 "id<TAB>platform<TAB>slug"，强制保留该链接（人工白名单，跳过 2/3/4）。
# 退出码：0 正常；1 参数错误
set -u -o pipefail

RAW=''
CACHE=''
OUT=''
OVR=''
REPORT=''

log()  { printf 'wiki_clean: %s\n' "$*" >&2; }
die()  { printf 'wiki_clean: [error] %s\n' "$*" >&2; exit 1; }

usage() {
  cat >&2 <<'EOF'
用法: bash tools/wiki_clean.sh --raw FILE --cache FILE --out FILE [--overrides FILE] [--report FILE]

  --raw FILE        合并后的原始抓取结果（id<TAB>cf<TAB>mr<TAB>中文名）
  --cache FILE      探针缓存（platform<TAB>slug<TAB>status<TAB>title<TAB>checked，可不存在）
  --out FILE        清理结果（首行固定写 "# id<TAB>cf<TAB>mr<TAB>name"）
  --overrides FILE  人工白名单，强制保留指定链接
  --report FILE     清理统计报告
EOF
}

while [ $# -gt 0 ]; do
  case $1 in
    --raw)       [ $# -ge 2 ] || die '--raw 缺少参数'; RAW=$2; shift 2 ;;
    --cache)     [ $# -ge 2 ] || die '--cache 缺少参数'; CACHE=$2; shift 2 ;;
    --out)       [ $# -ge 2 ] || die '--out 缺少参数'; OUT=$2; shift 2 ;;
    --overrides) [ $# -ge 2 ] || die '--overrides 缺少参数'; OVR=$2; shift 2 ;;
    --report)    [ $# -ge 2 ] || die '--report 缺少参数'; REPORT=$2; shift 2 ;;
    -h|--help)   usage; exit 0 ;;
    *)           usage; die "未知参数: $1" ;;
  esac
done

[ -n "$RAW" ] || { usage; die '缺少 --raw'; }
[ -f "$RAW" ] && [ -r "$RAW" ] || die "raw 不可读: $RAW"
[ -n "$OUT" ] || { usage; die '缺少 --out'; }

work=$(mktemp -d 2>/dev/null) || die 'mktemp 失败'
trap 'rm -rf -- "$work"' EXIT

if [ -z "$CACHE" ] || [ ! -r "$CACHE" ]; then CACHE=''; fi
if [ -z "$OVR" ] || [ ! -r "$OVR" ]; then OVR=''; fi

# 把缓存与白名单合成一份带标记的元数据（避免用路径做 FILENAME 比较：Windows 下路径含反斜杠会被 awk 转义）
: > "$work/meta.tsv"
if [ -n "$CACHE" ] && [ -s "$CACHE" ]; then sed 's/^/C\t/' "$CACHE" >> "$work/meta.tsv"; fi
if [ -n "$OVR" ] && [ -s "$OVR" ]; then sed 's/^/O\t/' "$OVR" >> "$work/meta.tsv"; fi

# 第一遍：给每条链接打分。输出 L（可留链接）/ R（条目）/ D（被摘链接）/ C（计数）
awk -F'\t' '
  function norm(s,   r, i, c) {
    s = tolower(s)
    r = ""
    for (i = 1; i <= length(s); i++) {
      c = substr(s, i, 1)
      if (c ~ /[a-z0-9]/) r = r c
    }
    return r
  }
  # 1 = 名称一致；0 = 无法判断（保留）；-1 = 不存在或不一致（摘除）
  # 判定用「名字里的 ASCII 片段」与平台标题互相包含；加载器/版式通用词不参与
  function score(id, platform, slug,   k, nt, i) {
    k = platform SUBSEP slug
    if ((id SUBSEP k) in ovKey) return 1
    if (!(k in st)) return 0
    # Modrinth 官方 API 的 404 可信；cfwidget 对关闭第三方 API 的项目也回 404
    # （实测 supermartijn642s-core-lib 网页存在但 cfwidget 404），因此 CF 侧只记未验证
    if (st[k] == "404") {
      if (platform == "cf") { cf404++; return 0 }
      reason = "not-found"; return -1
    }
    if (st[k] != "200") return 0
    if (ntok == 0) return 0
    nt = norm(ti[k])
    if (nt == "") return 0
    for (i = 1; i <= ntok; i++) {
      if (index(nt, tok[i]) > 0 || index(tok[i], nt) > 0) return 1
    }
    reason = "name-mismatch"
    return -1
  }
  BEGIN {
    split("forge fabric quilt neoforge mod mods edition port legacy unofficial backport fork updated expanded reborn classic neo", g, " ")
    for (i in g) gen[g[i]] = 1
  }
  $1 == "C" { if (NF >= 5) { k = $2 SUBSEP $3; st[k] = $4; ti[k] = $5 } next }
  $1 == "O" { if (NF >= 4 && $2 !~ /^#/) ovKey[$2 SUBSEP $3 SUBSEP $4] = 1; next }
  {
    if ($0 ~ /^#/ || NF < 4) next
    id = $1 + 0
    if (id <= 0) next
    cf = $2
    mr = $3
    name = $4
    for (i = 5; i <= NF; i++) name = name " " $i
    rows++

    # 英文名候选：名字里所有长度 >=3 的 ASCII 字母数字片段（去掉通用词）
    ntok = 0
    split("", tok)
    s = name
    while (match(s, /[A-Za-z0-9]+/)) {
      p = tolower(substr(s, RSTART, RLENGTH))
      s = substr(s, RSTART + RLENGTH)
      if (length(p) >= 3 && !(p in gen)) { ntok++; tok[ntok] = p }
    }

    has = 0
    if (cf != "") {
      sc = score(id, "cf", cf)
      if (sc < 0) { dropped++; print "D\t" id "\t" "cf" "\t" cf "\t" reason }
      else { print "L\t" id "\t" "cf" "\t" cf "\t" sc; has = 1 }
    }
    if (mr != "") {
      sc = score(id, "mr", mr)
      if (sc < 0) { dropped++; print "D\t" id "\t" "mr" "\t" mr "\t" reason }
      else { print "L\t" id "\t" "mr" "\t" mr "\t" sc; has = 1 }
    }
    if (has) { print "R\t" id "\t" cf "\t" mr "\t" name; keptRows++ } else noLink++
  }
  END {
    printf "C\t%d\t%d\t%d\t%d\n", rows, keptRows, noLink, dropped
    printf "U\t%d\n", cf404
  }
' "$work/meta.tsv" "$RAW" > "$work/pass1.tsv"

# 每个 slug 只留一个认领者：名称一致优先，其次 id 更小
awk -F'\t' '$1 == "L" { print $2 "\t" $3 "\t" $4 "\t" $5 }' "$work/pass1.tsv" \
  | LC_ALL=C sort -t$'\t' -k2,2 -k3,3 -k4,4r -k1,1n \
  | awk -F'\t' '!seen[$2 SUBSEP $3]++ { print $1 "\t" $2 "\t" $3 }' > "$work/owners.tsv"

# 第二遍：只保留自己真正拿到 slug 的链接
cat "$work/owners.tsv" "$work/pass1.tsv" | awk -F'\t' '
  $1 == "R" {
    id = $2
    cf = $3
    mr = $4
    name = $5
    for (i = 6; i <= NF; i++) name = name " " $i
    if (cf != "" && own["cf" SUBSEP cf] != id) { print "X\t" id "\t" "cf" "\t" cf; cf = "" }
    if (mr != "" && own["mr" SUBSEP mr] != id) { print "X\t" id "\t" "mr" "\t" mr; mr = "" }
    if (cf == "" && mr == "") { dupLost++; next }
    print "R\t" id "\t" cf "\t" mr "\t" name
    kept++
    next
  }
  $1 ~ /^[0-9]+$/ { own[$2 SUBSEP $3] = $1 }
  END { printf "C\t%d\t%d\n", kept, dupLost }
' > "$work/pass2.tsv"

awk -F'\t' '$1 == "R" { print $2 "\t" $3 "\t" $4 "\t" $5 }' "$work/pass2.tsv" \
  | LC_ALL=C sort -t$'\t' -k1,1n > "$work/rows.tsv"

{
  printf '# id\tcf\tmr\tname\n'
  cat "$work/rows.tsv"
} > "$OUT"

rows=0; keptRows=0; noLink=0; dropped=0; kept=0; dupLost=0; cf404=0
if [ -s "$work/pass1.tsv" ]; then
  read -r rows keptRows noLink dropped < <(awk -F'\t' '$1 == "C" { print $2 "\t" $3 "\t" $4 "\t" $5 }' "$work/pass1.tsv")
  cf404=$(awk -F'\t' '$1 == "U" { print $2 }' "$work/pass1.tsv")
fi
if [ -s "$work/pass2.tsv" ]; then read -r kept dupLost < <(awk -F'\t' '$1 == "C" { print $2 "\t" $3 }' "$work/pass2.tsv"); fi

summary=$(printf 'raw=%s kept=%s no_link=%s links_dropped=%s cf_unverified=%s dup_lost=%s final=%s' \
  "$rows" "$keptRows" "$noLink" "$dropped" "$cf404" "$dupLost" "$kept")
log "$summary"

if [ -n "$REPORT" ]; then
  {
    printf '# wiki_clean 报告（%s）\n\n' "$(date -u +%F)"
    printf '%s\n\n' "$summary"
    printf '%s\n' '## 被摘除的链接（最多 50 条：id, platform, slug, reason）'
    if awk -F'\t' '$1 == "D" { f = 1 } END { exit(f ? 0 : 1) }' "$work/pass1.tsv"; then
      awk -F'\t' '$1 == "D" { print $2 "\t" $3 "\t" $4 "\t" $5 }' "$work/pass1.tsv" | head -n 50
    else
      printf '(无)\n'
    fi
    printf '\n%s\n' '## 同名 slug 冲突被摘除的链接（最多 50 条：id, platform, slug）'
    if awk -F'\t' '$1 == "X" { f = 1 } END { exit(f ? 0 : 1) }' "$work/pass2.tsv"; then
      awk -F'\t' '$1 == "X" { print $2 "\t" $3 "\t" $4 }' "$work/pass2.tsv" | head -n 50
    else
      printf '(无)\n'
    fi
    printf '\n%s\n' '## 其余统计'
    printf 'rows=%s kept_rows=%s no_link_rows=%s dup_lost_rows=%s final_rows=%s\n' \
      "$rows" "$keptRows" "$noLink" "$dupLost" "$kept"
  } > "$REPORT"
fi

exit 0
