// Small test doubles for Width/Renderer in the game's display-only methods.
// Native renderer measurements and screenshots are documented in COMMUNITY-1.2.4.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object obj) { return !ReferenceEquals(obj, null); }
    }
    public static class Mathf { public static float Min(float a, float b) { return Math.Min(a,b); } }
    public struct Vector2
    {
        public float x,y;
        public static Vector2 zero { get { return new Vector2(); } }
        public static implicit operator Vector2(Vector3 v) { return new Vector2{x=v.x,y=v.y}; }
    }
    public class Collider2D : Object { public bool enabled = true; }
    public class BoxCollider2D : Collider2D { public Vector2 offset, size; }
    public enum SystemLanguage { English, Russian }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z); }
    }
    public struct Bounds { public Vector3 min, max, size, center; }
    public class Material { public float lower; public float GetFloat(string field) { return lower; } }
    public class Transform : Object
    {
        public Vector3 position, localPosition;
        public Transform child;
        public Transform Find(string name) { return name=="Sprite" ? child : null; }
        public Vector3 InverseTransformVector(Vector3 v) { return v; }
        public Vector3 InverseTransformPoint(Vector3 v) { return v; }
        public Vector3 lossyScale = new Vector3(1f, 1f, 1f);
        public Transform parent;
        public SpriteRenderer sprite;
        public T GetComponent<T>() where T : class { return sprite as T; }
    }
    public class GameObject : Object
    {
        public Transform transform = new Transform();
        public bool activeSelf = true;
        public Renderer renderer;
        public BoxCollider2D collider;
        public void SetActive(bool value) { activeSelf=value; }
        public T GetComponent<T>() where T : class { return renderer as T ?? collider as T; }
    }
    public class TextMesh : GameObject
    {
        public string text = "";
        public float characterSize = 1f;
        public float visualHeight;
        public bool richText = true;
        public TextMesh() { renderer = new Renderer(this); }
    }
    public class Renderer : Object
    {
        public bool enabled = true;
        GameObject owner;
        float fixedWidth;
        public Renderer(GameObject owner, float width=0) { this.owner=owner; fixedWidth=width; }
        public Bounds bounds { get {
            TextMesh text = owner as TextMesh;
            float width = text == null ? fixedWidth : text.characterSize * text.text.Sum(c => c=='i' ? .07f : c=='W' ? .30f : .17f);
            float start = owner.transform.position.x;
            float bottom = owner.transform.position.y - (text == null ? 0f : text.visualHeight);
            return new Bounds { min=new Vector3(start,bottom,0), max=new Vector3(start+width,0,0), size=new Vector3(width,text==null ? .7f : text.visualHeight,0) };
        }}
    }
    public class SpriteRenderer : Renderer { public SpriteRenderer(GameObject obj,float width):base(obj,width){} }
}
class CountryController
{
    public TextMesh[] up_text = new TextMesh[5], info_text = new TextMesh[4];
    public SimpleButtonScript[] factions_buttons = new SimpleButtonScript[3];
}
class SimpleButtonScript : UnityEngine.Object { public TextMesh text; public SpriteRenderer spriteRenderer; }
class Slot { public string name; }
class InfoScript { public int num; public TextMesh text; }
class SaveLocalController { public Slot save; public TextMesh text; }
class SaveController { public TextMesh text; public SaveLocalController[] saveLocalControllers; }
class Mod { public int type; }
class ModManager { public static ModManager inst; public int loadedMod; public List<Mod> mods; }
class Minister { public int owner; }
class ThisParty { public int money, admin; }
class GlobalScript { public static GlobalScript inst; public GameState gameState; }
class LawsVotingController
{
    public TextMesh deputies = new TextMesh(), playerStat = new TextMesh();
    public GameObject[] buttons = Enumerable.Range(0,7).Select(i=>new GameObject()).ToArray();
}
class GameState { public int[] laws=new int[34], deputies=new int[7];
    public ThisParty[] parties=Enumerable.Range(0,7).Select(i=>new ThisParty()).ToArray();
    public int party; public int[] modifies = new int[141]; public Minister[] ministers = new Minister[14]; }
