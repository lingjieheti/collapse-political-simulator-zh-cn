using System;
using UnityEngine;

internal static partial class SaveNameLayout
{
    public static void RefreshEventChoices(EventController panel, int page)
    {
        bool chinese = ChineseHud();
        for (int i = 0; i < panel.buttons_meshes.Length; ++i) {
            var label = panel.buttons_meshes[i];
            if (!label || !label.gameObject.activeSelf) continue;
            // A locked option has a separate hoverable text object. Its page
            // index must advance just like the enabled button underneath it.
            var trigger = label.GetComponent<EventButton>();
            if (trigger) trigger.num = page * panel.buttons.Length + i;
            if (!chinese) continue;
            var plate = panel.ButtonScripts[i].spriteRenderer;
            if (!plate) continue;
            // A locked option's plate is inactive. Its local drawing bounds
            // still describe the same space as the other five options.
            var size = plate.transform.TransformVector(plate.localBounds.size);
            FitDiplomacyBounds(label, Mathf.Abs(size.x) * .94f, Mathf.Abs(size.y) * .85f);
        }
    }

    public static bool ExplainEventChoice(EventController panel, int num, int stage, int selected, int[] answers,
        bool fromMod, bool click)
    {
        if (stage != 1 || !ChineseHud()) return false;
        string hint;
        if (num == -1 && selected < 0) {
            hint = "请先选择一个可用选项，再确认。";
        } else if (num == -1 && !click && answers != null && selected >= 0 && selected < answers.Length) {
            var entries = fromMod ? LanguageManager.inst.mod_text : LanguageManager.inst.event_text;
            int row = answers[selected];
            if (row < 0 || row >= entries.Length || String.IsNullOrEmpty(entries[row])) return false;
            hint = "确认已选回应\n" + entries[row].Replace("{0}", " ");
        } else if (answers != null && num >= 0 && num < answers.Length && answers[num] < -1) {
            hint = "当前无法选择此项。";
            if (panel.opis != null && num < panel.opis.Length && !String.IsNullOrWhiteSpace(panel.opis[num])) {
                hint += "\n" + panel.opis[num];
            } else {
                // Many stock events put the unmet condition in the disabled caption.
                var entries = fromMod ? LanguageManager.inst.mod_text : LanguageManager.inst.event_text;
                int row = -answers[num];
                if (row > 0 && row < entries.Length && !String.IsNullOrWhiteSpace(entries[row]))
                    hint += "\n" + entries[row].Replace("{0}", " ");
            }
        } else {
            return false;
        }
        PopUpWindowManager.inst.Show(hint);
        return true;
    }

    public static void ClickEventChoiceLabel(EventButton button)
    {
        // Only locked options receive mouse events on their TextMesh.
        if (button.GetComponent<TextMesh>() && ChineseHud())
            button.controller.ButtonEnter(button.num);
    }
}
