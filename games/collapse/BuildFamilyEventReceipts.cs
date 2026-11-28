using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    // Events 794 and 795 share these native choice and settlement branches.
    static void PatchFamilyEventReceipts(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        var settle = game.GetType("EventKosycinScript").Methods.Single(m => m.Name == "GetEventResultAndProcessIt");
        var code = settle.Body.Instructions;
        var il = settle.Body.GetILProcessor();
        var quote = new VariableDefinition(game.TypeSystem.Int32);
        settle.Body.Variables.Add(quote);

        // Keep the post-event value of 7. The gift must use the price already
        // displayed and checked on the choice page before this reset.
        var reset = code.Single(i => i.OpCode == OpCodes.Ldc_I4_7 && i.Previous.OpCode == OpCodes.Ldc_I4
            && (int)i.Previous.Operand == 780 && i.Next.OpCode == OpCodes.Stelem_I4);
        var amounts = reset.Previous.Previous.Operand as FieldReference;
        var read = code.Single(i => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 780
            && i.Next.OpCode == OpCodes.Ldelem_I4 && i.Next.Next.OpCode == OpCodes.Sub
            && i.Next.Next.Next.Operand is FieldReference && ((FieldReference)i.Next.Next.Next.Operand).Name == "cipr");
        if (amounts == null || amounts.Name != "somethings" || read.Previous.Operand != amounts ||
            read.Previous.Previous.OpCode != OpCodes.Ldarg_0 || reset.Offset >= read.Offset)
            throw new InvalidDataException("Unexpected family gift settlement");
        il.InsertBefore(reset, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(reset, Instruction.Create(OpCodes.Ldfld, amounts));
        il.InsertBefore(reset, Instruction.Create(OpCodes.Ldc_I4, 780));
        il.InsertBefore(reset, Instruction.Create(OpCodes.Ldelem_I4));
        il.InsertBefore(reset, Instruction.Create(OpCodes.Stloc, quote));
        read.Previous.Previous.OpCode = OpCodes.Ldloc;
        read.Previous.Previous.Operand = quote;
        foreach (var step in new[]{ read.Previous, read, read.Next }) {
            step.OpCode = OpCodes.Nop;
            step.Operand = null;
        }

        // The original bases are shared between the two spouse variants.
        // Only the wife's responses were swapped: the husband's business and
        // Cossack-unit responses already agree with the selected actions.
        foreach (int first in new[]{7646, 7648}) {
            var index = code.Single(i => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == first);
            var spouse = index.Next.Next.Next;
            var addition = spouse.Next.Next;
            if (index.Previous.Operand as FieldReference == null || ((FieldReference)index.Previous.Operand).Name != "event_text"
                || spouse.OpCode != OpCodes.Ldc_I4_S || (sbyte)spouse.Operand != 25 ||
                spouse.Next.OpCode != OpCodes.Ldelem_I4 || addition.OpCode != OpCodes.Add || addition.Next.OpCode != OpCodes.Ldelem_Ref)
                throw new InvalidDataException("Unexpected family response " + first);
            il.InsertBefore(addition, Instruction.Create(OpCodes.Dup));
            il.InsertBefore(addition, Instruction.Create(OpCodes.Ldc_I4_1));
            il.InsertBefore(addition, Instruction.Create(OpCodes.Bne_Un, addition));
            il.InsertBefore(addition, Instruction.Create(OpCodes.Ldc_I4, first == 7646 ? 2 : -2));
            il.InsertBefore(addition, Instruction.Create(OpCodes.Add));
        }
        PatchTuition(game, source, helpers);
        ExpandShortBranches(settle);
    }
    static Instruction FamilyCase(MethodDefinition method, int id)
    {
        return method.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == id
            && i.Previous.OpCode == OpCodes.Ldarg_1 && (i.Next.OpCode == OpCodes.Bne_Un || i.Next.OpCode == OpCodes.Bne_Un_S)).Next;
    }

    // The parser initializes the answer arrays before dispatching to each
    // event. Keep the common setup and replace only the matching case body.
    static void ReplaceFamilyCase(MethodDefinition method, int id, params Instruction[] body)
    {
        var check = FamilyCase(method, id);
        var nextCase = (Instruction)check.Operand;
        var code = method.Body.Instructions;
        var first = check.Next;
        var old = code.SkipWhile(i => i != first).TakeWhile(i => i != nextCase).ToArray();
        if (old.Length == 0 || old.Any(i => i == nextCase) || !code.Contains(nextCase))
            throw new InvalidDataException("Unexpected family event dispatch");
        if (code.Except(old).Any(i => i.Operand is Instruction && old.Contains((Instruction)i.Operand)
            || i.Operand is Instruction[] && ((Instruction[])i.Operand).Any(old.Contains)))
            throw new InvalidDataException("Family event body is shared with another case");
        var il = method.Body.GetILProcessor();
        foreach (var i in body) il.InsertBefore(first, i);
        foreach (var i in old) il.Remove(i);
        ExpandShortBranches(method);
    }

    static void PatchTuition(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        foreach (string field in new[]{ "tuitionLetterState", "tuitionLetterChild" }) {
            var f = source.Fields.Single(v => v.Name == field);
            helpers.Fields.Add(new FieldDefinition(f.Name, f.Attributes, ImportType(game, f.FieldType)));
        }
        System.Func<string,MethodDefinition> add = n => CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == n));
        add("DueTuitionChild");
        add("TuitionAmount");
        add("LetterChild");
        var letter = add("TuitionLetter");
        var choices = add("TuitionChoices");
        var settle = add("SettleTuition");
        var events = game.GetType("EventKosycinScript");
        var result = events.Methods.Single(m => m.Name == "GetEventResultAndProcessIt");
        // Admission choice 3 is labelled free by the native school chooser.
        // Jump directly to the existing enrollment code for this choice.
        var start = FamilyCase(result, 801);
        var enrollment = result.Body.Instructions.SkipWhile(i => i != start).TakeWhile(i => i != start.Operand)
            .Single(i => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 788 && i.Next.OpCode == OpCodes.Ldelem_I4);
        var il = result.Body.GetILProcessor();
        var first = start.Next;
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_2));
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4_3));
        il.InsertBefore(first, Instruction.Create(OpCodes.Beq, enrollment.Previous.Previous));

        ReplaceFamilyCase(events.Methods.Single(m => m.Name == "GetEventText"), 802,
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_2),
            Instruction.Create(OpCodes.Call, letter), Instruction.Create(OpCodes.Ret));
        ReplaceFamilyCase(events.Methods.Single(m => m.Name == "GetEventAnswers"), 802,
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_2), Instruction.Create(OpCodes.Ldind_Ref),
            Instruction.Create(OpCodes.Ldarg_3), Instruction.Create(OpCodes.Ldind_Ref),
            Instruction.Create(OpCodes.Call, choices), Instruction.Create(OpCodes.Ret));
        ReplaceFamilyCase(result, 802, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_2),
            Instruction.Create(OpCodes.Call, settle), Instruction.Create(OpCodes.Ret));
    }

}
