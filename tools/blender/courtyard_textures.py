"""Makes the courtyard's game-ready textures from raw generations (art/raw/courtyard -> art/textures).

    blender -b --factory-startup --python tools/blender/courtyard_textures.py

- courtyard-rock.png, courtyard-grass.png, courtyard-paving.png, courtyard-masonry.png: the tileable painted textures,
  resized to 1024 (they tile many times).
- city-ashlar.png, city-paving.png: the tidy city's clean white limestone and street paving, also at 1024.
- corner-wall.png, corner-paving.png: the forgotten corner's crumbling limestone (brick showing through) and its
  overgrown brick path, also at 1024. The courtyard's city is modelled; the painted city backdrop is retired
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

# the tidy city's own clean stone (the courtyard's weathered masonry stays on the forgotten corner)
for name in ("city-ashlar", "city-paving", "corner-wall", "corner-paving"):
    px, _ = load(name + ".png")
    px[..., 3] = 1
    save(f"{name}.png", px, 1024)
