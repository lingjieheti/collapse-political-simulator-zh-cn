using UnityEngine;

internal static partial class SaveNameLayout
{
    // The paper shader fades the first 0.28 world units. Leave its edge fixed so
    // scrolling text never crosses the dark title band. This text has 0.45 Y scale.
    public static float EndingTextTop(float nativeTop)
    {
        return ChineseVoting() ? nativeTop - .65f : nativeTop;
    }

    public static void SetEndingVisibleLines(EndingController panel)
    {
        if (panel && ChineseVoting() && panel.max_lines == 12) panel.max_lines = 11;
    }

    public static void ApplyEndingLayout(EndingController panel, bool canSelect)
    {
        if (!panel || !ChineseVoting()) return;
        if (panel.title && !string.IsNullOrEmpty(panel.title.text)) {
            panel.title.characterSize = .33f;
            FitDiplomacyBounds(panel.title, 15.45f, .76f);
        }
        var labels = panel.thenames;
        var buttons = panel.buttons;
        if (labels == null || buttons == null || labels.Length != 11 || buttons.Length != 11) return;

        // In single-page endings the original game clears only the first six
        // labels. The five remaining prefab letters have no navigation target.
        if (!canSelect) {
            string[] prefab = "||||||П|П|Р|У|Ч".Split('|');
            for (int i = 0; i < labels.Length; i++) {
                if (!buttons[i] || !labels[i] ||
                    (!string.IsNullOrEmpty(labels[i].text) && labels[i].text != prefab[i])) return;
            }
            for (int i = 0; i < buttons.Length; i++) buttons[i].gameObject.SetActive(false);
            return;
        }

        // A custom chapter list keeps its original controls and names.
        string[] original = "政党|国家|区域|伙伴|派系|白|摩|斯|R|U|Ch".Split('|');
        string[] names = "政党|国家|区域|伙伴|派系|白俄罗斯|摩尔多瓦|斯洛伐克|俄罗斯|乌克兰|车臣".Split('|');
        for (int i = 0; i < labels.Length; i++) {
            if (!labels[i] || !buttons[i] || !buttons[i].spriteRenderer ||
                (labels[i].text != original[i] && labels[i].text != names[i])) return;
        }

        // RepaintText has already restored this tab's real text and scroll state.
        // RepaintButtons marks only the selected native button inactive.
        if (panel.textMesh && string.IsNullOrWhiteSpace(panel.textMesh.text)) {
            for (int i = 5; i < buttons.Length; i++) {
                if (!buttons[i].active) {
                    panel.textMesh.text = "本次结局没有该地区的专属报道。";
                    break;
                }
            }
        }

        // The eleven controls keep their own GenericButtonPress receivers and hit
        // boxes. Resize their faces together and cancel the parent's X multiplier
        // on the glyphs so that the font's aspect ratio stays constant.
        const float gap = .038f;
        const float unit = (17.05f - 10f * gap) / (30f + 11f * 1.25f);
        float left = -8.635f;
        for (int i = 0; i < labels.Length; i++) {
            var label = labels[i];
            var button = buttons[i].transform;
            var sprite = buttons[i].spriteRenderer;
            float width = unit * (names[i].Length + 1.25f);
            float factor = width / sprite.bounds.size.x;
            var scale = button.localScale;
            scale.x *= factor;
            button.localScale = scale;
            var glyphScale = label.transform.localScale;
            glyphScale.x /= factor;
            label.transform.localScale = glyphScale;
            var buttonPosition = button.position;
            buttonPosition.x += left + width * .5f - sprite.bounds.center.x;
            button.position = buttonPosition;
            label.text = names[i];
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = .198f;
            var pos = label.transform.position;
            pos.x = sprite.bounds.center.x;
            pos.y = sprite.bounds.center.y;
            label.transform.position = pos;
            FitDiplomacyBounds(label, width - .12f, .47f);
            left += width + gap;
        }
    }
}