class LanguageManager
{
    public static LanguageManager inst;
    public SystemLanguage language;
    public string[] gui_text = new string[2777], system_text = new string[1800];
    public string GetPartyName(int party, bool colored) { return "party" + party; }
    public string GetCountryName(int index) { return "country" + index; }
}
class SaveNameTests
{
    static void Check(bool ok) { if(!ok) throw new Exception("Save name regression"); }
    static void Diplomacy()
    {
        LanguageManager.inst = new LanguageManager { language = SystemLanguage.English };
        var action = new TextMesh { text = "圣彼得堡国际经济论坛" };
        var plate = new SpriteRenderer(new GameObject(), 1.46f);
        action.transform.child = new Transform { sprite = plate };
        var grid = new[] {action};
        float[] native = null;
        SaveNameLayout.FitDiplomacyGrid(grid, ref native);
        Check(native[0] == 1f && action.characterSize < 1f && action.renderer.bounds.size.x <= 1.291f);
        action.text = "短词";
        SaveNameLayout.FitDiplomacyGrid(grid, ref native);
        Check(action.characterSize == 1f);
        action.text = "圣彼得堡国际经济论坛";
        SaveNameLayout.FitDiplomacyGrid(grid, ref native);
        var panel = new CountryController();
        for (int i = 0; i < 5; ++i)
            panel.up_text[i] = new TextMesh { text=i<4 ? (1991+i*4).ToString() : "", collider=new BoxCollider2D() };
        panel.factions_buttons[1] = new SimpleButtonScript { text=new TextMesh{text=new string('长',36)},
            spriteRenderer=new SpriteRenderer(new GameObject(),.9f) };
        bool applied = SaveNameLayout.FitDiplomacyPanel(panel, false);
        Check(panel.factions_buttons[1].text.characterSize < .14f);
        Check(applied && panel.up_text[0].characterSize == .112f && panel.up_text[4].collider.size.x == 0f);
        for (int i=0; i<5; ++i) Check(Math.Abs(panel.up_text[i].transform.localPosition.x - (i-2)*2.89f) < .01f);
        panel.up_text[4].text = "0";
        SaveNameLayout.FitDiplomacyPanel(panel, applied);
        Check(panel.up_text[4].collider.size.x > 1f);
        Check(SaveNameLayout.DiplomacyPunctuation("已经成立：: 条件： :") == "已经成立： 条件：");
        Check(SaveNameLayout.DiplomacyTitle("说明。\n已参加：") == "说明。");
        Check(SaveNameLayout.DiplomacyTitle("说明。<color=#8B0000>所需条件") == "说明。");
        LanguageManager.inst.system_text[1652] = "无人主导";
        Check(SaveNameLayout.DiplomacyParty(LanguageManager.inst,-1,true) == "无人主导");
        Check(SaveNameLayout.DiplomacyParty(LanguageManager.inst,6,true) == "party6");
        Check(SaveNameLayout.DiplomacyThresholdCountry(LanguageManager.inst,0) == "俄罗斯");
        foreach (int n in new[] {20,22,23}) {
            Check(SaveNameLayout.DiplomacyTitleIndex(-1,0,n) == 884+n);
            Check(SaveNameLayout.DiplomacyTitleIndex(-1,1,n) == 909+n);
            Check(SaveNameLayout.DiplomacyTitleIndex(-1,2,n) == -1);
        }
        Check(SaveNameLayout.DiplomacyTitleIndex(100,1,20) == 100);
        Check(SaveNameLayout.DiplomacyTitleIndex(-1,0,21) == -1);
        LanguageManager.inst.language = SystemLanguage.Russian;
        SaveNameLayout.FitDiplomacyGrid(grid, ref native);
        Check(native == null && action.characterSize == 1f);
        Check(!SaveNameLayout.FitDiplomacyPanel(panel, applied) && panel.up_text[0].characterSize == .2f);
        Check(SaveNameLayout.DiplomacyParty(LanguageManager.inst,-1,true) == "party-1");
        Check(SaveNameLayout.DiplomacyThresholdCountry(LanguageManager.inst,0) == "country0");
        Check(SaveNameLayout.DiplomacyTitleIndex(-1,0,20) == -1);
        Check(SaveNameLayout.DiplomacyPunctuation("原文：:") == "原文：:");
        Check(SaveNameLayout.DiplomacyTitle("原文。\n状态") == "原文。\n状态");
        LanguageManager.inst = null;
    }
    static void VotingPanels()
    {
        LanguageManager.inst=new LanguageManager { language=SystemLanguage.English };
        Check(!SaveNameLayout.ChineseVoting());
        LanguageManager.inst.gui_text[1563]="通过门槛：{1}%";
        var state=new GameState{party=2};
        GlobalScript.inst=new GlobalScript{gameState=state};
        var panel=new LawsVotingController();
        state.parties[2].money=300;state.parties[2].admin=800;
        panel.buttons[2].activeSelf=false;
        Check(!SaveNameLayout.VotingHasSeats(state) && SaveNameLayout.VotingShare(0,0)==0f);
        Check(SaveNameLayout.VotingShare(5,10)==.5f);
        var chart = new GameObject();
        chart.renderer = new Renderer(chart);
        chart.collider = new BoxCollider2D();
        SaveNameLayout.VotingChartVisibility(chart,false);
        Check(chart.activeSelf && !chart.renderer.enabled && !chart.collider.enabled);
        SaveNameLayout.VotingChartVisibility(chart,true);
        Check(chart.activeSelf && chart.renderer.enabled && chart.collider.enabled);
        Check(SaveNameLayout.VotingFixedBlocked(state,true,false) && !SaveNameLayout.VotingFixedBlocked(state,true,true));
        Check(!SaveNameLayout.VotingFixedBlocked(state,false,false));
        SaveNameLayout.VotingPanelText(panel,true,false,-1,1,false,false,true);
        Check(panel.deputies.text.Contains("无议席") && panel.deputies.text.Contains("撤回"));
        Check(!panel.playerStat.text.Contains("预计") && panel.playerStat.text.Contains("30"));
        panel.buttons[5].activeSelf=false;
        SaveNameLayout.VotingPanelText(panel,false,true,2,1,false,false,true);
        Check(panel.deputies.text.Contains("可返回地图"));
        SaveNameLayout.VotingPanelText(panel,false,false,1,1,false,false,true);
        Check(panel.deputies.text.Contains("可表态") && panel.playerStat.text.Contains("提案党承担费用"));
        state.deputies[1]=39;
        panel.deputies.text="56.4%";SaveNameLayout.VotingPanelText(panel,false,false,1,1,false,false,true);
        Check(panel.deputies.text.Contains("56.4%") && panel.deputies.text.Contains("可表态"));
        state.deputies[2]=1;panel.buttons[2].activeSelf=true;
        panel.deputies.text="50.1%\nother";panel.playerStat.text="expense";
        SaveNameLayout.VotingPanelText(panel,true,false,-1,0,false,false,true);
        Check(panel.deputies.text.Contains("尚未选择") && !panel.playerStat.text.Contains("expense"));
        panel.deputies.text="normal";panel.playerStat.text="real forecast";
        SaveNameLayout.VotingPanelText(panel,true,false,-1,1,false,false,true);
        Check(panel.deputies.text=="normal" && panel.playerStat.text=="real forecast");
        Check(SaveNameLayout.VotingVetoCost(1000).Equals(100f) && SaveNameLayout.VotingThreshold(50).Equals("50.1"));
        var draft=new int[34];draft[0]=3;int[] callerPressure={6,5,4,3,2,1,0};int[] pressure=callerPressure;
        bool fixedProposal=true, offered=true;int proposer=2;
        SaveNameLayout.ClearBlockedVotingDraft(state,draft,ref pressure,ref fixedProposal,ref offered,ref proposer);
        Check(draft[0]==0 && !fixedProposal && !offered && proposer==-1 && callerPressure[0]==6 && pressure[0]==0);
        LanguageManager.inst.language=SystemLanguage.Russian;
        state.deputies[2]=0;panel.deputies.text="native";panel.playerStat.text="native";
        SaveNameLayout.VotingPanelText(panel,true,false,-1,1,false,false,true);
        Check(panel.deputies.text=="native" && panel.playerStat.text=="native");
        Check(SaveNameLayout.VotingVetoCost(1000).Equals(1000) && SaveNameLayout.VotingThreshold(50).Equals(50));
        LanguageManager.inst=null;
    }
    static void Main()
    {
        Diplomacy();
        VotingPanels();
        var paragraph = new TextMesh { text="正文", visualHeight=4f };
        var mask = new Material { lower=0f };
        const float top=2.12f, previousBottom=9f;
        foreach (var origin in new[]{-3f,0f,7f}) {
            paragraph.transform.position=new Vector3(0f,origin,0f);
            Check(Math.Abs(SaveNameLayout.NewGameScrollY(previousBottom,top,1f,paragraph,mask)-2f)<.0001f);
            Check(Math.Abs(SaveNameLayout.NewGameScrollY(previousBottom,top,.5f,paragraph,mask)-3.04f)<.0001f);
            Check(Math.Abs(SaveNameLayout.NewGameScrollY(previousBottom,top,0f,paragraph,mask)-4.08f)<.0001f);
        }
        paragraph.visualHeight=.5f;
        Check(Math.Abs(SaveNameLayout.NewGameScrollY(previousBottom,top,0f,paragraph,mask)-2f)<.0001f);
        var name=new TextMesh(); var date=new TextMesh(); date.transform.position=new Vector3(2.5f,0,0);
        foreach(var full in new[]{"May14",new string('i',28),"", "<b>X</b>"}) {
            SaveNameLayout.SetRow(name,full,date); Check(name.text==full && !name.richText);
        }
        var slot=new SaveLocalController{save=new Slot(),text=name};
        var screen=new SaveController{text=new TextMesh(),saveLocalControllers=new[]{slot}};
        string previous=null;
        foreach(var marker in new[]{"A","B"}) {
            string full=new string('W',15)+marker+new string('W',8)+"0001";
            slot.save.name=full;
            SaveNameLayout.SetRow(name,full,date);
            Check(name.text.Contains("…") && name.renderer.bounds.max.x<=2.38f && slot.save.name==full);
            if(previous!=null)Check(previous==name.text);
            previous=name.text;
            SaveNameLayout.SetSelection(screen,0); Check(screen.text.text=="完整名称："+full);
            SaveNameLayout.SetSelection(screen,-1); Check(screen.text.text=="");
        }
        name.transform.parent=new Transform{sprite=new SpriteRenderer(new GameObject(),2.7f)};
        string query=new string('W',28);
        SaveNameLayout.SetSearch(name,"搜索: "+query,query);
        Check(name.text.StartsWith("搜索: …") && name.text.EndsWith("WW") && name.renderer.bounds.size.x<=2.58f);
        var letters=Enumerable.Range(0,28).Select(i=>{
            var g=new GameObject(); g.transform.position=new Vector3(-2+i*.35f,0,0);
            g.renderer=new Renderer(g,.30f); return g;
        }).ToArray();
        SaveNameLayout.ScrollNameEntry(letters);
        Check(!letters[0].activeSelf && letters[27].activeSelf);
        Check(letters.Where(g=>g.activeSelf).All(g=>g.renderer.bounds.min.x>=-2.201f && g.renderer.bounds.max.x<=2.081f));
        Check(SaveNameLayout.SettingsValue("English")=="简体中文" && SaveNameLayout.SettingsValue("Old")=="经典名称");
        Check(SaveNameLayout.ClassicChinese("萨莱蒂安边疆区")=="边疆区");
        Check(SaveNameLayout.ClassicChinese("<b>切尔诺里兹斯卡亚共和国</b>{0}")=="<b>共和国</b>{0}");
        Check(SaveNameLayout.ClassicChinese("萨莱蒂人")=="萨莱蒂人");
        string zero = "{0}{2} ({1})";
        string positive = "{0} <color=#99FF73>(+{1})</color>";
        string negative = "{0} <color=#FF001D>({1})</color>";
        LanguageManager.inst = new LanguageManager { language = SystemLanguage.English };
        LanguageManager.inst.gui_text[1914] = "Steam 上传进度：{0}/11";
        var uploadProgress = new TextMesh();
        SaveNameLayout.ShowInitialUploadProgress(uploadProgress);
        Check(uploadProgress.text == "Steam 上传进度：0/11");
        Check(SaveNameLayout.DisplayLiteral("Native", "中文") == "中文");
        Check(String.Format(SaveNameLayout.HudChangeFormat(zero), "行政资源", 0, "：25").Contains("上月变化：0"));
        Check(String.Format(SaveNameLayout.HudChangeFormat(positive), "支持率", 5).EndsWith("<color=#99FF73>+5</color>"));
        Check(String.Format(SaveNameLayout.HudChangeFormat(negative), "支持率", -5).EndsWith("<color=#FF001D>-5</color>"));
        Check(SaveNameLayout.HudThousands("М.") == "千" && SaveNameLayout.HudThousands("Т.") == "千");
        var counter = new InfoScript { num = 12, text = new TextMesh { text = "123456789千" } };
        float counterSize = 0f;
        SaveNameLayout.FitHudCounter(counter, ref counterSize);
        Check(counterSize == 1f && counter.text.renderer.bounds.size.x <= 1.601f && counter.text.text == "123456789千");
        counter.text.text = "42";
        SaveNameLayout.FitHudCounter(counter, ref counterSize);
        Check(counter.text.characterSize == 1f);
        counter.text.text = "123456789千";
        SaveNameLayout.FitHudCounter(counter, ref counterSize);
        LanguageManager.inst.language = SystemLanguage.Russian;
        SaveNameLayout.FitHudCounter(counter, ref counterSize);
        Check(counter.text.characterSize == 1f);
        LanguageManager.inst.language = SystemLanguage.English;
        counter.num = 11;
        SaveNameLayout.FitHudCounter(counter, ref counterSize);
        Check(counter.text.characterSize == 1f);
        foreach (int raw in new int[] { -38, 0, 38 }) {
            foreach (int indicator in new int[] { 3, 4, 5, 12 })
                Check(SaveNameLayout.HudDeltaValue(raw, indicator).Equals(raw));
            foreach (int indicator in new int[] { 0, 1, 2, 6, 7 })
                Check(SaveNameLayout.HudDeltaValue(raw, indicator).Equals(raw / 10f));
        }
        Check(SaveNameLayout.HudDeltaValue(200000001, 12).Equals(200000001));
        var tip = new TextMesh();
        tip.transform.parent = new Transform { sprite = new SpriteRenderer(new GameObject(), 2f) };
        float templateSize = 0f;
        SaveNameLayout.SetHudText(tip, "精英阶层支持率　上月变化：+38", ref templateSize);
        Check(templateSize == 1f && tip.characterSize < 1f && tip.renderer.bounds.size.x <= 1.781f);
        SaveNameLayout.SetHudText(tip, "对俄关系", ref templateSize);
        Check(tip.characterSize == 1f);
        GameState game = new GameState { party = 2 };
        game.ministers[8] = new Minister { owner = 2 };
        foreach (var raw in new[] { 0, 1, 99, 500, 527, 1000 })
            Check(SaveNameLayout.PledgeThreshold(raw).Equals(raw / 10f));
        Check(SaveNameLayout.BusinessProjectRelation(5) / 10f == 5f);
        const string neutral = "local {2}||standard {3} {4} {5}";
        LanguageManager.inst.gui_text[1760] = "treasury +{5}|upper aggression +{0}";
        LanguageManager.inst.gui_text[1761] = "funds after grant > 0 AND administration > 0: lower needs +{0}; otherwise no gain";
        game.modifies[133] = 6;
        game.modifies[9 + 5] = 1;
        string regional = SaveNameLayout.CoalitionTemplate(neutral, game);
        Check(string.Format(System.Globalization.CultureInfo.InvariantCulture, regional, 1.2f, 2.6f, 0.4f, "x", "y", 2.6f)
              == "local 0.4||treasury +2.6|upper aggression +1.2|funds after grant > 0 AND administration > 0: lower needs +1.2; otherwise no gain");
        game.modifies[9 + game.party] = 1;
        Check(SaveNameLayout.CoalitionTemplate(neutral, game) == neutral);
        game.modifies[9 + game.party] = 0;
        game.modifies[9 + 5] = 0;
        Check(SaveNameLayout.CoalitionTemplate(neutral, game) == neutral);
        game.modifies[9 + 5] = 1;
        LanguageManager.inst.gui_text[272] = "文体与教育部";
        LanguageManager.inst.gui_text[1139] = "公共对话委员会";
        foreach (int cost in new int[] { 500, 200 }) {
            game.ministers[8].owner = 2;
            Check(SaveNameLayout.FundingPreview("old", game, cost) == "文体与教育部预算：−" + cost);
            game.ministers[8].owner = 1;
            Check(SaveNameLayout.FundingPreview("old", game, cost) == "公共对话委员会预算：−" + cost);
        }
        LanguageManager.inst.language = SystemLanguage.Russian;
        uploadProgress.text = "native";
        SaveNameLayout.ShowInitialUploadProgress(uploadProgress);
        Check(uploadProgress.text == "native" && SaveNameLayout.DisplayLiteral("Native", "中文") == "Native");
        Check(SaveNameLayout.CoalitionTemplate(neutral, game) == neutral);
        Check(SaveNameLayout.PledgeThreshold(527).Equals(527));
        Check(SaveNameLayout.BusinessProjectRelation(5) == 5);
        Check(SaveNameLayout.HudChangeFormat(zero) == zero && SaveNameLayout.HudThousands("М.") == "М.");
        Check(SaveNameLayout.FundingPreview("старый", game, 500) == "старый");
        Check(SaveNameLayout.HudDeltaValue(38, 12).Equals(3.8f));
        LanguageManager.inst.language = SystemLanguage.English;
        ModManager.inst=new ModManager{loadedMod=0,mods=new List<Mod>{new Mod{type=2}}};
        Check(SaveNameLayout.CoalitionTemplate(neutral, game) == neutral);
        Check(SaveNameLayout.PledgeThreshold(527).Equals(527));
        Check(SaveNameLayout.BusinessProjectRelation(5) == 5);
        Check(SaveNameLayout.HudChangeFormat(zero) == zero && SaveNameLayout.HudThousands("Т.") == "Т.");
        Check(SaveNameLayout.FundingPreview("mod", game, 500) == "mod");
        Check(SaveNameLayout.HudDeltaValue(-38, 3).Equals(-3.8f));
        Check(SaveNameLayout.SettingsValue("English")=="English");
        Check(SaveNameLayout.ClassicChinese("萨莱蒂安边疆区")=="萨莱蒂安边疆区");
    }
}
