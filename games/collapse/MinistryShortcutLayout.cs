using UnityEngine;

internal static partial class SaveNameLayout
{
    public static void MinistryShortcutLayout(MinistryController panel, ref Vector4[] native)
    {
        bool chinese = ChineseVoting();
        if (chinese == (native != null)) return;
        for (int i = 0; i < 2; i++) {
            var button = panel.transform.Find(i == 0 ? "HelpButton (2)" : "HelpButton (3)");
            if (!button) continue;
            var plate = button.GetComponent<SpriteRenderer>();
            var label = button.GetComponentInChildren<TextMesh>();
            if (!plate || !ResetStatisticsLabel(label, ref native, 2, i, chinese)) continue;
            var b = plate.bounds;
            // The native font reserves much more space below '+' than below '-'.
            float centerY = b.center.y - b.size.y * (i == 0 ? .13f : .035f);
            PlaceStatisticsLabel(label, b.center.x, centerY, b.size.x * .72f, b.size.y * 1.10f);
        }
        if (!chinese) native = null;
    }

    public static void PartyThresholdLayout(PartyController panel, ref Vector4[] native)
    {
        bool chinese = ChineseVoting();
        if (!chinese && native == null) return;
        var labels = panel.up_text;
        if (labels == null || labels.Length != 5) return;
        for (int i = 0; i < labels.Length; i++) {
            var label = labels[i];
            if (!label || !label.transform.parent) continue;
            var plate = label.transform.parent.GetComponent<SpriteRenderer>();
            if (!plate) continue;
            bool adjust = ResetStatisticsLabel(label, ref native, 10, i, chinese);
            var box = label.GetComponent<BoxCollider2D>();
            if (!adjust) {
                if (box && native != null && native[i+5].z > 0f) {
                    var old = native[i+5];
                    box.enabled = true;
                    box.offset = new Vector2(old.x,old.y);
                    box.size = new Vector2(old.z,old.w);
                }
                continue;
            }
            if (box && native[i+5].z == 0f)
                native[i+5] = new Vector4(box.offset.x,box.offset.y,box.size.x,box.size.y);
            bool visible = !string.IsNullOrEmpty(label.text);
            if (box) box.enabled = visible;
            if (!visible) continue;
            var b = plate.bounds;
            float x = b.center.x + b.size.x * .204f * (i-2);
            PlaceStatisticsLabel(label, x, b.center.y - b.size.y * .044f, b.size.x * .143f, b.size.y * .54f);
            if (box) {
                // Each white plate owns its own hover region, including the whitespace around its number.
                Vector3 center = new Vector3(x,b.center.y,label.transform.position.z);
                box.offset = label.transform.InverseTransformPoint(center);
                box.size = label.transform.InverseTransformVector(new Vector3(b.size.x * .164f,b.size.y * .85f,0f));
            }
        }
        if (!chinese) native = null;
    }

    static string MinistryBudgetStatus(GameState state, int ministry, int button)
    {
        var port = state.ministers[ministry];
        if (button == 120)
            return "用党费自动补足部门预算\n当前" + (port.data[0] > 0 ? "已启用" : "已关闭");
        return "本部门占比：" + port.pecent + "%\n本党各部门合计：" + state.somethings[443] +
            "%\n待分配：" + state.somethings[444] + "%";
    }

    public static bool MinistryBudgetShortcutHelp(int ministry, int button, bool click)
    {
        if (button < 120 || button > 122 || !ChineseVoting()) return false;
        var state = GlobalScript.inst.gameState;
        var port = state.ministers[ministry];
        if (port.owner != (int)state.party) {
            PopUpWindowManager.inst.Show("本党未掌管该部门。\n无法调整预算。");
            return true;
        }
        if (click) return false;
        string action = button == 120 ? "" : button == 121 ? "增加国家预算分配比例\n" : "减少国家预算分配比例\n";
        PopUpWindowManager.inst.Show(action + MinistryBudgetStatus(state,ministry,button));
        return true;
    }

    public static int MinistryShareBefore(GameState state, int ministry, int button)
    {
        return (button == 121 || button == 122) && ChineseVoting() ? state.ministers[ministry].pecent : 0;
    }

    public static void MinistryBudgetResult(PopUpWindowManager popup, GameState state, int ministry, int button, int previous)
    {
        if (!ChineseVoting()) { popup.Hide(); return; }
        string result = "";
        if (button != 120) {
            bool changed = state.ministers[ministry].pecent != previous;
            if (button == 121) result = changed ? "已增加 1 个百分点" : "没有可分配的预算比例";
            else result = changed ? "已减少 1 个百分点" : "本部门占比已为零";
            result += "\n";
        }
        popup.Show(result + MinistryBudgetStatus(state,ministry,button));
    }
}
