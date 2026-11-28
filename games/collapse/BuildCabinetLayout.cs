using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static VariableDefinition CabinetResult(MethodDefinition repaint, string method)
    {
        var store = repaint.Body.Instructions.Single(i => i.OpCode == OpCodes.Call &&
            ((MethodReference)i.Operand).FullName.StartsWith("System.Int32 CoalitionFormationLogic::" + method + "(")).Next;
        if (store.OpCode == OpCodes.Stloc || store.OpCode == OpCodes.Stloc_S) return (VariableDefinition)store.Operand;
        if (store.OpCode == OpCodes.Stloc_0) return repaint.Body.Variables[0];
        if (store.OpCode == OpCodes.Stloc_1) return repaint.Body.Variables[1];
        if (store.OpCode == OpCodes.Stloc_2) return repaint.Body.Variables[2];
        if (store.OpCode == OpCodes.Stloc_3) return repaint.Body.Variables[3];
        throw new InvalidDataException("Expected the native result of " + method);
    }

    // Reuse the existing return instruction so all jumps to the common exit
    // also update the captions. No game-state field or click handler is written.
    static void CabinetAfter(MethodDefinition method, params Instruction[] update)
    {
        var exit = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
        var il = method.Body.GetILProcessor();
        exit.OpCode = OpCodes.Ldarg_0;
        foreach (var i in update) { il.InsertAfter(exit,i); exit = i; }
        il.InsertAfter(exit,Instruction.Create(OpCodes.Ret));
    }

    static void PatchCabinetLayout(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        Func<string,MethodDefinition> add = name => CopyFormatter(game,helpers,source.Methods.Single(m=>m.Name==name));
        var cards = add("FitCabinetCards");
        add("SetCabinetButton");
        add("CabinetConfirm");
        add("CabinetNote");
        var start = add("StartCabinetLabels");
        var terms = add("ShowCoalitionTerms");
        var budget = add("ShowCabinetAllocation");
        var crisis = add("ShowCabinetCrisis");
        var panel = game.GetType("MinistrySelectController");
        var stage = panel.Fields.Single(f=>f.Name=="stage");
        var mode = panel.Fields.Single(f=>f.Name=="mode");
        var state = panel.Fields.Single(f=>f.Name=="state");
        var points = state.FieldType.Resolve().Fields.Single(f=>f.Name=="points" && f.FieldType.FullName=="System.Int32[]");
        var negotiation = panel.Methods.Single(m=>m.Name=="RepaintCoalition");
        CabinetAfter(panel.Methods.Single(m=>m.Name=="Start"),Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldfld,stage),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,mode),
            Instruction.Create(OpCodes.Call,start));
        CabinetAfter(negotiation,Instruction.Create(OpCodes.Ldloc,CabinetResult(negotiation,"CountSupportingDeputies")),
            Instruction.Create(OpCodes.Ldloc,CabinetResult(negotiation,"Cohesion")),Instruction.Create(OpCodes.Call,terms));
        CabinetAfter(panel.Methods.Single(m=>m.Name=="RepaintMinisters"),Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldfld,stage),Instruction.Create(OpCodes.Call,cards));
        CabinetAfter(panel.Methods.Single(m=>m.Name=="RepaintParties"),Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldfld,stage),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,mode),
            Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,state),Instruction.Create(OpCodes.Ldfld,points),
            Instruction.Create(OpCodes.Call,budget));
        CabinetAfter(panel.Methods.Single(m=>m.Name=="EnterCrisis"),Instruction.Create(OpCodes.Call,crisis));
    }
}
