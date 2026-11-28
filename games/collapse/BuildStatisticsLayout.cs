using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    // Run after PatchVoting (resource/language check) and PatchDiplomacy (text fitting).
    static void PatchStatisticsLayout(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        foreach (string name in new[] { "ResetStatisticsLabel", "PlaceStatisticsLabel", "StatisticsHeadings",
            "ReformToggle", "ReformColumns", "ReformSummary" })
            CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == name));
        var statistics = game.GetType("StatisticControlller");
        var reform = game.GetType("EconomicReformController");
        AddStatisticsLayout(game, helpers, statistics, "Repaint", "statistic_buttons", "StatisticsHeadings");
        AddStatisticsLayout(game, helpers, reform, "UpdateCollapseLabel", "collapseLabel", "ReformToggle");
        AddStatisticsLayout(game, helpers, reform, "UpdateHeaders", "columnHeaders", "ReformColumns");
        AddStatisticsLayout(game, helpers, reform, "UpdateStatus", "statusText", "ReformSummary");
    }

    static void AddStatisticsLayout(ModuleDefinition game, TypeDefinition helpers, TypeDefinition panel,
        string methodName, string fieldName, string helperName)
    {
        var painter = panel.Methods.Single(m => m.Name == methodName && !m.HasParameters);
        var code = painter.Body.Instructions;
        var exit = code.Last();
        int exits = methodName == "UpdateHeaders" || methodName == "UpdateStatus" ? 2 : 1;
        if (exit.OpCode != OpCodes.Ret || code.Count(i => i.OpCode == OpCodes.Ret) != exits || !code.Any(i => i.OpCode == OpCodes.Ldfld &&
            ((FieldReference)i.Operand).Name == fieldName))
            throw new InvalidDataException("Unexpected layout for " + panel.Name + "." + methodName);
        var fit = helpers.Methods.Single(m => m.Name == helperName);
        var state = new FieldDefinition("zh" + helperName + "Native", FieldAttributes.Private,
            ((ByReferenceType)fit.Parameters[1].ParameterType).ElementType);
        panel.Fields.Add(state);
        var il = painter.Body.GetILProcessor();
        // Keep all original branch targets, including the shared return.
        exit.OpCode = OpCodes.Ldarg_0;
        il.Append(Instruction.Create(OpCodes.Ldarg_0));
        il.Append(Instruction.Create(OpCodes.Ldflda, state));
        if (helperName == "ReformSummary") {
            var color = new FieldDefinition("zhReformSummaryColor", FieldAttributes.Private,
                ((ByReferenceType)fit.Parameters[2].ParameterType).ElementType);
            panel.Fields.Add(color);
            il.Append(Instruction.Create(OpCodes.Ldarg_0));
            il.Append(Instruction.Create(OpCodes.Ldflda, color));
        }
        il.Append(Instruction.Create(OpCodes.Call, fit));
        il.Append(Instruction.Create(OpCodes.Ret));
    }
}
