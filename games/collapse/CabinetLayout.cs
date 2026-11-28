using System;
using UnityEngine;

internal static partial class SaveNameLayout
{
    // The original hit boxes handle the clicks. Fit the text inside the sprites
    // without changing where the player can click or how portfolios are assigned.
    public static void FitCabinetCards(MinistrySelectController panel, bool budget)
    {
        if (!ChineseVoting()) return;
        for (int i = 0; i < panel.ministry_points.Length; i++) {
            TextMesh amount = panel.ministry_points[i];
            Transform card = amount.transform.parent;
            var paper = card.GetComponent<SpriteRenderer>();
            if (!paper || !paper.sprite) continue;
            Vector3 size = paper.bounds.size;
            var heading = card.Find("Name");
            if (heading) FitDiplomacyBounds(heading.GetComponent<TextMesh>(), size.x * .90f, size.y * .29f);
            // The percentage is the share of the state budget. Both it and
            // the allocation/requirement ratio need to stay visible.
            if (budget) {
                string value = amount.text;
                int split = value.IndexOf('(');
                if (split > 0 && value.IndexOf('\n') < 0)
                    amount.text = value.Substring(0,split).TrimEnd() + "\n" + value.Substring(split);
            }
            amount.characterSize = .13f;
            FitDiplomacyBounds(amount, size.x * .86f, size.y * .30f);
            if (budget) {
                float offset = paper.bounds.center.y + size.y * .05f - amount.GetComponent<Renderer>().bounds.center.y;
                amount.transform.position += new Vector3(0f,offset,0f);
            }
        }
        foreach (TextMesh label in panel.party_points) {
            var strip = label.transform.parent.GetComponent<SpriteRenderer>();
            if (!strip || !strip.sprite) continue;
            label.characterSize = .17f;
            FitDiplomacyBounds(label, strip.bounds.size.x * .90f, strip.bounds.size.y * .82f);
        }
        var badge = panel.stage_indicator.transform.parent.GetComponent<SpriteRenderer>();
        if (badge && badge.sprite)
            FitDiplomacyBounds(panel.stage_indicator, badge.bounds.size.x * .88f, badge.bounds.size.y * .45f);
    }

    static void SetCabinetButton(GameObject button, string caption, bool enabled)
    {
        if (!button) return;
        var label = button.GetComponentInChildren<TextMesh>(true);
        if (!label) return;
        label.text = caption;
        var simple = button.GetComponent<SimpleButtonScript>();
        if (simple) simple.active = enabled;
        label.color = enabled ? Color.white : new Color(.72f,.72f,.72f,1f);
        var sprite = button.GetComponent<SpriteRenderer>();
        if (sprite) sprite.color = enabled ? Color.white : new Color(.55f,.55f,.55f,1f);
        var hit = button.GetComponent<Collider2D>();
        label.characterSize = .23f;
        if (hit) FitDiplomacyBounds(label, hit.bounds.size.x * .89f, hit.bounds.size.y * .85f);
    }

    static GameObject CabinetConfirm(MinistrySelectController panel)
    {
        var sibling = panel.next_turn_button.transform.parent.Find("Button (0)");
        return sibling ? sibling.gameObject : null;
    }

    static void CabinetNote(MinistrySelectController panel, string text)
    {
        TextMesh note = panel.actions;
        note.text = "<color=#FFF4ED>" + text + "</color>";
        note.characterSize = .081f;
        var card = panel.ministry_points[0].transform.parent.GetComponent<SpriteRenderer>();
        if (card) FitDiplomacyBounds(note, card.bounds.size.x * 1.08f, card.bounds.size.y * 3.7f);
    }

    // The scene's static translation components initialize after the original
    // negotiation repaint on some loads. Start runs after all scene Awake calls.
    public static void StartCabinetLabels(MinistrySelectController panel, int stage, int mode)
    {
        if (!ChineseVoting() || mode != (int)MinistrySelectController.MissionMode.Coalition) return;
        var confirm = CabinetConfirm(panel);
        if (!confirm) return;
        bool enabled = confirm.GetComponent<SimpleButtonScript>().active;
        SetCabinetButton(confirm,stage == 0 ? "组建政府" : "确认预算",enabled);
        if (stage == 0) SetCabinetButton(panel.next_turn_button,"移交组阁",true);
    }

    public static void ShowCoalitionTerms(MinistrySelectController panel, int support, int cohesion)
    {
        if (!ChineseVoting()) return;
        var state = GlobalScript.inst.gameState;
        int seats = 0;
        foreach (int count in state.deputies) seats += count;
        bool majority = CoalitionFormationLogic.HasMajority(state,support);
        int missing = Math.Max(0,(seats * 51 + 99) / 100 - support);
        string seatsColor = majority ? "#5BFF5B" : "#FF9595";
        string cohesionColor = cohesion >= 51 ? "#5BFF5B" : "#FF9595";
        string terms = "支持议席\n<color=" + seatsColor + ">" + support + " / " + seats + " 席</color>\n"
            + (seats == 0 ? "议会尚无议席" : majority ? "已达门槛" : "还差 " + missing + " 席")
            + "\n\n联盟凝聚力\n<color=" + cohesionColor + ">" + cohesion + " / 100</color>\n"
            + (cohesion >= 51 ? "已达门槛" : "至少需要 51");
        bool ready = majority && cohesion >= 51;
        string advice;
        if (ready) advice = "可以组阁";
        else if (seats == 0) advice = "请移交组阁。";
        else if (majority) advice = "可调整职位分配，\n提高凝聚力。";
        else if (!CoalitionFormationLogic.CanReachMajority(state,(int)state.party))
            advice = "无法取得多数，\n请移交组阁。";
        else advice = "可分配职位\n争取他党支持，\n也可移交组阁。";
        CabinetNote(panel,terms + "\n\n" + advice);
        SetCabinetButton(CabinetConfirm(panel),"组建政府",ready);
        SetCabinetButton(panel.next_turn_button,"移交组阁",true);
        FitCabinetCards(panel,false);
    }

    public static void ShowCabinetAllocation(MinistrySelectController panel, int stage, int mode, int[] points)
    {
        if (!ChineseVoting() || stage != 1) return;
        int remaining = points[(int)GlobalScript.inst.gameState.party];
        SetCabinetButton(CabinetConfirm(panel),"确认预算",remaining == 0);
        if (mode == (int)MinistrySelectController.MissionMode.Coalition) {
            CabinetNote(panel,"分配预算\n\n本党待分配\n" + remaining
                + (remaining > 0 ? "\n\n点击所属部委的\n加号分配资金。"
                                 : "\n\n分配完毕，\n可以确认预算。"));
        }
    }

    public static void ShowCabinetCrisis(MinistrySelectController panel)
    {
        if (!ChineseVoting()) return;
        CabinetNote(panel,"组阁失败\n\n六个月后\n重新选举。");
        SetCabinetButton(CabinetConfirm(panel),"返回地图",true);
        panel.next_turn_button.SetActive(false);
    }
}
