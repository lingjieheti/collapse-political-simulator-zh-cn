using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    // RepaintMinisters already ends with the localized card repaint.
    static void PatchCabinetHitTargets(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        var fit = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "CabinetBudgetHitTargets"));
        var panel = game.GetType("MinistrySelectController");
        var repaint = panel.Methods.Single(m => m.Name == "RepaintMinisters" && !m.HasParameters);
        var exit = repaint.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
        if (exit.Previous.OpCode != OpCodes.Call || ((MethodReference)exit.Previous.Operand).Name != "FitCabinetCards")
            throw new InvalidDataException("Unexpected cabinet card repaint");
        var stage = panel.Fields.Single(f => f.Name == "stage" && f.FieldType.FullName == "System.Int32");
        var native = new FieldDefinition("zhBudgetNativeHitAreas", FieldAttributes.Private,
            ((ByReferenceType)fit.Parameters[2].ParameterType).ElementType);
        panel.Fields.Add(native);
        CabinetAfter(repaint, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, stage),
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldflda, native), Instruction.Create(OpCodes.Call, fit));
    }
}
