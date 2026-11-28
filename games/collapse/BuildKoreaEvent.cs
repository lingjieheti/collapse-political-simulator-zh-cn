using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static void PatchKoreaEvent(ModuleDefinition game)
    {
        var result = game.GetType("EventsScript").Methods.Single(m => m.Name == "GetEventResultAndProcessIt2");
        var code = result.Body.Instructions;
        var regions = game.GetType("GameState").Fields.Single(f => f.Name == "regions");
        var classes = game.GetType("Region").Fields.Single(f => f.Name == "class_support");
        var party = game.GetType("GameState").Fields.Single(f => f.Name == "party");
        // In event 510 the government simulation returns the same two outcome texts.
        // It grants support in all five regions, among the upper or lower stratum
        // respectively. The player's corresponding decisions transposed the indices.
        foreach (int textKey in new[] { 4778, 4779 })
        {
            var responses = code.Where(i => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == textKey &&
                i.Previous.Operand is FieldReference && ((FieldReference)i.Previous.Operand).Name == "event_text" &&
                i.Next.OpCode == OpCodes.Ldelem_Ref).ToArray();
            if (responses.Length != 2)
                throw new InvalidOperationException("Unexpected event 510 outcome count for " + textKey);
            var end = responses[0].Previous.Previous.Previous;
            var entry = end.Operand as Instruction;
            if ((end.OpCode != OpCodes.Blt_S && end.OpCode != OpCodes.Blt) ||
                end.Previous.OpCode != OpCodes.Ldc_I4_5 || entry == null || entry.OpCode != OpCodes.Ldarg_0)
                throw new InvalidOperationException("Unexpected event 510 regional loop");
            var loop = code.Skip(code.IndexOf(entry)).Take(code.IndexOf(end) - code.IndexOf(entry))
                .Where(i => i.OpCode != OpCodes.Nop).ToArray();
            if (loop.Length != 19 || loop[1].Operand != regions || loop[2].OpCode != OpCodes.Ldc_I4_1 ||
                loop[3].OpCode != OpCodes.Ldelem_Ref || loop[4].Operand != classes ||
                loop[5].OpCode != OpCodes.Ldloc || loop[6].OpCode != OpCodes.Ldelem_Ref ||
                loop[7].OpCode != OpCodes.Ldarg_0 || loop[8].Operand != party ||
                loop[9].OpCode != OpCodes.Ldc_I4_S || (sbyte)loop[9].Operand != 20 ||
                loop[10].OpCode != OpCodes.Callvirt ||
                ((MethodReference)loop[10].Operand).FullName != "System.Void Support::Add(System.Int32,System.Int32)" ||
                loop[11].OpCode != OpCodes.Ldloc || loop[11].Operand != loop[5].Operand ||
                loop[12].OpCode != OpCodes.Stloc_S || loop[13].OpCode != OpCodes.Ldloc_S ||
                loop[12].Operand != loop[13].Operand || loop[14].OpCode != OpCodes.Ldc_I4_1 ||
                loop[15].OpCode != OpCodes.Add || loop[16].OpCode != OpCodes.Stloc ||
                loop[16].Operand != loop[5].Operand || loop[17].OpCode != OpCodes.Ldloc ||
                loop[17].Operand != loop[5].Operand)
                throw new InvalidOperationException("Unexpected event 510 support operation");
            loop[2].OpCode = loop[5].OpCode;
            loop[2].Operand = loop[5].Operand;
            loop[5].OpCode = textKey == 4778 ? OpCodes.Ldc_I4_2 : OpCodes.Ldc_I4_0;
            loop[5].Operand = null;
        }
    }
}
