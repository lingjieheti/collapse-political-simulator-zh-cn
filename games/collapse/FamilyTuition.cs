using System;

internal static partial class SaveNameLayout
{
    // Keep the addressee of the open letter. Advancing one child's timer must
    // not redirect the same confirmation to a sibling whose fees are also due.
    static GameState tuitionLetterState;
    static int tuitionLetterChild;

    static int DueTuitionChild(GameState state)
    {
        for (int child = 0; child < 2; child++) {
            int school = state.somethings[786 + child];
            if (school >= 1 && school <= 10 && state.somethings[789 + child] <= 0) return child;
        }
        return -1;
    }

    static int TuitionAmount(int school)
    {
        if (school == 4) return 0;
        if (school == 9) return 15;
        return school < 4 ? 10 : 25;
    }

    static int LetterChild(GameState state)
    {
        if (tuitionLetterState != state) {
            tuitionLetterState = state;
            tuitionLetterChild = DueTuitionChild(state);
        }
        return tuitionLetterChild;
    }

    public static string TuitionLetter(GameState state, out int eventName)
    {
        eventName = 7870;
        tuitionLetterState = state;
        tuitionLetterChild = DueTuitionChild(state);
        var language = LanguageManager.inst;
        var words = language.event_text;
        if (tuitionLetterChild < 0)
            return ChineseVoting() ? "目前没有到期的学费。" : String.Format(words[7879], '\n');
        int school = state.somethings[786 + tuitionLetterChild];
        return String.Format(words[7871], '\n', words[7798 + state.somethings[25]],
            language.gui_text[2129 + school], words[7872 + tuitionLetterChild], TuitionAmount(school));
    }

    public static void TuitionChoices(GameState state, int[] answers, string[] hints)
    {
        int child = LetterChild(state);
        if (child < 0) return;
        int school = state.somethings[786 + child];
        int fee = TuitionAmount(school);
        var language = LanguageManager.inst;
        var words = language.event_text;
        string charge = fee == 0 ? words[7864] : fee + " " + words[7616] + " £";
        hints[0] = String.Format("{0} <color=#0027FF>{1}</color>\n{2}: {3}\n{4}: {5} {6} £.",
            words[7876], language.gui_text[2129 + school], words[7877], charge, words[7613], state.cipr, words[7616]);
        answers[0] = fee == 0 || state.cipr >= fee ? 7874 : -(fee == 15 ? 7865 : fee == 10 ? 7866 : 7867);
        answers[1] = 7875;
    }

    public static string SettleTuition(GameState state, int answer)
    {
        int child = LetterChild(state);
        var words = LanguageManager.inst.event_text;
        if (child < 0) return String.Format(words[7879], '\n');
        int timer = 789 + child;
        // A redundant confirmation may acknowledge the existing receipt, but
        // cannot post another charge or advance a second course.
        if (state.somethings[timer] > 0)
            return String.Format(words[state.somethings[timer] == 12 ? 7878 : 7879], '\n');
        if (answer != 0) {
            state.somethings[timer] = 1000;
            return String.Format(words[7879], '\n');
        }
        int fee = TuitionAmount(state.somethings[786 + child]);
        if (fee > 0 && state.cipr < fee)
            return String.Format(words[fee == 15 ? 7865 : fee == 10 ? 7866 : 7867], '\n');
        state.cipr -= fee;
        state.somethings[timer] = 12;
        state.somethings[791 + child]++;
        return String.Format(words[7878], '\n');
    }
}
