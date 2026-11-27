// Small test doubles for Width/Renderer in the game's display-only methods.
// Native renderer measurements and screenshots are documented in COMMUNITY-1.2.4.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace UnityEngine
{
    public enum SystemLanguage { English, Russian }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z); }
    }
    public struct Bounds { public Vector3 min, max, size; }
    public class Material { public float lower; public float GetFloat(string field) { return lower; } }
    public class Transform
    {
        public Vector3 position;
        public Transform parent;
        public SpriteRenderer sprite;
        public T GetComponent<T>() where T : class { return sprite as T; }
    }
    public class GameObject
    {
        public Transform transform = new Transform();
        public bool activeSelf = true;
        public Renderer renderer;
        public void SetActive(bool value) { activeSelf=value; }
        public T GetComponent<T>() where T : class { return renderer as T; }
    }
    public class TextMesh : GameObject
    {
        public string text = "";
        public float characterSize = 1f;
        public float visualHeight;
        public bool richText = true;
        public TextMesh() { renderer = new Renderer(this); }
    }
    public class Renderer
    {
        GameObject owner;
        float fixedWidth;
        public Renderer(GameObject owner, float width=0) { this.owner=owner; fixedWidth=width; }
        public Bounds bounds { get {
            TextMesh text = owner as TextMesh;
            float width = text == null ? fixedWidth : text.characterSize * text.text.Sum(c => c=='i' ? .07f : c=='W' ? .30f : .17f);
            float start = owner.transform.position.x;
            float bottom = owner.transform.position.y - (text == null ? 0f : text.visualHeight);
            return new Bounds { min=new Vector3(start,bottom,0), max=new Vector3(start+width,0,0), size=new Vector3(width,0,0) };
        }}
    }
    public class SpriteRenderer : Renderer { public SpriteRenderer(GameObject obj,float width):base(obj,width){} }
}
class Slot { public string name; }
class SaveLocalController { public Slot save; public TextMesh text; }
class SaveController { public TextMesh text; public SaveLocalController[] saveLocalControllers; }
class Mod { public int type; }
class ModManager { public static ModManager inst; public int loadedMod; public List<Mod> mods; }
class Minister { public int owner; }
class GameState { public int party; public Minister[] ministers = new Minister[14]; }
class LanguageManager
{
    public static LanguageManager inst;
    public SystemLanguage language;
    public string[] gui_text = new string[1200];
}
class SaveNameTests
{
    static void Check(bool ok) { if(!ok) throw new Exception("Save name regression"); }
    static void Main()
    {
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
        Check(String.Format(SaveNameLayout.HudChangeFormat(zero), "行政资源", 0, "：25").Contains("上月变化：0"));
        Check(String.Format(SaveNameLayout.HudChangeFormat(positive), "支持率", 5).EndsWith("<color=#99FF73>+5</color>"));
        Check(String.Format(SaveNameLayout.HudChangeFormat(negative), "支持率", -5).EndsWith("<color=#FF001D>-5</color>"));
        Check(SaveNameLayout.HudThousands("М.") == "千" && SaveNameLayout.HudThousands("Т.") == "千");
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
        LanguageManager.inst.gui_text[272] = "文体与教育部";
        LanguageManager.inst.gui_text[1139] = "公共对话委员会";
        foreach (int cost in new int[] { 500, 200 }) {
            game.ministers[8].owner = 2;
            Check(SaveNameLayout.FundingPreview("old", game, cost) == "文体与教育部预算：−" + cost);
            game.ministers[8].owner = 1;
            Check(SaveNameLayout.FundingPreview("old", game, cost) == "公共对话委员会预算：−" + cost);
        }
        LanguageManager.inst.language = SystemLanguage.Russian;
        Check(SaveNameLayout.HudChangeFormat(zero) == zero && SaveNameLayout.HudThousands("М.") == "М.");
        Check(SaveNameLayout.FundingPreview("старый", game, 500) == "старый");
        Check(SaveNameLayout.HudDeltaValue(38, 12).Equals(3.8f));
        LanguageManager.inst.language = SystemLanguage.English;
        ModManager.inst=new ModManager{loadedMod=0,mods=new List<Mod>{new Mod{type=2}}};
        Check(SaveNameLayout.HudChangeFormat(zero) == zero && SaveNameLayout.HudThousands("Т.") == "Т.");
        Check(SaveNameLayout.FundingPreview("mod", game, 500) == "mod");
        Check(SaveNameLayout.HudDeltaValue(-38, 3).Equals(-3.8f));
        Check(SaveNameLayout.SettingsValue("English")=="English");
        Check(SaveNameLayout.ClassicChinese("萨莱蒂安边疆区")=="萨莱蒂安边疆区");
    }
}
