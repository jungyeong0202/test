# -*- coding: utf-8 -*-
"""만들어진 exe 가 .NET Framework 4.8 에 실제로 있는 API 만 부르는지 검사한다.

왜 필요한가:
    리눅스의 mcs 는 기본으로 Mono 자신의 클래스 라이브러리를 기준으로 컴파일한다.
    Mono 에는 .NET Core 시절 추가된 API 가 들어 있어서, 그대로 빌드하면 윈도우의
    .NET Framework 에 없는 메서드를 부르는 exe 가 만들어진다. Mono 로 돌리면 멀쩡히
    돌기 때문에 리눅스에서는 절대 드러나지 않고, 윈도우에서만 MissingMethodException
    으로 조용히 죽는다.

    실제로 string.Split(char, StringSplitOptions) 때문에 이 일이 있었다.

    빌드가 -sdk:4.8 을 쓰면 컴파일 단계에서 걸리지만, 그걸 빠뜨렸을 때를 대비해
    만들어진 IL 을 직접 한 번 더 본다.

    python3 tools/check_net48.py dist/PokemonTaskbar.exe
"""

import os
import re
import subprocess
import sys

REFERENCE_DIR = "/usr/lib/mono/4.8-api"

# 이 어셈블리에서 온 호출만 본다. 우리가 만든 타입은 검사 대상이 아니다.
WATCHED = ("mscorlib", "System", "System.Core", "System.Drawing", "System.Windows.Forms")

MEMBER_REF = re.compile(r"\[([\w.]+)\]([\w.`/]+)(?:<[^>]*>)?::(\.?\w+)\s*\(")
END_OF_METHOD = re.compile(r"\}\s*//\s*end of method ([\w.`/]+)::(\.?\w+)")
# 메서드 선언은 "... cil managed" 로 끝나지만, 뒤에 noinlining / internalcall /
# preservesig 같은 말이 더 붙는 경우가 많다. 그래서 끝을 글자로 맞추지 않고
# "괄호가 닫혔고 managed 가 나왔는가" 로 본다. 예전에는 끝을 못 알아보고 뒤따르는
# 메서드들을 통째로 삼켜, 멀쩡한 API 를 없는 것으로 잘못 신고했다.
MANAGED = re.compile(r"\b(managed|unmanaged)\b")


def disassemble(path):
    try:
        return subprocess.run(
            ["ikdasm", path], capture_output=True, text=True, check=True
        ).stdout
    except FileNotFoundError:
        sys.exit("ikdasm 이 없습니다. apt install mono-utils 로 설치하세요.")
    except subprocess.CalledProcessError as error:
        sys.exit("ikdasm 실패: %s\n%s" % (path, error.stderr[:400]))


def split_arguments(text, open_paren):
    """여는 괄호부터 짝이 맞는 닫는 괄호까지를 인자 단위로 자른다."""
    depth = 0
    parts = []
    start = open_paren + 1
    for index in range(open_paren, len(text)):
        char = text[index]
        if char in "([<":
            depth += 1
        elif char in ")]>":
            depth -= 1
            if depth == 0:
                parts.append(text[start:index])
                inside = text[open_paren + 1 : index].strip()
                return [] if not inside else parts
        elif char == "," and depth == 1:
            parts.append(text[start:index])
            start = index + 1
    return None


def strip_call(text, word):
    """`marshal( ... )` 처럼 괄호가 중첩된 장식을 통째로 걷어낸다."""
    while True:
        start = text.find(word + "(")
        if start < 0:
            return text
        depth = 0
        for index in range(start + len(word), len(text)):
            if text[index] == "(":
                depth += 1
            elif text[index] == ")":
                depth -= 1
                if depth == 0:
                    text = text[:start] + text[index + 1:]
                    break
        else:
            return text[:start]


NOISE = ("valuetype", "class", "instance", "explicit", "default", "modopt", "modreq")


def normalise_type(text, drop_name):
    """[어셈블리] 표기와 매개변수 이름을 걷어내고 타입 이름만 남긴다."""
    text = re.sub(r"\[[\w.]+\]", "", text)          # [mscorlib] 같은 접두사
    text = re.sub(r"modopt\([^)]*\)", "", text)
    text = re.sub(r"modreq\([^)]*\)", "", text)
    text = strip_call(text, "marshal")
    text = text.replace("valuetype", " ").replace("class", " ")
    text = re.sub(r"\s+", " ", text).strip()
    if not text:
        return ""
    # 두 낱말짜리 기본 타입은 먼저 한 낱말로 붙여 둔다.
    # 안 그러면 뒤의 낱말을 매개변수 이름으로 잘못 알고 떼어 버린다.
    text = text.replace("native unsigned int", "nativeuint")
    text = text.replace("native int", "nativeint")
    text = re.sub(r"unsigned int(\d*)", r"uint\1", text)
    if drop_name:
        # ikdasm 은 매개변수 이름을 "char[] separator" 로도, "bool'value'" 로도 붙인다.
        text = re.sub(r"'[^']*'\s*$", "", text).strip()
        parts = text.rsplit(" ", 1)
        if len(parts) == 2 and re.match(r"^[A-Za-z_]\w*$", parts[1]):
            text = parts[0]
    return re.sub(r"\s+", "", text)


