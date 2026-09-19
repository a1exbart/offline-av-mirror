#!/usr/bin/env python3
"""Render assets/icon.svg into the PNG and ICO the application ships.

Usage:
    uv run --with cairosvg --with pillow build/render-icons.py

The generated files are committed, so nobody needs this to build the project;
run it after changing the SVG.
"""

from __future__ import annotations

import io
from pathlib import Path

import cairosvg
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "assets" / "icon.svg"
ASSETS = ROOT / "src" / "OfflineAvMirror" / "Assets"
ICO_SIZES = (16, 24, 32, 48, 64, 128, 256)


def render(size: int) -> Image.Image:
    png = cairosvg.svg2png(url=str(SOURCE), output_width=size, output_height=size)
    return Image.open(io.BytesIO(png)).convert("RGBA")


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)
    render(128).save(ASSETS / "icon-128.png")
    render(256).save(
        ASSETS / "icon.ico",
        format="ICO",
        sizes=[(s, s) for s in ICO_SIZES],
    )
    print(f"wrote {ASSETS / 'icon-128.png'} and {ASSETS / 'icon.ico'}")


if __name__ == "__main__":
    main()
