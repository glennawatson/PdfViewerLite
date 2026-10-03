"""Trims a MeloTTS checkout to English, so exporting needs none of the Chinese, Japanese or Korean text tools."""

import pathlib
import sys

text = pathlib.Path(sys.argv[1]) / "melo" / "text"


def replace(name, old, new):
    path = text / name
    source = path.read_text(encoding="utf-8")
    if old not in source:
        raise SystemExit(f"{name}: expected text not found")
    path.write_text(source.replace(old, new), encoding="utf-8")


replace("cleaner.py", "from . import chinese, japanese, english, chinese_mix, korean, french, spanish", "from . import english")
replace(
    "cleaner.py",
    """language_module_map = {"ZH": chinese, "JP": japanese, "EN": english, 'ZH_MIX_EN': chinese_mix, 'KR': korean,
                    'FR': french, 'SP': spanish, 'ES': spanish}""",
    'language_module_map = {"EN": english}',
)
replace(
    "english.py",
    "from .japanese import distribute_phone\n",
    '''

def distribute_phone(n_phone, n_word):
    phones_per_word = [0] * n_word
    for _ in range(n_phone):
        phones_per_word[phones_per_word.index(min(phones_per_word))] += 1
    return phones_per_word

''',
)
init = text / "__init__.py"
source = init.read_text(encoding="utf-8")
start = source.index("def get_bert(")
init.write_text(
    source[:start]
    + "def get_bert(norm_text, word2ph, language, device):\n"
    + "    from .english_bert import get_bert_feature as en_bert\n\n"
    + "    return en_bert(norm_text, word2ph, device)\n",
    encoding="utf-8",
)
print("patched", text)
