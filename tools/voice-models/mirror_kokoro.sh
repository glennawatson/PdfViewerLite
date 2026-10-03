#!/usr/bin/env bash
# Mirrors the Kokoro-82M files Read Aloud uses into the release folder, at pinned revisions, with flat names.
set -euo pipefail
out="$1"
mkdir -p "$out"
curl -fsSL --retry 4 -o "$out/kokoro-model_uint8.onnx" "$KOKORO_ONNX/onnx/model_uint8.onnx"
for voice in af_heart af_bella am_michael bf_emma bm_george; do
  curl -fsSL --retry 4 -o "$out/kokoro-$voice.bin" "$KOKORO_ONNX/voices/$voice.bin"
done
for lexicon in us_gold us_silver gb_gold gb_silver; do
  curl -fsSL --retry 4 -o "$out/misaki-$lexicon.json" "$MISAKI/$lexicon.json"
done
