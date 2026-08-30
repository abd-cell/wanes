"""Render the Wanes brand mark into every platform icon slot.

The geometry mirrors `_MarkPainter` in lib/widgets/wanes_logo.dart, so the
launcher icon and the in-app logo are literally the same mark: a dark-ink route
curve running from a teal origin dot to an amber destination pin, on the brand
teal badge.

Run from app/wanes_app:  python tool/generate_icons.py
"""

import os
from PIL import Image, ImageDraw

# --- brand tokens (lib/core/theme.dart) -------------------------------------
TEAL = (0x0F, 0xAE, 0x9E, 255)  # WanesColors.route  — badge ground
INK = (0x04, 0x24, 0x1F, 255)  # WanesColors.inkDeep — route + origin dot
PING = (0xFF, 0x7A, 0x2F, 255)  # WanesColors.ping   — destination pin

# --- the 48-unit design grid, straight from _MarkPainter --------------------
GRID = 48.0
ROUTE = ((11.0, 37.0), (20.0, 20.0), (37.0, 13.0))  # M11 37 Q20 20 37 13
ROUTE_W = 4.5
ORIGIN, ORIGIN_R = (11.0, 37.0), 5.2
PIN, PIN_R, PIN_RING = (37.0, 13.0), 6.0, 3.0

# Ink extents of the mark within the grid: the origin disc sets the lower-left,
# the pin's outer ring the upper-right. Used to optically centre the mark.
_PIN_OUTER = PIN_R + PIN_RING / 2  # 7.5
BBOX = (
    ORIGIN[0] - ORIGIN_R,  # 5.8
    PIN[1] - _PIN_OUTER,  # 5.5
    PIN[0] + _PIN_OUTER,  # 44.5
    ORIGIN[1] + ORIGIN_R,  # 42.2
)


def _quad(p0, p1, p2, steps=96):
    """Flatten the quadratic bezier into a polyline."""
    pts = []
    for i in range(steps + 1):
        t = i / steps
        u = 1 - t
        pts.append(
            (
                u * u * p0[0] + 2 * u * t * p1[0] + t * t * p2[0],
                u * u * p0[1] + 2 * u * t * p1[1] + t * t * p2[1],
            )
        )
    return pts


def _disc(draw, cx, cy, r, fill):
    draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=fill)


def draw_mark(draw, px, py, scale):
    """Paint the mark; (px, py) is where grid (0,0) lands, `scale` px per unit."""

    def T(p):
        return (px + p[0] * scale, py + p[1] * scale)

    # route — stroked with round caps/joins, emulated by a thick polyline plus
    # a disc at every vertex (PIL has no round-join stroking of its own).
    pts = [T(p) for p in _quad(*ROUTE)]
    w = ROUTE_W * scale
    draw.line(pts, fill=INK, width=max(1, round(w)))
    for x, y in pts:
        _disc(draw, x, y, w / 2, INK)

    # origin dot
    _disc(draw, *T(ORIGIN), ORIGIN_R * scale, INK)

    # destination pin: amber core inside a dark ink ring
    cx, cy = T(PIN)
    _disc(draw, cx, cy, (PIN_R + PIN_RING / 2) * scale, INK)
    _disc(draw, cx, cy, (PIN_R - PIN_RING / 2) * scale, PING)


def render(size, *, mark=0.78, radius=None, ground=TEAL, alpha=True):
    """Render one icon.

    mark    fraction of the canvas the mark's ink extents should span
    radius  corner radius as a fraction of size; None = square (full bleed)
    ground  badge colour, or None for a transparent ground (adaptive foreground)
    alpha   False flattens to RGB — iOS app icons must not carry an alpha channel
    """
    ss = 4 if size * 4 <= 4096 else max(1, 4096 // size)
    S = size * ss
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if ground is not None:
        if radius is None:
            draw.rectangle((0, 0, S, S), fill=ground)
        else:
            draw.rounded_rectangle((0, 0, S - 1, S - 1), radius=radius * S, fill=ground)

    # Scale so the mark's ink extents span `mark` of the canvas, then centre
    # those extents (the raw grid is not symmetric about its middle).
    bw, bh = BBOX[2] - BBOX[0], BBOX[3] - BBOX[1]
    scale = mark * S / max(bw, bh)
    px = S / 2 - (BBOX[0] + bw / 2) * scale
    py = S / 2 - (BBOX[1] + bh / 2) * scale
    draw_mark(draw, px, py, scale)

    img = img.resize((size, size), Image.LANCZOS)
    if not alpha:
        flat = Image.new("RGB", (size, size), ground[:3] if ground else (255, 255, 255))
        flat.paste(img, mask=img.split()[3])
        img = flat
    return img


def write(path, img):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    print(f"  {path}  {img.size[0]}x{img.size[1]} {img.mode}")


def main():
    print("web")
    write("web/favicon.png", render(32, mark=0.84, radius=0.22))
    for s in (192, 512):
        write(f"web/icons/Icon-{s}.png", render(s, radius=0.22))
        # maskable: the launcher crops to a circle of 80% diameter, so the mark
        # is pulled in and the ground runs edge to edge.
        write(f"web/icons/Icon-maskable-{s}.png", render(s, mark=0.52))

    print("android — legacy launcher")
    for d, s in (("mdpi", 48), ("hdpi", 72), ("xhdpi", 96), ("xxhdpi", 144), ("xxxhdpi", 192)):
        write(f"android/app/src/main/res/mipmap-{d}/ic_launcher.png", render(s, radius=0.22))

    print("android — adaptive foreground (108dp grid, 72dp safe zone)")
    for d, s in (("mdpi", 108), ("hdpi", 162), ("xhdpi", 216), ("xxhdpi", 324), ("xxxhdpi", 432)):
        write(
            f"android/app/src/main/res/mipmap-{d}/ic_launcher_foreground.png",
            render(s, mark=0.48, ground=None),
        )

    print("ios — full bleed, no alpha")
    ios = "ios/Runner/Assets.xcassets/AppIcon.appiconset"
    for name, s in (
        ("20x20@1x", 20), ("20x20@2x", 40), ("20x20@3x", 60),
        ("29x29@1x", 29), ("29x29@2x", 58), ("29x29@3x", 87),
        ("40x40@1x", 40), ("40x40@2x", 80), ("40x40@3x", 120),
        ("60x60@2x", 120), ("60x60@3x", 180),
        ("76x76@1x", 76), ("76x76@2x", 152),
        ("83.5x83.5@2x", 167),
        ("1024x1024@1x", 1024),
    ):
        write(f"{ios}/Icon-App-{name}.png", render(s, mark=0.68, alpha=False))


if __name__ == "__main__":
    main()
