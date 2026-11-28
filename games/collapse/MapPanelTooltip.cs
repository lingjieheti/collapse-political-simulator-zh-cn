using UnityEngine;

internal static partial class SaveNameLayout
{
    static Collider2D panelTipSource;
    static int panelTipScene;
    static int panelTipFrame;
    static float panelLineWidth;
    static PopUpWindowManager scaledPanelTip;
    static Vector3 originalPanelScale;
    static Collider2D[] panelTipControls;
    static int panelTipCount;

    public static void AnchorPanelTip(Component button)
    {
        int scene = button.gameObject.scene.buildIndex;
        if (scene == 5 || (scene >= 8 && scene <= 13))
            panelTipSource = button.GetComponent<Collider2D>();
    }

    public static void LeavePanelTip(Component button)
    {
        if (panelTipSource && panelTipSource.gameObject == button.gameObject)
            panelTipSource = null;
    }

    static Rect PanelTipRect(Bounds bounds)
    {
        return Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
    }

    static float PanelTipOverlap(Rect a, Rect b)
    {
        float width = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float height = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return Mathf.Max(0f, width) * Mathf.Max(0f, height);
    }

    public static void RestorePanelTipScale()
    {
        if (scaledPanelTip) scaledPanelTip.transform.localScale = originalPanelScale;
        scaledPanelTip = null;
    }

    static void CachePanelButtons(Component source)
    {
        var scene = source.gameObject.scene;
        if (panelTipScene == scene.handle && panelTipFrame == Time.frameCount && panelTipControls != null) return;
        panelTipScene = scene.handle;
        panelTipFrame = Time.frameCount;
        // Some panels create or rebind colliders after selecting a tab or a
        // district. Only reuse a list within the same rendered frame.
        panelTipControls = UnityEngine.Object.FindObjectsOfType<Collider2D>();
        panelTipCount = 0;
        for (int i = 0; i < panelTipControls.Length; ++i) {
            var c = panelTipControls[i];
            if (c.gameObject.scene == scene && (c.GetComponent<SimpleButtonScript>() || c.GetComponent<GenericButtonPressScript>() ||
                c.GetComponent<TimeButton>() || c.GetComponent<InfoScript>() || c.GetComponent<FlagDisplayHover>() || c.GetComponent<MembershipBadgeHover>()))
                panelTipControls[panelTipCount++] = c;
        }
    }

    static bool PanelTipSlot(Rect view, Vector2 size, Vector2 preferred, Rect[] controls, int count, out Vector2 position)
    {
        position = preferred;
        if (size.x > view.width || size.y > view.height) return false;
        var xs = new float[3 + 2*count];
        xs[0] = preferred.x; xs[1] = view.xMin; xs[2] = view.xMax - size.x;
        for (int i = 0; i < count; ++i) { xs[3+2*i] = controls[i].xMax; xs[4+2*i] = controls[i].xMin - size.x; }
        int nx = DistinctPanelCoordinates(xs, view.xMin, view.xMax - size.x);
        float best = float.PositiveInfinity;
        for (int k = 0; k < nx; ++k) {
            float x = xs[k], bottom = view.yMin;
            for (int i = 0; i <= count; ++i) {
                float top = view.yMax;
                if (i < count) {
                    var other = controls[i];
                    if (x >= other.xMax - .0001f || x + size.x <= other.xMin + .0001f) continue;
                    top = Mathf.Min(view.yMax, other.yMin);
                }
                if (top - bottom >= size.y) {
                    var p = new Vector2(x, Mathf.Clamp(preferred.y, bottom, top - size.y));
                    float distance = (p - preferred).sqrMagnitude;
                    if (distance < best) { best = distance; position = p; }
                }
                if (i < count) bottom = Mathf.Max(bottom, controls[i].yMax);
                if (bottom + size.y > view.yMax) break;
            }
        }
        return !float.IsPositiveInfinity(best);
    }

