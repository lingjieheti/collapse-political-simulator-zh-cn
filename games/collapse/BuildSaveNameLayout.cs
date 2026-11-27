// Builds the display-only change from our C# methods and the player's
// original assembly. The patched assembly is built inside the local installer.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class BuildSaveNameLayout
{
    static TypeReference ImportType(ModuleDefinition game, TypeReference reference)
    {
        if (reference is ArrayType)
            return new ArrayType(ImportType(game, ((ArrayType)reference).ElementType));
        return reference.Scope.Name == game.Assembly.Name.Name
            ? game.GetType(reference.FullName) : game.ImportReference(reference);
    }

    // The helper uses native TextMesh/Renderer APIs and public save metadata.
    // References to our source DLL's Assembly-CSharp must resolve to this game.
    static object ImportOperand(ModuleDefinition game, object value)
    {
        var field = value as FieldReference;
        if (field != null && field.DeclaringType.Scope.Name == game.Assembly.Name.Name)
            return game.GetType(field.DeclaringType.FullName).Fields.Single(f => f.Name == field.Name);
        if (field != null) return game.ImportReference(field);
        var call = value as MethodReference;
        if (call != null && call.DeclaringType.FullName == "SaveNameLayout")
            return game.GetType("SaveNameLayout").Methods.Single(m => m.Name == call.Name);
        if (call != null) return game.ImportReference(call);
        var type = value as TypeReference;
        return type != null ? ImportType(game, type) : value;
    }

    static MethodDefinition CopyFormatter(ModuleDefinition game, TypeDefinition type, MethodDefinition src)
    {
        var target = new MethodDefinition(src.Name, src.Attributes, ImportType(game, src.ReturnType));
        type.Methods.Add(target);
        foreach (var p in src.Parameters)
            target.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, ImportType(game, p.ParameterType)));
        target.Body.InitLocals = src.Body.InitLocals;
        foreach (var v in src.Body.Variables)
            target.Body.Variables.Add(new VariableDefinition(ImportType(game, v.VariableType)));
        var map = new Dictionary<Instruction,Instruction>();
        foreach (var instruction in src.Body.Instructions) {
            var next = Instruction.Create(OpCodes.Nop);
            next.OpCode = instruction.OpCode;
            target.Body.Instructions.Add(next);
            map[instruction] = next;
        }
        foreach (var i in src.Body.Instructions) {
            var v = i.Operand as VariableDefinition;
            var jump = i.Operand as Instruction;
            var parameter = i.Operand as ParameterDefinition;
            map[i].Operand = jump != null ? (object)map[jump] :
                v != null ? target.Body.Variables[v.Index] :
                parameter != null ? target.Parameters[parameter.Index] : ImportOperand(game, i.Operand);
        }
        return target;
    }

    static void PatchSettingsLabel(ModuleDefinition module, string type, int setters, MethodDefinition format)
    {
        var repaint = module.GetType(type).Methods.Single(m => m.Name == "Repaint");
        var writes = repaint.Body.Instructions.Where(i =>
            i.OpCode == OpCodes.Callvirt && i.Operand is MethodReference &&
            ((MethodReference)i.Operand).DeclaringType.FullName == "UnityEngine.TextMesh" &&
            ((MethodReference)i.Operand).Name == "set_text").ToArray();
        if (writes.Length != setters) throw new InvalidOperationException("Unexpected settings display: " + type);
        var il = repaint.Body.GetILProcessor();
        foreach (var write in writes) {
            var native = (MethodReference)write.Operand;
            // Existing branch targets still point to this instruction and pass through the formatter.
            write.OpCode = OpCodes.Call;
            write.Operand = format;
            il.InsertAfter(write, Instruction.Create(OpCodes.Callvirt, native));
        }
    }

    static void SetScrollLayout(TypeDefinition controller, string method, string textField,
                                string materialField, MethodDefinition layout)
    {
        var repaint = controller.Methods.Single(m => m.Name == method);
        var lerp = repaint.Body.Instructions.Single(i => i.OpCode == OpCodes.Call &&
            ((MethodReference)i.Operand).FullName ==
                "System.Single Utils::Lerp(System.Single,System.Single,System.Single)");
        var il = repaint.Body.GetILProcessor();
        var text = controller.Fields.Single(f => f.Name == textField);
        var material = controller.Fields.Single(f => f.Name == materialField);
        il.InsertBefore(lerp, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(lerp, Instruction.Create(OpCodes.Ldfld, text));
        il.InsertBefore(lerp, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(lerp, Instruction.Create(OpCodes.Ldfld, material));
        lerp.Operand = layout;
    }

    static void Main(string[] args)
    {
        using (var resolver = new DefaultAssemblyResolver()) {
        resolver.AddSearchDirectory(args[3]);
        using (var assembly = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver }))
        using (var owned = AssemblyDefinition.ReadAssembly(args[1])) {
            var game = assembly.MainModule;
            var source = owned.MainModule.GetType("SaveNameLayout");
            var helpers = new TypeDefinition("", "SaveNameLayout", source.Attributes, game.TypeSystem.Object);
            game.Types.Add(helpers);
            var row = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "SetRow"));
            var selection = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "SetSelection"));
            var input = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "ScrollNameEntry"));
            var search = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "SetSearch"));
            var controller = game.GetType("SaveController");
            var local = game.GetType("SaveLocalController");
            var repaint = controller.Methods.Single(m => m.Name == "Repaint");
            var instructions = repaint.Body.Instructions;
            var il = repaint.Body.GetILProcessor();
            var name = instructions.Single(i => i.OpCode == OpCodes.Ldfld && ((FieldReference)i.Operand).Name == "name");
            var set = name.Next;
            if (set.OpCode != OpCodes.Callvirt || ((MethodReference)set.Operand).Name != "set_text")
                throw new InvalidOperationException("Unexpected save-name binding");
            var rowLocal = (VariableDefinition)name.Previous.Previous.Previous.Operand;
            if (rowLocal.VariableType.FullName != "SaveLocalController")
                throw new InvalidOperationException("Unexpected save-list row");
            il.InsertBefore(set, Instruction.Create(OpCodes.Ldloc_S, rowLocal));
            il.InsertBefore(set, Instruction.Create(OpCodes.Ldfld, local.Fields.Single(f => f.Name == "saveDateLabel")));
            set.OpCode = OpCodes.Call;
            set.Operand = row;

            // Replace only the trailing clearing of the selected-row caption.
            var clear = instructions.Last(i => i.OpCode == OpCodes.Callvirt && ((MethodReference)i.Operand).Name == "set_text");
            if (clear.Previous.OpCode != OpCodes.Ldstr || (string)clear.Previous.Operand != "" ||
                ((FieldReference)clear.Previous.Previous.Operand).Name != "text")
                throw new InvalidOperationException("Unexpected selected-row caption");
            clear.Previous.Previous.OpCode = OpCodes.Ldarg_0;
            clear.Previous.Previous.Operand = null;
            clear.Previous.OpCode = OpCodes.Ldfld;
            clear.Previous.Operand = controller.Fields.Single(f => f.Name == "selected_save");
            clear.OpCode = OpCodes.Call;
            clear.Operand = selection;
            var entry = controller.Methods.Single(m => m.Name == "RepaintTextInSaveWindow");
            var finish = entry.Body.Instructions.Last();
            var inputIl = entry.Body.GetILProcessor();
            inputIl.InsertBefore(finish, Instruction.Create(OpCodes.Ldarg_0));
            inputIl.InsertBefore(finish, Instruction.Create(OpCodes.Ldfld, controller.Fields.Single(f => f.Name == "characters_obj")));
            inputIl.InsertBefore(finish, Instruction.Create(OpCodes.Call, input));

            var query = controller.Methods.Single(m => m.Name == "UpdateSearchInputDisplay");
            var assign = query.Body.Instructions.Single(i => i.OpCode == OpCodes.Callvirt && ((MethodReference)i.Operand).Name == "set_text");
            var searchIl = query.Body.GetILProcessor();
            searchIl.InsertBefore(assign, Instruction.Create(OpCodes.Ldarg_0));
            searchIl.InsertBefore(assign, Instruction.Create(OpCodes.Ldfld, controller.Fields.Single(f => f.Name == "filterText")));
            assign.OpCode = OpCodes.Call;
            assign.Operand = search;
            // The font's ASCII space is deliberately narrow for CJK wrapping.
            // Reserve a visible half-em between the calendar date and the clock.
            var columns = controller.Methods.Single(m => m.Name == "FillSaveColumns");
            columns.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldstr &&
                (string)i.Operand == "dd.MM.yy HH:mm").Operand = "dd.MM.yy\u2002HH:mm";

            CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "OwnTranslation"));
            CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "ChineseHud"));
            var hudChange = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "HudChangeFormat"));
            var thousands = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "HudThousands"));
            var deltaValue = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "HudDeltaValue"));
            var funding = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "FundingPreview"));
            var fitHud = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "SetHudText"));
            var showHud = game.GetType("InfoController").Methods.Single(m => m.Name == "ShowDesc");
            var formats = showHud.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr &&
                (((string)i.Operand).Contains("({1})") || ((string)i.Operand).Contains("(+{1})"))).ToArray();
            if (formats.Length != 9) throw new InvalidOperationException("Unexpected HUD change formats");
            foreach (var format in formats)
                showHud.Body.GetILProcessor().InsertAfter(format, Instruction.Create(OpCodes.Call, hudChange));
            var conversions = showHud.Body.Instructions.Where(i => i.OpCode == OpCodes.Conv_R4 &&
                i.Next.OpCode == OpCodes.Ldc_R4 && (float)i.Next.Operand == 10f &&
                i.Next.Next.OpCode == OpCodes.Div && i.Next.Next.Next.OpCode == OpCodes.Box &&
                ((TypeReference)i.Next.Next.Next.Operand).FullName == "System.Single").ToArray();
            if (conversions.Length != 9) throw new InvalidOperationException("Unexpected HUD delta conversions");
            foreach (var conversion in conversions) {
                // The raw integer is already on the stack. Format only the value
                // displayed by this tooltip; leave the monthly state untouched.
                var scale = conversion.Next;
                var divide = scale.Next;
                var box = divide.Next;
                conversion.OpCode = OpCodes.Ldarg_1;
                scale.OpCode = OpCodes.Call;
                scale.Operand = deltaValue;
                showHud.Body.GetILProcessor().Remove(divide);
                showHud.Body.GetILProcessor().Remove(box);
            }
            var normalSize = new FieldDefinition("zhHudSize", FieldAttributes.Private, game.TypeSystem.Single);
            var detailSize = new FieldDefinition("zhHudDetailSize", FieldAttributes.Private, game.TypeSystem.Single);
            showHud.DeclaringType.Fields.Add(normalSize);
            showHud.DeclaringType.Fields.Add(detailSize);
            var meshWrites = showHud.Body.Instructions.Where(i => i.OpCode == OpCodes.Callvirt &&
                ((MethodReference)i.Operand).FullName == "System.Void UnityEngine.TextMesh::set_text(System.String)").ToArray();
            if (meshWrites.Length != 12) throw new InvalidOperationException("Unexpected HUD text assignments");
            foreach (var write in meshWrites) {
                // Each assignment uses a single target TextMesh. Find the latest
                // receiver load even when its formatted text contains other fields.
                var receiver = showHud.Body.Instructions.TakeWhile(i => i != write).Last(i =>
                    i.OpCode == OpCodes.Ldfld && ((FieldReference)i.Operand).FieldType.FullName == "UnityEngine.TextMesh");
                var field = ((FieldReference)receiver.Operand).Name;
                if (field != "desc_text" && field != "descBig_text")
                    throw new InvalidOperationException("Unexpected HUD label");
                var hudIl = showHud.Body.GetILProcessor();
                hudIl.InsertBefore(write, Instruction.Create(OpCodes.Ldarg_0));
                hudIl.InsertBefore(write, Instruction.Create(OpCodes.Ldflda, field == "desc_text" ? normalSize : detailSize));
                write.OpCode = OpCodes.Call;
                write.Operand = fitHud;
            }
            var repaintHud = game.GetType("InfoScript").Methods.Single(m => m.Name == "Repaint");
            var units = repaintHud.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr &&
                ((string)i.Operand == "Т." || (string)i.Operand == "М.")).ToArray();
            if (units.Length != 5) throw new InvalidOperationException("Unexpected HUD abbreviations");
            foreach (var unit in units)
                repaintHud.Body.GetILProcessor().InsertAfter(unit, Instruction.Create(OpCodes.Call, thousands));
            var mystical = game.GetType("MysticalScript").Methods.Single(m => m.Name == "GetEventAnswers");
            foreach (int cost in new int[] { 500, 200 }) {
                var prefix = mystical.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldstr &&
                    (string)i.Operand == "{1}: -100{0}{2}{0}{3}: -" + cost);
                var format = mystical.Body.Instructions.SkipWhile(i => i != prefix)
                    .First(i => i.OpCode == OpCodes.Call && ((MethodReference)i.Operand).Name == "Format");
                if (format.Next.OpCode != OpCodes.Stelem_Ref)
                    throw new InvalidOperationException("Unexpected event 563 tooltip assignment");
                var fundingIl = mystical.Body.GetILProcessor();
                var write = format.Next;
                fundingIl.InsertBefore(write, Instruction.Create(OpCodes.Ldarg_0));
                fundingIl.InsertBefore(write, Instruction.Create(OpCodes.Ldc_I4, cost));
                fundingIl.InsertBefore(write, Instruction.Create(OpCodes.Call, funding));
            }
            var settings = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "SettingsValue"));
            PatchSettingsLabel(game, "LanguageSwitchScript", 1, settings);
            PatchSettingsLabel(game, "OldNamesSwitchScript", 2, settings);
            var classic = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "ClassicChinese"));
            var transform = game.GetType("LanguageManager").Methods.Single(m => m.Name == "TransformLine");
            var originalText = transform.Body.Instructions.Single(i => i.OpCode == OpCodes.Stfld &&
                ((FieldReference)i.Operand).Name == "s" && i.Previous.OpCode == OpCodes.Ldarg_1);
            transform.Body.GetILProcessor().InsertBefore(originalText, Instruction.Create(OpCodes.Call, classic));
            var newGame = game.GetType("GameSelectController");
            var scrollLayout = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "NewGameScrollY"));
            SetScrollLayout(newGame, "UpdateTextPosition", "scenario_desc", "fontMaterial", scrollLayout);
            SetScrollLayout(newGame, "UpdateTextPosition2", "party_desc", "fontMaterial2", scrollLayout);
            assembly.Write(args[2], new WriterParameters { Timestamp = 0 });
        }
        }
    }
}
