using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

// These pure formatters are copied into the game's existing helper type. They use
// the active language table and leave native colors, story decisions and vote counts intact.
static class RallyNewsText
{
    public static string RallyOfficeReport(string template, object lineBreak, string initiator, string[] articles, int owner)
    {
        if ((uint)owner < 7)
            return string.Format(template, lineBreak, initiator, articles[2479 + owner]);
        // A successful mass rally still affects departmental policy when the post is vacant.
        // Remove only the attribution, retaining the account of that policy.
        template = template.Replace(", controlled by {2},", "")
            .Replace(", подконтрольному {2},", "").Replace(", подконтрольное {2},", "");
        template = Regex.Replace(template, @"由\s*\{2\}\s*(?:控制|掌管|领导|主管|执掌)\s*的\s*", "");
        template = Regex.Replace(template, @"掌管\s*教育部\s*的\s*\{2\}", "教育部");
        return string.Format(template, lineBreak, initiator, "—");
    }

    public static string RallyPolicyParagraph(string template)
    {
        int paragraph = template.IndexOf("{0}", StringComparison.Ordinal);
        return paragraph < 0 ? template : template.Substring(paragraph + 3).TrimStart();
    }

    public static string RallyWithoutOffice(string[] articles, int partyName, int account, int noResponse)
    {
        if (account == 0) return articles[noResponse];
        // The first paragraph describes turnout; the next one attributes a policy to a party.
        string report = articles[account];
        int end = report.IndexOf("{0}", StringComparison.Ordinal);
        if (end <= 0) return articles[noResponse];
        return string.Format(report.Substring(0, end), '\n', articles[partyName]) + "\n" + articles[noResponse];
    }
}