    static float PanelTipCost(Rect box, Rect owner, Rect[] buttons, int count, Vector2 preferred)
    {
        float ownArea = Mathf.Max(.0001f, owner.width * owner.height);
        float ownCover = PanelTipOverlap(box, owner);
        // Preserve the hovered control first, then nearby controls. Distance
        // breaks ties to keep the explanation close to the control it describes.
        float result = ownCover > .00001f ? 100000f + 10000f * ownCover / ownArea : 0f;
        for (int i = 0; i < count; ++i) {
            float area = PanelTipOverlap(box, buttons[i]);
            if (area > 0f) result += 100f * area / (buttons[i].width * buttons[i].height);
        }
        return result + .002f * (box.position - preferred).sqrMagnitude;
    }

    public static void WrapPanelTip(PopUpWindowManager tip)
    {
        if (panelLineWidth > 0f) tip.default_x_size = panelLineWidth;
    }

    static void RenderPanelTip(PopUpWindowManager tip, string text, float width)
    {
        panelLineWidth = width;
        // The builder routes this call straight to the native wrapping/drawing
        // method, avoiding the ordinary Show wrapper during a second layout.
        tip.Show(text);
        panelLineWidth = 0f;
    }

    static Rect MeasuredPanelTip(PopUpWindowManager tip)
    {
        var bounds = tip.GetComponent<SpriteRenderer>().bounds;
        bounds.Encapsulate(tip.textMesh.GetComponent<Renderer>().bounds);
        return PanelTipRect(bounds);
    }

    static float LargestPanelTipScale(Rect view, Rect box, Rect[] controls, int count, out Vector2 position)
    {
        if (PanelTipSlot(view, box.size, box.position, controls, count, out position)) return 1f;
        float low = .48f, high = 1f;
        if (!PanelTipSlot(view, box.size*low, box.position, controls, count, out position)) return 0f;
        for (int i = 0; i < 7; ++i) {
            float mid = (low+high)*.5f;
            Vector2 next;
            if (PanelTipSlot(view, box.size*mid, box.position, controls, count, out next)) { position = next; low = mid; }
            else high = mid;
        }
        return low;
    }

