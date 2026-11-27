using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

// Display-only hooks for the game's save and backstory menu.
internal static class SaveNameLayout
{
    public static void SetRow(TextMesh label, string full, TextMesh date)
    {
        label.richText = false;
        label.text = full ?? "";
        if (String.IsNullOrEmpty(full) || date == null) return;
        Renderer renderer = label.GetComponent<Renderer>();
        if (renderer == null) return;
        float limit = date.transform.position.x - 0.12f;
        if (renderer.bounds.max.x <= limit) return;

        // Keep both the recognizable name and a slot/date suffix. Index text
        // elements so an imported name cannot be split inside a surrogate pair.
        int[] starts = StringInfo.ParseCombiningCharacters(full);
        int left = 0, right = starts.Length - 1;
        string best = "…";
        while (left <= right)
        {
            int kept = left + (right - left) / 2;
            int prefix = (kept + 1) / 2, suffix = kept / 2;
            string shown = full.Substring(0, starts[prefix]) + "…";
            if (suffix > 0) shown += full.Substring(starts[starts.Length - suffix]);
            label.text = shown;
            if (renderer.bounds.max.x <= limit)
            {
                best = shown;
                left = kept + 1;
            }
            else right = kept - 1;
        }
        label.text = best;
    }

    public static void SetSelection(SaveController screen, int selected)
    {
        TextMesh detail = screen.text;
        detail.richText = false;
        detail.text = "";
        SaveLocalController[] rows = screen.saveLocalControllers;
        if (rows == null || selected < 0 || selected >= rows.Length) return;
        SaveLocalController row = rows[selected];
        if (row == null || row.save == null || String.IsNullOrEmpty(row.save.name)) return;
        if (row.text.text != row.save.name) detail.text = "完整名称：" + row.save.name;
    }

    public static void ScrollNameEntry(GameObject[] letters)
    {
        // RepaintTextInSaveWindow restores the original position and visibility
        // of each glyph on every keystroke. Keep newly typed characters in view.
        Renderer last = null;
        for (int i = letters.Length - 1; i >= 0; i--) {
            if (letters[i].activeSelf) { last = letters[i].GetComponent<Renderer>(); break; }
        }
        if (last == null) return;
        const float left = -2.20f, right = 2.08f;
        float shift = right - last.bounds.max.x;
        if (shift >= 0) return;
        for (int i = 0; i < letters.Length && letters[i].activeSelf; i++) {
            GameObject letter = letters[i];
            Renderer glyph = letter.GetComponent<Renderer>();
            if (glyph.bounds.min.x + shift < left)
                letter.SetActive(false);
            else
                letter.transform.position += new Vector3(shift, 0, 0);
        }
    }

    public static void SetSearch(TextMesh label, string display, string query)
    {
        label.richText = false;
        label.text = display;
        if (String.IsNullOrEmpty(query)) return;
        Renderer text = label.GetComponent<Renderer>();
        SpriteRenderer plate = label.transform.parent.GetComponent<SpriteRenderer>();
        if (text == null || plate == null) return;
        float limit = plate.bounds.size.x - .12f;
        if (text.bounds.size.x <= limit) return;
        string prefix = display.Substring(0, display.Length - query.Length) + "…";
        int[] starts = StringInfo.ParseCombiningCharacters(query);
        for (int i = 1; i < starts.Length; i++) {
            label.text = prefix + query.Substring(starts[i]);
            if (text.bounds.size.x <= limit) return;
        }
        label.text = prefix;
    }

    // Position a new-game description between the two original mask planes.
    // The origin of this font lies below its tallest Chinese strokes.
    public static float NewGameScrollY(float fallbackBottom, float maskTop, float k,
                                       TextMesh text, Material mask)
    {
        float top = maskTop - .12f;
        if (k >= 1f) return top;
        Renderer renderer = text.GetComponent<Renderer>();
        float bottom = fallbackBottom;
        if (renderer != null && mask != null && !String.IsNullOrEmpty(text.text))
            bottom = Math.Max(top, text.transform.position.y - renderer.bounds.min.y +
                                  mask.GetFloat("min_y") + .08f);
        return bottom + (top - bottom) * Math.Max(0f, k);
    }

