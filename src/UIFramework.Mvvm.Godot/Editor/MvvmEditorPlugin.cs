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
    private GodotBindingPreview? _preview;
    private readonly List<Task> _tasks = new();
    public override void _EnterTree()
    {
        _dock = new VBoxContainer { Name = "View Bindings", CustomMinimumSize = new Vector2(680, 340) };
        var actions = new HBoxContainer(); _dock.AddChild(actions);
        Button(actions, "+ 数据源", () => AddSource()); Button(actions, "+ 绑定", () => AddBinding());
        Button(actions, "删除选中", Delete); Button(actions, "上移", () => Move(-1)); Button(actions, "下移", () => Move(1));
        Button(actions, "预览初始值", () => { StopPreview(); RequireView(); _preview = new GodotBindingPreview(_view!); _status.Text = "预览中：不执行命令或修改原始资产"; }); Button(actions, "恢复界面", StopPreview);
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
        var binding = new ViewBindingResource { Target = _view.GetParent().GetPathTo(control), Member = member.Length == 0 ? DefaultMember(control) : member };
        Change("Add View Binding", () => _view.Bindings.Add(binding), () => _view.Bindings.Remove(binding));
        _editorDock.MakeVisible();
    }
    private static string DefaultMember(Control control) => control is BaseButton ? "ButtonPressed" : control is global::Godot.Range ? "Value" : "Text";
    private void AddSource()
    {
        RequireView(); var view = _view!; var source = new ViewModelSourceResource { Name = "Source" + (view.ViewModels.Count + 1) };
        Change("Add Viewmodel", () => view.ViewModels.Add(source), () => view.ViewModels.Remove(source));
    }
    private void AddBinding(string path = "")
    { RequireView(); var view = _view!; var b = new ViewBindingResource { Source = path }; Change("Add View Binding", () => view.Bindings.Add(b), () => view.Bindings.Remove(b)); }
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
        _sources.Clear(); _bindings.Clear(); ClearDetails();
        if (_view == null) return;
        var sourceRoot = _sources.CreateItem(); foreach (var source in _view.ViewModels) { var row = _sources.CreateItem(sourceRoot); row.SetText(0, source.Name + " · " + source.Creation); row.SetMetadata(0, source); }
        var root = _bindings.CreateItem(); foreach (var b in _view.Bindings.Where(b => (b.Target + " " + b.Source + " " + b.Member).Contains(_search.Text, StringComparison.OrdinalIgnoreCase)))
        { var row = _bindings.CreateItem(root); row.SetText(0, b.Target + " · " + b.Member); row.SetText(1, b.Source); row.SetText(2, b.Kind == ViewBindingKind.Property ? b.Mode.ToString() : b.Kind.ToString()); row.SetMetadata(0, b); }
        _status.Text = _view.Bindings.Count + " 条绑定 · 双向绑定初始以 VM 为准";
    }
    private void Delete()
    {
        if (_bindings.GetSelected()?.GetMetadata(0).AsGodotObject() is ViewBindingResource binding)
        { var index = _view!.Bindings.IndexOf(binding); Change("Delete Binding", () => _view.Bindings.Remove(binding), () => _view.Bindings.Insert(index, binding)); }
        else if (_sources.GetSelected()?.GetMetadata(0).AsGodotObject() is ViewModelSourceResource source)
        { var index = _view!.ViewModels.IndexOf(source); Change("Delete Viewmodel", () => _view.ViewModels.Remove(source), () => _view.ViewModels.Insert(index, source)); }
    }
    private void Move(int offset)
    {
        if (_bindings.GetSelected()?.GetMetadata(0).AsGodotObject() is ViewBindingResource binding)
        { var index = _view!.Bindings.IndexOf(binding); var next = index + offset; if (next < 0 || next >= _view.Bindings.Count) return; Change("Reorder Binding", () => { _view.Bindings.RemoveAt(index); _view.Bindings.Insert(next, binding); }, () => { _view.Bindings.RemoveAt(next); _view.Bindings.Insert(index, binding); }); }
    }
    private void ClearDetails() { foreach (var n in _details.GetChildren()) { _details.RemoveChild(n); n.QueueFree(); } }
    private void SourceDetails()
    {
        ClearDetails(); if (_sources.GetSelected()?.GetMetadata(0).AsGodotObject() is not ViewModelSourceResource s) return;
        Text("名称", s.Name, value => Edit(s.Name, value, v => s.Name = v)); Choice("创建方式", s.Creation, v => Edit(s.Creation, v, value => s.Creation = value));
        var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(t => t.IsDefined(typeof(ViewModelAttribute), true) && !t.IsAbstract && !t.IsGenericType).OrderBy(t => t.FullName).ToArray();
        var picker = new OptionButton(); foreach (var type in types) picker.AddItem(type.FullName); var current = Array.FindIndex(types, t => t.AssemblyQualifiedName == s.TypeName); picker.Selected = current;
        picker.ItemSelected += index => Edit(s.TypeName, types[index].AssemblyQualifiedName!, value => s.TypeName = value); _details.AddChild(new Label { Text = "VM 类型" }); _details.AddChild(picker);
        Asset("初始值资产", s.Template, "Resource", value => Edit(s.Template, value, v => s.Template = v));
        var vmType = s.Template?.GetType() ?? Type.GetType(s.TypeName); if (vmType == null) return;
        var members = new MvvmMemberTree { HideRoot = true, CustomMinimumSize = new Vector2(0, 150) }; _details.AddChild(members); var root = members.CreateItem();
        foreach (var property in vmType.GetProperties().Where(p => p.IsDefined(typeof(FieldNotifyAttribute), true))) { var row = members.CreateItem(root); row.SetText(0, property.Name + " : " + property.PropertyType.Name); row.SetMetadata(0, s.Name + "." + property.Name); }
        foreach (var method in vmType.GetMethods().Where(m => m.IsDefined(typeof(CommandAttribute), true))) { var row = members.CreateItem(root); row.SetText(0, method.Name + " : Command"); row.SetMetadata(0, s.Name + "." + method.Name); }
    }
    private void BindingDetails()
    {
        ClearDetails(); if (_bindings.GetSelected()?.GetMetadata(0).AsGodotObject() is not ViewBindingResource b) return;
        Text("控件路径", b.Target, v => Edit(b.Target, new NodePath(v), value => b.Target = value)); Text("目标属性", b.Member, v => Edit(b.Member, v, value => b.Member = value)); Text("来源路径", b.Source, v => Edit(b.Source, v, value => b.Source = value));
        Choice("绑定类型", b.Kind, v => Edit(b.Kind, v, value => b.Kind = value)); Choice("方向", b.Mode, v => Edit(b.Mode, v, value => b.Mode = value));
        var sourceType = SourceType(b.Source); var target = _view!.GetParent().GetNodeOrNull<Control>(b.Target); Type? targetType = null;
        if (target != null && b.Kind == ViewBindingKind.Property) { using var property = GodotBindingTargets.Property(target, b.Member); targetType = property.ValueType; }
        Converter("正向转换", b.Converter, sourceType, targetType, v => Edit(b.Converter, v, value => b.Converter = value));
        Converter("反向转换", b.ConvertBack, targetType, sourceType, v => Edit(b.ConvertBack, v, value => b.ConvertBack = value));
        var nullable = new CheckButton { Text = "显式空值显示", ButtonPressed = b.HasNullValue }; nullable.Toggled += v => Edit(b.HasNullValue, v, value => b.HasNullValue = value); _details.AddChild(nullable);
        Text("空值内容", b.NullValue, v => Edit(b.NullValue, v, value => b.NullValue = value)); Asset("条目场景", b.ItemScene, "PackedScene", v => Edit(b.ItemScene, (PackedScene?)v, value => b.ItemScene = value));
    }
    private Type? SourceType(string path)
    { var parts = path.Split('.'); var source = _view!.ViewModels.FirstOrDefault(s => s.Name == parts[0]); var type = source?.Template?.GetType() ?? Type.GetType(source?.TypeName ?? ""); foreach (var part in parts.Skip(1)) type = type?.GetProperty(part)?.PropertyType; return type; }
    private void Converter(string label, string current, Type? input, Type? output, Action<string> set)
    { var choices = new[] { "" }.Concat(Converters.Discover().Where(c => input != null && output != null && c.Input.IsAssignableFrom(input) && output.IsAssignableFrom(c.Output)).Select(c => c.Name)).ToArray(); var field = new OptionButton(); foreach (var name in choices) field.AddItem(name.Length == 0 ? "无" : name); field.Selected = Array.IndexOf(choices, current); field.ItemSelected += index => set(choices[index]); _details.AddChild(new Label { Text = label }); _details.AddChild(field); }
    private void Edit<T>(T old, T value, Action<T> set) => Change("Edit View Binding", () => set(value), () => set(old));
    private void Text(string label, string value, Action<string> set) { _details.AddChild(new Label { Text = label }); var field = new LineEdit { Text = value }; field.TextSubmitted += v => set(v); field.FocusExited += () => { if (field.Text != value) set(field.Text); }; _details.AddChild(field); }
    private void Choice<T>(string label, T value, Action<T> set) where T : Enum { _details.AddChild(new Label { Text = label }); var field = new OptionButton(); foreach (var name in Enum.GetNames(typeof(T))) field.AddItem(name); field.Selected = Convert.ToInt32(value); field.ItemSelected += index => set((T)Enum.ToObject(typeof(T), index)); _details.AddChild(field); }
    private void Asset(string label, Resource? resource, string type, Action<Resource?> set) { _details.AddChild(new Label { Text = label }); var field = new EditorResourcePicker { BaseType = type, EditedResource = resource }; field.ResourceChanged += v => set(v); _details.AddChild(field); }
    private static void Button(Control parent, string label, Action action) { var b = new global::Godot.Button { Text = label }; b.Pressed += action; parent.AddChild(b); }
}
