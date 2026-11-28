using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    // The February diary and the TRACECA accession notice share event 540.
    // The game's scheduler sets status 431 to 3 specifically for the latter.
    static void PatchMysticalIntro(ModuleDefinition game)
    {
        var events = game.GetType("EventsScript").Methods.Single(m => m.Name == "GetEventText");
        var mystical = game.GetType("MysticalScript");
        var diary = mystical.Methods.Single(m => m.Name == "GetEventText");
        var scheduler = mystical.Methods.Single(m => m.Name == "GetEvent");
        var state = game.GetType("GameState");
        var scenario = state.Fields.Single(f => f.Name == "scenario");
        var flags = state.Fields.Single(f => f.Name == "somethings");
        var date = state.Fields.Single(f => f.Name == "date");
        Func<string, MethodReference> dateGetter = name => scheduler.Body.Instructions
            .Select(i => i.Operand as MethodReference)
            .First(m => m != null && m.DeclaringType.FullName == "System.DateTime" && m.Name == name);
        var title = events.Body.Instructions.Single(i =>
            i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 8714);
        var entry = title.Previous;
        if (entry.OpCode != OpCodes.Ldarg_2 || title.Next.OpCode != OpCodes.Stind_I4 ||
            title.Next.Next.OpCode != OpCodes.Ldarg_0 || title.Next.Next.Next.Operand != flags)
            throw new InvalidOperationException("Unexpected TRACECA event text on this Steam build");

        // Retain the identity of the original switch target. Existing jumps
        // continue here; the original notice still handles actual accession.
        var il = events.Body.GetILProcessor();
        var transport = Instruction.Create(OpCodes.Ldarg_2);
        entry.OpCode = OpCodes.Ldarg_0;
        foreach (var op in new[] {
            Instruction.Create(OpCodes.Ldfld, scenario), Instruction.Create(OpCodes.Ldc_I4_5),
            Instruction.Create(OpCodes.Bne_Un, transport),
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, flags),
            Instruction.Create(OpCodes.Ldc_I4, 431), Instruction.Create(OpCodes.Ldelem_I4),
            Instruction.Create(OpCodes.Ldc_I4_3), Instruction.Create(OpCodes.Beq, transport),
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldflda, date),
            Instruction.Create(OpCodes.Call, dateGetter("get_Year")), Instruction.Create(OpCodes.Ldc_I4, 1992),
            Instruction.Create(OpCodes.Bne_Un, transport),
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldflda, date),
            Instruction.Create(OpCodes.Call, dateGetter("get_Month")), Instruction.Create(OpCodes.Ldc_I4_2),
            Instruction.Create(OpCodes.Bne_Un, transport),
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_1),
            Instruction.Create(OpCodes.Ldarg_2), Instruction.Create(OpCodes.Call, diary), Instruction.Create(OpCodes.Ret),
            transport
        }) il.InsertBefore(title, op);
        ExpandShortBranches(events);
    }
}
