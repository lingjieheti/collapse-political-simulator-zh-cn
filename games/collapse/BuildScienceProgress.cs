using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static bool IsProjectSlot(Instruction i)
    {
        return i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 211;
    }

    static void PatchScienceProgress(ModuleDefinition game)
    {
        var states = game.GetType("GameState").Fields.Single(f => f.Name == "somethings");
        var faction = game.GetType("FactionController");
        var monthly = game.GetType("GameUtils").Methods.Single(m => m.Name == "ScienceChanges");
        var code = monthly.Body.Instructions;
        // Zero means no project. State index zero belongs to the Y2K storyline.
        if (code[0].OpCode != OpCodes.Ldarg_0 || !IsProjectSlot(code[4]) ||
            code[5].OpCode != OpCodes.Ldelem_I4 || code[6].OpCode != OpCodes.Ldelem_I4 ||
            code[8].OpCode.FlowControl != FlowControl.Cond_Branch ||
            ((Instruction)code[8].Operand).Previous.OpCode != OpCodes.Stind_I4)
            throw new InvalidDataException("Unexpected monthly research progress increment");
        var first = code[0];
        var resume = (Instruction)code[8].Operand;
        var il = monthly.Body.GetILProcessor();
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldfld, states));
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4, 211));
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldelem_I4));
        il.InsertBefore(first, Instruction.Create(OpCodes.Brfalse, resume));

        // With no selection the progress display is zero regardless of the historical Y2K choice.
        var paint = faction.Methods.Single(m => m.Name == "RepaintResearch");
        var read = paint.Body.Instructions.Single(i => IsProjectSlot(i) &&
            i.Next.OpCode == OpCodes.Ldelem_I4 && i.Next.Next.OpCode == OpCodes.Ldelem_I4 &&
            i.Next.Next.Next.OpCode == OpCodes.Box).Next.Next;
        il = paint.Body.GetILProcessor();
        foreach (var instruction in new[] {
            Instruction.Create(OpCodes.Dup), Instruction.Create(OpCodes.Brtrue, read),
            Instruction.Create(OpCodes.Pop), Instruction.Create(OpCodes.Pop),
            Instruction.Create(OpCodes.Ldc_I4_0), Instruction.Create(OpCodes.Br, read.Next)
        }) il.InsertBefore(read, instruction);
        ExpandShortBranches(paint);

        // A completed second/third-column project should take the same exit as a completed first-column one.
        var click = faction.Methods.Single(m => m.Name == "IButtonPressReceiver.OnButtonDown");
        var repaint = click.Body.Instructions.Single(i => i.OpCode == OpCodes.Call && i.Operand == paint).Previous;
        var completions = click.Body.Instructions.Where(i => (i.OpCode == OpCodes.Bgt_S || i.OpCode == OpCodes.Bgt) &&
            i.Previous.OpCode == OpCodes.Ldc_I4_S && (sbyte)i.Previous.Operand == 19 &&
            i.Previous.Previous.OpCode == OpCodes.Ldelem_I4).ToArray();
        if (completions.Length != 2 || completions[1].Operand != repaint || repaint.OpCode != OpCodes.Ldarg_0 ||
            completions[0].Previous.Previous.Previous.OpCode != OpCodes.Add)
            throw new InvalidDataException("Unexpected research selection branches");
        completions[0].OpCode = OpCodes.Bgt;
        completions[0].Operand = repaint;
        ExpandShortBranches(click);

        // Only the 15 research tiles use the original project's own help text.
        // Other factions, side entries and era headings retain their structured prerequisites/effects.
        var tips = faction.Methods.Single(m => m.Name == "TryShowStructuredTooltip");
        var tab = faction.Fields.Single(f => f.Name == "selected_faction");
        first = tips.Body.Instructions[0];
        il = tips.Body.GetILProcessor();
        foreach (var instruction in new[] {
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, tab),
            Instruction.Create(OpCodes.Ldc_I4_8), Instruction.Create(OpCodes.Bne_Un, first),
            Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)10), Instruction.Create(OpCodes.Blt, first),
            Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)34), Instruction.Create(OpCodes.Bgt, first),
            Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)10), Instruction.Create(OpCodes.Rem),
            Instruction.Create(OpCodes.Ldc_I4_4), Instruction.Create(OpCodes.Bgt, first),
            Instruction.Create(OpCodes.Ldc_I4_0), Instruction.Create(OpCodes.Ret)
        }) il.InsertBefore(first, instruction);
    }
}
