using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static void PatchMinistryWizards(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        foreach (string name in new[] { "WizardCollapseLabel", "WizardSecondHeader", "EnsureWizardBudgetSummary", "WizardStatus", "WizardSectorValue" })
            CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == name));
        foreach (string name in new[] { "PremierBudgetController", "PremierBudgetTakeController", "SectorPolicyController" }) {
            var panel = game.GetType(name);
            bool sector = name == "SectorPolicyController";
            AddWizardLabel(game, helpers, panel, "UpdateCollapseLabel", "collapseLabel", "WizardCollapseLabel", false);
            AddWizardLabel(game, helpers, panel, sector ? "ApplyLocalization" : "UpdateHeaders",
                sector ? "targetHeader" : "columnHeaders", "WizardSecondHeader", !sector);
            AddWizardLabel(game, helpers, panel, "UpdateStatus", "statusText", "WizardStatus", false);
            if (sector) {
                AddWizardLabel(game, helpers, panel, "UpdateValue", "valueText", "WizardSectorValue", false);
            } else {
                var painter = panel.Methods.Single(m => m.Name == "UpdateStatus" && !m.HasParameters);
                var first = painter.Body.Instructions[0];
                var created = new FieldDefinition("zhBudgetSummaryCreated", FieldAttributes.Private, game.TypeSystem.Boolean);
                panel.Fields.Add(created);
                var il = painter.Body.GetILProcessor();
                foreach (var instruction in new[]{
                    Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, panel.Fields.Single(f => f.Name == "columnHeaders")),
                    Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldflda, panel.Fields.Single(f => f.Name == "statusText")),
                    Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldflda, created),
                    Instruction.Create(OpCodes.Call, helpers.Methods.Single(m => m.Name == "EnsureWizardBudgetSummary"))
                }) il.InsertBefore(first, instruction);
            }
        }
    }

    static void AddWizardLabel(ModuleDefinition game, TypeDefinition helpers, TypeDefinition panel,
        string methodName, string fieldName, string helperName, bool isHeaderArray)
    {
        var painter = panel.Methods.Single(m => m.Name == methodName && !m.HasParameters);
        var field = panel.Fields.Single(f => f.Name == fieldName);
        var method = helpers.Methods.Single(m => m.Name == helperName);
        var state = new FieldDefinition("zh" + helperName + "Saved", FieldAttributes.Private,
            ((ByReferenceType)method.Parameters[1].ParameterType).ElementType);
        panel.Fields.Add(state);
        var il = painter.Body.GetILProcessor();
        if (!painter.Body.Instructions.Any(i => i.Operand == field) ||
            !painter.Body.Instructions.Any(i => i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "set_text"))
            throw new InvalidDataException("Unexpected wizard layout " + panel.Name + "." + methodName);
        var color = method.Parameters.Count == 3 ? new FieldDefinition("zh" + helperName + "Color", FieldAttributes.Private,
            ((ByReferenceType)method.Parameters[2].ParameterType).ElementType) : null;
        if (color != null) panel.Fields.Add(color);
        // The sector status formatter also has an early successful return for its initial prompt.
        foreach (var ret in painter.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray()) {
            if (ret.Previous.OpCode != OpCodes.Callvirt || !(ret.Previous.Operand is MethodReference)) continue;
            if (isHeaderArray && ret != painter.Body.Instructions.Last())
                throw new InvalidDataException("Unexpected budget header exit");
            ret.OpCode = OpCodes.Ldarg_0;
            var cursor = ret;
            Action<Instruction> insert = instruction => { il.InsertAfter(cursor, instruction); cursor = instruction; };
            insert(Instruction.Create(OpCodes.Ldfld, field));
            if (isHeaderArray) { insert(Instruction.Create(OpCodes.Ldc_I4_1)); insert(Instruction.Create(OpCodes.Ldelem_Ref)); }
            insert(Instruction.Create(OpCodes.Ldarg_0)); insert(Instruction.Create(OpCodes.Ldflda, state));
            if (color != null) {
                insert(Instruction.Create(OpCodes.Ldarg_0)); insert(Instruction.Create(OpCodes.Ldflda, color));
            }
            insert(Instruction.Create(OpCodes.Call, method)); insert(Instruction.Create(OpCodes.Ret));
        }
        if (!painter.Body.Instructions.Any(i => i.Operand == method))
            throw new InvalidDataException("Missing wizard appearance hook in " + panel.Name + "." + methodName);
        ExpandShortBranches(painter);
    }
}
