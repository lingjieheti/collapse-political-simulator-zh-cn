using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static void PatchVoting(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        Func<string,MethodDefinition> add = name => CopyFormatter(game, helpers, source.Methods.Single(m=>m.Name==name));
        add("ChineseVoting");
        var chartVisibility = add("VotingChartVisibility");
        var share = add("VotingShare");
        var seats = add("VotingHasSeats");
        var threshold = add("VotingThreshold");
        var blocked = add("VotingFixedBlocked");
        var clear = add("ClearBlockedVotingDraft");
        add("VotingBalances");
        var resultPanel = add("VotingResultPanel");
        var caption = add("VotingPanelText");
        var cost = add("VotingVetoCost");
        var status = add("VotingOtherPartyStatus");
        add("FitVotingText");
        add("VotingPanelColors");
        var layout = add("ApplyVotingLayout");
        var ministryLayout = add("ApplyMinistryThresholds");
        var thresholdPaint = game.GetType("MinistryController").Methods.Single(m => m.Name == "RepaintColumnThresholds");
        foreach (var ret in thresholdPaint.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray()) {
            ret.OpCode = OpCodes.Ldarg_0;
            var il = thresholdPaint.Body.GetILProcessor();
            var apply = Instruction.Create(OpCodes.Call, ministryLayout);
            il.InsertAfter(ret, apply);
            il.InsertAfter(apply, Instruction.Create(OpCodes.Ret));
        }
        ExpandShortBranches(thresholdPaint);
        var faction = game.GetType("FactionController");
        var cache = new FieldDefinition("zhActionLabelSizes", FieldAttributes.Private, new ArrayType(game.TypeSystem.Single));
        faction.Fields.Add(cache);
        var actionLabels = faction.Fields.Single(f => f.Name == "actions_buttons_text");
        var fitGrid = add("FitFactionGrid");
        foreach (string name in new[] { "Repaint", "RepaintResearch", "RepaintNaz" }) {
            var paint = faction.Methods.Single(m => m.Name == name);
            var il = paint.Body.GetILProcessor();
            var finish = Instruction.Create(OpCodes.Ldarg_0);
            foreach (var ret in paint.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray()) {
                ret.OpCode = OpCodes.Br;
                ret.Operand = finish;
            }
            il.Append(finish);
            il.Emit(OpCodes.Ldfld, actionLabels);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldflda, cache);
            il.Emit(OpCodes.Call, fitGrid);
            il.Emit(OpCodes.Ret);
            ExpandShortBranches(paint);
        }
        var type = game.GetType("LawsVotingController");
        Func<string,MethodDefinition> method = name => type.Methods.Single(m=>m.Name==name);
        Func<string,FieldDefinition> field = name => type.Fields.Single(f=>f.Name==name);
        var state = game.GetType("GameState");
        var global = game.GetType("GlobalScript");
        var inst = global.Fields.Single(f=>f.Name=="inst");
        var gs = global.Fields.Single(f=>f.Name=="gameState");
        var player = state.Fields.Single(f=>f.Name=="party");
        var president = state.Fields.Single(f=>f.Name=="prez_num");
        var click = method("IButtonPressReceiver.OnButtonDown");
        var hover = method("IButtonPressReceiver.OnButtonStay");
        var chartId = hover.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldc_I4 && (int)i.Operand==20000);
        if (chartId.Previous.OpCode!=OpCodes.Ldarg_1) throw new InvalidDataException("Unexpected voting chart");
        var p=click.Body.GetILProcessor();
        var original=click.Body.Instructions[0];
        // The chart has native hover and exit actions only. A click cannot
        // enter the fallback for negative expandable-row IDs.
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldarg_1));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldc_I4,20000));
        var checkReturn=Instruction.Create(OpCodes.Ldarg_1);
        p.InsertBefore(original,Instruction.Create(OpCodes.Bne_Un,checkReturn));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ret));
        p.InsertBefore(original,checkReturn);
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldc_I4_3));
        p.InsertBefore(original,Instruction.Create(OpCodes.Bne_Un,original));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldsfld,inst));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldfld,gs));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldsfld,field("player_fixed_laws")));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldarg_0));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldfld,field("playerVoted")));
        p.InsertBefore(original,Instruction.Create(OpCodes.Call,blocked));
        p.InsertBefore(original,Instruction.Create(OpCodes.Brfalse,original));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldsfld,inst));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldfld,gs));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldsfld,field("new_laws")));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldsflda,field("pressure")));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldsflda,field("player_fixed_laws")));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldsflda,field("has_offered")));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldsflda,field("party_propose")));
        p.InsertBefore(original,Instruction.Create(OpCodes.Call,clear));

        var buttons=method("RepaintButtons");
        var setActive=(MethodReference)buttons.Body.Instructions.First(i=>i.OpCode==OpCodes.Callvirt&&
            ((MethodReference)i.Operand).Name=="SetActive").Operand;
        // A fixed zero-seat proposal has no reset, submit or return button.
        // Offer the original map-return action without applying the proposal.
        var end=buttons.Body.Instructions.Last();
        if(end.OpCode!=OpCodes.Ret)throw new InvalidDataException("Unexpected voting actions");
        p=buttons.Body.GetILProcessor();
        end.OpCode=OpCodes.Ldsfld;end.Operand=inst;
        p.Emit(OpCodes.Ldfld,gs);
        p.Emit(OpCodes.Ldsfld,field("player_fixed_laws"));p.Emit(OpCodes.Ldarg_0);
        p.Emit(OpCodes.Ldfld,field("playerVoted"));p.Emit(OpCodes.Call,blocked);
        var done=Instruction.Create(OpCodes.Ret);p.Emit(OpCodes.Brfalse,done);
        p.Emit(OpCodes.Ldarg_0);p.Emit(OpCodes.Ldfld,field("buttons"));p.Emit(OpCodes.Ldc_I4_3);
        p.Emit(OpCodes.Ldelem_Ref);p.Emit(OpCodes.Ldc_I4_1);p.Emit(OpCodes.Callvirt,setActive);p.Append(done);
        ExpandShortBranches(buttons);

        var chart=method("RepaintDiagramm");
        p=chart.Body.GetILProcessor();
        var show=chart.Body.Instructions.First(i=>i.OpCode==OpCodes.Callvirt&&((MethodReference)i.Operand).Name=="SetActive");
        if (show.Previous.OpCode!=OpCodes.Ldc_I4_1) throw new InvalidDataException("Unexpected seat chart");
        show.Previous.OpCode=OpCodes.Ldsfld;show.Previous.Operand=inst;
        p.InsertBefore(show,Instruction.Create(OpCodes.Ldfld,gs));p.InsertBefore(show,Instruction.Create(OpCodes.Call,seats));
        show.OpCode=OpCodes.Call; show.Operand=chartVisibility;
        var div=chart.Body.Instructions.Single(i=>i.OpCode==OpCodes.Div);
        div.OpCode=OpCodes.Call;div.Operand=share;

        var passed=method("IsLawPassed");
        div=passed.Body.Instructions.Single(i=>i.OpCode==OpCodes.Div);
        div.OpCode=OpCodes.Call;div.Operand=share;
        var settle=method("ProcessResult");
        div=settle.Body.Instructions.First(i=>i.OpCode==OpCodes.Div);
        if (div.Previous.OpCode!=OpCodes.Conv_R4 || div.Next.OpCode!=OpCodes.Conv_R4 ||
            ((MethodReference)div.Previous.Previous.Operand).Name!="Sum") throw new InvalidDataException("Unexpected final vote share");
        div.OpCode=OpCodes.Call;div.Operand=share;

        // The original prediction routine both decided a veto and charged the
        // president. Retain its eligibility checks and its sole debit, applying
        // those writes only when the actual vote is settled.
        var veto=method("VetoedByAI");
        veto.Parameters.Add(new ParameterDefinition("settle",ParameterAttributes.None,game.TypeSystem.Boolean));
        var v=veto.Body.Instructions;
        var writes=v.Where(i=>i.OpCode==OpCodes.Stsfld&&((FieldReference)i.Operand).Name=="isVetoed").ToArray();
        if(writes.Length!=2 || writes[0].Previous.OpCode!=OpCodes.Ldc_I4_0 || writes[1].Previous.OpCode!=OpCodes.Ldc_I4_1)
            throw new InvalidDataException("Unexpected veto eligibility");
        writes[0].Previous.OpCode=OpCodes.Nop;writes[0].OpCode=OpCodes.Nop;writes[0].Operand=null;
        p=veto.Body.GetILProcessor();original=v[0];
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldarg_1));p.InsertBefore(original,Instruction.Create(OpCodes.Ldfld,president));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldarg_0));p.InsertBefore(original,Instruction.Create(OpCodes.Ldfld,field("vote")));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldlen));p.InsertBefore(original,Instruction.Create(OpCodes.Conv_I4));
        p.InsertBefore(original,Instruction.Create(OpCodes.Blt_Un,original));p.InsertBefore(original,Instruction.Create(OpCodes.Ldc_I4_0));p.InsertBefore(original,Instruction.Create(OpCodes.Ret));
        var debit=v.Single(i=>i.OpCode==OpCodes.Stfld&&((FieldReference)i.Operand).Name=="admin");
        if(debit.Next.OpCode!=OpCodes.Ldc_I4_1||debit.Next.Next.OpCode!=OpCodes.Ret) throw new InvalidDataException("Unexpected AI veto debit");
        p.InsertBefore(writes[1].Previous,Instruction.Create(OpCodes.Ldarg_3));p.InsertBefore(writes[1].Previous,Instruction.Create(OpCodes.Brfalse,debit.Next));
        ExpandShortBranches(veto);
        var awake=method("Awake");original=awake.Body.Instructions[0];p=awake.Body.GetILProcessor();
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldc_I4_0));p.InsertBefore(original,Instruction.Create(OpCodes.Stsfld,field("isVetoed")));

        // Keep the original computed vote share on the stack for the native
        // threshold test. A player veto has already been decided by button 6.
        var settled=new FieldDefinition("vetoSettled",FieldAttributes.Private,game.TypeSystem.Boolean);type.Fields.Add(settled);
        p=settle.Body.GetILProcessor();original=div.Next;
        var ratio=new VariableDefinition(game.TypeSystem.Single);settle.Body.Variables.Add(ratio);
        p.InsertBefore(original,Instruction.Create(OpCodes.Dup));p.InsertBefore(original,Instruction.Create(OpCodes.Stloc,ratio));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldarg_0));p.InsertBefore(original,Instruction.Create(OpCodes.Ldfld,settled));p.InsertBefore(original,Instruction.Create(OpCodes.Brtrue,original));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldarg_0));p.InsertBefore(original,Instruction.Create(OpCodes.Ldc_I4_1));p.InsertBefore(original,Instruction.Create(OpCodes.Stfld,settled));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldsfld,inst));p.InsertBefore(original,Instruction.Create(OpCodes.Ldfld,gs));p.InsertBefore(original,Instruction.Create(OpCodes.Ldfld,player));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldsfld,inst));p.InsertBefore(original,Instruction.Create(OpCodes.Ldfld,gs));p.InsertBefore(original,Instruction.Create(OpCodes.Ldfld,president));
        p.InsertBefore(original,Instruction.Create(OpCodes.Beq,original));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldarg_0));p.InsertBefore(original,Instruction.Create(OpCodes.Ldsfld,inst));p.InsertBefore(original,Instruction.Create(OpCodes.Ldfld,gs));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldloc,ratio));p.InsertBefore(original,Instruction.Create(OpCodes.Ldc_I4_1));p.InsertBefore(original,Instruction.Create(OpCodes.Call,veto));p.InsertBefore(original,Instruction.Create(OpCodes.Pop));
        end=settle.Body.Instructions.Last();
        if(end.OpCode!=OpCodes.Ret)throw new InvalidDataException("Unexpected roll call exit");
        end.OpCode=OpCodes.Ldarg_0;
        p.Emit(OpCodes.Ldloc,ratio);p.Emit(OpCodes.Call,resultPanel);
        p.Emit(OpCodes.Ldarg_0);p.Emit(OpCodes.Call,layout);p.Emit(OpCodes.Ret);
        ExpandShortBranches(settle);

        // The last mouse exit can otherwise repaint a finished vote as a
        // forecast, including charges the player never incurred.
        var hypothetical=method("RepaintHypotheticalShare");
        p=hypothetical.Body.GetILProcessor();original=hypothetical.Body.Instructions[0];
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldarg_0));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ldfld,field("playerVoted")));
        p.InsertBefore(original,Instruction.Create(OpCodes.Brfalse,original));
        p.InsertBefore(original,Instruction.Create(OpCodes.Ret));

        foreach(var paint in type.Methods.Where(m=>m.Name=="RepaintDeputiesNeeded")){
            bool forecast=paint.HasParameters;
            var code=paint.Body.Instructions;
            p=paint.Body.GetILProcessor();
            var is66=code.First(i=>i.OpCode==OpCodes.Call&&((MethodReference)i.Operand).Name=="Is66Percent");
            var box=code.Skip(code.IndexOf(is66)).First(i=>i.OpCode==OpCodes.Box);
            if(((TypeReference)box.Operand).FullName!="System.Int32")throw new InvalidDataException("Unexpected majority threshold");
            p.InsertAfter(box,Instruction.Create(OpCodes.Call,threshold));
            if(forecast) {
                var predict=code.Single(i=>i.OpCode==OpCodes.Call&&((MethodReference)i.Operand).Name=="VetoedByAI");
                p.InsertBefore(predict,Instruction.Create(OpCodes.Ldc_I4_0));
                var half=code.Single(i=>i.OpCode==OpCodes.Ldc_R4&&(float)i.Operand==.5f);half.Operand=.501f;
            }
            end=code.Last();if(end.OpCode!=OpCodes.Ret)throw new InvalidDataException("Unexpected seat panel");
            end.OpCode=OpCodes.Ldarg_0;
            p.Emit(OpCodes.Ldsfld,field("player_propose"));p.Emit(OpCodes.Ldsfld,field("player_fixed_laws"));p.Emit(OpCodes.Ldsfld,field("party_propose"));
            p.Emit(OpCodes.Ldarg_0);p.Emit(OpCodes.Ldfld,field("changes_count"));
            p.Emit(OpCodes.Ldarg_0);p.Emit(OpCodes.Ldfld,settled);
            p.Emit(OpCodes.Ldarg_0);p.Emit(OpCodes.Ldfld,field("currentScenario"));p.Emit(OpCodes.Ldc_I4_3);p.Emit(OpCodes.Ceq);
            p.Emit(OpCodes.Ldarg_0);p.Emit(OpCodes.Ldfld,field("vetoEnabled"));p.Emit(OpCodes.And);
            p.Emit(forecast?OpCodes.Ldc_I4_1:OpCodes.Ldc_I4_0);p.Emit(OpCodes.Call,caption);
            p.Emit(OpCodes.Ldarg_0);p.Emit(OpCodes.Call,layout);p.Emit(OpCodes.Ret);ExpandShortBranches(paint);
        }
        var enter=method("IButtonPressReceiver.OnButtonEnter");p=enter.Body.GetILProcessor();
        var rawCost=enter.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldfld&&((FieldReference)i.Operand).Name=="vetoCostCached");
        if(rawCost.Next.OpCode!=OpCodes.Box)throw new InvalidDataException("Unexpected veto tooltip value");
        rawCost.Next.OpCode=OpCodes.Call;rawCost.Next.Operand=cost;
        var parties=method("RepaintParties");
        var absent=parties.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldc_I4 && (int)i.Operand==315);
        if(absent.Next.OpCode!=OpCodes.Ldelem_Ref || ((FieldReference)absent.Previous.Operand).Name!="gui_text")
            throw new InvalidDataException("Unexpected party seat caption");
        parties.Body.GetILProcessor().InsertAfter(absent.Next,Instruction.Create(OpCodes.Call,status));
        foreach(var paint in type.Methods.Where(m=>m.Name=="RepaintLaws"||m.Name=="RepaintParties"||(m.Name=="RepaintSupportChanges"&&m.HasParameters))){
            p=paint.Body.GetILProcessor();end=paint.Body.Instructions.Last();
            if(end.OpCode!=OpCodes.Ret)throw new InvalidDataException("Unexpected voting layout exit");
            end.OpCode=OpCodes.Ldarg_0;p.Emit(OpCodes.Call,layout);p.Emit(OpCodes.Ret);ExpandShortBranches(paint);
        }
    }
}
