#!/usr/bin/env python3
"""Generate MonoGame SpriteFont XNB content (atlas texture + font descriptor)
using Pillow. No mgcb / no internet needed.

Usage:
  python3 make_font.py <font.ttf> <size_px> <out_dir> [asset_name] ["extra chars"]

Writes <Asset>.xnb and <Asset>_0.xnb into out_dir, ready for a MonoGame
DesktopGL project's Content folder (Content.Load<SpriteFont>("<Asset>")).
"""
import struct
import sys
import os

from PIL import Image, ImageDraw, ImageFont


def enc7(n: int) -> bytes:
    """XNB 7-bit encoded unsigned int (continuation on high bit)."""
    out = bytearray()
    while True:
        b = n & 0x7F
        n >>= 7
        if n:
            out.append(b | 0x80)
        else:
            out.append(b)
            return bytes(out)


def xnb(reader_type: str, body: bytes) -> bytes:
    o = bytearray(b"XNB\x05\x00\x00\x00\x00")
    rt = reader_type.encode("utf-8")
    o += enc7(len(rt)) + rt   # reader type name string
    o += enc7(0)              # LzxKey: empty string => uncompressed
    o += body
    return bytes(o)


def main():
    ttf, size, outdir = sys.argv[1], int(sys.argv[2]), sys.argv[3]
    asset = sys.argv[4] if len(sys.argv) > 4 else "AngryFont"
    extras = sys.argv[5] if len(sys.argv) > 5 else ""

    glyphs = [chr(c) for c in range(32, 127)]
    for ch in extras:
        if ch not in glyphs:
            glyphs.append(ch)

    font = ImageFont.truetype(ttf, size)
    pad = 2

    metrics = []
    row_x, row_y, row_h = 1, 1, 0
    max_w = 2048
    for ch in glyphs:
        l, t, r, b = font.getbbox(ch)
        w, h = max(r - l, 1), max(b - t, 1)
        advance = font.getlength(ch)
        if row_x + w + pad * 2 + 1 > max_w:
            row_x = 1
            row_y += row_h + 1
            row_h = 0
        gx, gy = row_x + pad, row_y + pad
        metrics.append(dict(c=ch, x=gx, y=gy, w=w, h=h,
                            xa=float(advance), xo=float(l), yo=float(t)))
        row_x += w + pad * 2 + 1
        row_h = max(row_h, h + pad)

    tex_w = 1
    while tex_w < row_x:
        tex_w <<= 1
    tex_h = 1
    while tex_h < row_y + row_h:
        tex_h <<= 1

    atlas = Image.new("RGBA", (tex_w, tex_h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(atlas)
    for m in metrics:
        draw.text((m["x"] - m["xo"], m["y"] - m["yo"]), m["c"],
                  font=font, fill=(255, 255, 255, 255))

    os.makedirs(outdir, exist_ok=True)
    atlas.save(os.path.join(outdir, f"{asset}_0.png"))

    line_spacing = size * 1.25

    # ---- sprite font xnb body ----------------------------------------------
    body = bytearray(struct.pack("<i", len(metrics)))
    for m in metrics:
        body += struct.pack("<iiii", m["x"], m["y"], m["w"], m["h"])  # Bounds
        body += struct.pack("<fff", m["xa"], m["xo"], m["yo"])        # Advance/Offsets
        body += struct.pack("<iiii", 0, 0, 0, 0)                      # Keringing (zeros)
    body += struct.pack("<f", line_spacing)
    body += struct.pack("<ii", tex_w, tex_h)
    body += enc7((7 << 24) | 0)   # ExternalReference<Texture2D>, index 0

    with open(os.path.join(outdir, f"{asset}.xnb"), "wb") as f:
        f.write(xnb("Microsoft.Xna.Framework.Content.SpriteFontReader", bytes(body)))

    # ---- texture xnb ---------------------------------------------------------
    tbody = struct.pack("<i", 4)              # SurfaceFormat.Color
    tbody += struct.pack("<ii", tex_w, tex_h)
    tbody += struct.pack("<i", 0)             # mip level count
    tbody += atlas.tobytes()                  # RGBA
    with open(os.path.join(outdir, f"{asset}_0.xnb"), "wb") as f:
        f.write(xnb("Microsoft.Xna.Framework.Content.Texture2DReader", tbody))

    print(f"OK: {len(metrics)} glyphs, atlas {tex_w}x{tex_h}, wrote {asset}.xnb + {asset}_0.xnb to {outdir}")


if __name__ == "__main__":
    main()
