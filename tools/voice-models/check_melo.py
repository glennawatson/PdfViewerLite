"""Checks the exported files against MeloTTS in PyTorch: the same audio without noise, and close BERT features.

    python check_melo.py <MeloTTS checkout> <export folder>
"""

import os
import sys

import numpy as np
import onnxruntime as ort
import torch

sys.path.insert(0, os.path.abspath(sys.argv[1]))
folder = sys.argv[2]

from transformers import AutoModel, AutoTokenizer  # noqa: E402

from huggingface_hub import hf_hub_download  # noqa: E402

from melo import utils  # noqa: E402
from melo.models import SynthesizerTrn  # noqa: E402

TEXT = "The garden behind the library had been neglected for years, and the fountain had long since stopped."
MAX_SAMPLE_DIFFERENCE = 1e-3
MIN_BERT_COSINE = 0.97

hps = utils.get_hparams_from_file(hf_hub_download("myshell-ai/MeloTTS-English", "config.json"))
model = SynthesizerTrn(
    len(hps.symbols),
    hps.data.filter_length // 2 + 1,
    hps.train.segment_size // hps.data.hop_length,
    n_speakers=hps.data.n_speakers,
    num_tones=hps.num_tones,
    num_languages=hps.num_languages,
    **hps.model,
)
model.load_state_dict(torch.load(hf_hub_download("myshell-ai/MeloTTS-English", "checkpoint.pth"), map_location="cpu")["model"], strict=True)
model.eval()
symbol_to_id = {s: i for i, s in enumerate(hps.symbols)}
bert, ja_bert, phones, tones, languages = utils.get_text_for_tts_infer(TEXT, "EN", hps, "cpu", symbol_to_id)
with torch.no_grad():
    expected = model.infer(
        phones[None], torch.tensor([len(phones)]), torch.tensor([3]), tones[None], languages[None], bert[None], ja_bert[None],
        noise_scale=0, noise_scale_w=0, sdp_ratio=0, length_scale=1,
    )[0][0, 0].numpy()

session = ort.InferenceSession(os.path.join(folder, "melo-en.onnx"))
actual = session.run(None, {
    "x": phones[None].numpy(), "x_lengths": np.array([len(phones)]), "tones": tones[None].numpy(),
    "languages": languages[None].numpy(), "ja_bert": ja_bert[None].numpy().astype(np.float32), "sid": np.array([3]),
    "noise_scale": np.array(0, np.float32), "length_scale": np.array(1, np.float32),
    "noise_scale_w": np.array(0, np.float32), "sdp_ratio": np.array(0, np.float32),
})[0]
difference = float(np.abs(actual - expected).max()) if len(actual) == len(expected) else float("inf")
print(f"synthesizer: {len(actual)} samples, largest difference {difference}")

tokenizer = AutoTokenizer.from_pretrained("bert-base-uncased")
model = AutoModel.from_pretrained("bert-base-uncased", output_hidden_states=True)
encoded = tokenizer(TEXT.lower(), return_tensors="np")
with torch.no_grad():
    reference = model(**{k: torch.tensor(v) for k, v in encoded.items()}).hidden_states[-3][0].numpy()
features = ort.InferenceSession(os.path.join(folder, "bert-en.onnx")).run(None, {k: v.astype(np.int64) for k, v in encoded.items()})[0]
cosine = float(np.mean(np.sum(features * reference, 1) / np.linalg.norm(features, axis=1) / np.linalg.norm(reference, axis=1)))
print(f"bert: mean cosine {cosine}")

if difference > MAX_SAMPLE_DIFFERENCE or cosine < MIN_BERT_COSINE:
    raise SystemExit("the exported voice does not match MeloTTS")
