using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    // The exchange action's sale requirement and both inventory changes use a
    // shared "faction strength" label. Other uses of that label are genuine.
    static void PatchCurrencyTooltip(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        var party = game.GetType("PartyController");
        var translate = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "CurrencyInventoryLabel"));
        var requirements = party.Methods.Single(m => m.Name == "BuildPCTab0Requirements");
        var effects = party.Methods.Single(m => m.Name == "BuildPCTab0Effects");
        PatchCurrencyLabel(requirements, 5, true, translate);
        PatchCurrencyLabel(effects, 4, false, translate);
        PatchCurrencyLabel(effects, 5, false, translate);
    }

    static void PatchCurrencyLabel(MethodDefinition method, int action, bool requirement, MethodDefinition translate)
    {
        var code = method.Body.Instructions;
        var cases = (Instruction[])code.First(i => i.OpCode == OpCodes.Switch).Operand;
        int first = code.IndexOf(cases[action]);
        int count = code.Skip(first).TakeWhile(i => i.OpCode != OpCodes.Ret).Count();
        var block = code.Skip(first).Take(count).ToArray();
        int key = Array.FindIndex(block, i => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 1685);
        if (key < 3 || key + 9 >= block.Length ||
            block.Count(i => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 1685) != 1 ||
            block[key-2].OpCode != OpCodes.Ldarg_2 || block[key-1].OpCode != OpCodes.Ldfld ||
            ((FieldReference)block[key-1].Operand).FullName != "System.String[] LanguageManager::gui_text" ||
            block[key+1].OpCode != OpCodes.Ldelem_Ref)
            throw new InvalidOperationException("Unexpected currency label in " + method.Name);
        if (requirement) {
            if (block[key-3].OpCode != OpCodes.Ldstr || (string)block[key-3].Operand != "{0} ≥ 10.0" ||
                block[key+2].OpCode != OpCodes.Call || ((MethodReference)block[key+2].Operand).Name != "Format" ||
                block[key+3].OpCode != OpCodes.Stfld || ((FieldReference)block[key+3].Operand).Name != "label" ||
                block[key+6].OpCode != OpCodes.Ldfld || ((FieldReference)block[key+6].Operand).Name != "somethings" ||
                block[key+7].OpCode != OpCodes.Ldc_I4 || (int)block[key+7].Operand != 197 ||
                block[key+9].OpCode != OpCodes.Ldc_I4_S || (sbyte)block[key+9].Operand != 100)
                throw new InvalidOperationException("Unexpected currency requirement");
        } else {
            if (block[key+2].OpCode != OpCodes.Stfld || ((FieldReference)block[key+2].Operand).Name != "label" ||
                block[key+4].OpCode != OpCodes.Ldstr || (string)block[key+4].Operand != "10" ||
                block[key+5].OpCode != OpCodes.Stfld || ((FieldReference)block[key+5].Operand).Name != "delta" ||
                block[key+7].OpCode != (action == 4 ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0) ||
                block[key+8].OpCode != OpCodes.Stfld || ((FieldReference)block[key+8].Operand).Name != "isBenefit")
                throw new InvalidOperationException("Unexpected currency effect");
        }
        method.Body.GetILProcessor().InsertAfter(block[key+1], Instruction.Create(OpCodes.Call, translate));
    }
}