def signature(text, open_paren, drop_names):
    parts = split_arguments(text, open_paren)
    if parts is None:
        return None
    return tuple(normalise_type(part, drop_names) for part in parts)


def calls_made(il):
    """exe 가 부르는 (어셈블리, 타입, 메서드, 인자수) 모음."""
    found = set()
    for match in MEMBER_REF.finditer(il):
        assembly, type_name, member = match.groups()
        if assembly not in WATCHED:
            continue
        sig = signature(il, match.end() - 1, drop_names=False)
        if sig is None:
            continue
        found.add((assembly, type_name.split("<")[0], member, sig))
    return found


CLASS_FLAGS = set("""public private auto ansi sealed beforefieldinit abstract interface
serializable nested family assembly famandassem famorassem explicit sequential
unicode autochar import literal specialname rtspecialname windowsruntime""".split())

CLASS_LINE = re.compile(r"^\.class\s+(.*)$")
END_OF_CLASS = re.compile(r"\}\s*//\s*end of class")


def class_name(rest):
    """.class 줄에서 타입 이름만 뽑는다."""
    rest = rest.split(" extends ")[0].split(" implements ")[0]
    for token in reversed(rest.split()):
        if token and token not in CLASS_FLAGS:
            return token
    return None


def declaration_ended(text):
    """메서드 선언(서명)이 여기서 끝났는지. 괄호가 닫히고 managed 가 나오면 끝이다."""
    if "(" not in text or not MANAGED.search(text):
        return False
    return text.count("(") <= text.count(")")


def methods_defined(il):
    """참조 어셈블리가 실제로 가진 (타입, 메서드, 인자타입) 모음."""
    defined = set()
    stack = []
    buffer = []
    collecting = False
    for raw in il.splitlines():
        line = raw.strip()

        if collecting:
            buffer.append(line)
            if declaration_ended(" ".join(buffer)):
                collecting = False
                text = " ".join(buffer)
                paren = text.find("(")
                sig = signature(text, paren, drop_names=True) if paren >= 0 else None
                name = method_name(text, paren)
                if sig is not None and name and stack:
                    defined.add(("/".join(stack), name, sig))
                buffer = []
            continue

        match = CLASS_LINE.match(line)
        if match:
            name = class_name(match.group(1))
            if name:
                stack.append(name if not stack else name)
            continue

        if END_OF_CLASS.search(line):
            if stack:
                stack.pop()
            continue

        if line.startswith(".method"):
            collecting = True
            buffer = [line]
            if declaration_ended(" ".join(buffer)):
                collecting = False
                text = line
                paren = text.find("(")
                sig = signature(text, paren, drop_names=True) if paren >= 0 else None
                name = method_name(text, paren)
                if sig is not None and name and stack:
                    defined.add(("/".join(stack), name, sig))
                buffer = []
    return defined


def method_name(text, paren):
    """서명 문자열에서 여는 괄호 바로 앞의 이름을 뽑는다."""
    if paren < 0:
        return None
    head = text[:paren].rstrip()
    head = re.sub(r"<[^<>]*>$", "", head)          # 제네릭 메서드의 <T>
    match = re.search(r"([A-Za-z_.`][\w.`]*)$", head)
    return match.group(1) if match else None


def reference_index():
    index = {}
    for assembly in WATCHED:
        path = os.path.join(REFERENCE_DIR, assembly + ".dll")
        if not os.path.exists(path):
            sys.exit("참조 어셈블리가 없습니다: %s" % path)
        index[assembly] = methods_defined(disassemble(path))
    return index


def main(paths):
    if not os.path.isdir(REFERENCE_DIR):
        print("건너뜀: %s 가 없어 검사하지 못했습니다." % REFERENCE_DIR)
        return 0

    index = reference_index()
    # 타입은 여러 어셈블리로 옮겨 다니므로(타입 전달), 어디에 있든 있으면 통과시킨다.
    everywhere = set()
    for members in index.values():
        everywhere |= members

    bad = []
    for path in paths:
        for assembly, type_name, member, sig in sorted(calls_made(disassemble(path))):
            if (type_name, member, sig) in everywhere:
                continue
            # 이름이 같은 메서드가 아예 없으면 제네릭 등 우리가 못 읽는 형태이므로 넘어간다.
            overloads = [
                other for known, name, other in everywhere
                if known == type_name and name == member
            ]
            if not overloads:
                continue
            bad.append((path, assembly, type_name, member, sig, overloads))

    if bad:
        print(".NET Framework 4.8 에 없는 API 를 부르고 있습니다:")
        for path, assembly, type_name, member, sig, overloads in bad:
            print("  %s" % path)
            print("    부름:   [%s]%s::%s(%s)" % (assembly, type_name, member, ", ".join(sig)))
            for other in sorted(overloads)[:6]:
                print("    있는것: %s::%s(%s)" % (type_name, member, ", ".join(other)))
        print()
        print("윈도우에서 MissingMethodException 으로 조용히 죽습니다.")
        print("빌드에 -sdk:4.8 이 빠졌는지 확인하세요.")
        return 1

    print("API 검사 통과: .NET Framework 4.8 에 있는 것만 부릅니다.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
