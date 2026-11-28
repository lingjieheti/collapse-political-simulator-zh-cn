// Builds the display-only change from our C# methods and the player's
// original assembly. The patched assembly is built inside the local installer.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

static partial class BuildSaveNameLayout
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
        if (call != null && call.DeclaringType.Scope.Name == game.Assembly.Name.Name)
            return game.GetType(call.DeclaringType.FullName).Methods.Single(m => m.FullName == call.FullName);
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

    static void PatchEconomicTooltips(ModuleDefinition game, MethodDefinition threshold, MethodDefinition relation)
    {
        var desc = game.GetType("LanguageManager").Methods.Single(m => m.Name == "GetModificatorDesc");
        var p = desc.Body.Instructions;
        int key = p.IndexOf(p.Single(i => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 1328));
        // system_text[1328 + num - 137], average / 10f, modifies[num].
        if (p[key-1].OpCode != OpCodes.Ldfld || ((FieldReference)p[key-1].Operand).Name != "system_text" ||
            p[key+1].OpCode != OpCodes.Ldarg_1 || p[key+2].OpCode != OpCodes.Add ||
            p[key+3].OpCode != OpCodes.Ldc_I4 || (int)p[key+3].Operand != 137 ||
            p[key+4].OpCode != OpCodes.Sub || p[key+5].OpCode != OpCodes.Ldelem_Ref ||
            p[key+7].OpCode != OpCodes.Conv_R4 || p[key+8].OpCode != OpCodes.Ldc_R4 ||
            (float)p[key+8].Operand != 10f || p[key+9].OpCode != OpCodes.Div ||
            p[key+10].OpCode != OpCodes.Box || ((TypeReference)p[key+10].Operand).FullName != "System.Single" ||
            p[key+12].OpCode != OpCodes.Ldfld || ((FieldReference)p[key+12].Operand).FullName != "System.Int32[] GameState::modifies" ||
            p[key+13].OpCode != OpCodes.Ldarg_1 || p[key+14].OpCode != OpCodes.Ldelem_I4 ||
            p[key+15].OpCode != OpCodes.Box || ((TypeReference)p[key+15].Operand).FullName != "System.Int32" ||
            p[key+16].OpCode != OpCodes.Call || ((MethodReference)p[key+16].Operand).FullName !=
            "System.String System.String::Format(System.String,System.Object,System.Object)" || p[key+17].OpCode != OpCodes.Ret)
            throw new InvalidOperationException("Unexpected election pledge display");
        p[key+15].OpCode = OpCodes.Call;
        p[key+15].Operand = threshold;

        var preview = game.GetType("RegionWindowController").Methods.Single(m => m.Name == "ClickEffectsSuffix");
        p = preview.Body.Instructions;
        key = p.IndexOf(p.Single(i => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 2750));
        // Verify the Budget +150 line and its neighboring faction relation preview.
        if (p[key-10].OpCode != OpCodes.Ldc_I4 || (int)p[key-10].Operand != 2749 ||
            p[key-9].OpCode != OpCodes.Ldelem_Ref || p[key-8].OpCode != OpCodes.Ldc_I4 ||
            (int)p[key-8].Operand != 150 || p[key-7].OpCode != OpCodes.Ldc_I4_0 ||
            p[key-6].OpCode != OpCodes.Ldc_I4_1 || p[key-5].OpCode != OpCodes.Ldnull ||
            p[key-4].OpCode != OpCodes.Call || p[key-1].OpCode != OpCodes.Ldfld ||
            ((FieldReference)p[key-1].Operand).Name != "gui_text" || p[key+1].OpCode != OpCodes.Ldelem_Ref ||
            p[key+2].OpCode != OpCodes.Ldc_I4_5 || p[key+3].OpCode != OpCodes.Ldc_I4_1 ||
            p[key+4].OpCode != OpCodes.Ldc_I4_1 || p[key+5].OpCode != OpCodes.Ldnull ||
            p[key+6].OpCode != OpCodes.Call || p[key-4].Operand != p[key+6].Operand ||
            !((MethodReference)p[key+6].Operand).Name.StartsWith("<ClickEffectsSuffix>g__Add|"))
            throw new InvalidOperationException("Unexpected business development display");
        preview.Body.GetILProcessor().InsertAfter(p[key+2], Instruction.Create(OpCodes.Call, relation));
    }

    static void PatchCoalitionTemplate(ModuleDefinition game, MethodDefinition formatter)
    {
        var method = game.GetType("LanguageManager").Methods.Single(m => m.Name == "GetModificatorDesc");
        var code = method.Body.Instructions;
        var regionalLabel = code.Single(i => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 1727);
        var firstFormat = code.Skip(code.IndexOf(regionalLabel)).First(i => i.OpCode == OpCodes.Call);
        int k = code.IndexOf(firstFormat);
        if (((MethodReference)firstFormat.Operand).FullName != "System.String System.String::Format(System.String,System.Object[])" ||
            code[k+1].OpCode != OpCodes.Ret || code[k+2].OpCode != OpCodes.Ldarg_0 ||
            code[k+3].OpCode != OpCodes.Ldfld || ((FieldReference)code[k+3].Operand).Name != "gui_text" ||
            code[k+4].OpCode != OpCodes.Ldloc_S || code[k+5].OpCode != OpCodes.Ldelem_Ref ||
            code[k+6].OpCode != OpCodes.Ldc_I4_6 || code[k+7].OpCode != OpCodes.Newarr ||
            ((TypeReference)code[k+7].Operand).FullName != "System.Object" ||
            method.Body.Variables[0].VariableType.FullName != "GameState")
            throw new InvalidOperationException("Unexpected coalition tooltip branches");
        var fillArgs = code[k+6];
        var il = method.Body.GetILProcessor();
        il.InsertBefore(fillArgs, Instruction.Create(OpCodes.Ldloc_0));
        il.InsertBefore(fillArgs, Instruction.Create(OpCodes.Call, formatter));
    }

    static void PatchDisplayLiterals(ModuleDefinition module, MethodDefinition translate, string path)
    {
        // Read the original method once. Later inserted Chinese literals must not alter its indices.
        foreach (var group in File.ReadLines(path).Select(line => line.Split('\t')).GroupBy(fields => new { Type = fields[0], Method = fields[1] })) {
            var method = module.GetType(group.Key.Type).Methods.Single(m => m.Name == group.Key.Method);
            foreach (var branch in method.Body.Instructions.Where(i => i.OpCode.OperandType == OperandType.ShortInlineBrTarget)) {
                string longName = branch.OpCode.Code.ToString().Replace("_S", "");
                branch.OpCode = (OpCode)typeof(OpCodes).GetField(longName).GetValue(null);
            }
            var literals = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr).ToArray();
            var il = method.Body.GetILProcessor();
            foreach (var fields in group) {
                var native = literals[int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture)];
                var bytes = System.Text.Encoding.UTF8.GetBytes((string)native.Operand);
                using (var sha = System.Security.Cryptography.SHA256.Create()) {
                    var hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                    if (!String.Equals(hash, fields[3], StringComparison.Ordinal)) throw new InvalidDataException("Unexpected display literal " + group.Key.Type + "." + group.Key.Method);
                }
                var replace = Instruction.Create(OpCodes.Ldstr, fields[4].Replace("\\n", "\n"));
                il.InsertAfter(native, replace);
                il.InsertAfter(replace, Instruction.Create(OpCodes.Call, translate));
            }
        }
    }

    static void ExpandShortBranches(MethodDefinition method)
    {
        foreach (var branch in method.Body.Instructions.Where(i => i.OpCode.OperandType == OperandType.ShortInlineBrTarget)) {
            string name = branch.OpCode.Code.ToString().Replace("_S", "");
            branch.OpCode = (OpCode)typeof(OpCodes).GetField(name).GetValue(null);
        }
    }

    static void PatchDiplomacy(ModuleDefinition game, TypeDefinition source, TypeDefinition helpers)
    {
        foreach (var name in new[] { "FitDiplomacyBounds", "WrapDiplomacyButton", "FitDiplomacyGrid",
                                     "FitDiplomacyPanel", "DiplomacyTitleIndex", "DiplomacyParty", "DiplomacyThresholdCountry",
                                     "DiplomacyRelationLabel", "DiplomacyPunctuation", "DiplomacyTitle" })
            CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == name));

        var country = game.GetType("CountryController");
        var repaint = country.Methods.Single(m => m.Name == "Repaint");
        var threshold = country.Methods.Single(m => m.Name == "ShowColumnThresholdTooltip");
        var title = country.Methods.Single(m => m.Name == "BuildButtonTitle");
        var parties = repaint.Body.Instructions.Where(i => i.OpCode == OpCodes.Callvirt &&
            ((MethodReference)i.Operand).FullName == "System.String LanguageManager::GetPartyName(System.Int32,System.Boolean)").ToArray();
        if (parties.Length != 4) throw new InvalidDataException("Unexpected diplomacy owner branches");
        foreach (var call in parties) {
            call.OpCode = OpCodes.Call;
            call.Operand = helpers.Methods.Single(m => m.Name == "DiplomacyParty");
        }
        var relation = threshold.Body.Instructions.Single(i => i.OpCode == OpCodes.Callvirt &&
            ((MethodReference)i.Operand).FullName == "System.String LanguageManager::GetCountryName(System.Int32)");
        relation.OpCode = OpCodes.Call;
        relation.Operand = helpers.Methods.Single(m => m.Name == "DiplomacyThresholdCountry");
        var trim = threshold.Body.Instructions.Last(i => i.OpCode == OpCodes.Callvirt && ((MethodReference)i.Operand).Name == "TrimEnd");
        threshold.Body.GetILProcessor().InsertAfter(trim, Instruction.Create(OpCodes.Call,
            helpers.Methods.Single(m => m.Name == "DiplomacyRelationLabel")));
        var index = title.Body.Instructions.Single(i => i.OpCode == OpCodes.Call &&
            ((MethodReference)i.Operand).Name == "GetTitleTemplateIdx");
        var titleIl = title.Body.GetILProcessor();
        var near = index.Next;
        titleIl.InsertBefore(near, Instruction.Create(OpCodes.Ldarg_0));
        titleIl.InsertBefore(near, Instruction.Create(OpCodes.Ldfld, country.Fields.Single(f => f.Name == "selected_country")));
        titleIl.InsertBefore(near, Instruction.Create(OpCodes.Ldarg_1));
        titleIl.InsertBefore(near, Instruction.Create(OpCodes.Call, helpers.Methods.Single(m => m.Name == "DiplomacyTitleIndex")));
        var strip = title.Body.Instructions.Single(i => i.OpCode == OpCodes.Call && ((MethodReference)i.Operand).Name == "StripTrailingStatusBlock");
        title.Body.GetILProcessor().InsertAfter(strip, Instruction.Create(OpCodes.Call, helpers.Methods.Single(m => m.Name == "DiplomacyTitle")));

        foreach (var method in country.Methods.Where(m => m.HasBody)) {
            var calls = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Callvirt &&
                ((MethodReference)i.Operand).FullName == "System.Void PopUpWindowManager::Show(System.String)").ToArray();
            if (calls.Length == 0) continue;
            var il = method.Body.GetILProcessor();
            foreach (var call in calls) {
                var show = (MethodReference)call.Operand;
                call.OpCode = OpCodes.Call;
                call.Operand = helpers.Methods.Single(m => m.Name == "DiplomacyPunctuation");
                il.InsertAfter(call, Instruction.Create(OpCodes.Callvirt, show));
            }
            ExpandShortBranches(method);
        }

        // The world tab never assigned info_text[3]. Reuse the union tab's
        // original owner-selection block on an independent repaint as well.
        var clear = repaint.Body.Instructions.Single(i => i.OpCode == OpCodes.Callvirt &&
            ((MethodReference)i.Operand).Name == "set_text" && i.Previous.OpCode == OpCodes.Ldnull &&
            i.Previous.Previous.OpCode == OpCodes.Ldelem_Ref && i.Previous.Previous.Previous.OpCode == OpCodes.Ldc_I4_2 &&
            ((FieldReference)i.Previous.Previous.Previous.Previous.Operand).Name == "info_text");
        var exit = repaint.Body.Instructions.Last();
        if (exit.OpCode != OpCodes.Ret || exit.Previous.OpCode != OpCodes.Callvirt ||
            ((MethodReference)exit.Previous.Operand).Name != "set_text") throw new InvalidDataException("Unexpected diplomacy world tab");
        var paintIl = repaint.Body.GetILProcessor();
        paintIl.InsertBefore(exit, Instruction.Create(OpCodes.Call, helpers.Methods.Single(m => m.Name == "ChineseHud")));
        paintIl.InsertBefore(exit, Instruction.Create(OpCodes.Brtrue, clear.Next));
        foreach (var type in new[] { country, game.GetType("MinistryController"), game.GetType("PartyController") }) {
            var paint = type.Methods.Single(m => m.Name == "Repaint");
            ExpandShortBranches(paint);
            var sizes = new FieldDefinition("zhActionLabelSizes", FieldAttributes.Private,
                                            new ArrayType(game.TypeSystem.Single));
            type.Fields.Add(sizes);
            var labels = type.Fields.Single(f => f.Name == "actions_buttons_text");
            var il = paint.Body.GetILProcessor();
            var finish = Instruction.Create(OpCodes.Ldarg_0);
            foreach (var ret in paint.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray()) {
                ret.OpCode = OpCodes.Br;
                ret.Operand = finish;
            }
            il.Append(finish);
            il.Emit(OpCodes.Ldfld, labels);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldflda, sizes);
            il.Emit(OpCodes.Call, helpers.Methods.Single(m => m.Name == "FitDiplomacyGrid"));
            if (type == country) {
                var applied = new FieldDefinition("zhDiplomacyApplied", FieldAttributes.Private, game.TypeSystem.Boolean);
                type.Fields.Add(applied);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, applied);
                il.Emit(OpCodes.Call, helpers.Methods.Single(m => m.Name == "FitDiplomacyPanel"));
                il.Emit(OpCodes.Stfld, applied);
                // Labels populated by other objects in Awake are ready at Start.
                if (type.Methods.Any(m => m.Name == "Start")) throw new InvalidDataException("Unexpected diplomacy Start");
                var start = new MethodDefinition("Start", MethodAttributes.Private | MethodAttributes.HideBySig, game.TypeSystem.Void);
                type.Methods.Add(start);
                var startIl = start.Body.GetILProcessor();
                var done = Instruction.Create(OpCodes.Ret);
                startIl.Emit(OpCodes.Call, helpers.Methods.Single(m => m.Name == "ChineseHud"));
                startIl.Emit(OpCodes.Brfalse, done);
                startIl.Emit(OpCodes.Ldarg_0);
                startIl.Emit(OpCodes.Call, paint);
                startIl.Append(done);
            }
            il.Emit(OpCodes.Ret);
        }
    }


    static void PatchHudCounters(MethodDefinition repaint, MethodDefinition fit)
    {
        var size = new FieldDefinition("zhCounterOriginalSize", FieldAttributes.Private, repaint.Module.TypeSystem.Single);
        repaint.DeclaringType.Fields.Add(size);
        var body = repaint.Body;
        foreach (var branch in body.Instructions.Where(i => i.OpCode.OperandType == OperandType.ShortInlineBrTarget)) {
            string name = branch.OpCode.Code.ToString().Replace("_S", "");
            branch.OpCode = (OpCode)typeof(OpCodes).GetField(name).GetValue(null);
        }
        var exits = body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
        if (exits.Length == 0) throw new InvalidDataException("No HUD refresh exit");
        var il = body.GetILProcessor();
        var finish = Instruction.Create(OpCodes.Ldarg_0);
        foreach (var exit in exits) {
            exit.OpCode = OpCodes.Br;
            exit.Operand = finish;
        }
        il.Append(finish);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldflda, size);
        il.Emit(OpCodes.Call, fit);
        il.Emit(OpCodes.Ret);
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
            var display = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "DisplayLiteral"));
            PatchDisplayLiterals(game, display, args[4]);
            var upload = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "ShowInitialUploadProgress"));
            var creator = game.GetType("ModCreator");
            var awake = creator.Methods.Single(m => m.Name == "Awake");
            var first = awake.Body.Instructions.First();
            var uploadIl = awake.Body.GetILProcessor();
            uploadIl.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
            uploadIl.InsertBefore(first, Instruction.Create(OpCodes.Ldfld, creator.Fields.Single(f => f.Name == "loadingText")));
            uploadIl.InsertBefore(first, Instruction.Create(OpCodes.Call, upload));
            var threshold = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "PledgeThreshold"));
            var relation = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "BusinessProjectRelation"));
            PatchEconomicTooltips(game, threshold, relation);
            var coalition = CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "CoalitionTemplate"));
            PatchCoalitionTemplate(game, coalition);
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
            PatchHudCounters(repaintHud, CopyFormatter(game, helpers, source.Methods.Single(m => m.Name == "FitHudCounter")));
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
            PatchDiplomacy(game, source, helpers);
            PatchVoting(game, source, helpers);
            PatchCurrencyTooltip(game, source, helpers);
            PatchMysticalIntro(game);
            PatchCampaignVacancies(game);
            PatchKoreaEvent(game);
            assembly.Write(args[2], new WriterParameters { Timestamp = 0 });
        }
        }
    }
}
