"""Makes the courtyard's game-ready textures from raw generations (art/raw/courtyard -> art/textures).

    blender -b --factory-startup --python tools/blender/courtyard_textures.py

- courtyard-rock.png, courtyard-grass.png, courtyard-paving.png, courtyard-masonry.png: the tileable painted textures,
  resized to 1024 (they tile many times).
- city-ashlar.png, city-paving.png: the tidy city's clean white limestone and street paving, also at 1024.
- corner-wall.png, corner-paving.png: the forgotten corner's crumbling limestone (brick showing through) and its
  overgrown brick path, also at 1024, their stone taken to the city's limestone (homogenise) so city and corner are
  one stone, differing only in weathering. The courtyard's city is modelled; the painted city backdrop is retired
  (its raw art stays in art/raw/courtyard as provenance).
"""
import os

import bpy
import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "art", "raw", "courtyard")
OUT = os.path.join(ROOT, "art", "textures")


def load(name):
    img = bpy.data.images.load(os.path.join(RAW, name))
    w, h = img.size
    px = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(px)
    return px.reshape(h, w, 4), img


def save(name, arr, width):
    """Saves an (h, w, 4) array (rows bottom-up, as Blender stores them) as PNG, resized to `width`."""
    h, w = arr.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels.foreach_set(arr.astype(np.float32).ravel())
    if width and width != w:
        img.scale(width, max(1, round(h * width / w)))
    os.makedirs(OUT, exist_ok=True)
    img.filepath_raw = os.path.join(OUT, name)
    img.file_format = "PNG"
    img.save()
    print("TEXTURE", name, img.size[0], img.size[1])


for name in ("rock", "grass", "paving", "masonry"):
    px, _ = load(name + ".png")
    px[..., 3] = 1
    save(f"courtyard-{name}.png", px, 1024)

# the tidy city's own clean stone
for name in ("city-ashlar", "city-paving"):
    px, _ = load(name + ".png")
    px[..., 3] = 1
    save(f"{name}.png", px, 1024)


def homogenise(px, ref):
    """Takes the stone in a forgotten-corner texture to the city's limestone: the ref's colour and brightness, the
    texture's own light and shade. Brick, moss and grass (the corner's weathering) are left as they are."""
    rgb, refrgb = px[..., :3], ref[..., :3]
    lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
    hi, lo = rgb.max(-1), rgb.min(-1)
    sat = (hi - lo) / (hi + 1e-4)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    green = np.clip((g - np.maximum(r, b)) / 0.06, 0, 1)
    brick = np.clip((r - b - 0.14) / 0.08, 0, 1) * np.clip((r - g - 0.05) / 0.05, 0, 1)
    stone = np.clip(1 - (sat - 0.16) / 0.16, 0, 1) * (1 - green) * (1 - brick)
    rl = refrgb @ np.array([0.299, 0.587, 0.114], np.float32)
    ref_mean = refrgb[rl > np.percentile(rl, 30)].mean(0)
    ref_lum = float(ref_mean @ np.array([0.299, 0.587, 0.114], np.float32))
    stone_lum = float((lum * stone).sum() / max(stone.sum(), 1))
    target = ref_mean[None, None, :] * (lum / stone_lum)[..., None] * (ref_lum / max(ref_lum, 1e-4))
    k = (stone * 0.8)[..., None]
    out = px.copy()
    out[..., :3] = np.clip(rgb * (1 - k) + target * k, 0, 1)
    return out


# the forgotten corner's stone: the city's own limestone, weathered (brick showing, moss, damp)
for name, ref in (("corner-wall", "city-ashlar"), ("corner-paving", "city-paving")):
    px, _ = load(name + ".png")
    rf, _ = load(ref + ".png")
    px[..., 3] = 1
    save(f"{name}.png", homogenise(px, rf), 1024)
