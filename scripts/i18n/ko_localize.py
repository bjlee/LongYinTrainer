#!/usr/bin/env python3
"""LongYinTrainer 한국어화 도구.

소스(.cs)의 문자열 리터럴을 docs/i18n/ko-KR.tsv(원문→번역)와
docs/i18n/ko-KR.context.tsv(문맥별 예외)에 따라 바꾼다.

  python scripts/i18n/ko_localize.py check   # 번역 안 된 중국어 문자열 목록 (파일 수정 없음)
  python scripts/i18n/ko_localize.py apply   # 번역 적용 (파일 수정)
  python scripts/i18n/ko_localize.py apply --dry-run

check 는 새 문자열을 docs/i18n/untranslated.tsv 로도 써 준다. 두 번째 칸을 채워
ko-KR.tsv 끝에 붙인 뒤 apply 하면 된다.
"""
import argparse, glob, os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MAP_FILE = os.path.join(ROOT, "docs", "i18n", "ko-KR.tsv")
CTX_FILE = os.path.join(ROOT, "docs", "i18n", "ko-KR.context.tsv")
OUT_FILE = os.path.join(ROOT, "docs", "i18n", "untranslated.tsv")
SOURCES = ["LongYinTrainer/*.cs", "*Regression/*.cs", "CharacterAudit/*.cs"]
HAN = re.compile(r"[一-鿿]")
HOLE = re.compile(r"\{[^{}]*\}")


class Lit:
    """문자열 리터럴 하나. start/end 는 따옴표 안쪽 내용의 위치."""
    def __init__(self, start, end, text, interp, verbatim):
        self.start, self.end, self.text = start, end, text
        self.interp, self.verbatim = interp, verbatim


def tokenize(src):
    """C# 소스에서 문자열 리터럴을 찾는다. 보간 문자열의 {...} 안에 든 문자열도 따로 찾는다."""
    lits = []
    i, n = 0, len(src)

    def read_string(i, interp, verbatim):
        # i 는 여는 따옴표 다음 위치
        start = i
        depth = 0
        while i < n:
            c = src[i]
            if depth == 0:
                if verbatim and c == '"':
                    if i + 1 < n and src[i + 1] == '"':
                        i += 2; continue
                    break
                if not verbatim and c == "\\":
                    i += 2; continue
                if not verbatim and c == '"':
                    break
                if c == "\n" and not verbatim:
                    break
                if interp and c == "{":
                    if i + 1 < n and src[i + 1] == "{":
                        i += 2; continue
                    depth = 1; i += 1; continue
                i += 1
            else:
                # 보간 구멍 안: 코드
                j = scan_code_token(i)
                if j is not None:
                    i = j; continue
                if c == "{": depth += 1
                elif c == "}": depth -= 1
                i += 1
        lits.append(Lit(start, i, src[start:i], interp, verbatim))
        return i + 1

    def scan_code_token(i):
        """i 에서 문자열/문자/주석이 시작하면 끝 위치를 돌려준다."""
        c = src[i]
        if src.startswith("//", i):
            j = src.find("\n", i); return n if j < 0 else j
        if src.startswith("/*", i):
            j = src.find("*/", i + 2); return n if j < 0 else j + 2
        if c == "'":
            j = i + 1
            while j < n and src[j] != "'":
                j += 2 if src[j] == "\\" else 1
            return j + 1
        for p, interp, verb in (('$@"', True, True), ('@$"', True, True), ('$"', True, False), ('@"', False, True), ('"', False, False)):
            if src.startswith(p, i):
                if src.startswith('"""', i):
                    raise SystemExit("C# raw string literal(\"\"\")은 지원하지 않습니다. 해당 줄을 직접 번역하세요.")
                return read_string(i + len(p), interp, verb)
        return None

    while i < n:
        j = scan_code_token(i)
        i = j if j is not None else i + 1
    return lits


def load_map():
    m = {}
    for line in open(MAP_FILE, encoding="utf-8"):
        line = line.rstrip("\r\n")
        if not line or line.startswith("#"): continue
        parts = line.split("\t")
        if len(parts) >= 2 and parts[1]: m[parts[0]] = parts[1]
    return m


def load_ctx():
    rules = []
    for line in open(CTX_FILE, encoding="utf-8"):
        line = line.rstrip("\r\n")
        if not line or line.startswith("#"): continue
        f, pat, src, dst = (line.split("\t") + ["", "", "", ""])[:4]
        rules.append((f, re.compile(pat), src, dst))
    return rules


