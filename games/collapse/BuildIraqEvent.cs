using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static void PatchIraqEvent(ModuleDefinition game)
    {
        // These are the two party statements and the two ministry statements
        // in event 529. The outcomes are settled before the text is formatted.
        var method = game.GetType("EventsScript").Methods.Single(m => m.Name == "GetEventResultAndProcessIt2");
        var code = method.Body.Instructions;
        Func<Instruction, int, bool> isKey = (i, n) => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == n &&
            i.Previous.OpCode == OpCodes.Ldfld && ((FieldReference)i.Previous.Operand).FullName == "System.String[] LanguageManager::event_text" &&
            i.Next.OpCode == OpCodes.Ldelem_Ref;
        var ministryFor = code.Single(i => isKey(i, 4984));
        var ministryAgainst = code.Single(i => isKey(i, 4986));
        var partyAgainst = code.Single(i => isKey(i, 4987));
        var partyFor = code.Skip(code.IndexOf(ministryFor)).TakeWhile(i => i != ministryAgainst).Single(i => isKey(i, 4944));
        if (code.IndexOf(partyAgainst) <= code.IndexOf(ministryAgainst))
            throw new InvalidDataException("Unexpected order of event 529 decisions");

        // The matching branch must pass the identical arguments: only a newline
        // for the ministry, or newline, party name and government statement for
        // a party comment. The original support option used an earlier event.
        Func<Instruction, string[]> arguments = key => code.Skip(code.IndexOf(key) + 1)
            .TakeWhile(i => i.OpCode != OpCodes.Ret).Select(i => i.OpCode.Name + " " + i.Operand).ToArray();
        string[] single = arguments(ministryFor), multiple = arguments(partyAgainst);
        if (!single.SequenceEqual(arguments(ministryAgainst)) || !multiple.SequenceEqual(arguments(partyFor)) ||
            single.Last() != "call System.String System.String::Format(System.String,System.Object)" ||
            multiple.Last() != "call System.String System.String::Format(System.String,System.Object,System.Object,System.Object)" ||
            !multiple.Contains("ldc.i4 299"))
            throw new InvalidDataException("Unexpected arguments for event 529 decisions");

        ministryAgainst.Operand = 4985;
        partyFor.Operand = 4986;
    }
}
