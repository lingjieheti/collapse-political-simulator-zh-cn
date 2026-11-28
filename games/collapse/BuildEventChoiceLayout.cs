using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static void PatchEventChoices(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        var panel = game.GetType("EventController");
        var refresh = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "RefreshEventChoices"));
        var explain = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "ExplainEventChoice"));
        var label = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "ClickEventChoiceLabel"));
        var repaint = panel.Methods.Single(m => m.Name == "RepaintAll");
        var marked = repaint.Body.Instructions.Single(i => i.Operand is MethodReference &&
            ((MethodReference)i.Operand).Name == "RepaintButtons");
        var after = marked.Next;
        var il = repaint.Body.GetILProcessor();
        il.InsertBefore(after, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(after, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(after, Instruction.Create(OpCodes.Ldfld, panel.Fields.Single(f => f.Name == "now_page")));
        il.InsertBefore(after, Instruction.Create(OpCodes.Call, refresh));
        ExpandShortBranches(repaint);

        foreach (string name in new[] { "ButtonEnter", "ButtonDown" }) {
            var handler = panel.Methods.Single(m => m.Name == name && m.Parameters.Count == 1);
            var first = handler.Body.Instructions[0];
            il = handler.Body.GetILProcessor();
            il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_1));
            foreach (string field in new[] { "now_stage", "now_answer", "answers", "from_mod" }) {
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldfld, panel.Fields.Single(f => f.Name == field)));
            }
            il.InsertBefore(first, Instruction.Create(name == "ButtonDown" ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
            il.InsertBefore(first, Instruction.Create(OpCodes.Call, explain));
            il.InsertBefore(first, Instruction.Create(OpCodes.Brfalse, first));
            il.InsertBefore(first, Instruction.Create(OpCodes.Ret));
        }
        var down = game.GetType("EventButton").Methods.Single(m => m.Name == "OnMouseDown");
        if (!down.Body.Instructions.Any(i => i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "ButtonDown"))
            throw new InvalidDataException("Unexpected event button handler");
        il = down.Body.GetILProcessor();
        var start = down.Body.Instructions[0];
        il.InsertBefore(start, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(start, Instruction.Create(OpCodes.Call, label));
    }
}
