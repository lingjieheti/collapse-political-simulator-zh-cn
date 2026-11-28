using UnityEngine;

internal static partial class SaveNameLayout
{
    static EventController newspaperPanel;
    static PopUpWindowManager scaledPaperTip;
    static Vector3 paperTipScale;

    public static bool NewspaperIsOpen()
    {
        return newspaperPanel && newspaperPanel.gameObject.activeInHierarchy;
    }

    static void RestorePaperTipScale()
    {
        if (scaledPaperTip) scaledPaperTip.transform.localScale = paperTipScale;
        scaledPaperTip = null;
    }

    public static void ResetPaperTip()
    {
        RestorePaperTipScale();
        if (PopUpWindowManager.inst) PopUpWindowManager.inst.Hide();
    }

    public static void OpenNewspaperPage(EventController panel)
    {
        newspaperPanel = panel;
        ResetPaperTip();
    }

    public static void CloseNewspaper(EventController panel)
    {
        if (newspaperPanel != panel) return;
        newspaperPanel = null;
        ResetPaperTip();
    }

    public static void LimitNewspaperLineWidth(PopUpWindowManager tip)
    {
        if (!NewspaperIsOpen()) return;
        var paper = newspaperPanel.GetComponent<SpriteRenderer>();
        var camera = Camera.main;
        if (!paper || !camera || !camera.orthographic) return;
        float halfWidth = camera.orthographicSize * camera.aspect;
        float center = camera.transform.position.x;
        float side = Mathf.Max(paper.bounds.min.x - (center - halfWidth), center + halfWidth - paper.bounds.max.x);
        // The stock drawing method uses a 20% panel minimum and adds 0.6 units
        // around the longest measured line. Set its width before it wraps.
        float room = (side - .045f * camera.orthographicSize) / Mathf.Abs(tip.transform.lossyScale.x);
        float line = Mathf.Min(room / 1.2f, room - .6f);
        if (line > 1.5f) tip.default_x_size = Mathf.Min(tip.default_x_size, line);
    }

    public static void ShowNewspaperTip(PopUpWindowManager tip, string text)
    {
        RestorePaperTipScale();
        // The builder points this call at the game's original drawing method.
        tip.Show(text);
        if (!NewspaperIsOpen() || !tip.is_visible) return;
        Camera camera = Camera.main;
        SpriteRenderer paper = newspaperPanel.GetComponent<SpriteRenderer>();
        SpriteRenderer background = tip.GetComponent<SpriteRenderer>();
        if (!camera || !camera.orthographic || !paper || !background) return;

        Bounds page = paper.bounds;
        Bounds box = background.bounds;
        box.Encapsulate(tip.textMesh.GetComponent<Renderer>().bounds);
        if (box.size.x <= 0f || box.size.y <= 0f) return;
        float halfH = camera.orthographicSize;
        float halfW = halfH * camera.aspect;
        Vector3 center = camera.transform.position;
        float gap = halfH * .022f;
        float xMin = center.x - halfW + gap, xMax = center.x + halfW - gap;
        float yMin = center.y - halfH + gap, yMax = center.y + halfH - gap;
        // Use an entire unused side of the newspaper. For a narrow window the
        // top or bottom band may retain more of the original type size.
        Rect[] spaces = {
            Rect.MinMaxRect(Mathf.Max(page.max.x + gap, xMin), yMin, xMax, yMax),
            Rect.MinMaxRect(xMin, yMin, Mathf.Min(page.min.x - gap, xMax), yMax),
            Rect.MinMaxRect(xMin, Mathf.Max(page.max.y + gap, yMin), xMax, yMax),
            Rect.MinMaxRect(xMin, yMin, xMax, Mathf.Min(page.min.y - gap, yMax))
        };
        int best = -1;
        float factor = 0f;
        for (int i = 0; i < spaces.Length; ++i) {
            float fit = Mathf.Min(1f, Mathf.Min(spaces[i].width / box.size.x, spaces[i].height / box.size.y));
            if (fit > factor) { best = i; factor = fit; }
        }
        if (best < 0) return;
        if (factor < 1f) {
            scaledPaperTip = tip;
            paperTipScale = tip.transform.localScale;
            tip.transform.localScale = new Vector3(paperTipScale.x * factor, paperTipScale.y * factor, paperTipScale.z);
            box = background.bounds;
            box.Encapsulate(tip.textMesh.GetComponent<Renderer>().bounds);
        }
        Rect area = spaces[best];
        float x = Mathf.Clamp(box.min.x, area.xMin, area.xMax - box.size.x);
        float y = Mathf.Clamp(box.min.y, area.yMin, area.yMax - box.size.y);
        if (best == 0) x = area.xMin;
        if (best == 1) x = area.xMax - box.size.x;
        if (best == 2) y = area.yMin;
        if (best == 3) y = area.yMax - box.size.y;
        tip.transform.position += new Vector3(x - box.min.x, y - box.min.y, 0f);
    }
}
