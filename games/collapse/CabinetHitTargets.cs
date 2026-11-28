using UnityEngine;

internal static partial class SaveNameLayout
{
    // + and - are separate TextMesh objects with BoxCollider2D on each object.
    // Keep their native hit areas, extending each only inside its own half-card.
    public static void CabinetBudgetHitTargets(MinistrySelectController panel, int stage, ref Vector4[] native)
    {
        bool adjust = stage == 1 && ChineseVoting();
        if (adjust == (native != null)) return;
        if (panel.money_buttons == null || panel.money_buttons.Length != 14) return;
        if (adjust) native = new Vector4[28];
        for (int i = 0; i < panel.money_buttons.Length; i++) {
            var controls = panel.money_buttons[i];
            if (!controls || !controls.transform.parent) continue;
            var sprite = controls.transform.parent.GetComponent<SpriteRenderer>();
            if (!sprite || !sprite.sprite) continue;
            Bounds card = sprite.bounds;
            for (int side = 0; side < 2; side++) {
                var label = controls.transform.Find(side == 0 ? "Left" : "Right");
                var box = label ? label.GetComponent<BoxCollider2D>() : null;
                if (!box) continue;
                int index = i * 2 + side;
                if (!adjust) {
                    Vector4 saved = native[index];
                    if (saved.z > 0 && saved.w > 0) {
                        box.offset = new Vector2(saved.x, saved.y);
                        box.size = new Vector2(saved.z, saved.w);
                    }
                    continue;
                }
                native[index] = new Vector4(box.offset.x, box.offset.y, box.size.x, box.size.y);
                // The signs occupy the band below the two budget lines. Leave
                // a central dead strip and preserve all native owners and IDs.
                Vector3 low = card.min + new Vector3(card.size.x * (side == 0 ? .15f : .535f), card.size.y * .095f, 0);
                Vector3 high = card.min + new Vector3(card.size.x * (side == 0 ? .465f : .85f), card.size.y * .408f, 0);
                low.z = high.z = label.position.z;
                Vector2 a = label.InverseTransformPoint(low);
                Vector2 b = label.InverseTransformPoint(high);
                Vector2 nativeHalf = box.size * .5f;
                Vector2 min = Vector2.Min(Vector2.Min(a, b), box.offset - nativeHalf);
                Vector2 max = Vector2.Max(Vector2.Max(a, b), box.offset + nativeHalf);
                box.offset = (min + max) * .5f;
                box.size = max - min;
            }
        }
        if (!adjust) native = null;
    }
}
