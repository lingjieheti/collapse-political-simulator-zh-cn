using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    // PatchDiplomacy supplies the common Repaint epilogue for each controller.
    // Run after PatchVoting has supplied the Chinese resource check as well.
    static void PatchAdjacentHeader(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        var fit = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "FitAdjacentHeader"));
        foreach (string name in new[] { "MinistryController", "PartyController" }) {
            var panel = game.GetType(name);
            var labels = panel.Fields.Single(f => f.Name == "info_text" && f.FieldType.FullName == "UnityEngine.TextMesh[]");
            var paint = panel.Methods.Single(m => m.Name == "Repaint");
            var exit = paint.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
            if (exit.Previous.OpCode != OpCodes.Call ||
                ((MethodReference)exit.Previous.Operand).Name != "FitDiplomacyGrid")
                throw new InvalidDataException("Unexpected " + name + " repaint epilogue");
            var sizes = new FieldDefinition("zhStripNativeSizes", FieldAttributes.Private, new ArrayType(game.TypeSystem.Single));
            panel.Fields.Add(sizes);
            var il = paint.Body.GetILProcessor();
            il.InsertBefore(exit, Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(exit, Instruction.Create(OpCodes.Ldfld, labels));
            il.InsertBefore(exit, Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(exit, Instruction.Create(OpCodes.Ldflda, sizes));
            il.InsertBefore(exit, Instruction.Create(OpCodes.Call, fit));
        }
    }
}
