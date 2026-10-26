"""Builds one Atrium asset: blender -b --factory-startup --python tools/blender/build.py -- <asset> [--no-previews]

Each asset is a module in tools/blender/assets/ with build(kit) -> (root_object, height_metres)."""
import importlib
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import atrium_kit as kit  # noqa: E402

args = kit.script_args()
if not args:
    raise SystemExit("usage: blender -b --factory-startup --python tools/blender/build.py -- <asset> [--no-previews]")
name = args[0]
kit.reset()
module = importlib.import_module("assets." + name.replace("-", "_"))
asset_root, height = module.build(kit)
kit.export(asset_root, name)
if "--no-previews" not in args:
    kit.previews(name, height)
