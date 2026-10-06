"""Generate the bundled Hungarian WAV clips for the content pack.

Development setup:
  python -m venv .tools/piper-venv
  .tools/piper-venv/Scripts/python -m pip install piper-tts==1.8.0
  .tools/piper-venv/Scripts/python -m piper.download_voices --download-dir .tools/piper-voices hu_HU-anna-medium
  .tools/piper-venv/Scripts/python HungarianWidget/scripts/generate_expanded_cards.py
  .tools/piper-venv/Scripts/python HungarianWidget/scripts/generate_audio.py

The Piper runtime and voice model are build-time tools only. The app bundles
the generated WAV files and does not synthesize speech or access the network.
"""

from __future__ import annotations

import argparse
import json
import wave
from pathlib import Path

from piper import PiperVoice
from piper.config import SynthesisConfig


ROOT = Path(__file__).resolve().parents[2]
APP_ROOT = ROOT / "HungarianWidget"
CARDS_FILE = APP_ROOT / "Content" / "cards.json"
EXPANDED_CARDS_FILE = APP_ROOT / "Content" / "expanded-cards.json"
MODEL_FILE = ROOT / ".tools" / "piper-voices" / "hu_HU-anna-medium.onnx"
AUDIO_DIR = APP_ROOT / "Content" / "Audio"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--force", action="store_true", help="Regenerate existing audio clips")
    args = parser.parse_args()

    if not MODEL_FILE.exists():
        raise SystemExit(f"Missing Piper model: {MODEL_FILE}")

    packs = [
        json.loads(path.read_text(encoding="utf-8"))["cards"]
        for path in (CARDS_FILE, EXPANDED_CARDS_FILE)
    ]
    cards = [card for pack_cards in packs for card in pack_cards]
    audio_jobs: list[tuple[str, str]] = []
    for card in cards:
        audio_jobs.append((card["id"], card["hungarian"]))
        if card.get("exampleAudioId") and card.get("exampleHungarian"):
            audio_jobs.append((card["exampleAudioId"], card["exampleHungarian"]))

    AUDIO_DIR.mkdir(parents=True, exist_ok=True)
    voice = PiperVoice.load(MODEL_FILE)
    synthesis = SynthesisConfig(length_scale=1.12, volume=1.0)

    generated = 0
    for index, (audio_id, hungarian) in enumerate(audio_jobs, start=1):
        output = AUDIO_DIR / f"{audio_id}.wav"
        if output.exists() and not args.force:
            continue

        chunks = list(voice.synthesize(hungarian, synthesis))
        if not chunks:
            raise RuntimeError(f"No audio was generated for {audio_id} ({hungarian})")

        with wave.open(str(output), "wb") as wav_file:
            wav_file.setnchannels(chunks[0].sample_channels)
            wav_file.setsampwidth(2)
            wav_file.setframerate(chunks[0].sample_rate)
            for chunk in chunks:
                wav_file.writeframes(chunk.audio_int16_bytes)

        generated += 1
        if generated % 50 == 0 or index == len(audio_jobs):
            print(f"Generated {generated} new clips ({index}/{len(audio_jobs)} clips processed)")

    print(f"Audio pack ready: {len(cards)} content records, {len(audio_jobs)} clips, {len(list(AUDIO_DIR.glob('*.wav')))} WAV files")


if __name__ == "__main__":
    main()
