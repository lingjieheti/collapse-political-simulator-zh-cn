using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

internal static partial class SaveNameLayout
{
    // This private child of the original saves folder stays on the same volume.
    // The stock save browser only enumerates *.smeta in the parent directory.
    private const string TransactionFolder = "saves/.collapse-save-transaction";

    private static void ValidateSlotName(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "." || name == ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            Path.GetFileName(name) != name)
            throw new InvalidDataException("Invalid save slot name.");
    }

    private static void CleanSaveStage(string folder)
    {
        // Remove only known scratch files. A pending journal is never removed here.
        foreach (string file in new[] { "plan.tmp", "new.save", "new.smeta",
            "old.save", "old.smeta", "undo.save", "undo.smeta" })
            File.Delete(Path.Combine(folder, file));
    }

    private static void UndoSaveFile(string folder, string target, string extension, bool existed)
    {
        string previous = Path.Combine(folder, "old" + extension);
        string next = Path.Combine(folder, "new" + extension);
        if (File.Exists(previous)) {
            // If the process stopped during rollback, the old bytes remain in
            // previous until this last rename has actually succeeded.
            if (File.Exists(target))
                File.Move(target, Path.Combine(folder, "undo" + extension));
            File.Move(previous, target);
        } else if (!existed && !File.Exists(next)) {
            // The newly created file has already been moved out of staging.
            File.Delete(target);
        }
    }

    private static void RollbackSaveStage(string folder)
    {
        string journal = Path.Combine(folder, "pending");
        if (!File.Exists(journal)) return;
        string name;
        bool hadData, hadMeta;
        using (var input = new BinaryReader(File.OpenRead(journal))) {
            name = input.ReadString();
            hadData = input.ReadBoolean();
            hadMeta = input.ReadBoolean();
        }
        ValidateSlotName(name);
        string target = Path.Combine("saves", name);
        UndoSaveFile(folder, target + ".smeta", ".smeta", hadMeta);
        UndoSaveFile(folder, target + ".save", ".save", hadData);
        File.Delete(journal);
    }

    private static void MoveSaveFile(string folder, string target, string extension, bool existed)
    {
        if (existed)
            File.Move(target, Path.Combine(folder, "old" + extension));
        File.Move(Path.Combine(folder, "new" + extension), target);
    }

    private static Save StageNewSave(string folder, string name, GameState state, BinaryFormatter formatter)
    {
        var utc = DateTime.UtcNow;
        var save = new Save();
        save.version = GlobalScript.version;
        save.date = state.date;
        save.name = name;
        save.special_save_number = state.special_save_number;
        save.can_get_achivments = state.can_get_achivments;
        save.party = state.party;
        save.scenario = state.scenario;
        save.updatedUtc = utc;
        using (var data = File.Create(Path.Combine(folder, "new.save"))) {
            formatter.Serialize(data, state);
            data.Flush(true);
        }
        using (var meta = File.Create(Path.Combine(folder, "new.smeta")))
        using (var writer = new BinaryWriter(meta)) {
            // Same order, widths, string encoding and UTC representation as WriteSave.
            writer.Write(save.version);
            writer.Write(save.date.Year);
            writer.Write(save.date.Month);
            writer.Write(save.date.Day);
            writer.Write(save.can_get_achivments);
            writer.Write(save.special_save_number);
            writer.Write(save.name);
            writer.Write((int)save.party);
            writer.Write(save.scenario);
            writer.Write(utc.ToBinary());
            writer.Flush();
            meta.Flush(true);
        }
        return save;
    }

    private static void RecordSavePlan(string folder, string name, bool hadData, bool hadMeta)
    {
        string tmp = Path.Combine(folder, "plan.tmp");
        using (var file = File.Create(tmp))
        using (var writer = new BinaryWriter(file)) {
            writer.Write(name);
            writer.Write(hadData);
            writer.Write(hadMeta);
            writer.Flush();
            file.Flush(true);
        }
        File.Move(tmp, Path.Combine(folder, "pending"));
    }

    private static FileStream LeaseSaveDirectory()
    {
        Directory.CreateDirectory(TransactionFolder);
        string file = Path.Combine(TransactionFolder, "guard");
        for (int i = 0; ; i++) {
            try { return new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) {
                if (i == 4) throw;
                System.Threading.Thread.Sleep(15);
            }
        }
    }

    private static void ReportSaveError(Exception error)
    {
        Debug.LogWarning(error.ToString());
        if (MessageManager.inst != null) MessageManager.inst.Message(635);
    }

    public static bool TrySaveTransaction(SaveLoadManager owner, string name, GameState state, BinaryFormatter formatter, bool createOnly)
    {
        try {
            ValidateSlotName(name);
            string folder = TransactionFolder;
            using (var guard = LeaseSaveDirectory()) {
                RollbackSaveStage(folder);
                CleanSaveStage(folder);
                string target = Path.Combine("saves", name);
                bool hadData = File.Exists(target + ".save");
                bool hadMeta = File.Exists(target + ".smeta");
                if ((hadData || hadMeta) && (createOnly ||
                    (state.special_save_number >= 0 && !owner.saves.ContainsKey(name)))) {
                    if (MessageManager.inst != null) MessageManager.inst.Message(66);
                    return false;
                }
                var save = StageNewSave(folder, name, state, formatter);
                RecordSavePlan(folder, name, hadData, hadMeta);
                try {
                    MoveSaveFile(folder, target + ".save", ".save", hadData);
                    MoveSaveFile(folder, target + ".smeta", ".smeta", hadMeta);
                    // Removing the journal commits the pair. Before this point a
                    // fresh process restores the previous files, including new slots.
                    File.Delete(Path.Combine(folder, "pending"));
                } catch {
                    RollbackSaveStage(folder);
                    throw;
                }
                owner.saves[name] = save;
                // A committed slot remains valid if housekeeping must be retried later.
                try { CleanSaveStage(folder); }
                catch (IOException cleanup) { Debug.LogWarning(cleanup.Message); }
                catch (UnauthorizedAccessException cleanup) { Debug.LogWarning(cleanup.Message); }
            }
            return true;
        } catch (Exception error) {
            ReportSaveError(error);
            return false;
        }
    }

    public static void ReadSaveIndexSafely(SaveLoadManager manager, Action scan)
    {
        // The background-story browser temporarily replaces manager.saves with
        // its own dictionary. Wait until the controller restores the campaign list.
        if (SaveLoadManager.backstoryMode) {
            manager.CancelInvoke("GetSaves");
            manager.Invoke("GetSaves", 0.3f);
            return;
        }
        try {
            using (var guard = LeaseSaveDirectory()) {
                RollbackSaveStage(TransactionFolder);
                manager.last_free_special_save_number = 0;
                manager.saves.Clear();
                scan();
            }
        } catch (Exception error) {
            if (manager.last_free_special_save_number != -1) Debug.LogWarning(error.ToString());
            manager.last_free_special_save_number = -1;
            manager.CancelInvoke("GetSaves");
            manager.Invoke("GetSaves", 0.3f);
        }
    }

    public static bool CanStartSaveIndex(GameSelectController menu, int button)
    {
        if (button != 10 || !menu.ach_enabled || SaveLoadManager.inst.last_free_special_save_number >= 0)
            return true;
        if (MessageManager.inst != null) MessageManager.inst.Message(635);
        return false;
    }

    public static void ReadSaveSafely(Save slot, Action<Save> read)
    {
        try {
            using (var guard = LeaseSaveDirectory()) {
                RollbackSaveStage(TransactionFolder);
                read(slot);
            }
        } catch (IOException error) { ReportSaveError(error); }
          catch (UnauthorizedAccessException error) { ReportSaveError(error); }
    }

    public static void DeleteSaveSafely(string name, Action<string> remove)
    {
        try {
            ValidateSlotName(name);
            using (var guard = LeaseSaveDirectory()) {
                RollbackSaveStage(TransactionFolder);
                remove(name);
            }
        } catch (IOException error) { ReportSaveError(error); }
          catch (UnauthorizedAccessException error) { ReportSaveError(error); }
    }

    public static void DeleteAllSavesSafely(Action remove)
    {
        try {
            using (var guard = LeaseSaveDirectory()) {
                RollbackSaveStage(TransactionFolder);
                remove();
            }
        } catch (IOException error) { ReportSaveError(error); }
          catch (UnauthorizedAccessException error) { ReportSaveError(error); }
    }
}
