"""Draws the three flat illustrations of the closing card "Saturday, 3 pm" (debrief, phase 6) as 512x512 PNGs.

No real people, no photos: simple shapes only (numpy, no extra packages). Output: Assets/Hospital/Art/Debrief/.
  Consequence_Good.png      a video call from the party: the granddaughter with a party hat, waving
  Consequence_Mixed.png     the birthday card still closed on the nightstand, rain on the window
  Consequence_Critical.png  an empty chair next to a birthday cake
Run from Tools/AiServer:  .venv/Scripts/python.exe tools/gen_consequence_images.py
"""
from __future__ import annotations

import math
import struct
import zlib
from pathlib import Path

import numpy as np

PROJECT = Path(__file__).resolve().parents[3]
OUT = PROJECT / "Assets/Hospital/Art/Debrief"
N = 512
Y, X = np.mgrid[0:N, 0:N].astype(np.float32)


def rgb(h: str) -> np.ndarray:
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32)


class Canvas:
    def __init__(self, color: str):
        self.img = np.zeros((N, N, 3), np.float32)
        self.img[:] = rgb(color)

    def paint(self, mask: np.ndarray, color: str, alpha: float = 1.0) -> None:
        m = np.clip(mask.astype(np.float32), 0, 1)[..., None] * alpha
        self.img = self.img * (1 - m) + rgb(color) * m

    def rect(self, x0, y0, x1, y1, color, r=0.0, alpha=1.0):
        cx, cy, hw, hh = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2
        dx = np.maximum(np.abs(X - cx) - hw + r, 0)
        dy = np.maximum(np.abs(Y - cy) - hh + r, 0)
        d = np.sqrt(dx * dx + dy * dy) - r
        self.paint(np.clip(0.5 - d, 0, 1), color, alpha)

    def circle(self, cx, cy, r, color, alpha=1.0, sx=1.0, sy=1.0):
        d = np.sqrt(((X - cx) / sx) ** 2 + ((Y - cy) / sy) ** 2) - r
        self.paint(np.clip(0.5 - d, 0, 1), color, alpha)

    def poly(self, pts, color, alpha=1.0):
        inside = np.ones((N, N), bool)
        n = len(pts)
        area = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n))
        s = 1 if area > 0 else -1
        for i in range(n):
            (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % n]
            inside &= s * ((x1 - x0) * (Y - y0) - (y1 - y0) * (X - x0)) >= 0
        self.paint(inside, color, alpha)

    def line(self, x0, y0, x1, y1, w, color, alpha=1.0):
        dx, dy = x1 - x0, y1 - y0
        length2 = dx * dx + dy * dy
        t = np.clip(((X - x0) * dx + (Y - y0) * dy) / length2, 0, 1)
        d = np.sqrt((X - x0 - t * dx) ** 2 + (Y - y0 - t * dy) ** 2) - w / 2
        self.paint(np.clip(0.5 - d, 0, 1), color, alpha)

    def arc(self, cx, cy, r, a0, a1, w, color):
        ang = np.arctan2(Y - cy, X - cx)
        d = np.abs(np.sqrt((X - cx) ** 2 + (Y - cy) ** 2) - r) - w / 2
        self.paint(np.clip(0.5 - d, 0, 1) * ((ang >= a0) & (ang <= a1)), color)

    def save(self, path: Path):
        img = np.clip(self.img, 0, 255).astype(np.uint8)
        raw = b"".join(b"\x00" + img[y].tobytes() for y in range(N))

        def chunk(tag, data):
            return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

        png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", N, N, 8, 2, 0, 0, 0))
        png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(png)


def cake(c: Canvas, cx, base, w=150):
    c.rect(cx - w / 2, base - 70, cx + w / 2, base, "f2c6d3", r=10)
    c.rect(cx - w / 2, base - 80, cx + w / 2, base - 62, "fff4f0", r=8)
    for i in range(5):
        x = cx - w / 2 + 18 + i * (w - 36) / 4
        c.circle(x, base - 30, 6, "e2557a")
    for i in range(3):
        x = cx - 30 + i * 30
        c.rect(x - 4, base - 112, x + 4, base - 80, "7fb3e6", r=2)
        c.circle(x, base - 120, 6, "ffc93c", sy=1.5)
    c.rect(cx - w / 2 - 20, base, cx + w / 2 + 20, base + 10, "d9d9d9", r=4)


