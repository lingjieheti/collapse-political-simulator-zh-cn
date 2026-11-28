using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    // Keep the original branch target so the guard also runs on loop iterations.
    static void GuardVacantOffice(MethodDefinition method, Instruction first, Instruction after,
                                  IList<Instruction> loadParty)
    {
        var il = method.Body.GetILProcessor();
        if (first.OpCode.OperandType != OperandType.InlineNone)
            throw new InvalidOperationException("Unexpected office effect entry");
        var resume = Instruction.Create(first.OpCode);
        var next = first.Next;
        first.OpCode = OpCodes.Nop;
        foreach (var op in loadParty) il.InsertBefore(next, op);
        il.InsertBefore(next, Instruction.Create(OpCodes.Ldc_I4_0));
        il.InsertBefore(next, Instruction.Create(OpCodes.Blt, after));
        il.InsertBefore(next, resume);
    }

    static void PatchCampaignVacancies(ModuleDefinition game)
    {
        var state = game.GetType("GameState");
        var ministers = state.Fields.Single(f => f.Name == "ministers");
        var president = state.Fields.Single(f => f.Name == "prez_num");
        var owner = game.GetType("Minister").Fields.Single(f => f.Name == "owner");
        var scoring = game.GetType("GameUtils").Methods.Single(m => m.Name == "ScienceChanges");
        var score = scoring.Body.Instructions.Single(i => i.Operand == owner);
        var start = score.Previous.Previous.Previous.Previous.Previous;
        var finish = score;
        while (finish.OpCode != OpCodes.Stind_I4) finish = finish.Next;
        if (start.OpCode != OpCodes.Ldloc_0 || score.Previous.Previous.OpCode != OpCodes.Ldloc_2 ||
            score.Next.OpCode != OpCodes.Ldelema || finish.Next.OpCode != OpCodes.Ldloc_2)
            throw new InvalidOperationException("Unexpected research leadership scoring loop");
        GuardVacantOffice(scoring, start, finish.Next, new[] {
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, ministers),
            Instruction.Create(OpCodes.Ldloc_2), Instruction.Create(OpCodes.Ldelem_Ref),
            Instruction.Create(OpCodes.Ldfld, owner)
        });
        ExpandShortBranches(scoring);

        // In event 11's final choice the cabinet acts independently. Each
        // regional, faction, or party bonus goes only to its specified holder.
        // In a mixed system a vacant premier must not cancel presidential effects.
        var outcome = game.GetType("EventsScript").Methods.Single(m => m.Name == "GetEventResultAndProcessIt");
        var body = outcome.Body.Instructions;
        Func<int,Instruction> response = index => body.Single(i =>
            i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == index &&
            i.Previous.Operand is FieldReference && ((FieldReference)i.Previous.Operand).Name == "event_text" &&
            i.Next.OpCode == OpCodes.Ldelem_Ref && i.Next.Next.OpCode == OpCodes.Ret);
        var lower = response(206);
        var upper = response(208);
        var calls = body.Skip(body.IndexOf(lower)).Take(body.IndexOf(upper)-body.IndexOf(lower))
            .Where(i => i.Operand is FieldReference && new[]{"regions","parties","factions"}.Contains(((FieldReference)i.Operand).Name)
                        && i.Previous.OpCode == OpCodes.Ldarg_0).ToArray();
        int guarded = 0;
        foreach (var group in calls)
        {
            var next = group.Next;
            FieldReference holder = null;
            while (next.OpCode.FlowControl == FlowControl.Next || next.OpCode.FlowControl == FlowControl.Call)
            {
                if (next.Operand == president || next.Operand == owner) holder = (FieldReference)next.Operand;
                var call = next.Operand as MethodReference;
                if (call != null && (call.FullName == "System.Void Support::Add(System.Int32,System.Int32)" ||
                                     call.FullName == "System.Void IntArray::set_Item(System.Int32,System.Int32)")) break;
                if (next.OpCode == OpCodes.Stelem_I4 || next.OpCode == OpCodes.Stind_I4) break;
                next = next.Next;
            }
            if (holder == null) continue;
            var endpoint = next.Operand as MethodReference;
            if (endpoint == null || (endpoint.Name != "Add" && endpoint.Name != "set_Item"))
                throw new InvalidOperationException("Unexpected Olympic event recipient write");
            var read = new List<Instruction> { Instruction.Create(OpCodes.Ldarg_0) };
            if (holder == owner) {
                read.Add(Instruction.Create(OpCodes.Ldfld, ministers));
                read.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                read.Add(Instruction.Create(OpCodes.Ldelem_Ref));
            }
            read.Add(Instruction.Create(OpCodes.Ldfld, holder));
            GuardVacantOffice(outcome, group.Previous, next.Next, read);
            guarded++;
        }
        if (guarded != 54) throw new InvalidOperationException("Unexpected Olympic event effect count: " + guarded);
        ExpandShortBranches(outcome);
    }
}