def decide(rel, line, text, m, rules):
    """None=그대로 둠, str=바꿀 내용."""
    for f, pat, src, dst in rules:
        if (f == "*" or rel.endswith(f)) and pat.search(line) and (src == "*" or src == text):
            return None if dst == "=" else dst
    return m.get(text)


def line_of(src, pos):
    a = src.rfind("\n", 0, pos) + 1
    b = src.find("\n", pos)
    return src[a:] if b < 0 else src[a:b]


def process(path, m, rules, write):
    rel = os.path.relpath(path, ROOT).replace("\\", "/")
    raw = open(path, "rb").read()
    src = raw.decode("utf-8-sig")
    bom = raw.startswith(b"\xef\xbb\xbf")
    changed, untranslated, problems = 0, [], []
    for _ in range(4):  # 보간 구멍 안의 문자열까지 처리하려고 여러 번 돈다
        edits = []
        for lit in tokenize(src):
            line = line_of(src, lit.start)
            own = HOLE.sub("", lit.text) if lit.interp else lit.text  # 보간 구멍 안의 문자열은 따로 처리
            if not HAN.search(own):
                # 한자가 없는 리터럴은 문맥 규칙에 원문이 정확히 적힌 경우에만 바꾼다(예: 전각 괄호)
                dst = decide(rel, line, lit.text, {}, [r for r in rules if r[2] != "*"])
                if dst is not None and dst != lit.text: edits.append((lit.start, lit.end, dst))
                continue
            dst = decide(rel, line, lit.text, m, rules)
            if dst is None:
                if lit.text not in m and not is_kept(rel, line, lit.text, rules):
                    untranslated.append((rel, src.count("\n", 0, lit.start) + 1, lit.text))
                continue
            if lit.interp and sorted(HOLE.findall(lit.text)) != sorted(HOLE.findall(dst)):
                problems.append(f"{rel}: 보간 구멍이 원문과 다름: {lit.text!r} -> {dst!r}"); continue
            if dst != lit.text: edits.append((lit.start, lit.end, dst))
        # 바깥 리터럴과 안쪽 리터럴이 겹치면 바깥쪽 하나만 이번에 바꾼다
        edits.sort()
        chosen, last_end = [], -1
        for s, e, d in edits:
            if s >= last_end:
                chosen.append((s, e, d)); last_end = e
        if not chosen: break
        for s, e, d in reversed(chosen):
            src = src[:s] + d + src[e:]
        changed += len(chosen)
        untranslated = []
    if changed and write:
        open(path, "wb").write((b"\xef\xbb\xbf" if bom else b"") + src.encode("utf-8"))
    return changed, untranslated, problems


def is_kept(rel, line, text, rules):
    for f, pat, src, dst in rules:
        if (f == "*" or rel.endswith(f)) and pat.search(line) and (src == "*" or src == text) and dst == "=":
            return True
    return False


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["check", "apply"])
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()
    try:  # Windows 콘솔(cp949)에서 출력할 수 없는 글자가 있어도 멈추지 않게
        sys.stdout.reconfigure(errors="replace")
    except AttributeError:
        pass
    m, rules = load_map(), load_ctx()
    total, todo, probs = 0, [], []
    files = sorted({p for g in SOURCES for p in glob.glob(os.path.join(ROOT, g))})
    for p in files:
        c, u, pr = process(p, m, rules, a.cmd == "apply" and not a.dry_run)
        total += c; todo += u; probs += pr
    for p in probs: print("오류:", p)
    seen = {}
    for rel, ln, t in todo: seen.setdefault(t, f"{rel}:{ln}")
    if a.cmd == "apply":
        print(f"{'(시험) ' if a.dry_run else ''}바꾼 문자열: {total}곳")
    print(f"번역되지 않은 중국어 문자열: {len(seen)}개")
    for t, loc in seen.items(): print(f"  {loc}\t{t}")
    if seen:
        with open(OUT_FILE, "w", encoding="utf-8") as o:
            o.write("# 원문<TAB>번역 — 두 번째 칸을 채워 ko-KR.tsv 끝에 붙이세요. 게임 데이터와 비교하는 값이면 ko-KR.context.tsv 에 '=' 규칙을 추가하세요.\n")
            for t, loc in seen.items(): o.write(f"{t}\t\t# {loc}\n")
        print(f"목록 저장: {os.path.relpath(OUT_FILE, ROOT)}")
    return 1 if probs else 0


if __name__ == "__main__":
    sys.exit(main())