def good() -> Canvas:
    c = Canvas("2b2f3a")
    c.rect(40, 30, 472, 482, "111318", r=36)              # the tablet
    c.rect(58, 50, 454, 430, "bfe3f5", r=16)              # the call
    for i in range(40):                                   # confetti
        rx, ry = (i * 97) % 380 + 70, (i * 53) % 340 + 60
        c.rect(rx, ry, rx + 9, ry + 5, ["ff6b8b", "ffd24a", "6fd3a0", "7aa7ff"][i % 4])
    c.rect(150, 300, 362, 430, "ff8fb1", r=60)            # body (party dress)
    c.circle(256, 230, 78, "f4c9a8")                      # face
    c.circle(256, 180, 82, "6b4226", sy=0.62)             # hair
    c.circle(186, 250, 28, "6b4226", sy=1.6)
    c.circle(326, 250, 28, "6b4226", sy=1.6)
    c.circle(256, 245, 66, "f4c9a8", sy=0.9)
    c.circle(230, 235, 8, "2b2b2b")
    c.circle(282, 235, 8, "2b2b2b")
    c.arc(256, 255, 30, 0.35, 2.8, 6, "c0392b")           # smile
    c.circle(212, 262, 10, "f3a6a6", alpha=0.6)
    c.circle(300, 262, 10, "f3a6a6", alpha=0.6)
    c.poly([(220, 140), (292, 140), (256, 40)], "7aa7ff")  # party hat
    for i in range(3):
        c.line(228 + i * 10, 125 - i * 30, 284 - i * 10, 125 - i * 30, 6, "ffd24a")
    c.circle(256, 38, 10, "ff6b8b")
    c.circle(360, 300, 26, "f4c9a8")                      # waving hand
    c.line(360, 330, 335, 380, 26, "ff8fb1")
    c.rect(350, 340, 440, 420, "8fd19e", r=8)            # the patient's small picture (in bed, smiling)
    c.circle(395, 372, 16, "e8c4a4")
    c.arc(395, 376, 8, 0.4, 2.7, 3, "8b3a2a")
    c.circle(256, 455, 16, "e74c3c")                      # end call button
    return c


def mixed() -> Canvas:
    c = Canvas("cfd6dd")
    c.rect(60, 40, 452, 280, "6f7f95", r=8)               # window: grey sky
    c.rect(60, 40, 452, 280, "4d5b70", alpha=0.4)
    for i in range(60):                                   # rain streaks
        x = (i * 71) % 380 + 70
        y = (i * 37) % 210 + 50
        c.line(x, y, x - 6, y + 22, 2, "c8d6e8", alpha=0.7)
    c.rect(250, 40, 262, 280, "e9edf0")                   # window frame
    c.rect(60, 154, 452, 164, "e9edf0")
    c.rect(52, 32, 460, 288, "e9edf0", alpha=0.0)
    c.rect(0, 380, 512, 512, "a7b0b8")                    # floor
    c.rect(130, 300, 382, 470, "a0764e", r=6)             # nightstand
    c.rect(150, 340, 362, 400, "8a6440", r=4)
    c.circle(256, 370, 7, "e0c08a")
    c.rect(120, 290, 392, 306, "b88a5c", r=4)
    c.poly([(176, 290), (336, 290), (330, 240), (182, 240)], "fdfaf2")  # the closed envelope
    c.line(182, 240, 256, 272, 3, "d7cfbe")
    c.line(330, 240, 256, 272, 3, "d7cfbe")
    c.rect(300, 246, 322, 262, "e2557a", r=2)             # a little heart stamp
    c.rect(330, 220, 350, 290, "e8eef2", r=4)             # a glass of water
    return c


def critical() -> Canvas:
    c = Canvas("e8e1d6")
    c.rect(0, 360, 512, 512, "b9ae9c")                    # floor
    for i, (x, col) in enumerate([(70, "ff8fb1"), (120, "7aa7ff"), (440, "ffd24a")]):  # balloons
        c.line(x, 140, x + 6, 300, 2, "8c8c8c")
        c.circle(x, 110, 34, col, sy=1.2)
    c.rect(60, 290, 300, 304, "8a6440", r=4)              # table
    c.rect(80, 304, 92, 430, "7a5638")
    c.rect(268, 304, 280, 430, "7a5638")
    cake(c, 180, 290, 140)
    c.rect(340, 210, 450, 226, "6d4c33", r=4)             # the empty chair
    c.rect(340, 110, 352, 330, "6d4c33", r=4)
    c.rect(438, 110, 450, 330, "6d4c33", r=4)
    c.rect(330, 320, 460, 338, "7a5638", r=4)
    c.rect(338, 338, 350, 440, "6d4c33")
    c.rect(440, 338, 452, 440, "6d4c33")
    c.rect(352, 150, 438, 166, "6d4c33", r=3)
    c.rect(360, 296, 430, 322, "f1e9dc", r=3)            # a party plate, untouched
    c.img = c.img * 0.85 + np.array([90, 90, 95], np.float32) * 0.15  # a little grey
    return c


if __name__ == "__main__":
    good().save(OUT / "Consequence_Good.png")
    mixed().save(OUT / "Consequence_Mixed.png")
    critical().save(OUT / "Consequence_Critical.png")
    print("3 images in", OUT)
