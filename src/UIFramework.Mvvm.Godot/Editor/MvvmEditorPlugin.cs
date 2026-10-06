using EngineMvvm = global::UIFramework.Mvvm.Godot;
using Godot;
using System.Reflection;

namespace UIFramework.Mvvm.Godot;

[Tool]
public partial class MvvmEditorPlugin : EditorPlugin
{
    private VBoxContainer _dock = null!;
    private Tree _sources = null!, _bindings = null!;
    private VBoxContainer _details = null!;
    private LineEdit _search = null!;
    private Label _status = null!;
    private MvvmBindings? _view;
    private MvvmInspectorPlugin _inspector = null!;
    private EditorDock _editorDock = null!;
    private readonly Dictionary<int, (Action Perform, Action Reverse)> _edits = new();
    private int _editId;
    private EngineMvvm.BindingPreview? _preview;
    private readonly List<Task> _tasks = new();
    public override void _EnterTree()
    {
        _dock = new VBoxContainer { Name = "View Bindings", CustomMinimumSize = new Vector2(680, 340) };
        var actions = new HBoxContainer(); _dock.AddChild(actions);
        Button(actions, "+ 数据源", () => AddSource()); Button(actions, "+ 绑定", () => AddBinding());
        Button(actions, "删除选中", Delete); Button(actions, "上移", () => Move(-1)); Button(actions, "下移", () => Move(1));
        Button(actions, "预览初始值", StartPreview); Button(actions, "恢复界面", StopPreview);
        var columns = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; _dock.AddChild(columns);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(220, 0) }; columns.AddChild(left);
        left.AddChild(new Label { Text = "Viewmodels" }); _sources = new Tree { HideRoot = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; left.AddChild(_sources); _sources.ItemSelected += SourceDetails;
        var center = new VBoxContainer { CustomMinimumSize = new Vector2(340, 0) }; columns.AddChild(center);
        center.AddChild(new Label { Text = "View Bindings" }); _search = new LineEdit { PlaceholderText = "搜索控件 / 来源路径" }; center.AddChild(_search); _search.TextChanged += _ => Refresh();
        _bindings = new MvvmBindingTree { Columns = 3, HideRoot = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; center.AddChild(_bindings);
        _bindings.SetColumnTitle(0, "控件 · 属性"); _bindings.SetColumnTitle(1, "来源路径"); _bindings.SetColumnTitle(2, "方向"); _bindings.ColumnTitlesVisible = true;
        ((MvvmBindingTree)_bindings).Dropped += path => AddBinding(path); _bindings.ItemSelected += BindingDetails;
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(260, 0) }; columns.AddChild(scroll); _details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(_details);
        _status = new Label { Text = "选择界面节点或 MvvmBindings。配置随场景保存。" }; _dock.AddChild(_status);
        _editorDock = new EditorDock { Title = "View Bindings", DefaultSlot = EditorDock.DockSlot.Bottom }; _editorDock.AddChild(_dock); AddDock(_editorDock);
        _inspector = new MvvmInspectorPlugin { Plugin = this }; AddInspectorPlugin(_inspector);
        EditorInterface.Singleton.GetSelection().SelectionChanged += Selected;
    }
    public override void _ExitTree()
    { StopPreview(); ObserveTasks(); EditorInterface.Singleton.GetSelection().SelectionChanged -= Selected; RemoveInspectorPlugin(_inspector); RemoveDock(_editorDock); _editorDock.QueueFree(); }
    public override void _Process(double delta) => ObserveTasks();
    private void ObserveTasks() { foreach (var task in _tasks.ToArray()) if (task.IsCompleted) { _tasks.Remove(task); task.GetAwaiter().GetResult(); } }
    private void StartPreview()
    {
        StopPreview(); RequireView();
        var missing = _view!.ViewModels.FirstOrDefault(s => s.Template == null);
        if (missing != null) { _status.Text = "缺少 VM 资产：" + missing.Name; return; }
        _preview = new EngineMvvm.BindingPreview(_view);
        _status.Text = "预览中：不执行命令或修改原始资产";
    }
    private void StopPreview() { if (_preview == null) return; _tasks.Add(_preview.DisposeAsync().AsTask()); _preview = null; ObserveTasks(); }
    private void Selected()
    {
        StopPreview();
        var selected = EditorInterface.Singleton.GetSelection().GetSelectedNodes().FirstOrDefault();
        _view = selected as MvvmBindings ?? selected?.GetChildren().OfType<MvvmBindings>().SingleOrDefault();
        if (_view == null && selected != null) _view = selected.GetParent()?.GetChildren().OfType<MvvmBindings>().SingleOrDefault(); Refresh();
    }
    public void BindControl(Control control, string member = "")
    {
        _view = null;
        var current = control;
        while (_view == null && current != null) { _view = current.GetChildren().OfType<MvvmBindings>().SingleOrDefault(); current = current.GetParent() as Control; }
        if (_view == null) throw new InvalidOperationException("先在界面根节点添加 MvvmBindings。");
        var view = _view;
        var binding = new ViewBindingResource { Member = member.Length == 0 ? DefaultMember(control) : member };
        Change("Add View Binding", () => view.AddBinding(control, binding), () => { var index = view.Bindings.IndexOf(binding); view.Targets.RemoveAt(index); view.Bindings.RemoveAt(index); });
        _editorDock.MakeVisible();
    }
    private static string DefaultMember(Control control) => control is BaseButton ? "ButtonPressed" : control is global::Godot.Range ? "Value" : "Text";
    private void AddSource()
    {
        RequireView(); var view = _view!; var source = new ViewModelSourceResource { Name = "Source" + (view.ViewModels.Count + 1) };
        Change("Add Viewmodel", () => view.ViewModels.Add(source), () => view.ViewModels.Remove(source));
    }
    private void AddBinding(string path = "")
    { RequireView(); var view = _view!; var b = new ViewBindingResource { Source = path }; Change("Add View Binding", () => view.AddBinding(null!, b), () => { var index = view.Bindings.IndexOf(b); view.Targets.RemoveAt(index); view.Bindings.RemoveAt(index); }); }
    private void RequireView() { if (_view == null) throw new InvalidOperationException("选择界面上的 MvvmBindings。"); }
    private void Change(string label, Action perform, Action reverse)
    {
        StopPreview();
        RequireView(); var undo = GetUndoRedo(); var id = ++_editId; _edits.Add(id, (perform, reverse)); undo.CreateAction(label, customContext: _view!.GetParent());
        undo.AddDoMethod(this, nameof(ApplyEdit), id, true); undo.AddUndoMethod(this, nameof(ApplyEdit), id, false); undo.CommitAction();
    }
    public void ApplyEdit(int id, bool forward) { var edit = _edits[id]; if (forward) edit.Perform(); else edit.Reverse(); Refresh(); }
    private void Refresh()
    {
        var selectedSource = _sources.GetSelected()?.GetMetadata(0).AsGodotObject();
        var selectedBinding = _bindings.GetSelected()?.GetMetadata(0).AsGodotObject();
        _sources.Clear(); _bindings.Clear(); ClearDetails();
        if (_view == null) return;
        var sourceRoot = _sources.CreateItem(); foreach (var source in _view.ViewModels) { var row = _sources.CreateItem(sourceRoot); row.SetText(0, source.Name + " · " + source.Creation); row.SetMetadata(0, source); if (source == selectedSource) row.Select(0); }
        var root = _bindings.CreateItem(); foreach (var b in _view.Bindings.Where(b => (TargetName(b) + " " + b.Source + " " + b.Member).Contains(_search.Text, StringComparison.OrdinalIgnoreCase)))
        { var row = _bindings.CreateItem(root); row.SetText(0, TargetName(b) + " · " + b.Member); row.SetText(1, b.Source); row.SetText(2, b.Kind == ViewBindingKind.Property ? b.Mode.ToString() : b.Kind.ToString()); row.SetMetadata(0, b); if (b == selectedBinding) row.Select(0); }
        _status.Text = _view.Bindings.Count + " 条绑定 · 双向绑定初始以 VM 为准";
        if (_bindings.GetSelected() != null) BindingDetails();
        else if (_sources.GetSelected() != null) SourceDetails();
    }
    private void Delete()
    {
        var view = _view!;
        if (_bindings.GetSelected()?.GetMetadata(0).AsGodotObject() is ViewBindingResource binding)
        { var index = view.Bindings.IndexOf(binding); var target = view.Targets[index]; Change("Delete Binding", () => { view.Targets.RemoveAt(index); view.Bindings.RemoveAt(index); }, () => { view.Targets.Insert(index, target); view.Bindings.Insert(index, binding); }); }
        else if (_sources.GetSelected()?.GetMetadata(0).AsGodotObject() is ViewModelSourceResource source)
        { var index = view.ViewModels.IndexOf(source); Change("Delete Viewmodel", () => view.ViewModels.Remove(source), () => view.ViewModels.Insert(index, source)); }
    }
    private void Move(int offset)
    {
        var view = _view!;
        if (_bindings.GetSelected()?.GetMetadata(0).AsGodotObject() is ViewBindingResource binding)
        { var index = view.Bindings.IndexOf(binding); var target = view.Targets[index]; var next = index + offset; if (next < 0 || next >= view.Bindings.Count) return; Change("Reorder Binding", () => { view.Targets.RemoveAt(index); view.Targets.Insert(next, target); view.Bindings.RemoveAt(index); view.Bindings.Insert(next, binding); }, () => { view.Targets.RemoveAt(next); view.Targets.Insert(index, target); view.Bindings.RemoveAt(next); view.Bindings.Insert(index, binding); }); }
    }
    private void ClearDetails() { foreach (var n in _details.GetChildren()) { _details.RemoveChild(n); n.QueueFree(); } }
    private void SourceDetails()
    {
        ClearDetails(); if (_sources.GetSelected()?.GetMetadata(0).AsGodotObject() is not ViewModelSourceResource s) return;
        Text("名称", s.Name, value => Edit(s.Name, value, v => s.Name = v)); Choice("创建方式", s.Creation, v => Edit(s.Creation, v, value => s.Creation = value));
        var assets = ViewModelAssetsInProject().ToArray();
        var filtered = assets;
        var search = new LineEdit { PlaceholderText = "搜索 VM 资产" }; _details.AddChild(search);
        var picker = new OptionButton();
        void Filter(string text)
        {
            filtered = assets.Where(asset => asset.ResourcePath.Contains(text, StringComparison.OrdinalIgnoreCase)).ToArray();
            picker.Clear(); picker.AddItem("选择 VM 资产"); foreach (var asset in filtered) picker.AddItem(asset.ResourcePath);
            picker.Selected = s.Template == null ? 0 : Array.FindIndex(filtered, asset => asset == s.Template) + 1;
        }
        Filter(""); search.TextChanged += Filter;
        picker.ItemSelected += index => { if (index > 0) Edit(s.Template, filtered[index - 1], value => s.Template = value); };
        _details.AddChild(new Label { Text = "VM 资产" }); _details.AddChild(picker);
        if (s.Template == null) return;
        Button(_details, "定位资产", () => EditorInterface.Singleton.EditResource(s.Template));
        var vmType = s.Template.GetType();
        var members = new MvvmMemberTree { HideRoot = true, CustomMinimumSize = new Vector2(0, 150) }; _details.AddChild(members); var root = members.CreateItem();
        foreach (var property in vmType.GetProperties().Where(p => p.IsDefined(typeof(FieldNotifyAttribute), true))) { var row = members.CreateItem(root); row.SetText(0, property.Name + " : " + property.PropertyType.Name); row.SetMetadata(0, s.Name + "." + property.Name); }
        foreach (var method in vmType.GetMethods().Where(m => m.IsDefined(typeof(CommandAttribute), true))) { var row = members.CreateItem(root); row.SetText(0, method.Name + " : Command"); row.SetMetadata(0, s.Name + "." + method.Name); }
    }
    private void BindingDetails()
    {
        ClearDetails(); if (_bindings.GetSelected()?.GetMetadata(0).AsGodotObject() is not ViewBindingResource b) return;
        Target(b);
        var selectedTarget = _view!.Targets[_view.Bindings.IndexOf(b)];
        if (GodotObject.IsInstanceValid(selectedTarget) && b.Kind == ViewBindingKind.Property)
        {
            var definitions = BindingTargetRegistry.Members(selectedTarget.GetType());
            var picker = new OptionButton(); foreach (var definition in definitions) picker.AddItem(definition.Member);
            picker.Selected = definitions.ToList().FindIndex(d => d.Member == b.Member);
            picker.ItemSelected += index => Edit(b.Member, definitions[(int)index].Member, value => b.Member = value);
            _details.AddChild(new Label { Text = "目标属性" }); _details.AddChild(picker);
        }
        if (b.Kind != ViewBindingKind.Property || b.ConverterInput == ConverterInput.ViewModel) Text("来源路径", b.Source, v => Edit(b.Source, v, value => b.Source = value));
        Choice("绑定类型", b.Kind, v => Edit(b.Kind, v, value => b.Kind = value)); Choice("方向", b.Mode, v => { var oldMode = b.Mode; var oldInput = b.ConverterInput; Change("Change Binding Direction", () => { b.Mode = v; if (v is BindingMode.TwoWay or BindingMode.OneWayToViewModel) b.ConverterInput = ConverterInput.ViewModel; }, () => { b.Mode = oldMode; b.ConverterInput = oldInput; }); });
        var sourceType = SourceType(b.Source); var target = _view!.Targets[_view.Bindings.IndexOf(b)]; Type? targetType = null;
        if (target != null && b.Kind == ViewBindingKind.Property) { targetType = BindingTargetRegistry.Find(target.GetType(), b.Member).ValueType; }
        if (b.Kind != ViewBindingKind.Property)
        {
            if (b.Kind == ViewBindingKind.Command)
            {
                var path = b.Source.Split('.'); var source = _view.ViewModels.FirstOrDefault(v => v.Name == path[0]);
                Type? vmType = source?.Template?.GetType(); foreach (var part in path.Skip(1).Take(path.Length - 2)) vmType = vmType?.GetProperty(part)?.PropertyType;
                var command = vmType?.GetMethods().SingleOrDefault(m => m.Name == path.Last() && m.IsDefined(typeof(CommandAttribute), true));
                var parameter = command?.GetParameters().SingleOrDefault(p => p.ParameterType != typeof(CancellationToken));
                if (parameter != null)
                {
                    _details.AddChild(new Label { Text = "命令参数：" + parameter.ParameterType.Name });
                    Choice("参数方式", b.ParameterMode, v => Edit(b.ParameterMode, v, value => b.ParameterMode = value));
                    if (b.ParameterMode == CommandParameterMode.Constant) Text("参数常量", b.ParameterConstant, v => Edit(b.ParameterConstant, v, value => b.ParameterConstant = value));
                    if (b.ParameterMode == CommandParameterMode.SourcePath) Text("参数来源", b.ParameterPath, v => Edit(b.ParameterPath, v, value => b.ParameterPath = value));
                    try { if (b.ParameterMode == CommandParameterMode.Constant) CommandParameters.Validate(parameter.ParameterType, CommandParameters.ParseConstant(b.ParameterConstant, parameter.ParameterType)); else if (b.ParameterMode == CommandParameterMode.None) CommandParameters.Validate(parameter.ParameterType, null); else if (!parameter.ParameterType.IsAssignableFrom(SourceType(b.ParameterPath) ?? throw new MissingMemberException(b.ParameterPath))) throw new InvalidCastException("参数来源类型不匹配"); }
                    catch (Exception error) { _details.AddChild(new Label { Text = error.Message }); }
                }
            }
            if (b.Kind == ViewBindingKind.Collection) Asset("条目场景", b.ItemScene, "PackedScene", v => Edit(b.ItemScene, (PackedScene?)v, value => b.ItemScene = value));
            return;
        }
        if (b.Mode is BindingMode.OneWay or BindingMode.OneTime) Choice("转换器输入", b.ConverterInput, v => Edit(b.ConverterInput, v, value => b.ConverterInput = value));
        Converter("正向转换", b.Converter, sourceType, targetType, b.ConverterInput == ConverterInput.Constant, v => Edit(b.Converter, v, value => b.Converter = value));
        if (b.Converter.Length != 0)
        {
            var info = Converters.Discover().Single(c => c.Name == b.Converter);
            _details.AddChild(new Label { Text = "输入：" + info.Input.Name + " → 输出：" + info.Output.Name });
            if (b.ConverterInput == ConverterInput.Constant) Text("输入常量", b.ConverterConstant, v => Edit(b.ConverterConstant, v, value => b.ConverterConstant = value));
            else _details.AddChild(new Label { Text = "输入参数绑定到来源路径，成员变化时重新转换。" });
        }
        if (b.Mode == BindingMode.TwoWay) Converter("反向转换", b.ConvertBack, targetType, sourceType, false, v => Edit(b.ConvertBack, v, value => b.ConvertBack = value));
        var nullable = new CheckButton { Text = "显式空值显示", ButtonPressed = b.HasNullValue }; nullable.Toggled += v => Edit(b.HasNullValue, v, value => b.HasNullValue = value); _details.AddChild(nullable);
        Text("空值内容", b.NullValue, v => Edit(b.NullValue, v, value => b.NullValue = value)); Asset("条目场景", b.ItemScene, "PackedScene", v => Edit(b.ItemScene, (PackedScene?)v, value => b.ItemScene = value));
    }
    private string TargetName(ViewBindingResource binding)
    { var target = _view!.Targets[_view.Bindings.IndexOf(binding)]; return GodotObject.IsInstanceValid(target) ? target.Name.ToString() : "选择控件"; }
    private void Target(ViewBindingResource binding)
    {
        var view = _view!; var index = view.Bindings.IndexOf(binding); var original = view.Targets[index];
        var controls = Controls(view.GetParent()).ToArray(); var picker = new OptionButton(); picker.AddItem("选择控件");
        foreach (var control in controls) picker.AddItem(control.Name.ToString() + " · " + control.GetType().Name);
        picker.Selected = Array.IndexOf(controls, original) + 1;
        picker.ItemSelected += selected => { if (selected > 0) Edit(original, controls[selected - 1], value => view.Targets[index] = value); };
        _details.AddChild(new Label { Text = "目标控件引用" }); _details.AddChild(picker);
    }
    private static IEnumerable<Control> Controls(global::Godot.Node node)
    { if (node is Control control) yield return control; foreach (var child in node.GetChildren()) foreach (var nested in Controls(child)) yield return nested; }
    private static IEnumerable<Resource> ViewModelAssetsInProject()
    {
        return Read(EditorInterface.Singleton.GetResourceFilesystem().GetFilesystem());
        static IEnumerable<Resource> Read(EditorFileSystemDirectory directory)
        {
            for (int i = 0; i < directory.GetFileCount(); i++)
            {
                var path = directory.GetFilePath(i);
                if (path.GetExtension() is not ("tres" or "res")) continue;
                var resource = ResourceLoader.Load<Resource>(path);
                if (resource.GetType().IsDefined(typeof(ViewModelAttribute), false)) yield return resource;
            }
            for (int i = 0; i < directory.GetSubdirCount(); i++) foreach (var asset in Read(directory.GetSubdir(i))) yield return asset;
        }
    }
    private Type? SourceType(string path)
    { var parts = path.Split('.'); var source = _view!.ViewModels.FirstOrDefault(s => s.Name == parts[0]); var type = source?.Template?.GetType(); foreach (var part in parts.Skip(1)) type = type?.GetProperty(part)?.PropertyType; return type; }
    private void Converter(string label, string current, Type? input, Type? output, bool constant, Action<string> set)
    { var choices = new[] { "" }.Concat(Converters.Discover().Where(c => (input != null || constant) && output != null && (constant ? Converters.SupportsConstant(c.Input) : c.Input.IsAssignableFrom(input)) && output.IsAssignableFrom(c.Output)).Select(c => c.Name)).ToArray(); var field = new OptionButton(); foreach (var name in choices) field.AddItem(name.Length == 0 ? "无" : name); field.Selected = Array.IndexOf(choices, current); field.ItemSelected += index => set(choices[index]); _details.AddChild(new Label { Text = label }); _details.AddChild(field); }
    private void Edit<T>(T old, T value, Action<T> set) => Change("Edit View Binding", () => set(value), () => set(old));
    private void Text(string label, string value, Action<string> set)
    {
        _details.AddChild(new Label { Text = label }); var field = new LineEdit { Text = value }; var committed = value;
        void Commit() { if (field.Text == committed) return; committed = field.Text; set(committed); }
        field.TextSubmitted += _ => Commit(); field.FocusExited += Commit; _details.AddChild(field);
    }
    private void Choice<T>(string label, T value, Action<T> set) where T : Enum { _details.AddChild(new Label { Text = label }); var field = new OptionButton(); foreach (var name in Enum.GetNames(typeof(T))) field.AddItem(name); field.Selected = Convert.ToInt32(value); field.ItemSelected += index => set((T)Enum.ToObject(typeof(T), index)); _details.AddChild(field); }
    private void Asset(string label, Resource? resource, string type, Action<Resource?> set) { _details.AddChild(new Label { Text = label }); var field = new EditorResourcePicker { BaseType = type, EditedResource = resource }; field.ResourceChanged += v => set(v); _details.AddChild(field); }
    private static void Button(Control parent, string label, Action action) { var b = new global::Godot.Button { Text = label }; b.Pressed += action; parent.AddChild(b); }
}
