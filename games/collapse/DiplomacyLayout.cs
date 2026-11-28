using System;
using UnityEngine;

internal static partial class SaveNameLayout
{
    // TextMesh uses its actual font's advances. Bounds and the sprite are in
    // the same world space, including all parent transforms and camera scales.
    static void FitDiplomacyBounds(TextMesh label, float width, float height)
    {
        if (!label || width <= 0f || height <= 0f || String.IsNullOrEmpty(label.text)) return;
        var renderer = label.GetComponent<Renderer>();
        if (!renderer) return;
        Vector3 size = renderer.bounds.size;
        float scale = 1f;
        if (size.x > width) scale = width / size.x;
        if (size.y > height) scale = Mathf.Min(scale, height / size.y);
        if (scale < 1f) label.characterSize *= scale;
    }

    // Prefer the existing word breaks supplied by the translation. Keep
    // numbers, initials, markup and already-wrapped labels intact.
    static string WrapDiplomacyButton(string value)
    {
        if (value.IndexOf('\n') >= 0 || value.IndexOf('<') >= 0) return value;
        int best = -1, distance = value.Length;
        for (int i = 1; i < value.Length - 1; ++i) {
            if (value[i] != ' ' || value[i - 1] < 0x2e80 || value[i + 1] < 0x2e80) continue;
            int d = Math.Abs(2 * i - value.Length);
            if (d < distance) { best = i; distance = d; }
        }
        return best > 0 && distance < value.Length / 2
            ? value.Substring(0, best) + "\n" + value.Substring(best + 1) : value;
    }

    public static void FitDiplomacyGrid(TextMesh[] labels, ref float[] original)
    {
        bool chinese = ChineseHud();
        if (labels == null || (!chinese && original == null)) return;
        if (original == null) {
            original = new float[labels.Length];
            for (int i = 0; i < labels.Length; ++i)
                if (labels[i]) original[i] = labels[i].characterSize;
        }
        for (int i = 0; i < labels.Length; ++i) {
            TextMesh label = labels[i];
            if (!label || original[i] <= 0f) continue;
            label.characterSize = original[i];
            if (!chinese || String.IsNullOrEmpty(label.text)) continue;
            Transform plate = label.transform.Find("Sprite");
            var sprite = plate ? plate.GetComponent<SpriteRenderer>() : null;
            if (!sprite) continue;
            Vector3 size = sprite.bounds.size;
            float width = size.x - 0.17f, height = size.y - 0.10f;
            if (width <= 0 || height <= 0) continue;
            var renderer = label.GetComponent<Renderer>();
            if (renderer && width < renderer.bounds.size.x * .72f)
                label.text = WrapDiplomacyButton(label.text);
            FitDiplomacyBounds(label, width, height);
        }
        if (!chinese) original = null;
    }

    public static bool FitDiplomacyPanel(CountryController panel, bool applied)
    {
        bool chinese = ChineseHud();
        if (!chinese && !applied) return false;
        for (int i = 0; i < panel.up_text.Length && i < 5; ++i) {
            TextMesh label = panel.up_text[i];
            if (!label) continue;
            Vector3 p = label.transform.localPosition;
            // The five equal fascia plates share their centre at the parent
            // origin; the world-relations thresholds use these same plates.
            float originalX = i == 0 ? -5.74f : i == 1 ? -2.85f : i == 2 ? .07f : i == 3 ? 2.92f : 5.85f;
            p.x = chinese ? (i - 2) * 2.89f : originalX;
            p.y = chinese ? 0f : -.11f;
            label.transform.localPosition = p;
            label.characterSize = chinese ? .112f : .2f;
            var box = label.GetComponent<BoxCollider2D>();
            if (box) {
                if (chinese) {
                    box.offset = Vector2.zero;
                    box.size = String.IsNullOrEmpty(label.text) ? Vector2.zero :
                        (Vector2)label.transform.InverseTransformVector(new Vector3(1.21f, .72f, 0f));
                } else {
                    Bounds bounds = label.GetComponent<Renderer>().bounds;
                    box.offset = label.transform.InverseTransformPoint(bounds.center);
                    box.size = label.transform.InverseTransformVector(bounds.size);
                }
            }
        }
        for (int i = 0; i < 3 && i < panel.factions_buttons.Length; ++i) {
            var button = panel.factions_buttons[i];
            if (!button || !button.text) continue;
            button.text.characterSize = .14f;
            if (chinese && button.spriteRenderer) {
                Vector3 b = button.spriteRenderer.bounds.size;
                FitDiplomacyBounds(button.text, b.x - .25f, b.y - .27f);
            }
        }
        for (int i = 0; i < panel.info_text.Length; ++i) {
            TextMesh label = panel.info_text[i];
            if (!label) continue;
            label.characterSize = i == 3 ? .12f : .07f;
            if (chinese) FitDiplomacyBounds(label, i == 3 ? 3.68f : i == 0 ? 1.99f : 2.16f, i == 3 ? .73f : .33f);
        }
        return chinese;
    }

    public static int DiplomacyTitleIndex(int native, int tab, int button)
    {
        // The original action handler has descriptions for these six buttons,
        // but its newer structured-tooltip path only indexes the first 16.
        if (native >= 0 || !ChineseHud() || (tab != 0 && tab != 1)) return native;
        if (button != 20 && button != 22 && button != 23) return native;
        return (tab == 0 ? 884 : 909) + button;
    }

    public static string DiplomacyParty(LanguageManager language, int party, bool colored)
    {
        return ChineseHud() && (party < 0 || party > 6) ? language.system_text[1652] : language.GetPartyName(party, colored);
    }

    public static string DiplomacyThresholdCountry(LanguageManager language, int index)
    {
        if (ChineseHud() && (index == 0 || index == 1)) return index == 0 ? "俄罗斯" : "美国";
        return language.GetCountryName(index);
    }

    public static string DiplomacyRelationLabel(string native)
    {
        return ChineseHud() ? native.TrimEnd('：') : native;
    }

    // The native tooltip builder appends an ASCII colon after labels that
    // may already end in the Chinese full-width colon. Limit this adjustment
    // to the final text emitted by the diplomacy controller.
    public static string DiplomacyPunctuation(string native)
    {
        if (!ChineseHud() || String.IsNullOrEmpty(native)) return native;
        return native.Replace("：:", "：").Replace("： :", "：").Replace("：：", "：");
    }

    public static string DiplomacyTitle(string native)
    {
        if (!ChineseHud() || String.IsNullOrEmpty(native)) return native;
        int line = native.IndexOf('\n');
        int red = native.IndexOf("<color=#8B0000>", StringComparison.OrdinalIgnoreCase);
        int end = line < 0 ? red : red < 0 ? line : Math.Min(line, red);
        return end >= 0 ? native.Substring(0, end).TrimEnd() : native;
    }
}
