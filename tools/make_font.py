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
    """Wrap an asset body in a valid XNB v5 container.

    Byte-for-byte layout expected by MonoGame's ContentManager
    (see ContentManager.GetContentReaderFromXnb / ReadAsset<T>):

      offset 0..2 : magic 'X','N','B'
      offset 3    : format version byte (5)
      offset 4    : flags (0 = uncompressed; non-zero means LZX/Xbox packing,
                    which is what triggers "Did you process your content for
                    Windows?" on DesktopGL)
      offset 5..8 : total file size including this header, uint32 LE
      offset 9..  : the serialized object stream, whose *first* element is
                    always the main reader-type-name string (7-bit length
                    prefix), followed by the LzxKey string ("", i.e. 0x00).

    Historical bugs in this function that produced the exact runtime error
    "Asset does not appear to be a valid XNB file. Did you process your
    content for Windows?":
      1. b"XNB\\x05\\x00..." wrote version 0 into the version slot.
      2. Writing the size field as four zero bytes (or otherwise mismatching
         it against the real file length) makes the loader reject the file.
      3. Emitting spurious extra count bytes between the LzxKey and the
         object body desynchronizes every subsequent read.
    """
    rt = reader_type.encode("utf-8")
    payload = bytearray()
    payload += enc7(len(rt)) + rt   # main reader type name string
    payload += enc7(0)              # LzxKey: empty string => uncompressed
    payload += body
    total = 9 + len(payload)        # 'XNB'+ver(4) + flags(1) + size(4)
    o = bytearray(b"XNB")
    o.append(5)                     # format version
    o.append(0)                     # no compression flags
    o += struct.pack("<I", total)   # full file size
    o += payload
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
        body += struct.pack("<ffff", 0.0, 0.0, 0.0, 0.0)              # Keringing: 4 * float32 zeros
    body += struct.pack("<f", line_spacing)
    body += struct.pack("<ii", tex_w, tex_h)
    body += enc7(0)               # ExternalReference<Texture2D>, shared index 0

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

# NOTE: The .xnb files written by this script are the actual runtime assets.
# If you ever see "Asset does not appear to be a valid XNB file" or an
# IndexOutOfRangeException when loading AngryFont, regenerate them with:
#
#   python3 tools/make_font.py Content/AngryFont.ttf 48 Content AngryFont
#
# (requires: pip install pillow). DontPressSpace.csproj copies Content/**/*.xnb
# to the output directory automatically on build, so `dotnet run` will find
# Content\AngryFont.xnb next to the executable.
