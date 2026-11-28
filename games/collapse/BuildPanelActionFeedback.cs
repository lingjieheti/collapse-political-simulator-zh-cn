using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static void PatchPanelActionFeedback(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        Func<string,MethodDefinition> add = name => CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == name));
        add("HasPresidentialParty");
        var misconduct = add("PresidentialMisconduct");
        var terms = add("ExplainImpeachmentTerms");
        var explain = add("ExplainImpeachment");
        var confirmed = add("ConfirmImpeachment");
        var budget = add("CabinetBudgetControl");
        var ministry = game.GetType("MinistryController");
        var selected = ministry.Fields.Single(f => f.Name == "selected_ministry");
        var constraints = ministry.Methods.Single(m => m.Name == "BuildPremierRequirements");
        foreach (var m in new[] { constraints, ministry.Methods.Single(x => x.Name == "ButtonThis") }) {
            var partyIndex = m.Body.Instructions.Single(i => i.Operand is FieldReference &&
                ((FieldReference)i.Operand).FullName == "System.Int32 GameState::prez_num");
            var end = partyIndex.Next.Next;
            var parties = partyIndex.Previous.Previous;
            if (partyIndex.Previous.OpCode != OpCodes.Ldarg_1 || parties.OpCode != OpCodes.Ldfld ||
                ((FieldReference)parties.Operand).Name != "parties" || partyIndex.Next.OpCode != OpCodes.Ldelem_Ref ||
                end.OpCode != OpCodes.Ldfld || ((FieldReference)end.Operand).Name != "anti_actions")
                throw new InvalidDataException("Unexpected presidential party lookup in " + m.Name);
            parties.OpCode = OpCodes.Call;
            parties.Operand = misconduct;
            for (var i = parties.Next; i != end.Next; i = i.Next) { i.OpCode = OpCodes.Nop; i.Operand = null; }
        }
        // The game queues a map transition after committing the charge. Replace
        // the previous hover text immediately while that transition is pending.
        var action = ministry.Methods.Single(m => m.Name == "ButtonThis");
        var guard = action.Body.Instructions.Single(i => i.Operand == misconduct);
        var transition = action.Body.Instructions.SkipWhile(i => i != guard).First(i => i.Operand is MethodReference &&
            ((MethodReference)i.Operand).Name == "LoadSceneAsync");
        if (transition.Previous.OpCode != OpCodes.Ldc_I4_5 || transition.Previous.Previous.OpCode != OpCodes.Stelem_I4 ||
            transition.Previous.Previous.Previous.OpCode != OpCodes.Ldc_I4_1)
            throw new InvalidDataException("Unexpected impeachment completion");
        action.Body.GetILProcessor().InsertBefore(transition.Previous, Instruction.Create(OpCodes.Call, confirmed));
        // Efficiency is stored in tenths and the ledger deducts 50.0 on success.
        constraints.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldstr &&
            (string)i.Operand == "{0} ≥ 500.0").Operand = "{0} ≥ 50.0";
        var buildTerms = ministry.Methods.Single(m => m.Name == "BuildRequirements");
        var original = buildTerms.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret && i.Previous.OpCode == OpCodes.Ldloc_S);
        var il = buildTerms.Body.GetILProcessor();
        il.InsertBefore(original, Instruction.Create(OpCodes.Ldloc_0));
        il.InsertBefore(original, Instruction.Create(OpCodes.Call, terms));
        var cabinet = game.GetType("MinistrySelectController");
        var stage = cabinet.Fields.Single(f => f.Name == "stage");
        var state = cabinet.Fields.Single(f => f.Name == "state");
        foreach (bool click in new[] { false, true }) {
            string name = "IButtonPressReceiver.OnButton" + (click ? "Down" : "Enter");
            var method = ministry.Methods.Single(m => m.Name == name);
            var first = method.Body.Instructions[0];
            il = method.Body.GetILProcessor();
            var flag = click ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
            foreach (var i in new[] { Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, selected),
                Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(flag), Instruction.Create(OpCodes.Call, explain),
                Instruction.Create(OpCodes.Brfalse, first), Instruction.Create(OpCodes.Ret) }) il.InsertBefore(first, i);
            method = cabinet.Methods.Single(m => m.Name == name);
            first = method.Body.Instructions[0];
            il = method.Body.GetILProcessor();
            var check = new List<Instruction> { Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, stage),
                Instruction.Create(OpCodes.Ldc_I4_1), Instruction.Create(OpCodes.Bne_Un, first), Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(flag) };
            foreach (string field in new[] { "points", "ministry_onwer", "ministry_money" }) {
                check.Add(Instruction.Create(OpCodes.Ldarg_0));
                check.Add(Instruction.Create(OpCodes.Ldfld, state));
                check.Add(Instruction.Create(OpCodes.Ldfld, state.FieldType.Resolve().Fields.Single(f => f.Name == field)));
            }
            check.Add(Instruction.Create(OpCodes.Call, budget));
            check.Add(Instruction.Create(OpCodes.Brfalse, first));
            check.Add(Instruction.Create(OpCodes.Ret));
            foreach (var i in check) il.InsertBefore(first, i);
        }
    }
}
