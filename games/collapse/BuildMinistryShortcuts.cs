using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static void PatchMinistryShortcuts(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        foreach (string name in new[] {"MinistryShortcutLayout","PartyThresholdLayout","MinistryBudgetStatus",
            "MinistryBudgetShortcutHelp","MinistryShareBefore","MinistryBudgetResult"})
            CopyFormatter(game,helpers,source.Methods.Single(m => m.Name == name));
        var ministry = game.GetType("MinistryController");
        var party = game.GetType("PartyController");
        foreach (var panel in new[] {ministry,party}) {
            var method = helpers.Methods.Single(m => m.Name == (panel == ministry ? "MinistryShortcutLayout" : "PartyThresholdLayout"));
            var native = new FieldDefinition("zhMiniStripLayout",FieldAttributes.Private,
                ((ByReferenceType)method.Parameters[1].ParameterType).ElementType);
            panel.Fields.Add(native);
            var paint = panel.Methods.Single(m => m.Name == "Repaint");
            var exit = paint.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
            var il = paint.Body.GetILProcessor();
            // Include native branches that jump directly to this return.
            exit.OpCode = OpCodes.Ldarg_0;
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldflda,native); il.Emit(OpCodes.Call,method); il.Emit(OpCodes.Ret);
        }

        var selected = ministry.Fields.Single(f => f.Name == "selected_ministry");
        var help = helpers.Methods.Single(m => m.Name == "MinistryBudgetShortcutHelp");
        foreach (string name in new[]{"IButtonPressReceiver.OnButtonDown","IButtonPressReceiver.OnButtonEnter"}) {
            var method = ministry.Methods.Single(m=>m.Name==name);
            var il = method.Body.GetILProcessor();
            var entry = method.Body.Instructions[0];
            foreach (var item in new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,selected),
                Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(name.EndsWith("Down") ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0),
                Instruction.Create(OpCodes.Call,help),Instruction.Create(OpCodes.Brfalse,entry),Instruction.Create(OpCodes.Ret)})
                il.InsertBefore(entry,item);
        }
        var action = ministry.Methods.Single(m=>m.Name=="ButtonThis");
        var code = action.Body.Instructions;
        var insert = action.Body.GetILProcessor();
        var prior = new VariableDefinition(game.TypeSystem.Int32);
        action.Body.Variables.Add(prior);
        var first = code[0];
        foreach (var item in new[]{Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldfld,selected),Instruction.Create(OpCodes.Ldarg_2),
            Instruction.Create(OpCodes.Call,helpers.Methods.Single(m=>m.Name=="MinistryShareBefore")),
            Instruction.Create(OpCodes.Stloc,prior)}) insert.InsertBefore(first,item);
        var finish = helpers.Methods.Single(m=>m.Name=="MinistryBudgetResult");
        foreach (int button in new[]{120,121,122}) {
            var start = code.First(i=>i.OpCode == OpCodes.Ldc_I4_S && (sbyte)i.Operand == button && i.Previous.OpCode==OpCodes.Ldarg_2);
            if (start.Next.OpCode != OpCodes.Bne_Un_S) throw new InvalidDataException("Unexpected ministry shortcut branch");
            var resume = (Instruction)start.Next.Operand;
            var hide = code.SkipWhile(i=>i!=start).TakeWhile(i=>i!=resume).Single(i=>i.OpCode==OpCodes.Callvirt &&
                ((MethodReference)i.Operand).FullName=="System.Void PopUpWindowManager::Hide()");
            foreach (var load in new[]{Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldarg_0),
                Instruction.Create(OpCodes.Ldfld,selected),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Ldloc,prior)})
                insert.InsertBefore(hide,load);
            hide.OpCode=OpCodes.Call;hide.Operand=finish;
        }
        ExpandShortBranches(action);
    }
}
