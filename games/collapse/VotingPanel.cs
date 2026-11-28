using System;
using UnityEngine;

internal static partial class SaveNameLayout
{
    // A zero-seat chamber can neither pass a bill nor allocate a chart slice.
    public static float VotingShare(float numerator, float seats)
    {
        return seats > 0f ? numerator / seats : 0f;
    }

    public static bool ChineseVoting()
    {
        if (!ChineseHud()) return false;
        string[] table = LanguageManager.inst.gui_text;
        return table != null && table.Length > 1563 && table[1563] != null &&
               table[1563].StartsWith("通过", StringComparison.Ordinal);
    }

    public static void FitFactionGrid(TextMesh[] labels, ref float[] sizes)
    {
        if (ChineseVoting()) {
            FitDiplomacyGrid(labels, ref sizes);
            return;
        }
        if (sizes == null || labels == null) return;
        for (int i = 0; i < labels.Length && i < sizes.Length; ++i)
            if (labels[i] && sizes[i] > 0f) labels[i].characterSize = sizes[i];
        sizes = null;
    }

    // The chart and its tally text share a parent. Keep the text visible even
    // in an empty chamber, while hiding the pie and disabling its own hitbox.
    public static void VotingChartVisibility(GameObject chart, bool hasSeats)
    {
        chart.SetActive(true);
        var renderer = chart.GetComponent<Renderer>();
        var hitbox = chart.GetComponent<Collider2D>();
        if (renderer) renderer.enabled = hasSeats;
        if (hitbox) hitbox.enabled = hasSeats;
    }

    public static bool VotingHasSeats(GameState state)
    {
        foreach (int n in state.deputies) if (n > 0) return true;
        return false;
    }

    public static string VotingOtherPartyStatus(string native)
    {
        return ChineseVoting() ? "无议席" : native;
    }

    public static object VotingVetoCost(int raw)
    {
        return ChineseVoting() ? (object)(raw / 10f) : raw;
    }

    public static object VotingThreshold(object original)
    {
        return ChineseVoting() && (int)original == 50 ? (object)"50.1" : original;
    }

    public static bool VotingFixedBlocked(GameState state, bool fixedProposal, bool voted)
    {
        return fixedProposal && !voted && state.deputies[(int)state.party] == 0;
    }

    // The native "return" handler only loads the map. Fixed proposals
    // normally cannot return until after voting, when their draft is cleared.
    public static void ClearBlockedVotingDraft(GameState state, int[] laws, ref int[] pressure,
        ref bool fixedProposal, ref bool offered, ref int proposer)
    {
        Array.Copy(state.laws, laws, laws.Length);
        pressure = new int[state.deputies.Length];
        fixedProposal = false;
        offered = false;
        proposer = -1;
    }

    static string VotingBalances(GameState state)
    {
        var party = state.parties[(int)state.party];
        return "党内资金\n" + (party.money / 10f) + "\n行政资源\n" + (party.admin / 10f);
    }

    public static void VotingResultPanel(LawsVotingController panel, float actualShare)
    {
        if (!ChineseVoting()) return;
        GameState state = GlobalScript.inst.gameState;
        panel.playerStat.text = VotingBalances(state);
        panel.deputies.text = VotingHasSeats(state)
            ? "表决已结束\n赞成票占比：" + (actualShare * 100f).ToString("0.0") + "%"
            : "表决已结束\n议会尚无议席，本次无议员票";
    }

    public static void VotingPanelText(LawsVotingController panel, bool proposing, bool fixedProposal,
        int proposer, int changes, bool voted, bool vetoHover, bool forecast)
    {
        if (!ChineseVoting() || voted) return;
        GameState state = GlobalScript.inst.gameState;
        int me = (int)state.party;
        bool mine = proposing || fixedProposal || proposer == me;
        bool noSeat = state.deputies[me] == 0;
        bool blocked = mine && !panel.buttons[2].activeSelf;
        bool emptyDraft = proposing && !fixedProposal && changes == 0;
        if (noSeat) {
            string reason = mine ? "本党无议席，无法提交" : "本党无议席，可表态";
            if (!VotingHasSeats(state))
                panel.deputies.text = "议会尚无议席\n暂无支持率\n" + reason;
            else if (mine)
                panel.deputies.text = reason;
            else
                panel.deputies.text += "\n" + reason;
            if (mine)
                panel.deputies.text += panel.buttons[5].activeSelf ? "\n先撤回修改，再返回" : "\n可返回地图";
        }
        else if (blocked && proposing && state.modifies[127] > 0) {
            panel.deputies.text = "议会大楼维修中，暂不能提交" +
                (panel.buttons[5].activeSelf ? "\n先撤回修改，再返回" : "\n可返回地图");
        }
        else if (emptyDraft) {
            string summary = panel.deputies.text;
            int end = summary.IndexOf('\n');
            panel.deputies.text = (end < 0 ? "" : summary.Substring(0, end) + "\n") + "尚未选择要修改的法案";
        }
        if (!forecast || blocked || !mine || emptyDraft) {
            panel.playerStat.text = VotingBalances(state);
            if (!mine)
                panel.playerStat.text += vetoHover ? "\n否决耗100行政资源" : "\n提案党承担费用";
        }
    }
}
