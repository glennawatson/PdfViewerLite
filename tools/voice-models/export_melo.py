"""Exports MeloTTS-English and the BERT layers it reads to ONNX, with the text front end's data files.

Run by .github/workflows/voice-models.yml, never by the app: PdfViewerLite reads the files this writes with
ONNX Runtime and its own C# front end, so nothing here ships.

    python export_melo.py <MeloTTS checkout> <output folder>
"""

import json
import os
import struct
import sys

import numpy as np
import torch

melo_root = os.path.abspath(sys.argv[1])
out = os.path.abspath(sys.argv[2])
sys.path.insert(0, melo_root)
os.makedirs(out, exist_ok=True)

from huggingface_hub import hf_hub_download  # noqa: E402
from transformers import AutoModel, AutoTokenizer  # noqa: E402

from melo import utils  # noqa: E402
from melo.models import SynthesizerTrn  # noqa: E402

OPSET = 17
BERT_LAYERS = 10  # MeloTTS reads hidden_states[-3] of 12 layers: the output of layer 10.


class Synthesizer(torch.nn.Module):
    """MeloTTS's inference, with the unused Chinese BERT input fixed at zero."""

    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, x, x_lengths, tones, languages, ja_bert, sid, noise_scale, length_scale, noise_scale_w, sdp_ratio):
        bert = torch.zeros(x.shape[0], 1024, x.shape[1], dtype=ja_bert.dtype)
        audio = self.model.infer(
            x, x_lengths, sid, tones, languages, bert, ja_bert,
            noise_scale=noise_scale, length_scale=length_scale, noise_scale_w=noise_scale_w, sdp_ratio=sdp_ratio,
        )[0]
        return audio[0, 0]


def export_synthesizer():
    config_path = hf_hub_download("myshell-ai/MeloTTS-English", "config.json")
    checkpoint_path = hf_hub_download("myshell-ai/MeloTTS-English", "checkpoint.pth")
    hps = utils.get_hparams_from_file(config_path)
    model = SynthesizerTrn(
        len(hps.symbols),
        hps.data.filter_length // 2 + 1,
        hps.train.segment_size // hps.data.hop_length,
        n_speakers=hps.data.n_speakers,
        num_tones=hps.num_tones,
        num_languages=hps.num_languages,
        **hps.model,
    )
    state = torch.load(checkpoint_path, map_location="cpu")
    model.load_state_dict(state["model"], strict=True)
    model.eval()
    for module in model.modules():
        if hasattr(module, "remove_weight_norm"):
            try:
                module.remove_weight_norm()
            except ValueError:
                pass

    length = 40
    inputs = (
        torch.randint(1, 100, (1, length), dtype=torch.long),
        torch.tensor([length], dtype=torch.long),
        torch.zeros(1, length, dtype=torch.long),
        torch.full((1, length), 2, dtype=torch.long),
        torch.randn(1, 768, length),
        torch.tensor([3], dtype=torch.long),
        torch.tensor(0.6),
        torch.tensor(1.0),
        torch.tensor(0.8),
        torch.tensor(0.2),
    )
    names = ["x", "x_lengths", "tones", "languages", "ja_bert", "sid", "noise_scale", "length_scale", "noise_scale_w", "sdp_ratio"]
    with torch.no_grad():
        torch.onnx.export(
            Synthesizer(model), inputs, os.path.join(out, "melo-en.onnx"),
            input_names=names, output_names=["audio"], opset_version=OPSET, dynamo=False,
            dynamic_axes={"x": {1: "t"}, "tones": {1: "t"}, "languages": {1: "t"}, "ja_bert": {2: "t"}, "audio": {0: "samples"}},
        )

    with open(os.path.join(out, "melo-en.json"), "w", encoding="utf-8") as f:
        json.dump({"symbols": hps.symbols, "speakers": dict(hps.data.spk2id.items()), "sampleRate": hps.data.sampling_rate}, f, ensure_ascii=False)


class Bert(torch.nn.Module):
    """bert-base-uncased cut after the layer MeloTTS reads."""

    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, input_ids, attention_mask, token_type_ids):
        return self.model(input_ids=input_ids, attention_mask=attention_mask, token_type_ids=token_type_ids).last_hidden_state[0]


def export_bert():
    tokenizer = AutoTokenizer.from_pretrained("bert-base-uncased")
    model = AutoModel.from_pretrained("bert-base-uncased", attn_implementation="eager")
    model.encoder.layer = model.encoder.layer[:BERT_LAYERS]
    model.eval()
    encoded = tokenizer("the garden behind the library", return_tensors="pt")
    path = os.path.join(out, "bert-en-fp32.onnx")
    with torch.no_grad():
        torch.onnx.export(
            Bert(model), (encoded["input_ids"], encoded["attention_mask"], encoded["token_type_ids"]), path,
            input_names=["input_ids", "attention_mask", "token_type_ids"], output_names=["hidden"], opset_version=OPSET, dynamo=False,
            dynamic_axes={"input_ids": {1: "t"}, "attention_mask": {1: "t"}, "token_type_ids": {1: "t"}, "hidden": {0: "t"}},
        )

    from onnxruntime.quantization import QuantType, quantize_dynamic

    quantize_dynamic(path, os.path.join(out, "bert-en.onnx"), weight_type=QuantType.QInt8, per_channel=True)
    os.remove(path)
    tokenizer.save_vocabulary(out)
    os.replace(os.path.join(out, "vocab.txt"), os.path.join(out, "bert-en-vocab.txt"))


def export_lexicon():
    """Writes MeloTTS's CMU dictionary, then nltk's entries it lacks, as 'WORD<TAB>AH0 B ...' lines."""
    from melo.text.english import eng_dict
    from nltk.corpus import cmudict

    lines = {}
    for word, syllables in eng_dict.items():
        lines[word.upper()] = " ".join(p for s in syllables for p in s)
    for word, prons in cmudict.dict().items():
        lines.setdefault(word.upper(), " ".join(prons[0]))
    with open(os.path.join(out, "melo-en-lexicon.txt"), "w", encoding="utf-8", newline="\n") as f:
        for word in sorted(lines):
            f.write(f"{word}\t{lines[word]}\n")


def export_g2p():
    """Writes g2p_en's spelling-to-sound network as named little-endian float32 arrays."""
    import g2p_en

    variables = np.load(os.path.join(os.path.dirname(g2p_en.__file__), "checkpoint20.npz"))
    names = ["enc_emb", "enc_w_ih", "enc_w_hh", "enc_b_ih", "enc_b_hh", "dec_emb", "dec_w_ih", "dec_w_hh", "dec_b_ih", "dec_b_hh", "fc_w", "fc_b"]
    with open(os.path.join(out, "g2p-en.bin"), "wb") as f:
        for name in names:
            array = variables[name].astype("<f4")
            f.write(struct.pack("<i", array.ndim))
            for dim in array.shape:
                f.write(struct.pack("<i", dim))
            f.write(array.tobytes())


if __name__ == "__main__":
    steps = sys.argv[3:] or ["synthesizer", "bert", "lexicon", "g2p"]
    for step in steps:
        {"synthesizer": export_synthesizer, "bert": export_bert, "lexicon": export_lexicon, "g2p": export_g2p}[step]()
        print("exported", step)
