using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static void PatchFactionActions(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        var appointment = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "CanEndMinorityAppointment"));
        var conditions = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "MinorityActionConditions"));
        var availability = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "ApplyResearchAvailability"));
        var research = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "ShowResearchAction"));
        var faction = game.GetType("FactionController");
        var tab = faction.Fields.Single(f => f.Name == "selected_faction");
        var global = game.GetType("GlobalScript");
        var instance = global.Fields.Single(f => f.Name == "inst");
        var state = global.Fields.Single(f => f.Name == "gameState");
        var click = faction.Methods.Single(m => m.Name == "IButtonPressReceiver.OnButtonDown");
        var code = click.Body.Instructions;
        var il = click.Body.GetILProcessor();

        var repaint = code.Single(i => i.OpCode == OpCodes.Call && ((MethodReference)i.Operand).Name == "RepaintResearch");
        var exit = repaint.Next;
        if (exit.OpCode != OpCodes.Ret) throw new InvalidDataException("Unexpected research selection exit");
        foreach (var i in new[] { Instruction.Create(OpCodes.Ldc_I4_8), Instruction.Create(OpCodes.Ldarg_1),
            Instruction.Create(OpCodes.Call, research), Instruction.Create(OpCodes.Pop) }) il.InsertBefore(exit, i);

        var minority = code.Single(i => i.OpCode == OpCodes.Call && ((MethodReference)i.Operand).Name == "ButtonNaz");
        var accept = minority.Previous.Previous.Previous.Previous;
        var reject = accept.Previous;
        if (accept.OpCode != OpCodes.Ldarg_0 || reject.OpCode != OpCodes.Ret || reject.Previous.OpCode != OpCodes.Bge)
            throw new InvalidDataException("Unexpected minority action thresholds");
        // The button and its detailed tooltip require BOTH the minimum regional
        // radicalism and maximum party relationship. Require both before charging.
        var minimum = code.Single(i => i.OpCode == OpCodes.Ble && i.Operand == accept &&
            i.Previous.OpCode == OpCodes.Ldloc_1 && i.Previous.Previous.OpCode == OpCodes.Mul);
        minimum.OpCode = OpCodes.Bgt;
        minimum.Operand = reject;
        var toGrid = code.Single(i => i.OpCode == OpCodes.Call && ((MethodReference)i.Operand).Name == "NumToKol");
        var gridEntry = toGrid.Previous;
        if (gridEntry.OpCode != OpCodes.Ldarg_1 || gridEntry.Previous.OpCode != OpCodes.Bne_Un ||
            gridEntry.Previous.Previous.OpCode != OpCodes.Ldc_I4_7)
            throw new InvalidDataException("Unexpected minority tab dispatch");
        foreach (var i in new[]{Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldsfld,instance),
            Instruction.Create(OpCodes.Ldfld,state),Instruction.Create(OpCodes.Call,appointment),
            Instruction.Create(OpCodes.Brtrue,accept)}) il.InsertBefore(gridEntry,i);
        ExpandShortBranches(click);

        // This tab's per-action builder already supplies adjusted relation and
        // radicalism conditions, plus any extra legal or cabinet prerequisites.
        var req = faction.Methods.Single(m => m.Name == "BuildRequirements");
        il = req.Body.GetILProcessor();
        var per = req.Body.Instructions.Single(i => i.OpCode == OpCodes.Call &&
            ((MethodReference)i.Operand).Name == "BuildPerKolRequirements");
        var save = per.Next;
        if (save.OpCode != OpCodes.Stloc_S || !(save.Operand is VariableDefinition))
            throw new InvalidDataException("Unexpected faction requirement list");
        var combine = save.Next;
        foreach (var i in new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,tab),
            Instruction.Create(OpCodes.Ldc_I4_7),Instruction.Create(OpCodes.Bne_Un,combine),
            Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldloc_0),
            Instruction.Create(OpCodes.Ldloc,(VariableDefinition)save.Operand),Instruction.Create(OpCodes.Call,conditions),
            Instruction.Create(OpCodes.Ret)}) il.InsertBefore(combine,i);
        ExpandShortBranches(req);

        var hover = faction.Methods.Single(m => m.Name == "TryShowStructuredTooltip");
        var first = hover.Body.Instructions[0];
        il = hover.Body.GetILProcessor();
        foreach (var i in new[] { Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld,tab),
            Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Call,research), Instruction.Create(OpCodes.Brfalse,first),
            Instruction.Create(OpCodes.Ldc_I4_1), Instruction.Create(OpCodes.Ret) }) il.InsertBefore(first,i);

        // The original popup describes the state before the click until the
        // pointer leaves. Refresh the current tooltip after either policy toggles.
        var popup = game.GetType("PopUpWindowManager");
        var afterAction = code.Single(i => i.OpCode == OpCodes.Call && ((MethodReference)i.Operand).Name == "RepaintNaz");
        if (afterAction.Next.OpCode != OpCodes.Ret)
            throw new InvalidDataException("Unexpected minority toggle repaint");
        exit = afterAction.Next;
        var checkHover = Instruction.Create(OpCodes.Ldsfld, popup.Fields.Single(f => f.Name == "inst"));
        il = click.Body.GetILProcessor();
        foreach (var i in new[] { Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Ldc_I4_S,(sbyte)51),
            Instruction.Create(OpCodes.Beq,checkHover), Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Ldc_I4_S,(sbyte)53),
            Instruction.Create(OpCodes.Bne_Un,exit), checkHover, Instruction.Create(OpCodes.Ldfld,popup.Fields.Single(f => f.Name == "is_visible")),
            Instruction.Create(OpCodes.Brfalse,exit), Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_1),
            Instruction.Create(OpCodes.Call,hover), Instruction.Create(OpCodes.Pop) }) il.InsertBefore(exit,i);

        var paint = faction.Methods.Single(m => m.Name == "RepaintResearch");
        var layout = paint.Body.Instructions.Single(i => i.OpCode == OpCodes.Call &&
            ((MethodReference)i.Operand).Name == "FitFactionGrid");
        var restoreColors = layout.Next;
        il = paint.Body.GetILProcessor();
        il.InsertBefore(restoreColors, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(restoreColors, Instruction.Create(OpCodes.Call, availability));
    }
}
