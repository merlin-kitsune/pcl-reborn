import argparse, sys

DIGITS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz/+=!?@#$%^&*()[]{}<>;:',"

def encode86(value, width=3):
    if value < 0: raise ValueError(value)
    out = ''
    while value:
        value, rem = divmod(value, 86)
        out = DIGITS[rem] + out
    out = out or '0'
    if len(out) > width: raise ValueError('popularity too large: %d' % value)
    return out.rjust(width, '0')

def decode86(text):
    n = 0
    for ch in text:
        n = n * 86 + DIGITS.index(ch)
    return n

def read_old(path):
    with open(path, encoding='utf-8', newline='') as f:
        raw = f.read()
    lines = raw.split('\n')
    if lines and lines[-1] == '': lines.pop()
    lines = [l[:-1] if l.endswith('\r') else l for l in lines]
    popline = lines[-1]
    if len(popline) % 3: raise ValueError('popularity line length %d not divisible by 3' % len(popline))
    pops = [decode86(popline[i:i+3]) for i in range(0, len(popline), 3)]
    result = {}
    idx = 0
    for lineno, line in enumerate(lines[:-1], start=1):
        if line == '': continue
        if idx >= len(pops): raise ValueError('popularity queue exhausted at line %d' % lineno)
        result[lineno] = pops[idx]
        idx += 1
    if idx != len(pops): print('warn: %d popularity values unused' % (len(pops) - idx), file=sys.stderr)
    return result

def sanitize(name):
    return name.replace('|', '\uff5c').replace('*', '\uff0a').strip()

def slug_spec(cf, mr):
    if cf and mr:
        return cf + '@' if cf == mr else cf + '@' + mr
    if cf: return cf
    if mr: return '@' + mr
    return ''

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--tsv', required=True)
    ap.add_argument('--old', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--stats', default='')
    a = ap.parse_args()
    old = read_old(a.old)
    rows = []
    with open(a.tsv, encoding='utf-8', newline='') as f:
        for line in f:
            line = line.rstrip('\r\n')
            if not line or line.startswith('#'): continue
            p = line.split('\t')
            if len(p) < 4 or not p[0].isdigit(): continue
            rows.append((int(p[0]), p[1].strip(), p[2].strip(), p[3]))
    max_id = max([r[0] for r in rows] + list(old.keys()))
    slots = [''] * (max_id + 1)
    entries = 0
    skipped = 0
    for wid, cf, mr, name in rows:
        spec = slug_spec(cf, mr)
        name = sanitize(name)
        if not spec or not name:
            skipped += 1
            continue
        cell = spec + '|' + name
        slots[wid] = cell if not slots[wid] else slots[wid] + '\u00a8' + cell
        entries += 1
    pops = []
    carried = 0
    for lineno in range(1, max_id + 1):
        if slots[lineno] == '': continue
        value = old.get(lineno, 0)
        if value: carried += 1
        pops.append(encode86(value))
    body = '\r\n'.join(slots[1:]) + '\r\n' + ''.join(pops)  # 末尾不留换行，解析器以最后一行为浏览量
    with open(a.out, 'w', encoding='utf-8', newline='') as f:
        f.write(body)
    stats = 'lines=%d entries=%d skipped=%d popularity=%d carried=%d max_id=%d' % (
        max_id, entries, skipped, len(pops), carried, max_id)
    print(stats)
    if a.stats:
        with open(a.stats, 'w', encoding='utf-8') as f: f.write(stats + '\n')

if __name__ == '__main__':
    main()