static partial class BuildSaveNameLayout
{
    static void PatchRallyEvent(ModuleDefinition game)
    {
        var events = game.GetType("EventsScript").Methods.Single(m => m.Name == "GetEventResultAndProcessIt2");
        var code = events.Body.Instructions;
        var il = events.Body.GetILProcessor();
        var table = game.GetType("LanguageManager").Fields.Single(f => f.Name == "event_text");
        var language = game.GetType("LanguageManager").Fields.Single(f => f.Name == "inst");
        var state = game.GetType("GameState");
        var ministers = state.Fields.Single(f => f.Name == "ministers");
        var owner = game.GetType("Minister").Fields.Single(f => f.Name == "owner");
        var flags = state.Fields.Single(f => f.Name == "somethings");
        var helpers = game.GetType("SaveNameLayout");
        MethodDefinition office, vacant, policy;
        using (var self = ModuleDefinition.ReadModule(typeof(BuildSaveNameLayout).Assembly.Location)) {
            var source = self.GetType("RallyNewsText");
            office = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "RallyOfficeReport"));
            policy = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "RallyPolicyParagraph"));
            vacant = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "RallyWithoutOffice"));
        }
        Func<int, Instruction[]> rows = key => code.Where(i => i.OpCode == OpCodes.Ldc_I4 &&
            (int)i.Operand == key && i.Previous.Operand == table && i.Next.OpCode == OpCodes.Ldelem_Ref).ToArray();
        Func<Instruction, Instruction> exit = i => {
            while (i != null && i.OpCode != OpCodes.Ret) i = i.Next;
            if (i == null) throw new InvalidDataException("Missing rally result return");
            return i;
        };
        Action<Instruction> nop = i => { i.OpCode = OpCodes.Nop; i.Operand = null; };
        var strong = new[] { 5950, 5953, 6015, 6035 };
        var reports = strong.Concat(new[]{5951,5952,5954,5955,6043,6016,6034,6036,6037,6042})
            .SelectMany(rows).ToArray();
        if (reports.Length != 16) throw new InvalidDataException("Unexpected rally report count");
        int nested = 0;
        foreach (var report in reports) {
            var format = exit(report).Previous;
            var holder = format.Previous.Previous;
            if (holder.OpCode == OpCodes.Ldelem_I4) { nop(holder); holder = holder.Previous; nested++; }
            var add = holder;
            holder = holder.Previous;
            var key = holder.Previous.Previous.Previous.Previous.Previous;
            if (format.OpCode != OpCodes.Call || ((MethodReference)format.Operand).Name != "Format" ||
                format.Previous.OpCode != OpCodes.Ldelem_Ref || add.OpCode != OpCodes.Add ||
                holder.Operand != owner || (int)key.Operand != 2479)
                throw new InvalidDataException("Unexpected rally party-name lookup");
            if (key.Previous.Operand == flags) { nop(key.Previous.Previous); nop(key.Previous); }
            nop(key); nop(add); nop(format.Previous);
            format.Operand = office;
        }
        if (nested != 12) throw new InvalidDataException("Unexpected rally state-array reads");

        // With "Ignore", the cabinet may continue its policy but the player did not hold a rally.
        // Keep the shared original articles intact for the actions where the player actually did.
        foreach (var key in new[]{5955,6037}) {
            var reused = rows(key);
            if (reused.Length != 2) throw new InvalidDataException("Unexpected silent rally outcome");
            il.InsertAfter(reused[1].Next, Instruction.Create(OpCodes.Call, policy));
        }

        // Protect the low-turnout and government-response paths before their first
        // ministry action. Party rally costs and political consequences preceding them remain.
        var weak = new[]{5951,5954,6016,6036,0,0};
        var anchorRows = strong.Concat(new[]{5978,6040}).ToArray();
        for (int n = 0; n < anchorRows.Length; n++) {
            var first = exit(rows(anchorRows[n]).Single()).Next;
            var check = Instruction.Create(OpCodes.Ldarg_0);
            foreach (var i in code) {
                if (i.Operand == first) i.Operand = check;
                var targets = i.Operand as Instruction[];
                if (targets != null) for (int k = 0; k < targets.Length; k++) if (targets[k] == first) targets[k] = check;
            }
            foreach (var i in new[] {
                check, Instruction.Create(OpCodes.Ldfld, ministers), Instruction.Create(OpCodes.Ldc_I4_8),
                Instruction.Create(OpCodes.Ldelem_Ref), Instruction.Create(OpCodes.Ldfld, owner),
                Instruction.Create(OpCodes.Ldc_I4_7), Instruction.Create(OpCodes.Blt_Un, first),
                Instruction.Create(OpCodes.Ldsfld, language), Instruction.Create(OpCodes.Ldfld, table),
                Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, flags),
                Instruction.Create(OpCodes.Ldc_I4, 299), Instruction.Create(OpCodes.Ldelem_I4),
                Instruction.Create(OpCodes.Ldc_I4, weak[n]),
                Instruction.Create(OpCodes.Ldc_I4, n == 0 || n == 1 || n == 4 ? 5979 : 6041),
                Instruction.Create(OpCodes.Call, vacant), Instruction.Create(OpCodes.Ret)
            }) il.InsertBefore(first, i);
        }
        // "Ignore" with a player-controlled education ministry should use the
        // decommunization story's own no-response text, rather than a Liberal alliance report.
        var ignore = rows(5879).Single(i => i.Offset > rows(5978).Single().Offset && i.Offset < rows(6043).Single().Offset);
        ignore.Operand = 5979;
        var bans = rows(5976).Concat(rows(6017)).ToArray();
        if (bans.Length != 6) throw new InvalidDataException("Unexpected automatic rally vote count");
        foreach (var ban in bans) {
            var size = ban.Next.Next;
            if (size.OpCode != OpCodes.Ldc_I4_7 || size.Next.OpCode != OpCodes.Newarr)
                throw new InvalidDataException("Unexpected rally vote format arguments");
            size.OpCode = OpCodes.Ldc_I4_S; size.Operand = (sbyte)9;
            var stores = new List<Instruction>();
            for (var i = size.Next; i.OpCode != OpCodes.Ret; i = i.Next)
                if (i.OpCode == OpCodes.Dup) stores.Add(i.Next);
            if (stores.Count != 7 || stores[4].OpCode != OpCodes.Ldc_I4_4 ||
                stores[5].OpCode != OpCodes.Ldc_I4_5 || stores[6].OpCode != OpCodes.Ldc_I4_6)
                throw new InvalidDataException("Unexpected rally outcome text slots");
            stores[4].OpCode = OpCodes.Ldc_I4_6;
            stores[5].OpCode = OpCodes.Ldc_I4_7;
            stores[6].OpCode = OpCodes.Ldc_I4_8;
        }
        // In automatic and roll-call votes, party-name offsets are relative to the
        // first party, not relative to the player's already offset party name.
        foreach (var vote in bans.Concat(rows(5958)).Concat(rows(6038))) {
            int changed = 0;
            for (var i = vote; i.OpCode != OpCodes.Ret; i = i.Next) {
                if (i.OpCode != OpCodes.Ldc_I4 || (int)i.Operand != 299 || i.Previous.Operand != flags ||
                    i.Next.Next.OpCode == OpCodes.Ldelem_Ref) continue;
                nop(i.Previous.Previous); nop(i.Previous); i.Operand = 2479; nop(i.Next); changed++;
            }
            if (changed != 2) throw new InvalidDataException("Unexpected rally vote party-name offsets");
        }
        // The right-wing cabinet path needs a real president belonging to party 0..2.
        var start = rows(5950).Single().Offset;
        var stop = rows(6001).Single().Offset;
        var presidentialTests = code.Where(i => i.Offset > start && i.Offset < stop &&
            i.Operand is FieldReference && ((FieldReference)i.Operand).Name == "prez_num" &&
            i.Next.OpCode == OpCodes.Ldc_I4_2 && (i.Next.Next.OpCode == OpCodes.Bgt || i.Next.Next.OpCode == OpCodes.Bgt_S)).ToArray();
        if (presidentialTests.Length != 3) throw new InvalidDataException("Unexpected rally presidential checks");
        foreach (var test in presidentialTests) test.Next.Next.OpCode = OpCodes.Bgt_Un;
        // Event 666's constitution and criminal-code proposals have separate vote accounts.
        var votes = new[]{6001,6002,6003,6004}.Select(key => rows(key).Single()).ToArray();
        int[] correct = {6003,6004,6001,6002};
        for (int i = 0; i < votes.Length; i++) votes[i].Operand = correct[i];
        ExpandShortBranches(events);
    }
}
