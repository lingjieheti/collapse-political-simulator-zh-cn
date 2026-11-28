using System;
using UnityEngine;

internal static partial class SaveNameLayout
{
    static DialogEventController dialogPanel;
    static Bounds dialogPaperBounds;

    public static int DistrictRestrictionRow(int governor, int region)
    {
        // The native party captions occupy seven consecutive rows. Other
        // governor values have a region-specific "we do not control it" caption.
        return governor >= 0 && governor < 7 ? 6705 + governor : 4846 + region;
    }

    public static bool CloseReadOnlyDialog(int[] answers)
    {
        if (answers == null) return false;
        // -1 hides the slot. Other negative values display a locked response,
        // so a notice is read-only only when every slot is actually absent.
        foreach (int row in answers) if (row != -1) return false;
        ResetPaperTip();
        UnityEngine.SceneManagement.SceneManager.LoadScene(5);
        return true;
    }

    static bool TryPaperBounds(out Bounds page)
    {
        if (NewspaperIsOpen()) {
            var background = newspaperPanel.GetComponent<SpriteRenderer>();
            if (background) { page = background.bounds; return true; }
        }
        page = dialogPaperBounds;
        return dialogPanel && dialogPanel.isActiveAndEnabled && page.size.x > 0f;
    }

    static void FitDialogControl(Transform plate, ref Vector4[] native, int index, bool chinese)
    {
        if (!plate || !plate.gameObject.activeInHierarchy) return;
        var child = plate.Find("Text");
        var label = child ? child.GetComponent<TextMesh>() : null;
        var background = plate.GetComponent<SpriteRenderer>();
        if (!background || !ResetStatisticsLabel(label, ref native, 9, index, chinese)) return;
        Bounds bounds = background.bounds;
        if (index < 3) dialogPaperBounds.Encapsulate(bounds);
        PlaceStatisticsLabel(label, bounds.center.x, bounds.center.y, bounds.size.x * .85f, bounds.size.y * .73f);
    }

    public static void FitDialogPage(DialogEventController panel, int page, ref Vector4[] native)
    {
        bool chinese = ChineseHud();
        dialogPanel = chinese ? panel : null;
        if (!chinese && native == null) return;
        ResetPaperTip();
        var stage = panel.first_stage.activeInHierarchy ? panel.first_stage : panel.second_stage;
        var background = stage.GetComponent<SpriteRenderer>();
        if (!background) return;
        dialogPaperBounds = background.bounds;
        FitDialogControl(panel.first_stage.transform.Find("Button"), ref native, 0, chinese);
        FitDialogControl(panel.second_stage.transform.Find("Button"), ref native, 1, chinese);
        FitDialogControl(panel.nazadbutton.transform, ref native, 2, chinese);
        for (int i = 0; i < panel.buttons.Length; i++) {
            FitDialogControl(panel.buttons[i].transform.Find("Left"), ref native, 3 + i, chinese);
            var label = panel.buttons_meshes[i];
            if (!label || !label.gameObject.activeInHierarchy) continue;
            // The native scene pages the active buttons, but the disabled text
            // colliders keep sending 0..5 on every page.
            var target = label.GetComponent<DialogEventButtonTemp>();
            if (target) target.num = page * panel.buttons.Length + i;
        }
    }

    public static bool ExplainDialogChoice(int num, int stage, int selected, int[] answers, string[] notes, bool fromMod)
    {
        if (!ChineseHud() || stage != 1) return false;
        string hint;
        if (num == -1 && selected < 0) {
            // The native ending transition takes priority over choosing an answer.
            if (GlobalScript.inst.gameState.somethings[286] == 10) return false;
            hint = "请先选择一个可用回应，再继续。";
        } else if (answers != null && num >= 0 && num < answers.Length && answers[num] < -1) {
            hint = "当前无法选择此项。";
            var entries = fromMod ? LanguageManager.inst.mod_text : LanguageManager.inst.event_text;
            int row = -answers[num];
            string caption = row > 0 && row < entries.Length ? entries[row] : null;
            string detail = notes != null && num < notes.Length ? notes[num] : null;
            if (!String.IsNullOrWhiteSpace(caption)) hint += "\n" + caption.Replace("{0}", " ");
            if (!String.IsNullOrWhiteSpace(detail) && detail != caption) hint += "\n" + detail.Replace("{0}", " ");
        } else {
            return false;
        }
        ShowNewspaperTip(PopUpWindowManager.inst, hint);
        return true;
    }

    public static void ClickLockedDialogLabel(DialogEventButtonTemp label)
    {
        if (label.enabled && ChineseHud()) label.controller.ButtonEnter(label.num);
    }
}
