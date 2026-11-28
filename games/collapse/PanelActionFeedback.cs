using UnityEngine;

internal static partial class SaveNameLayout
{
    static bool HasPresidentialParty(GameState game)
    {
        return game.prez_num >= 0 && game.prez_num < game.parties.Length
            && game.parties[game.prez_num] != null;
    }

    // In a caretaker government the presidency may have no party.
    public static int PresidentialMisconduct(GameState game)
    {
        return HasPresidentialParty(game) ? game.parties[game.prez_num].anti_actions : 0;
    }

    public static RequirementLine[] ExplainImpeachmentTerms(RequirementLine[] native, GameState game)
    {
        if (!ChineseVoting()) return native;
        var terms = new RequirementLine[native.Length + 1];
        System.Array.Copy(native, 0, terms, 1, native.Length);
        terms[0] = new RequirementLine { label = "本党担任总理", isMet = game.ministers[0].owner == (int)game.party };
        return terms;
    }

    public static bool ExplainImpeachment(int ministry, int raw, bool click)
    {
        if (ministry != 0 || raw != 52) return false;
        bool chinese = ChineseVoting();
        if (!click && !chinese) return false;
        var game = GlobalScript.inst.gameState;
        var office = game.ministers[0];
        string reason;
        if (!HasPresidentialParty(game))
            reason = "当前没有可供弹劾的总统。";
        else if (!click) return false;
        else if (office.owner != (int)game.party) reason = "本党未担任总理。";
        else if (office.value[22] != 0) reason = "弹劾已发起，请等待处理。";
        else if (GameUtils.IsHeadOfRepublic(game, (int)game.party)) reason = "本党正在领导共和国。";
        else if (PresidentialMisconduct(game) <= 0) reason = "总统所属政党没有违宪行动记录。";
        else if (office.have_money < 500 || office.rabota < 500) reason = "需要部门预算 500、效率 50.0。";
        else return false;
        if (chinese) PopUpWindowManager.inst.Show("无法发起弹劾\n" + reason);
        return true;
    }

    public static void ConfirmImpeachment()
    {
        if (ChineseVoting())
            PopUpWindowManager.inst.Show("已发起总统弹劾。\n部门预算 −500，效率 −50.0。");
    }

    public static bool CabinetBudgetControl(MinistrySelectController panel, int raw, bool click,
        int[] points, int[] owners, int[] allocations)
    {
        // Party cards display seats. Their 500-series IDs must never reach the
        // native %100 dispatch for ministry allocation buttons.
        if (raw >= 500 && raw <= 506) {
            if (!click) return false;
            ((IButtonPressReceiver)panel).OnButtonEnter(raw);
            return true;
        }
        if (!ChineseVoting()) return false;
        int party = (int)GlobalScript.inst.gameState.party;
        int remaining = points[party];
        bool minus = raw >= 100 && raw < 100 + owners.Length;
        bool plus = raw >= 200 && raw < 200 + owners.Length;
        string reason;
        if (raw == -1 && remaining > 0) reason = "请先分配剩余预算。";
        else if (minus || plus) {
            int ministry = raw % 100;
            if (owners[ministry] != party) reason = "本党未掌管此部委。\n无法调整拨款。";
            else if (minus && allocations[ministry] <= 0) reason = "该部委尚无拨款。";
            else if (plus && remaining == 0) reason = "预算已分配完毕。\n请先收回其他拨款。";
            else {
                if (click) return false;
                PopUpWindowManager.inst.Show("每次调整 1\nCtrl ×10，Shift ×100\nAlt ×1000，退格 ×5000");
                return true;
            }
        } else return false;
        if (click) {
            PopUpWindowManager.inst.Hide();
            CabinetNote(panel, "分配预算\n\n本党待分配\n" + remaining + "\n\n" + reason);
        } else PopUpWindowManager.inst.Show(reason);
        return true;
    }
}
