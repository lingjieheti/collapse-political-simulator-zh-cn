using UnityEngine;

internal static partial class SaveNameLayout
{
    public static void FitFactionListRow(FactionController panel, GameObject row, ref bool seen)
    {
        if (!row || !panel || !panel.advancedScrollController) return;
        bool chinese = ChineseVoting();
        if (!chinese && !seen) return;
        var label = row.GetComponent<TextMesh>();
        var ink = row.GetComponent<Renderer>();
        var hit = row.GetComponent<Collider2D>();
        var button = row.GetComponent<SimpleButtonScript>();
        if (!label || !ink || !hit || !button || !button.spriteRenderer) return;
        // These are pooled rows. Always restore visibility before measuring the
        // next name. The generic scroll surface keeps receiving scroll input.
        ink.enabled = true;
        hit.enabled = true;
        button.spriteRenderer.enabled = true;
        if (!chinese) {
            label.characterSize = .093f;
            return;
        }
        seen = true;
        // English and Russian split the same slot over two lines. Chinese names
        // fit on one line and keep their party colour after selection or scrolling.
        label.text = label.text.Replace("\r", "").Replace("\n", " ").Replace("  ", " ")
            .Replace("#B95A16", "#783B0B").Replace("#0027FF", "#183980")
            .Replace("#575000", "#4B440B").Replace("#036700", "#174A29")
            .Replace("#E500A6", "#752755").Replace("#780200", "#681F20")
            .Replace("#CA0000", "#85282E");
        label.characterSize = .092f;
        FitDiplomacyBounds(label, button.spriteRenderer.bounds.size.x - .25f, .43f);
        var scroller = panel.advancedScrollController;
        var b = ink.bounds;
        float center = scroller.transform.position.y;
        // The scroll mask fades the outer band of the viewport. A partly exposed
        // row has no readable label or click target until it scrolls into view.
        bool visible = b.max.y <= center + scroller.half_size - .25f &&
                       b.min.y >= center - scroller.half_size + .25f;
        ink.enabled = visible;
        hit.enabled = visible;
        button.spriteRenderer.enabled = visible;
    }

    public static void FitFactionHeader(FactionController panel, ref bool seen)
    {
        if (!panel) return;
        bool chinese = ChineseVoting();
        if (!chinese && !seen) return;
        if (chinese) seen = true;
        var info = panel.info_text;
        if (info == null || info.Length < 4 || panel.border_indication == null) return;
        for (int i = 0; i < 4; i++) {
            var text = info[i];
            if (!text) continue;
            text.characterSize = i == 3 ? .12f : .07f;
            if (!chinese) continue;
            // The statistics background is a single textured sprite. Its three
            // clear areas exclude the slanted separators and the adjacent ? tile.
            if (i == 3) {
                text.characterSize = .096f;
                FitDiplomacyBounds(text, 3.65f, .69f);
            } else {
                // These two translation-table labels are also used in condition
                // comparisons. Add the divider only to the actual stat value.
                if (i == 0 || i == 2) {
                    var table = LanguageManager.inst.gui_text;
                    string key = table[i == 0 ? 254 : 256];
                    if (!string.IsNullOrEmpty(key) && !key.EndsWith(":") && !key.EndsWith("：") &&
                        text.text.StartsWith(key + " ", System.StringComparison.Ordinal))
                        text.text = key + "：" + text.text.Substring(key.Length).TrimStart();
                }
                FitDiplomacyBounds(text, i == 0 ? 2.10f : (i == 1 ? 2.05f : 1.98f), .59f);
            }
        }
        foreach (var number in panel.border_indication) {
            if (!number) continue;
            number.characterSize = chinese ? .146f : .2f;
            if (chinese) FitDiplomacyBounds(number, 1.04f, .62f);
        }
        // The game's existing grid fitter restores native sizing on a language
        // change. Preserve the native green status marker on both button surfaces.
        SimpleButtonScript[][] cols = { panel.actions_buttons_column_0, panel.actions_buttons_column_1,
            panel.actions_buttons_column_2, panel.actions_buttons_column_3, panel.actions_buttons_column_4 };
        if (panel.actions_buttons_text == null) return;
        for (int i = 0; i < panel.actions_buttons_text.Length && i < 25; i++) {
            var text = panel.actions_buttons_text[i];
            if (!text) continue;
            // Recycled hidden or empty slots may still carry a prior dark-face
            // foreground. Restore it even before an actual label is assigned.
            Color32 color = text.color;
            if (color.r == 245 && color.g == 241 && color.b == 234) text.color = new Color32(61,28,33,255);
            if (color.r == 211 && color.g == 248 && color.b == 189) text.color = new Color32(0,106,39,255);
            if (!chinese || !text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
            if (cols[i / 5] == null || i % 5 >= cols[i / 5].Length) continue;
            var button = cols[i / 5][i % 5];
            if (!button || !button.spriteRenderer) continue;
            color = text.color;
            bool green = color.g > color.r + 12 && color.g > color.b + 12;
            text.characterSize = .077f;
            var size = button.spriteRenderer.bounds.size;
            FitDiplomacyBounds(text, size.x - .16f, size.y - .16f);
            if (!button.active) text.color = green ? new Color32(211,248,189,255) : new Color32(245,241,234,255);
        }
    }

    public static void SetFactionActionHover(FactionController panel, int raw, bool enter)
    {
        if (!panel || !ChineseVoting() || raw < 10 || raw > 54 || raw % 10 >= 5) return;
        int column = raw / 10 - 1, row = raw % 10;
        SimpleButtonScript[][] columns = {panel.actions_buttons_column_0, panel.actions_buttons_column_1,
            panel.actions_buttons_column_2, panel.actions_buttons_column_3, panel.actions_buttons_column_4};
        if (columns[column] == null || row >= columns[column].Length || panel.actions_buttons_text == null ||
            column * 5 + row >= panel.actions_buttons_text.Length) return;
        var button = columns[column][row];
        var text = panel.actions_buttons_text[column * 5 + row];
        if (!button || !button.active || !text || string.IsNullOrEmpty(text.text)) return;
        Color32 color = text.color;
        bool green = color.g > color.r + 12 && color.g > color.b + 12;
        if (enter) text.color = green ? new Color32(211,248,189,255) : new Color32(245,241,234,255);
        else text.color = green ? new Color32(0,106,39,255) : new Color32(61,28,33,255);
    }
}
