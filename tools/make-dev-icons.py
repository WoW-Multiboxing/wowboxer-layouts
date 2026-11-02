"""Makes the teal "WoWBoxer Dev" icons from the normal gold ones (run after the app icon changes).

    python tools/make-dev-icons.py

Writes src/WoWBoxer.Layouts.App/Assets/dev/app.ico and app.png: the same picture with every gold pixel turned
teal (hue +130 degrees), so the dev build is never mistaken for the normal app in the taskbar, the tray,
the Start menu or on the desktop. Needs Pillow.
"""
import colorsys
from pathlib import Path

from PIL import Image

ASSETS = Path(__file__).resolve().parent.parent / "src" / "WoWBoxer.Layouts.App" / "Assets"
SHIFT = 130 / 360


def tint(im: Image.Image) -> Image.Image:
    im = im.convert("RGBA")
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if s < 0.2 or not 0.03 <= h <= 0.2:
                continue  # only the gold turns teal; greys and the navy shield stay as they are
            r2, g2, b2 = colorsys.hsv_to_rgb((h + SHIFT) % 1.0, s, v)
            px[x, y] = (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)
    return im


def main() -> None:
    out = ASSETS / "dev"
    out.mkdir(exist_ok=True)
    tint(Image.open(ASSETS / "app.png")).save(out / "app.png")
    ico = Image.open(ASSETS / "app.ico")
    sizes = sorted(ico.info["sizes"])
    frames = []
    for size in sizes:
        ico.size = size
        frames.append(tint(ico.copy()))
    big = frames[-1]
    big.save(out / "app.ico", format="ICO", sizes=sizes, append_images=frames[:-1])
    print("wrote", out / "app.png", out / "app.ico", sizes)


if __name__ == "__main__":
    main()
