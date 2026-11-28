using UnityEngine;

internal static partial class SaveNameLayout
{
    static void FitVotingText(TextMesh label, float initialSize, float width, float height)
    {
        if (!label || !label.gameObject.activeInHierarchy) return;
        // The scroll reuses rows. Always start from the selected size so that
        // a short law following a long one grows back to the regular size.
        label.characterSize = initialSize;
        FitDiplomacyBounds(label, width, height);
    }

    static void VotingPanelColors(TextMesh label)
    {
        if (!label || string.IsNullOrEmpty(label.text) || label.text.IndexOf("<color=") < 0) return;
        string source = label.text;
        string readable = source.Replace("<color=green>", "<color=#B2F3BD>")
            .Replace("<color=red>", "<color=#FFB5B0>")
            .Replace("<color=#FF0027>", "<color=#FFD0BD>")
            .Replace("<color=#FF008E>", "<color=#F7BCE6>")
            .Replace("<color=#001DB0>", "<color=#C0D7FF>");
        if (readable != source) label.text = readable;
    }

    public static void ApplyVotingLayout(LawsVotingController panel)
    {
        if (!panel || !ChineseVoting()) return;
        for (int i = 0; i < panel.laws_names.Length; ++i) {
            var label = panel.laws_names[i];
            if (!label || !label.gameObject.activeInHierarchy) continue;
            var button = panel.laws_names_sprites[i];
            if (!button || !button.spriteRenderer) continue;
            var size = button.spriteRenderer.bounds.size;
            FitVotingText(label, .12f, size.x - .26f, size.y * .76f);
        }
        for (int i = 0; i < panel.laws_states.Length; ++i) {
            var label = panel.laws_states[i];
            if (!label || !label.gameObject.activeInHierarchy) continue;
            var button = panel.laws_state_buttons[i];
            if (!button || !button.spriteRenderer) continue;
            var size = button.spriteRenderer.bounds.size;
            // The help button occupies the left part of this same row.
            FitVotingText(label, .105f, size.x - 1.25f, size.y * .71f);
            var help = label.transform.Find("ButtonSelect/Text");
            if (help) {
                var mark = help.GetComponent<TextMesh>();
                if (mark) mark.characterSize = .047f;
                var p = help.localPosition;
                p.y = 0f;
                help.localPosition = p;
            }
        }
        foreach (var label in panel.parties) {
            if (!label) continue;
            FitVotingText(label, .135f, 1.78f, .21f);
            var badge = label.transform.parent.GetComponent<SpriteRenderer>();
            if (badge) badge.color = new Color32(148, 144, 146, 255);
            VotingPanelColors(label);
        }
        foreach (var button in panel.buttons) {
            if (button && button.activeInHierarchy)
                FitVotingText(button.GetComponent<TextMesh>(), .18f, 2.07f, .43f);
        }
        FitVotingText(panel.playerStat, .165f, 1.79f, 1.92f);
        FitVotingText(panel.deputies, .154f, 3.95f, 1.62f);
        string resultText = panel.result ? panel.result.text : null;
        bool rollCall = panel.diagramm && !panel.diagramm.activeSelf && !string.IsNullOrEmpty(resultText)
            && (resultText.Contains(":\n<color=") || resultText.Contains("： <color="));
        if (rollCall && panel.result && !string.IsNullOrEmpty(panel.result.text)) {
            // Once the chart is hidden, each party and its two counts can share
            // a row. Keep the native totals, outcome and any veto explanation.
            string roll = panel.result.text;
            string compact = roll.Replace(":\n<color=", "： <color=");
            if (compact != roll) {
                int lines = 0;
                float scale = Mathf.Abs(panel.result.transform.lossyScale.x);
                if (scale > 0f && panel.result.font)
                    compact = MessageManager.Perenos(panel.result.font, 4.0f / (.146f * scale),
                        compact.Replace("，党内", "，\n党内"), panel.result.fontSize, ref lines);
                panel.result.text = compact;
            }
        }
        FitVotingText(panel.result, .146f, 4.02f, rollCall ? 5.94f : 1.40f);
        FitVotingText(panel.support_change, .143f, 4.48f, 1.23f);
        VotingPanelColors(panel.deputies);
        VotingPanelColors(panel.result);
        VotingPanelColors(panel.support_change);
        foreach (var symbol in panel.faction_support_symbol) VotingPanelColors(symbol);
        if (panel.messageManager && panel.messageManager.textMesh) {
            // The native dialog uses this size for wrapping and scroll travel.
            panel.messageManager.textMesh.characterSize = .064f;
            var confirm = panel.messageManager.transform.Find("Buton/Text");
            if (confirm) {
                var label = confirm.GetComponent<TextMesh>();
                if (label) {
                    if (label.text == "OK") label.text = "确定";
                    label.characterSize = .061f;
                }
            }
        }
    }

    public static void ApplyMinistryThresholds(MinistryController panel)
    {
        if (!panel || panel.up_text == null) return;
        bool chinese = ChineseVoting();
        foreach (var label in panel.up_text) {
            if (!label) continue;
            // All five original headings use .2, including the inactive ones.
            if (chinese) FitVotingText(label, .15f, 1.04f, .62f);
            else label.characterSize = .2f;
        }
    }
}
