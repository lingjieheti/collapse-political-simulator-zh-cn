using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

// These stand-ins contain only the state consumed by the shipping save helper.
// Every invocation starts with a fresh in-memory index and a private working directory.
namespace UnityEngine { public static class Debug { public static void LogWarning(object value) {} } }
public static class GlobalScript { public const string version = "test"; }
public class Save {
    public string name, version;
    public DateTime date, updatedUtc;
    public int party, scenario, special_save_number;
    public bool can_get_achivments;
}
[Serializable] public class GameState {
    public DateTime date;
    public int party, scenario, special_save_number = -1;
    public bool can_get_achivments;
}
public class MessageManager {
    public static MessageManager inst = new MessageManager();
    public int last;
    public void Message(int code) { last = code; }
}
public class GameSelectController { public bool ach_enabled; }
public class SaveLoadManager {
    public static SaveLoadManager inst = new SaveLoadManager();
    public static bool backstoryMode;
    public Dictionary<string, Save> saves = new Dictionary<string, Save>();
    public int last_free_special_save_number;
    public bool retry;
    public void CancelInvoke(string method) { retry = false; }
    public void Invoke(string method, float delay) { retry = method == "GetSaves"; }
}

internal static class TransactionalSavesTests
{
    const string Name = "Campaign";
    const string Slot = "saves/Campaign";
    const string Stage = "saves/.collapse-save-transaction";
    static readonly BinaryFormatter Formatter = new BinaryFormatter();
    static readonly SaveLoadManager Index = SaveLoadManager.inst;

    static void Check(bool ok, string message) {
        if (!ok) throw new InvalidOperationException(message);
    }

    static bool Write(string name, int year, bool createOnly) {
        var state = new GameState { date = new DateTime(year, 1, 1) };
        return SaveNameLayout.TrySaveTransaction(Index, name, state, Formatter, createOnly);
    }

    static void CheckSavedYear(int year) {
        bool called = false;
        SaveNameLayout.ReadSaveSafely(new Save { name = Name }, delegate(Save slot) {
            using (var data = File.OpenRead(Slot + ".save"))
                Check(((GameState)Formatter.Deserialize(data)).date.Year == year, "Wrong campaign data");
            using (var reader = new BinaryReader(File.OpenRead(Slot + ".smeta"))) {
                Check(reader.ReadString() == GlobalScript.version && reader.ReadInt32() == year,
                      "Wrong campaign metadata");
            }
            called = true;
        });
        Check(called, "Load failed");
    }

    // Re-create the exact point after moving new data into place and moving old
    // metadata aside. Exit with the journal still present; another process reopens it.
    static void Interrupt() {
        Directory.CreateDirectory(Stage);
        bool existed = File.Exists(Slot + ".save");
        using (var journal = new BinaryWriter(File.Create(Stage + "/pending"))) {
            journal.Write(Name);
            journal.Write(existed);
            journal.Write(existed);
        }
        if (existed) {
            File.Move(Slot + ".save", Stage + "/old.save");
            File.Move(Slot + ".smeta", Stage + "/old.smeta");
        }
        File.WriteAllText(Slot + ".save", "uncommitted data");
        File.WriteAllText(Stage + "/new.smeta", "uncommitted metadata");
    }

    public static void Main(string[] args) {
        switch (args[0]) {
        case "seed":
            Check(Write(Name, 1992, false), "Could not create old save");
            break;
        case "locked-overwrite":
            var old = new Save { name = Name };
            Index.saves.Add(Name, old);
            using (File.Open(Slot + args[1], FileMode.Open, FileAccess.Read, FileShare.None))
                Check(!Write(Name, 1998, false), "Overwrote locked file");
            Check(Index.saves[Name] == old && MessageManager.inst.last == 635,
                  "Failed save changed the cached slot or missed the failure message");
            CheckSavedYear(1992);
            break;
        case "retry":
            Check(Write(Name, 1998, false), "Overwrite did not succeed after releasing lock");
            break;
        case "read-new":
            CheckSavedYear(1998);
            break;
        case "create-again":
            Check(!Write(args[1], 1998, true), "New save overwrote an existing slot");
            Check(Index.saves.Count == 0 && MessageManager.inst.last == 66, "Missing duplicate-name result");
            break;
        case "interrupt":
            Interrupt();
            break;
        case "delete":
            SaveNameLayout.DeleteSaveSafely(Name, delegate(string name) {
                File.Delete("saves/" + name + ".smeta");
                File.Delete("saves/" + name + ".save");
            });
            break;
        case "delete-all":
            SaveNameLayout.DeleteAllSavesSafely(delegate {
                foreach (string file in Directory.GetFiles("saves")) File.Delete(file);
            });
            break;
        case "scan":
        case "scan-empty":
            bool scanned = false;
            SaveNameLayout.ReadSaveIndexSafely(Index, delegate {
                scanned = true;
                bool expected = args[0] == "scan";
                Check(File.Exists(Slot + ".smeta") == expected && File.Exists(Slot + ".save") == expected,
                      "Scan saw an incomplete save pair");
            });
            Check(scanned && !Index.retry, "Save index was not ready");
            if (args[0] == "scan") CheckSavedYear(1992);
            break;
        case "busy-index":
            using (File.Open(Stage + "/guard", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
                bool entered = false;
                SaveNameLayout.ReadSaveIndexSafely(Index, delegate { entered = true; });
                Check(!entered && Index.retry && Index.last_free_special_save_number == -1,
                      "Busy index scanned a partial save or failed to schedule a retry");
                Check(!SaveNameLayout.CanStartSaveIndex(new GameSelectController { ach_enabled = true }, 10),
                      "An ironman number could be allocated from the incomplete index");
            }
            break;
        case "backstory":
            var campaigns = Index.saves;
            var stories = new Dictionary<string, Save> { { "Prologue", new Save() } };
            Index.saves = stories;
            SaveLoadManager.backstoryMode = true;
            bool scannedStories = false;
            SaveNameLayout.ReadSaveIndexSafely(Index, delegate { scannedStories = true; });
            Check(!scannedStories && Index.retry && stories.ContainsKey("Prologue"),
                  "A retry modified the separate backstory list");
            SaveLoadManager.backstoryMode = false;
            Index.saves = campaigns;
            Index.retry = false;
            SaveNameLayout.ReadSaveIndexSafely(Index, delegate {
                if (File.Exists(Slot + ".smeta")) Index.saves.Add(Name, new Save { name = Name });
            });
            Check(!Index.retry && campaigns.ContainsKey(Name) && !stories.ContainsKey(Name),
                  "Returning from the backstory browser did not restore the campaign list");
            CheckSavedYear(1992);
            break;
        default:
            throw new ArgumentException("Unknown fixture command");
        }
    }
}
