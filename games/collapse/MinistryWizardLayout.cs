using UnityEngine;

internal static partial class SaveNameLayout
{
    public static void WizardCollapseLabel(TextMesh label, ref Vector4[] native)
    {
        bool chinese = ChineseVoting();
        if (!ResetStatisticsLabel(label, ref native, 1, 0, chinese)) {
            if (!chinese) native = null;
            return;
        }
        var plate = label.transform.parent.GetComponent<SpriteRenderer>();
        if (!plate) return;
        Bounds bounds = plate.bounds;
        PlaceStatisticsLabel(label, bounds.center.x, bounds.center.y, bounds.size.x * .60f, bounds.size.y * .68f);
    }

    public static void WizardSecondHeader(TextMesh label, ref Vector4[] native)
    {
        bool chinese = ChineseVoting();
        if (!ResetStatisticsLabel(label, ref native, 1, 0, chinese)) {
            if (!chinese) native = null;
            return;
        }
        var atlas = label.transform.parent.GetComponent<SpriteRenderer>();
        if (!atlas) return;
        Bounds bounds = atlas.bounds;
        // The second plate contains the recipient/source or the full target-share title.
        string compact = label.text.Replace(" ", "");
        int units = compact.IndexOf('（');
        if (units > 0) label.text = compact.Substring(0, units) + "\n" + compact.Substring(units);
        PlaceStatisticsLabel(label, bounds.min.x + bounds.size.x * .294f, bounds.center.y - bounds.size.y * .035f,
            bounds.size.x * .15f, bounds.size.y * .77f);
    }

    public static void EnsureWizardBudgetSummary(TextMesh[] headers, ref TextMesh status, ref bool created)
    {
        bool chinese = ChineseVoting();
        if (status) {
            if (created && status.gameObject.activeSelf != chinese) status.gameObject.SetActive(chinese);
            return;
        }
        // These two stock overlays have status formatters but their scene bindings are empty.
        if (!chinese || headers == null || headers.Length == 0 || !headers[0]) return;
        var source = headers[0];
        var fascia = source.transform.parent;
        var sheet = fascia ? fascia.parent : null;
        if (!sheet || !sheet.GetComponent<SpriteRenderer>()) return;
        var holder = new GameObject("BudgetSummary");
        holder.transform.SetParent(fascia, false);
        holder.transform.localScale = source.transform.localScale;
        holder.transform.localRotation = source.transform.localRotation;
        holder.transform.position = source.transform.position;
        holder.transform.SetParent(sheet, true);
        status = holder.AddComponent<TextMesh>();
        status.font = source.font;
        status.fontSize = source.fontSize;
        status.fontStyle = source.fontStyle;
        status.characterSize = source.characterSize * .80f;
        status.anchor = TextAnchor.MiddleCenter;
        status.alignment = TextAlignment.Center;
        status.richText = true;
        var r = status.GetComponent<Renderer>();
        var template = source.GetComponent<Renderer>();
        r.sharedMaterial = template.sharedMaterial;
        r.sortingLayerID = template.sortingLayerID;
        r.sortingOrder = template.sortingOrder;
        created = true;
    }

    public static void WizardStatus(TextMesh label, ref Vector4[] native, ref Color nativeColor)
    {
        bool chinese = ChineseVoting();
        if (!label || (!chinese && native == null)) return;
        if (native == null) nativeColor = label.color;
        if (!ResetStatisticsLabel(label, ref native, 1, 0, chinese)) {
            label.color = nativeColor;
            native = null;
            return;
        }
        var sheet = label.transform.parent.GetComponent<SpriteRenderer>();
        if (!sheet || string.IsNullOrEmpty(label.text)) return;
        Bounds bounds = sheet.bounds;
        PlaceStatisticsLabel(label, bounds.center.x, bounds.min.y + bounds.size.y * .088f,
            bounds.size.x * .89f, bounds.size.y * .11f);
        label.color = new Color(.98f, .95f, .90f, nativeColor.a);
    }

    public static void WizardSectorValue(TextMesh label, ref Vector4[] native, ref Color nativeColor)
    {
        bool chinese = ChineseVoting();
        if (!label || (!chinese && native == null)) return;
        if (native == null) nativeColor = label.color;
        if (!ResetStatisticsLabel(label, ref native, 1, 0, chinese)) {
            label.color = nativeColor;
            native = null;
            return;
        }
        if (string.IsNullOrEmpty(label.text)) return;
        var column = label.transform.parent;
        var upper = column ? column.Find("Button (0)/Sprite") : null;
        var lower = column ? column.Find("Button (2)/Sprite") : null;
        var minus = upper ? upper.GetComponent<SpriteRenderer>() : null;
        var plus = lower ? lower.GetComponent<SpriteRenderer>() : null;
        if (!minus || !plus) return;
        Bounds a = minus.bounds, b = plus.bounds;
        Vector3 center = (a.center + b.center) * .5f;
        label.text = label.text.Replace(" → ", "\n→ ");
        label.characterSize *= 1.2f;
        PlaceStatisticsLabel(label, center.x, center.y, a.size.x * .98f, a.size.y * .93f);
        label.color = new Color(.98f, .95f, .90f, nativeColor.a);
    }
}
