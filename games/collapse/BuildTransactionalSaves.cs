using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    // The original display helpers have no protected regions. Save I/O uses
    // using/finally and catches; preserve their exact boundaries and types.
    static MethodDefinition CopySaveProcedure(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers, string name)
    {
        var original = source.Methods.Single(m => m.Name == name);
        var result = CopyFormatter(game, helpers, original);
        Func<Instruction, Instruction> imported = instruction => instruction == null ? null :
            result.Body.Instructions[original.Body.Instructions.IndexOf(instruction)];
        foreach (var area in original.Body.ExceptionHandlers)
            result.Body.ExceptionHandlers.Add(new ExceptionHandler(area.HandlerType) {
                CatchType = area.CatchType == null ? null : ImportType(game, area.CatchType),
                TryStart = imported(area.TryStart), TryEnd = imported(area.TryEnd),
                HandlerStart = imported(area.HandlerStart), HandlerEnd = imported(area.HandlerEnd),
                FilterStart = imported(area.FilterStart)
            });
        return result;
    }

    static void WrapSaveOperation(ModuleDefinition game, MethodDefinition original, MethodDefinition wrapper, bool passManager)
    {
        var native = new MethodDefinition(original.Name + "UnderSaveLease",
            MethodAttributes.Private|MethodAttributes.HideBySig, original.ReturnType);
        original.DeclaringType.Methods.Add(native);
        foreach (var arg in original.Parameters)
            native.Parameters.Add(new ParameterDefinition(arg.Name, arg.Attributes, arg.ParameterType));
        native.Body = original.Body;
        original.Body = new MethodBody(original);
        var delegateType = wrapper.Parameters.Last().ParameterType;
        var ctor = new MethodReference(".ctor", game.TypeSystem.Void, delegateType) { HasThis = true };
        ctor.Parameters.Add(new ParameterDefinition(game.TypeSystem.Object));
        ctor.Parameters.Add(new ParameterDefinition(game.TypeSystem.IntPtr));
        var il = original.Body.GetILProcessor();
        if (passManager) il.Emit(OpCodes.Ldarg_0);
        if (original.HasParameters) il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldftn, native); il.Emit(OpCodes.Newobj, ctor);
        il.Emit(OpCodes.Call, wrapper); il.Emit(OpCodes.Ret);
    }

    static void PatchTransactionalSaves(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        foreach (string name in new[] { "ValidateSlotName", "CleanSaveStage", "UndoSaveFile", "RollbackSaveStage",
                "MoveSaveFile", "StageNewSave", "RecordSavePlan", "LeaseSaveDirectory", "ReportSaveError" })
            CopySaveProcedure(game, source, helpers, name);
        var store = CopySaveProcedure(game, source, helpers, "TrySaveTransaction");
        var recover = CopySaveProcedure(game, source, helpers, "ReadSaveIndexSafely");
        var canStart = CopySaveProcedure(game, source, helpers, "CanStartSaveIndex");
        var read = CopySaveProcedure(game, source, helpers, "ReadSaveSafely");
        var remove = CopySaveProcedure(game, source, helpers, "DeleteSaveSafely");
        var removeAll = CopySaveProcedure(game, source, helpers, "DeleteAllSavesSafely");
        var manager = game.GetType("SaveLoadManager");
        var write = manager.Methods.Single(m => m.Name == "WriteSave" && m.Parameters.Count == 2);
        if (write.Body.Instructions.Count(i => i.Operand is MethodReference &&
            ((MethodReference)i.Operand).Name == "RemoveSaveEntryAndFiles") != 1 ||
            write.Body.Instructions.Count(i => i.Operand is MethodReference &&
            ((MethodReference)i.Operand).Name == "Serialize") != 1)
            throw new InvalidDataException("Unexpected save writer");
        var formatter = manager.Fields.Single(f => f.FieldType.FullName == "System.Runtime.Serialization.Formatters.Binary.BinaryFormatter");
        var attempt = new MethodDefinition("TryWriteSavePair", MethodAttributes.Public|MethodAttributes.HideBySig, game.TypeSystem.Boolean);
        foreach (var arg in write.Parameters)
            attempt.Parameters.Add(new ParameterDefinition(arg.Name, arg.Attributes, arg.ParameterType));
        attempt.Parameters.Add(new ParameterDefinition("createOnly", ParameterAttributes.None, game.TypeSystem.Boolean));
        manager.Methods.Add(attempt);
        var il = attempt.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, formatter); il.Emit(OpCodes.Ldarg_3); il.Emit(OpCodes.Call, store); il.Emit(OpCodes.Ret);

        // Keep the original public void method for game, workshop and third-party callers.
        write.Body = new MethodBody(write);
        il = write.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Call, attempt); il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ret);

        var panel = game.GetType("SaveController").Methods.Single(m => m.Name == "ButtonPress");
        ExpandShortBranches(panel);
        il = panel.Body.GetILProcessor();
        var calls = panel.Body.Instructions.Where(i => i.Operand is MethodReference &&
            ((MethodReference)i.Operand).FullName == write.FullName).ToArray();
        if (calls.Length != 3) throw new InvalidDataException("Unexpected save panel write calls");
        foreach (var call in calls) {
            var next = call.Next;
            call.OpCode = call == calls[1] ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0; call.Operand = null;
            var run = Instruction.Create(OpCodes.Call, attempt);
            il.InsertAfter(call, run);
            var branch = Instruction.Create(OpCodes.Brtrue, next);
            il.InsertAfter(run, branch); il.InsertAfter(branch, Instruction.Create(OpCodes.Ret));
        }
        var scan = manager.Methods.Single(m => m.Name == "GetSaves" && !m.HasParameters);
        WrapSaveOperation(game, scan, recover, true);
        var load = manager.Methods.Single(m => m.Name == "LoadSave" && m.Parameters.Count == 1);
        WrapSaveOperation(game, load, read, false);
        WrapSaveOperation(game, manager.Methods.Single(m=>m.Name=="DeleteSave" && m.Parameters[0].ParameterType.FullName=="System.String"), remove, false);
        WrapSaveOperation(game, manager.Methods.Single(m=>m.Name=="DeleteAllSaves" && !m.HasParameters), removeAll, false);
        var newGame = game.GetType("GameSelectController").Methods.Single(m=>m.Name=="ButtonPress" && m.Parameters.Count==1);
        il = newGame.Body.GetILProcessor();
        var begin = newGame.Body.Instructions[0];
        il.InsertBefore(begin, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(begin, Instruction.Create(OpCodes.Ldarg_1));
        il.InsertBefore(begin, Instruction.Create(OpCodes.Call, canStart));
        il.InsertBefore(begin, Instruction.Create(OpCodes.Brtrue, begin));
        il.InsertBefore(begin, Instruction.Create(OpCodes.Ret));
    }
}
