using System;

internal static partial class SaveNameLayout
{
    public static bool CanEndMinorityAppointment(int raw, GameState game)
    {
        return raw == 51 && game.modifies[215] > 0;
    }

    public static RequirementLine[] MinorityActionConditions(int raw, GameState game, RequirementLine[] conditions)
    {
        // Both active measures can be withdrawn independently of their original prerequisites.
        if (CanEndMinorityAppointment(raw, game) || (raw == 53 && game.modifies[216] > 0))
            return Array.Empty<RequirementLine>();
        return conditions;
    }

    public static void ApplyResearchAvailability(FactionController panel)
    {
        if (!ChineseVoting()) return;
        var game = GlobalScript.inst.gameState;
        bool canLead = game.somethings[210] == (int)game.party;
        var columns = new[] { panel.actions_buttons_column_0, panel.actions_buttons_column_1, panel.actions_buttons_column_2 };
        // RepaintResearch has already restored each column for its opening year.
        // Leave the collider live so disabled projects can still explain why.
        for (int column = 0; column < columns.Length; column++) {
            var buttons = columns[column];
            for (int row = 0; row < buttons.Length; row++) {
                var button = buttons[row];
                if (!button.active || (canLead && game.somethings[166 + column * 5 + row] < 20)) continue;
                button.SetMode(true);
                button.Active(false);
            }
        }
    }

    // The research grid has three columns of five. Other controls on this
    // screen (including the help button) occupy the gaps between their IDs.
    public static bool ShowResearchAction(int tab, int raw)
    {
        if (tab != 8 || raw < 10 || raw > 34 || raw % 10 >= 5 || !ChineseVoting())
            return false;

        int column = raw / 10 - 1;
        int index = column * 5 + raw % 10;
        int project = 166 + index;
        var game = GlobalScript.inst.gameState;
        int progress = game.somethings[project];
        int owner = game.somethings[210];
        int year = 1990 + 5 * column;
        string status;
        if (progress >= 20)
            status = "此课题已完成。";
        else if (owner < 0)
            status = "目前没有政党主导科研。";
        else if (owner != (int)game.party)
            status = "本党没有科研主导权。";
        else if (column > 0 && game.date.Year < year)
            status = string.Format("{0} 年起可研究。", year);
        else if (game.somethings[211] == project)
            status = string.Format("正在研究（{0}/20 月）。", progress);
        else if (progress > 0)
            status = string.Format("点击继续研究（{0}/20 月）。", progress);
        else
            status = "点击开始研究。";

        string description = string.Format(LanguageManager.inst.gui_text[654 + index], '\n');
        PopUpWindowManager.inst.Show(status + "\n" + description);
        return true;
    }
}
