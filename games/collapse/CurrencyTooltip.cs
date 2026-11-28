internal static partial class SaveNameLayout
{
    public static string CurrencyInventoryLabel(string native)
    {
        if (!ChineseHud()) return native;
        string[] titles = LanguageManager.inst.gui_text;
        if (titles == null || titles.Length <= 677 ||
            titles[676] != "买入本币" || titles[677] != "卖出本币") return native;
        return "本币持有量";
    }
}
