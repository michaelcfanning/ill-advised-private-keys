#!/usr/bin/env bash
# Mirror all Blockchair Bitcoin outputs dumps (ever-funded data for the economics).
# Resumable: skips complete files, resumes partials. Polite parallelism.
set -u
BASE="https://gz.blockchair.com/bitcoin/outputs"
DIR="/e/ill-advised/blockchair/outputs"
MANIFEST="/e/ill-advised/blockchair/outputs.manifest.txt"
LOG="/e/ill-advised/blockchair/download.log"
PARALLEL=5

mkdir -p "$DIR"
cd "$DIR" || exit 1
: > "$LOG"
total=$(wc -l < "$MANIFEST")
echo "$(date -u +%FT%TZ) start: $total files -> $DIR" >> "$LOG"

export BASE LOG
cat "$MANIFEST" | xargs -P "$PARALLEL" -I {} bash -c '
  f="{}"
  # already complete?
  if [ -s "$f" ] && [ ! -f "$f.part" ]; then exit 0; fi
  if curl -s -f --retry 6 --retry-delay 3 -C - -o "$f.part" "$BASE/$f"; then
    mv -f "$f.part" "$f"
  else
    echo "$(date -u +%FT%TZ) FAIL $f" >> "$LOG"
  fi
'

done_count=$(ls -1 "$DIR"/blockchair_bitcoin_outputs_*.tsv.gz 2>/dev/null | grep -vc '\.part$')
echo "$(date -u +%FT%TZ) done: $done_count/$total present" >> "$LOG"
