"""Writes what MeloTTS's English front end makes of a set of sentences, for PdfViewerLite's C# front end to match.

    python reference_melo.py <MeloTTS checkout> <output json>
"""

import json
import os
import sys

sys.path.insert(0, os.path.abspath(sys.argv[1]))

from transformers import AutoTokenizer  # noqa: E402

from melo import commons  # noqa: E402
from melo.text import cleaned_text_to_sequence  # noqa: E402
from melo.text.cleaner import clean_text  # noqa: E402
from melo.text.english import _g2p  # noqa: E402
from melo.text.symbols import symbols  # noqa: E402

SENTENCES = [
    "the garden behind the library had been neglected for years.",
    "brambles covered the old brick paths, and the fountain had long since stopped!",
    "don't you think it's a well-known café in brisbane?",
    "the activationist met a zyxglorp near kookaburra creek; nobody knew why.",
    "she said, 'quietly now', and walked away...",
    "pdfviewerlite reads documents aloud with kokoro and melotts.",
    "on the twelfth of march, doctor smith paid one thousand two hundred dollars.",
    "antidisestablishmentarianism is a very long word indeed",
]

WORDS = ["activationist", "zyxglorp", "melotts", "pdfviewerlite", "kookaburra", "wollongong", "nbn", "x"]

tokenizer = AutoTokenizer.from_pretrained("bert-base-uncased")
symbol_to_id = {s: i for i, s in enumerate(symbols)}
cases = []
for sentence in SENTENCES:
    norm_text, phones, tones, word2ph = clean_text(sentence, "EN")
    ids, tone_ids, languages = cleaned_text_to_sequence(phones, tones, "EN", symbol_to_id)
    ids = commons.intersperse(ids, 0)
    tone_ids = commons.intersperse(tone_ids, 0)
    languages = commons.intersperse(languages, 0)
    word2ph = [w * 2 for w in word2ph]
    word2ph[0] += 1
    cases.append({
        "text": norm_text,
        "phones": phones,
        "ids": ids,
        "tones": tone_ids,
        "languages": languages,
        "wordToPhones": word2ph,
        "tokenIds": tokenizer(norm_text)["input_ids"],
    })

predictions = [{"word": w, "phones": _g2p.predict(w)} for w in WORDS]
with open(sys.argv[2], "w", encoding="utf-8") as f:
    json.dump({"cases": cases, "predictions": predictions}, f, ensure_ascii=False, indent=1)
print(json.dumps(cases[2], ensure_ascii=False))
