using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
{
    static void PatchMapPanelTooltip(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        foreach (var name in new[] { "panelTipSource", "panelTipScene", "panelTipFrame", "panelLineWidth", "originalPanelScale", "scaledPanelTip", "panelTipControls", "panelTipCount" }) {
            var field = source.Fields.Single(f => f.Name == name);
            helpers.Fields.Add(new FieldDefinition(name, field.Attributes, ImportType(game, field.FieldType)));
        }
        foreach (var name in new[] { "PanelTipRect", "PanelTipOverlap", "CachePanelButtons", "PanelTipCost", "DistinctPanelCoordinates", "PanelTipSlot", "MeasuredPanelTip", "LargestPanelTipScale", "RenderPanelTip" })
            CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == name));
        var anchor = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "AnchorPanelTip"));
        var leave = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "LeavePanelTip"));
        var place = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "PositionPanelTip"));
        foreach (var type in new[] { "SimpleSceneScwitch2", "GenericButtonPressScript", "TimeButton", "Test", "MapButtonScript", "SimpleTooltipHover", "FlagDisplayHover", "MembershipBadgeHover", "ShowHideScript2" }) {
            foreach (var method in game.GetType(type).Methods.Where(m => m.HasBody && (m.Name == "OnMouseEnter" ||
                         m.Name == "OnMouseDown" || m.Name == "OnMouseExit"))) {
                var first = method.Body.Instructions[0];
                var il = method.Body.GetILProcessor();
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
                il.InsertBefore(first, Instruction.Create(OpCodes.Call, method.Name == "OnMouseExit" ? leave : anchor));
            }
        }
        // Newspaper explanations call DrawTooltipContents directly. The ordinary
        // Show wrapper owns this separate post-draw placement for panel controls.
        var restore = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "RestorePanelTipScale"));
        var popup = game.GetType("PopUpWindowManager");
        foreach (var method in popup.Methods.Where(m => m.Name == "Hide" || m.Name == "DrawTooltipContents"))
            method.Body.GetILProcessor().InsertBefore(method.Body.Instructions[0], Instruction.Create(OpCodes.Call, restore));
        var show = game.GetType("PopUpWindowManager").Methods.Single(m => m.Name == "Show" && m.Parameters.Count == 1
            && m.Parameters[0].ParameterType.MetadataType == MetadataType.String);
        var draw = show.Body.Instructions.Single(i => i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "DrawTooltipContents");
        var nativeDraw = popup.Methods.Single(m => m.Name == "DrawTooltipContents");
        var redraw = helpers.Methods.Single(m => m.Name == "RenderPanelTip");
        redraw.Body.Instructions.Single(i => i.Operand == show).Operand = nativeDraw;
        var wrap = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "WrapPanelTip"));
        var requestGlyphs = nativeDraw.Body.Instructions.Single(i => i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "RequestCharactersInTexture");
        nativeDraw.Body.GetILProcessor().InsertBefore(requestGlyphs, Instruction.Create(OpCodes.Ldarg_0));
        nativeDraw.Body.GetILProcessor().InsertBefore(requestGlyphs, Instruction.Create(OpCodes.Call, wrap));
        ExpandShortBranches(nativeDraw);
        var caller = show.Body.GetILProcessor();
        var load = Instruction.Create(OpCodes.Ldarg_0);
        caller.InsertAfter(draw, load);
        var text = Instruction.Create(OpCodes.Ldarg_1);
        caller.InsertAfter(load, text);
        caller.InsertAfter(text, Instruction.Create(OpCodes.Call, place));
    }
}
