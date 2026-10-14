"""
Draws the toolbar icons.

Kept as a script, and the PNGs beside it regenerated from it, so the icons are something that
can be changed rather than three binaries nobody can edit. There is no image library on the
machines this is built on, so the PNG is written out by hand — which is less work than it
sounds: a PNG is a handful of length-prefixed chunks around a zlib stream.

The mark is the SimbaFlow one — a globe, as a circle with a meridian and the equator — in white
on the brand green. Drawn at four times the size and averaged down, which is where the smooth
edges come from; at 16 pixels a hard-edged circle looks like a cog.
"""

import math
import struct
import zlib
from pathlib import Path

GREEN = (0x00, 0x66, 0x33)
WHITE = (0xFF, 0xFF, 0xFF)
SS = 4  # supersampling factor

# All in fractions of the icon's width, so one set of numbers serves every size.
CORNER = 0.22
RADIUS = 0.30
STROKE = 0.085
MERIDIAN = 0.45  # the meridian's width as a fraction of the globe's radius


def inside_rounded_square(x: float, y: float) -> bool:
    r = CORNER
    cx = min(max(x, r), 1 - r)
    cy = min(max(y, r), 1 - r)
    # Inside the cross of the rounded square, or within the radius of the nearest corner.
    return (x - cx) ** 2 + (y - cy) ** 2 <= r * r or (r <= x <= 1 - r) or (r <= y <= 1 - r)


def on_mark(x: float, y: float) -> bool:
    dx, dy = x - 0.5, y - 0.5
    half = STROKE / 2
    dist = math.hypot(dx, dy)

    if abs(dist - RADIUS) <= half:          # the globe itself
        return True
    if dist <= RADIUS and abs(dy) <= half:  # the equator
        return True

    a = RADIUS * MERIDIAN
    if a > 0:                               # the meridian, as an ellipse
        k = math.hypot(dx / a, dy / RADIUS)
        if k > 0 and abs((k - 1.0) * a) <= half and dist <= RADIUS + half:
            return True
    return False


def render(size: int) -> bytes:
    big = size * SS
    rows = []
    for py in range(size):
        row = bytearray([0])  # PNG filter byte: none
        for px in range(size):
            r = g = b = a = 0.0
            for sy in range(SS):
                for sx in range(SS):
                    x = (px * SS + sx + 0.5) / big
                    y = (py * SS + sy + 0.5) / big
                    if not inside_rounded_square(x, y):
                        continue
                    colour = WHITE if on_mark(x, y) else GREEN
                    r += colour[0]
                    g += colour[1]
                    b += colour[2]
                    a += 255
            n = SS * SS
            # Premultiplied while averaging, so the edge fades to the colour under it rather
            # than to black.
            alpha = a / n
            if alpha > 0:
                row += bytes((round(r / a * 255), round(g / a * 255), round(b / a * 255)))
            else:
                row += bytes((0, 0, 0))
            row.append(round(alpha))
        rows.append(bytes(row))
    return b"".join(rows)


def png(size: int) -> bytes:
    def chunk(kind: bytes, payload: bytes) -> bytes:
        return (
            struct.pack(">I", len(payload))
            + kind
            + payload
            + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)
        )

    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)  # 8-bit RGBA
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", header)
        + chunk(b"IDAT", zlib.compress(render(size), 9))
        + chunk(b"IEND", b"")
    )


if __name__ == "__main__":
    here = Path(__file__).parent
    for size in (16, 32, 48, 128):
        path = here / f"icon{size}.png"
        path.write_bytes(png(size))
        print(f"{path.name}  {path.stat().st_size} bytes")
