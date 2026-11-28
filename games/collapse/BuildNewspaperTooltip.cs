using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static void PatchNewspaperTooltip(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        foreach (string name in new[] { "newspaperPanel", "scaledPaperTip", "paperTipScale" }) {
            var field = source.Fields.Single(f => f.Name == name);
            helpers.Fields.Add(new FieldDefinition(name, field.Attributes, ImportType(game, field.FieldType)));
        }
        var active = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "NewspaperIsOpen"));
        CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "RestorePaperTipScale"));
        var clear = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "ResetPaperTip"));
        var open = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "OpenNewspaperPage"));
        var close = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "CloseNewspaper"));
        var limitWidth = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "LimitNewspaperLineWidth"));
        var eventTip = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "ShowNewspaperTip"));

        var popup = game.GetType("PopUpWindowManager");
        var show = popup.Methods.Single(m => m.Name == "Show" && m.Parameters.Count == 1
            && m.Parameters[0].ParameterType.MetadataType == MetadataType.String);
        // Keep the original method and all its callers, but route a known
        // newspaper control directly to the drawing code while the modal is up.
        var draw = CopyFormatter(game, popup, show);
        draw.Name = "DrawTooltipContents";
        draw.Attributes = MethodAttributes.Assembly | MethodAttributes.HideBySig;
        var requestGlyphs = draw.Body.Instructions.Single(i => i.Operand is MethodReference
            && ((MethodReference)i.Operand).Name == "RequestCharactersInTexture");
        // The final width assignment branches to the first instruction loading
        // the font. Insert immediately before the call, leaving its operands in
        // place so all branches pass through this adjustment.
        draw.Body.GetILProcessor().InsertBefore(requestGlyphs, Instruction.Create(OpCodes.Ldarg_0));
        draw.Body.GetILProcessor().InsertBefore(requestGlyphs, Instruction.Create(OpCodes.Call, limitWidth));
        ExpandShortBranches(draw);
        var nativeCall = eventTip.Body.Instructions.Single(i => i.Operand == show);
        nativeCall.Operand = draw;
        show.Body = new MethodBody(show);
        var il = show.Body.GetILProcessor();
        var done = Instruction.Create(OpCodes.Ret);
        il.Emit(OpCodes.Call, active);
        il.Emit(OpCodes.Brtrue, done);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, draw);
        il.Append(done);

        var controller = game.GetType("EventController");
        foreach (var method in new[] { controller.Methods.Single(m => m.Name == "ButtonEnter"),
                                       helpers.Methods.Single(m => m.Name == "ExplainEventChoice") }) {
            var calls = method.Body.Instructions.Where(i => i.Operand is MethodReference
                && ((MethodReference)i.Operand).FullName == show.FullName).ToArray();
            if (calls.Length == 0) throw new InvalidDataException("Missing newspaper tooltip in " + method.Name);
            foreach (var call in calls) { call.OpCode = OpCodes.Call; call.Operand = eventTip; }
        }

        var repaint = controller.Methods.Single(m => m.Name == "RepaintAll");
        var first = repaint.Body.Instructions[0];
        il = repaint.Body.GetILProcessor();
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, Instruction.Create(OpCodes.Call, open));
        var choose = controller.Methods.Single(m => m.Name == "AnswerMade");
        choose.Body.GetILProcessor().InsertBefore(choose.Body.Instructions[0], Instruction.Create(OpCodes.Call, clear));

        if (controller.Methods.Any(m => m.Name == "OnDisable"))
            throw new InvalidDataException("Unexpected newspaper disable callback");
        var disable = new MethodDefinition("OnDisable", MethodAttributes.Private | MethodAttributes.HideBySig,
                                           game.TypeSystem.Void);
        controller.Methods.Add(disable);
        il = disable.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, close);
        il.Emit(OpCodes.Ret);
    }
}
