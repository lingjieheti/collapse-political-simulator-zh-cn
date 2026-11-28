using UnityEngine;

internal static partial class SaveNameLayout
{
    // Remember each native label's local coordinates and character size. Changing
    // the language restores both without moving the owner sprite or its collider.
    static bool ResetStatisticsLabel(TextMesh label, ref Vector4[] native, int count, int index, bool chinese)
    {
        if (!label || (!chinese && native == null)) return false;
        if (native == null) native = new Vector4[count];
        Vector4 old = native[index];
        if (old.w == 0f) {
            if (!chinese) return false;
            Vector3 pos = label.transform.localPosition;
            native[index] = new Vector4(pos.x, pos.y, pos.z, label.characterSize);
        } else {
            label.transform.localPosition = new Vector3(old.x, old.y, old.z);
            label.characterSize = old.w;
        }
        return chinese;
    }

    static void PlaceStatisticsLabel(TextMesh label, float x, float y, float width, float height)
    {
        FitDiplomacyBounds(label, width, height);
        var center = label.GetComponent<Renderer>().bounds.center;
        label.transform.position += new Vector3(x - center.x, y - center.y, 0f);
    }

    public static float ModifierCaptionSize(float nativeSize)
    {
        // These fifteen narrow rows share the largest size that fits the native
        // width calculation. Reserve vertical space for full-height CJK glyphs.
        return ChineseVoting() ? nativeSize * .74f : nativeSize;
    }

    public static void StatisticsHeadings(StatisticControlller panel, ref Vector4[] native)
    {
        bool chinese = ChineseVoting();
        if (!chinese && native == null) return;
        var tabs = panel.statistic_buttons;
        if (tabs == null || tabs.Length != 5) return;
        for (int i = 0; i < tabs.Length; i++) {
            var tab = tabs[i];
            if (!tab) continue;
            var title = tab.Find("Text");
            var handle = tab.Find("Button");
            if (!title || !handle) continue;
            var label = title.GetComponent<TextMesh>();
            var plate = tab.GetComponent<SpriteRenderer>();
            var knob = handle.GetComponent<SpriteRenderer>();
            if (!plate || !knob || !ResetStatisticsLabel(label, ref native, 5, i, chinese)) continue;
            Bounds card = plate.bounds;
            float top = card.max.y;
            float bottom = knob.bounds.max.y;
            if (top <= bottom) continue;
            PlaceStatisticsLabel(label, card.center.x, bottom + (top - bottom) * .41f,
                card.size.x * .92f, (top - bottom) * .78f);
        }
        if (!chinese) native = null;
    }

    public static void ReformToggle(EconomicReformController panel, ref Vector4[] native)
    {
        var label = panel.collapseLabel;
        bool chinese = ChineseVoting();
        if (!ResetStatisticsLabel(label, ref native, 1, 0, chinese)) {
            if (!chinese) native = null;
            return;
        }
        var plate = panel.collapseToggle ? panel.collapseToggle.GetComponent<SpriteRenderer>() : null;
        if (!plate) return;
        var bounds = plate.bounds;
        PlaceStatisticsLabel(label, bounds.center.x, bounds.center.y, bounds.size.x * .60f, bounds.size.y * .65f);
    }

    public static void ReformColumns(EconomicReformController panel, ref Vector4[] native)
    {
        var headers = panel.columnHeaders;
        bool chinese = ChineseVoting();
        if (headers == null || headers.Length != 5 || (!chinese && native == null)) return;
        var label = headers[4];
        if (!ResetStatisticsLabel(label, ref native, 1, 0, chinese)) {
            if (!chinese) native = null;
            return;
        }
        var plate = label.transform.parent.GetComponent<SpriteRenderer>();
        if (!plate || !headers[0]) return;
        // Only the fifth cell overflows. Keep the native baseline shared by all five titles.
        var bounds = plate.bounds;
        float y = headers[0].GetComponent<Renderer>().bounds.center.y;
        PlaceStatisticsLabel(label, bounds.min.x + bounds.size.x * .915f, y,
            bounds.size.x * .145f, bounds.size.y * .58f);
    }

    public static void ReformSummary(EconomicReformController panel, ref Vector4[] native, ref Color nativeColor)
    {
        var label = panel.statusText;
        bool chinese = ChineseVoting();
        if (!label || (!chinese && native == null)) return;
        if (native == null) nativeColor = label.color;
        if (!ResetStatisticsLabel(label, ref native, 1, 0, chinese)) {
            label.color = nativeColor;
            native = null;
            return;
        }
        var plate = label.transform.parent.GetComponent<SpriteRenderer>();
        if (!plate) return;
        // The sixth region button is at bottom left. All selectable source,
        // target, percentage and cancel buttons end above the bottom strip.
        var bounds = plate.bounds;
        PlaceStatisticsLabel(label, bounds.min.x + bounds.size.x * .665f, bounds.min.y + bounds.size.y * .073f,
            bounds.size.x * .59f, bounds.size.y * .065f);
        label.color = new Color(.98f, .95f, .90f, nativeColor.a);
    }
}
