using System;
using UnityEngine;

internal static partial class SaveNameLayout
{
    // The ministry and party screens share the same three-cell strip. The two
    // banks of diagonal strokes are part of its sprite and overlap wide labels.
    public static void FitAdjacentHeader(TextMesh[] labels, ref float[] nativeSizes)
    {
        bool chinese = ChineseVoting();
        if (labels == null || labels.Length < 3 || (!chinese && nativeSizes == null)) return;
        if (nativeSizes == null) nativeSizes = new float[3];
        for (int i = 0; i < 3; i++) {
            var label = labels[i];
            if (!label) continue;
            if (nativeSizes[i] == 0f) nativeSizes[i] = label.characterSize;
            label.characterSize = nativeSizes[i];
            if (!chinese || String.IsNullOrEmpty(label.text)) continue;
            var group = label.transform.parent;
            var strip = group && group.parent ? group.parent.GetComponent<SpriteRenderer>() : null;
            if (!strip || !strip.sprite || strip.sprite.name != "ActionWindowsAtlas_1") continue;
            // Avoid the full sweep of the diagonals at either end of each cell.
            var size = strip.bounds.size;
            float width = size.x * .184f;
            if (label.text.IndexOf('\n') < 0 && label.GetComponent<Renderer>().bounds.size.x > width) {
                int delta = label.text.IndexOf(" (", StringComparison.Ordinal);
                if (delta > 0) label.text = label.text.Substring(0, delta) + "\n" + label.text.Substring(delta + 1);
            }
            FitDiplomacyBounds(label, width, size.y * .74f);
        }
        if (!chinese) nativeSizes = null;
    }
}
