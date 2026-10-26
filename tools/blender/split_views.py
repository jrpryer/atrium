"""Splits a turnaround sheet into one image per view, all at the same scale:
    blender -b --factory-startup --python tools/blender/split_views.py -- old-stoic-sheet.png

Reads the view boxes from sheets.json and writes art/reference/views/<sheet>-<view>.png. Everything outside a
view's own box is filled with the sheet's background grey, so neighbouring views never bleed in."""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import atrium_kit as kit  # noqa: E402

name = kit.script_args()[0]
spec = json.load(open(os.path.join(os.path.dirname(__file__), "sheets.json"), encoding="utf-8"))[name]
sheet = kit.load_sheet(name)
grey = sheet[20:60, 20:60, :3].reshape(-1, 3).mean(axis=0)
cw, ch = spec["canvas"]
bx, by = spec["base"]
out_dir = os.path.join(kit.ART, "reference", "views")
os.makedirs(out_dir, exist_ok=True)
stem = os.path.splitext(name)[0].replace("-sheet", "")
for view, b in spec["views"].items():
    src = sheet.copy()
    for cx0, cy0, cx1, cy1 in b.get("clear", []):
        src[cy0:cy1, cx0:cx1, :3] = grey
    crop = src[b["y0"]:b["y1"], b["x0"]:b["x1"], :3]
    canvas = np.empty((ch, cw, 4), dtype=np.float32)
    canvas[..., :3] = grey
    canvas[..., 3] = 1
    left = bx - (b["base_x"] - b["x0"])
    top = by - (b["y1"] - b["y0"])
    # clip to the canvas
    sx0, sy0 = max(0, -left), max(0, -top)
    dx0, dy0 = max(0, left), max(0, top)
    w = min(crop.shape[1] - sx0, cw - dx0)
    h = min(crop.shape[0] - sy0, ch - dy0)
    canvas[dy0:dy0 + h, dx0:dx0 + w, :3] = crop[sy0:sy0 + h, sx0:sx0 + w]
    img = kit.bpy.data.images.new(f"{stem}-{view}", width=cw, height=ch, alpha=False)
    img.pixels.foreach_set(canvas[::-1].ravel())
    img.filepath_raw = os.path.join(out_dir, f"{stem}-{view}.png")
    img.file_format = "PNG"
    img.save()
    print("VIEW", img.filepath_raw)