    public static void PositionPanelTip(PopUpWindowManager tip, string text)
    {
        var source = panelTipSource;
        var camera = Camera.main;
        if (!source || !source.isActiveAndEnabled || !tip.is_visible || !camera || !camera.orthographic) return;
        var background = tip.GetComponent<SpriteRenderer>();
        if (!background) return;
        var bounds = background.bounds;
        bounds.Encapsulate(tip.textMesh.GetComponent<Renderer>().bounds);
        Rect box = PanelTipRect(bounds), owner = PanelTipRect(source.bounds);
        float halfH = camera.orthographicSize, halfW = halfH * camera.aspect, gap = halfH * .018f;
        var center = camera.transform.position;
        Rect view = Rect.MinMaxRect(center.x - halfW + gap, center.y - halfH + gap, center.x + halfW - gap, center.y + halfH - gap);
        if (box.width <= 0f || box.height <= 0f ||
            owner.width <= 0f || owner.height <= 0f || !view.Overlaps(owner)) return;
        CachePanelButtons(source);
        var controls = new Rect[panelTipCount + 1];
        var region = source.GetComponentInParent<RegionWindowController>();
        int count = 0;
        for (int i = 0; i < panelTipCount; ++i) {
            var c = panelTipControls[i];
            if (!c || !c.isActiveAndEnabled || c == source || (region && !c.transform.IsChildOf(region.transform))) continue;
            var r = PanelTipRect(c.bounds);
            if (r.width > 0f && r.height > 0f && (r.width < view.width || r.height < view.height) && view.Overlaps(r)) controls[count++] = r;
        }
        controls[count++] = owner;
        // Enumerate empty vertical strips in y order. First keep the native
        // size; on a crowded panel take the largest scale that keeps every
        // button clear, with a lower limit for readable Chinese on small screens.
        for (int i = 0; i < count; ++i) {
            var r = controls[i];
            r = Rect.MinMaxRect(r.xMin-gap, r.yMin-gap, r.xMax+gap, r.yMax+gap);
            int j = i;
            while (j > 0 && controls[j-1].yMin > r.yMin) { controls[j] = controls[j-1]; --j; }
            controls[j] = r;
        }
        Vector2 pos;
        float bestScale = LargestPanelTipScale(view, box, controls, count, out pos);
        if (bestScale < .91f) {
            float originalWidth = tip.default_x_size, bestWidth = originalWidth, current = originalWidth;
            // A free margin is often tall and narrow. Retain native glyph size
            // whenever rewrapping into that margin offers a more readable result.
            for (int attempt = 0; attempt < 4; ++attempt) {
                float factor = attempt == 0 ? .67f : attempt == 1 ? .46f : attempt == 2 ? .31f : 1.42f;
                current = Mathf.Max(1.45f, originalWidth*factor);
                RenderPanelTip(tip, text, current);
                Vector2 candidate;
                float scale = LargestPanelTipScale(view, MeasuredPanelTip(tip), controls, count, out candidate);
                if (scale > bestScale + .025f) { bestScale = scale; bestWidth = current; pos = candidate; }
                if (bestScale >= 1f) break;
            }
            if (current != bestWidth) RenderPanelTip(tip, text, bestWidth);
            box = MeasuredPanelTip(tip);
        }
        if (bestScale >= .55f) {
            if (bestScale < 1f) {
                scaledPanelTip = tip;
                originalPanelScale = tip.transform.localScale;
                tip.transform.localScale = new Vector3(originalPanelScale.x*bestScale, originalPanelScale.y*bestScale, originalPanelScale.z);
                box = MeasuredPanelTip(tip);
            }
            tip.transform.position += new Vector3(pos.x-box.x, pos.y-box.y, 0f);
            return;
        }
        // Contacts with a control edge, the viewport edges, and the original
        // cursor position are the points at which rectangle overlap changes.
        var xs = new float[6 + 2*count];
        var ys = new float[xs.Length];
        xs[0] = view.xMin; xs[1] = view.xMax - box.width;
        ys[0] = view.yMin; ys[1] = view.yMax - box.height;
        xs[2] = box.x; ys[2] = box.y;
        xs[3] = owner.xMax + gap; xs[4] = owner.xMin - box.width - gap; xs[5] = owner.center.x - box.width*.5f;
        ys[3] = owner.yMax + gap; ys[4] = owner.yMin - box.height - gap; ys[5] = owner.center.y - box.height*.5f;
        for (int i = 0; i < count; ++i) {
            xs[6+2*i] = controls[i].xMax + gap; xs[7+2*i] = controls[i].xMin - box.width - gap;
            ys[6+2*i] = controls[i].yMax + gap; ys[7+2*i] = controls[i].yMin - box.height - gap;
        }
        int nx = DistinctPanelCoordinates(xs, view.xMin, view.xMax - box.width);
        int ny = DistinctPanelCoordinates(ys, view.yMin, view.yMax - box.height);
        var best = box.position;
        var preferred = box.position;
        float bestCost = float.PositiveInfinity;
        for (int ix = 0; ix < nx; ++ix) {
            for (int iy = 0; iy < ny; ++iy) {
                var option = new Rect(xs[ix], ys[iy], box.width, box.height);
                float cost = PanelTipCost(option, owner, controls, count, preferred);
                if (cost < bestCost) { best = option.position; bestCost = cost; }
            }
        }
        tip.transform.position += new Vector3(best.x - box.x, best.y - box.y, 0f);
    }

    static int DistinctPanelCoordinates(float[] values, float low, float high)
    {
        int count = 0;
        for (int i = 0; i < values.Length; ++i) {
            float v = Mathf.Clamp(values[i], low, high);
            int j = 0;
            while (j < count && Mathf.Abs(values[j] - v) > .001f) ++j;
            if (j == count) values[count++] = v;
        }
        return count;
    }
}