    // A translation or combined Workshop mod supplies its own naming conventions.
    public static bool OwnTranslation()
    {
        ModManager manager = ModManager.inst;
        if (!System.Object.ReferenceEquals(manager, null) && manager.loadedMod >= 0)
        {
            System.Collections.IList mods = manager.mods;
            if (mods == null || manager.loadedMod >= mods.Count) return false;
            Mod enabled = mods[manager.loadedMod] as Mod;
            if (enabled == null || (int)enabled.type == 0 || (int)enabled.type == 2) return false;
        }
        return true;
    }

    public static string SettingsValue(string native)
    {
        if (!OwnTranslation()) return native;
        if (native == "English") return "简体中文";
        if (native == "Russian") return "Русский";
        if (native == "New") return "新版名称";
        if (native == "Old") return "经典名称";
        return native;
    }

    public static bool ChineseHud()
    {
        LanguageManager language = LanguageManager.inst;
        return !System.Object.ReferenceEquals(language, null) &&
               language.language == SystemLanguage.English && OwnTranslation();
    }

    public static string HudChangeFormat(string native)
    {
        if (!ChineseHud()) return native;
        // The game finalizes these nine changes on the first day of the month.
        // Leave the signed value and its original positive/negative color intact.
        return native.Replace(" ", "　上月变化：").Replace("(", "").Replace(")", "");
    }

    public static string HudThousands(string native)
    {
        // Each compact counter is exactly 1/1000 of its unabridged display.
        return ChineseHud() ? "千" : native;
    }

    public static object HudDeltaValue(int raw, int indicator)
    {
        // Support is a whole percentage, and state budget is a whole amount.
        // Party cohesion/resources/budget and foreign relations use tenths.
        if ((indicator >= 3 && indicator <= 5 || indicator == 12) && ChineseHud())
            return raw;
        return raw / 10f;
    }

    public static void SetHudText(TextMesh label, string text, ref float originalSize)
    {
        // Some HUDs sit against the screen edge. Fit just the affected label
        // inside its own plate, keeping the position and the other HUDs intact.
        if (originalSize > 0f) label.characterSize = originalSize;
        label.text = text;
        if (String.IsNullOrEmpty(text) || text.IndexOf("上月变化：", StringComparison.Ordinal) < 0 ||
            !ChineseHud() || label.transform.parent == null) return;
        SpriteRenderer plate = label.transform.parent.GetComponent<SpriteRenderer>();
        Renderer glyphs = label.GetComponent<Renderer>();
        if (plate == null || glyphs == null) return;
        float width = glyphs.bounds.size.x;
        float available = plate.bounds.size.x - .22f;
        if (available <= 0f || width <= available) return;
        if (originalSize == 0f) originalSize = label.characterSize;
        label.characterSize = originalSize * available / width;
    }

    public static string FundingPreview(string native, GameState state, int cost)
    {
        if (!ChineseHud()) return native;
        int label = state.ministers[8].owner == (int)state.party ? 272 : 1139;
        string title = LanguageManager.inst.gui_text[label];
        return title + "预算：−" + cost.ToString(CultureInfo.InvariantCulture);
    }

    // TransformLine runs only in classic mode. Preserve native format tokens,
    // color tags, and the distinct ethnic name for the Salets.
    public static string ClassicChinese(string line)
    {
        if (String.IsNullOrEmpty(line) || (line.IndexOf('切') < 0 && line.IndexOf('萨') < 0) ||
            !OwnTranslation()) return line;
        if (line.IndexOf('切') >= 0)
            line = Regex.Replace(line, @"切 *尔 *诺 *里 *兹 *斯 *卡 *亚 *(?=(?:[东西南北部方人民右翼基辅自由票证青年工业苏维埃社会主义 ]{0,20}|\{[01]\} *)共 *和)", "");
        if (line.IndexOf('萨') >= 0)
        {
            line = Regex.Replace(line, @"萨 *莱 *蒂 *安 *P\. *D\.", "人民民主");
            line = Regex.Replace(line, @"萨 *莱 *蒂 *安 *", "");
            line = Regex.Replace(line, @"萨 *莱 *蒂(?! *人)", "小共和国");
        }
        return line;
    }
}
