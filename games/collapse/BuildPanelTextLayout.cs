using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static void AddDialogPaperBounds(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        foreach (string name in new[] { "dialogPanel", "dialogPaperBounds" }) {
            var field = source.Fields.Single(f => f.Name == name);
            helpers.Fields.Add(new FieldDefinition(name, field.Attributes, ImportType(game, field.FieldType)));
        }
        CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "TryPaperBounds"));
    }

    static void PatchPanelTextLayout(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers, MethodDefinition show)
    {
        var district = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "DistrictRestrictionRow"));
        PatchDistrictRestrictionRows(game, district);
        CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "FitDialogControl"));
        var fit = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "FitDialogPage"));
        var explain = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "ExplainDialogChoice"));
        var click = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "ClickLockedDialogLabel"));
        var dialog = game.GetType("DialogEventController");
        var readOnly = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "CloseReadOnlyDialog"));
        var next = dialog.Methods.Single(m => m.Name == "NextPage");
        var choices = next.Body.Instructions.First(i => i.OpCode == OpCodes.Ldfld &&
            ((FieldReference)i.Operand).Name == "nazadbutton");
        // Both stock and mod answer providers join here before displaying the
        // choice stage. Keep branches pointing to the first load.
        var before = next.Body.GetILProcessor();
        before.InsertBefore(choices, Instruction.Create(OpCodes.Ldfld, dialog.Fields.Single(f => f.Name == "answers")));
        before.InsertBefore(choices, Instruction.Create(OpCodes.Call, readOnly));
        var resume = Instruction.Create(OpCodes.Ldarg_0);
        before.InsertBefore(choices, Instruction.Create(OpCodes.Brfalse, resume));
        before.InsertBefore(choices, Instruction.Create(OpCodes.Ret));
        before.InsertBefore(choices, resume);
        ExpandShortBranches(next);
        var saved = new FieldDefinition("zhDialogControlLayout", FieldAttributes.Private,
            ((ByReferenceType)fit.Parameters[2].ParameterType).ElementType);
        dialog.Fields.Add(saved);
        var repaint = dialog.Methods.Single(m => m.Name == "RepaintAll");
        var returns = repaint.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
        if (returns.Length < 2) throw new InvalidDataException("Unexpected dialog stage exits");
        var il = repaint.Body.GetILProcessor();
        foreach (var exit in returns) {
            // A branch targeting the native return must run the layout as well.
            exit.OpCode = OpCodes.Ldarg_0;
            var at = Instruction.Create(OpCodes.Ret);
            il.InsertAfter(exit, at);
            il.InsertBefore(at, Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(at, Instruction.Create(OpCodes.Ldfld, dialog.Fields.Single(f => f.Name == "now_page")));
            il.InsertBefore(at, Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(at, Instruction.Create(OpCodes.Ldflda, saved));
            il.InsertBefore(at, Instruction.Create(OpCodes.Call, fit));
        }
        ExpandShortBranches(repaint);

        var enter = dialog.Methods.Single(m => m.Name == "ButtonEnter");
        var tips = enter.Body.Instructions.Where(i => i.Operand is MethodReference &&
            ((MethodReference)i.Operand).FullName == "System.Void PopUpWindowManager::Show(System.String)").ToArray();
        if (tips.Length != 5) throw new InvalidDataException("Unexpected dialog tooltip routes");
        foreach (var tip in tips) { tip.OpCode = OpCodes.Call; tip.Operand = show; }
        foreach (var handler in new[] { enter, dialog.Methods.Single(m => m.Name == "ButtonDown") }) {
            var first = handler.Body.Instructions[0];
            il = handler.Body.GetILProcessor();
            il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_1));
            foreach (string name in new[] { "now_stage", "now_answer", "answers", "opis", "from_mod" }) {
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldfld, dialog.Fields.Single(f => f.Name == name)));
            }
            il.InsertBefore(first, Instruction.Create(OpCodes.Call, explain));
            il.InsertBefore(first, Instruction.Create(OpCodes.Brfalse, first));
            il.InsertBefore(first, Instruction.Create(OpCodes.Ret));
        }
        var trigger = game.GetType("DialogEventButtonTemp");
        if (trigger.Methods.Any(m => m.Name == "OnMouseDown"))
            throw new InvalidDataException("Unexpected locked dialog click handler");
        var down = new MethodDefinition("OnMouseDown", MethodAttributes.Private | MethodAttributes.HideBySig, game.TypeSystem.Void);
        trigger.Methods.Add(down);
        il = down.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, click);
        il.Emit(OpCodes.Ret);
    }

    static void PatchDistrictRestrictionRows(ModuleDefinition game, MethodDefinition format)
    {
        var m = game.GetType("EventKosycinScript").Methods.Single(x => x.Name == "GetEventAnswers");
        var rows = m.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 6705).ToArray();
        if (rows.Length != 5) throw new InvalidDataException("Unexpected regional event restriction count");
        var il = m.Body.GetILProcessor();
        foreach (var row in rows) {
            var regions = row.Next.Next;
            var index = regions.Next;
            var owner = index.Next.Next.Next.Next;
            var add = owner.Next;
            if (!(regions.Operand is FieldReference) || ((FieldReference)regions.Operand).Name != "regions" ||
                index.Next.OpCode != OpCodes.Ldelem_Ref || !(owner.Operand is MethodReference) ||
                ((MethodReference)owner.Operand).Name != "get_Item" || add.OpCode != OpCodes.Add ||
                add.Next.OpCode != OpCodes.Mul || add.Next.Next.OpCode != OpCodes.Stelem_I4)
                throw new InvalidDataException("Unexpected governor lookup in a disabled dialog choice");
            // Keep the original gate and the negative result marking this choice
            // disabled. Only the descriptive language-table index changes.
            row.OpCode = OpCodes.Nop;
            row.Operand = null;
            var push = Instruction.Create(OpCodes.Nop);
            push.OpCode = index.OpCode;
            push.Operand = index.Operand;
            il.InsertBefore(add, push);
            add.OpCode = OpCodes.Call;
            add.Operand = format;
        }
        ExpandShortBranches(m);
    }
}
