"""Read-only developer inventory of Unity text components, tables and recorded media."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "games" / "collapse"))
import collapse


def inspect(game):
    import UnityPy

    data = game / collapse.ASSET.parent
    scenes = sorted(p for p in data.iterdir() if re.fullmatch(r"level\d+", p.name))
    shared = sorted(data.glob("sharedassets*.assets"))
    archives = scenes + shared + [data / "resources.assets", data / "globalgamemanagers.assets"]
    counts = Counter()
    unique_mesh_texts = set()
    assets = Counter()
    for archive in archives:
        env = UnityPy.load(str(archive))
        for obj in env.objects:
            kind = obj.type.name
            if kind == "TextMesh":
                unique_mesh_texts.add(obj.read_typetree()["m_Text"])
            if kind == "TextAsset":
                assets[obj.peek_name()] += 1
            if kind in {"TextMesh", "TextAsset", "VideoClip", "Texture2D", "Sprite"}:
                counts[kind] += 1
    return {
        "archives": len(archives), "scenes": len(scenes), "components": dict(counts),
        "unique_mesh_initial_values": len(unique_mesh_texts),
        "text_assets_by_name": dict(sorted(assets.items())),
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-dir", type=Path, help="Steam game root")
    args = parser.parse_args()
    print(json.dumps(inspect(args.game_dir or collapse.locate()), ensure_ascii=False, indent=2))
