# -*- coding: utf-8 -*-
"""사용자 설정을 파일에 저장하고 불러온다.

우클릭 메뉴에서 바꾼 내용(어떤 포켓몬이 몇 마리, 크기, 속도 등)을 기억해
다음에 켤 때 그대로 되살리기 위한 것이다. C# 판도 같은 파일을 읽고 쓰므로
형식은 한 줄에 하나씩인 아주 단순한 `이름 = 값` 텍스트로 맞춰 두었다.
"""

import os

APP_NAME = "PokemonTaskbar"
FILE_NAME = "settings.txt"

DEFAULTS = {
    "species": ["pikachu"],
    "scale": 4.5,
    "speed": 55.0,
    "offset": 0,
    "on_taskbar": False,
}


ENV_OVERRIDE = "POKEMON_TASKBAR_SETTINGS"


def settings_path():
    """설정 파일 경로. 윈도우는 %APPDATA%, 그 외에는 홈 아래에 둔다.

    POKEMON_TASKBAR_SETTINGS 환경 변수로 다른 곳을 지정할 수 있다.
    """
    override = os.environ.get(ENV_OVERRIDE)
    if override:
        return override
    appdata = os.environ.get("APPDATA")
    if appdata:
        folder = os.path.join(appdata, APP_NAME)
    else:
        base = os.environ.get("XDG_CONFIG_HOME") or os.path.join(
            os.path.expanduser("~"), ".config"
        )
        folder = os.path.join(base, "pokemon-taskbar")
    return os.path.join(folder, FILE_NAME)


def parse_text(text, known_species=None):
    """설정 텍스트를 딕셔너리로. 이상한 줄은 조용히 무시한다."""
    values = dict(DEFAULTS)
    values["species"] = list(DEFAULTS["species"])

    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        name, separator, raw = line.partition("=")
        if not separator:
            continue
        name = name.strip()
        raw = raw.strip()
        try:
            if name == "species":
                names = [part.strip() for part in raw.split(",") if part.strip()]
                if known_species is not None:
                    names = [n for n in names if n in known_species]
                if names:
                    values["species"] = names
            elif name == "scale":
                number = float(raw)
                if number > 0:
                    values["scale"] = number
            elif name == "speed":
                number = float(raw)
                if number > 0:
                    values["speed"] = number
            elif name == "offset":
                values["offset"] = int(raw)
            elif name == "on_taskbar":
                values["on_taskbar"] = raw.lower() in ("1", "true", "yes", "on")
        except ValueError:
            continue          # 숫자가 아니면 기본값을 그대로 둔다
    return values


def format_text(values):
    return "\n".join([
        "# 하단바 포켓몬 설정 - 프로그램이 자동으로 저장합니다.",
        "# 직접 고쳐도 되고, 파일을 지우면 처음 상태로 돌아갑니다.",
        "species = %s" % ", ".join(values["species"]),
        "scale = %g" % values["scale"],
        "speed = %g" % values["speed"],
        "offset = %d" % values["offset"],
        "on_taskbar = %s" % ("true" if values["on_taskbar"] else "false"),
        "",
    ])


def load(path=None, known_species=None):
    """저장된 설정을 읽는다. 파일이 없거나 깨졌으면 기본값."""
    path = path or settings_path()
    try:
        with open(path, encoding="utf-8") as handle:
            return parse_text(handle.read(), known_species)
    except (OSError, UnicodeDecodeError):
        values = dict(DEFAULTS)
        values["species"] = list(DEFAULTS["species"])
        return values


def save(values, path=None):
    """설정을 저장한다. 실패해도 프로그램이 죽지 않도록 조용히 넘어간다."""
    path = path or settings_path()
    try:
        folder = os.path.dirname(path)
        if folder and not os.path.isdir(folder):
            os.makedirs(folder)
        with open(path, "w", encoding="utf-8") as handle:
            handle.write(format_text(values))
        return True
    except OSError:
        return False
