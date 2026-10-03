"""Writes voices.json: the name, size and SHA-256 of every file in the release folder, which the app checks downloads against."""

import hashlib
import json
import os
import sys

folder = sys.argv[1]
files = []
for name in sorted(os.listdir(folder)):
    if name == "voices.json":
        continue
    digest = hashlib.sha256()
    with open(os.path.join(folder, name), "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    files.append({"name": name, "bytes": os.path.getsize(os.path.join(folder, name)), "sha256": digest.hexdigest()})
with open(os.path.join(folder, "voices.json"), "w", encoding="utf-8") as f:
    json.dump({"files": files}, f, indent=2)
print(json.dumps(files, indent=2))
