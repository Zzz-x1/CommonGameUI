using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UIFramework.Mvvm.Unity;

namespace UIFramework.Mvvm.EditorUnity
{
    public sealed class MvvmEditorWindow : EditorWindow
    {
        private const string Package = "Packages/com.ui-framework.mvvm.unity/Editor/";
        private MvvmBindings _view;
        private ListView _sources, _bindings;
        private ScrollView _details, _members;
        private TextField _search;
        private Label _status;
        private UnityBindingPreview _preview;
        private readonly List<ViewBindingData> _visible = new List<ViewBindingData>();
        private readonly List<ViewModelSourceData> _sourceRows = new List<ViewModelSourceData>();
        [MenuItem("Window/UIFramework/View Bindings")]
        public static void Open() => GetWindow<MvvmEditorWindow>("View Bindings");
        public static void Inspect(MvvmBindings view, Component target = null)
        {
            var window = GetWindow<MvvmEditorWindow>("View Bindings"); window.Select(view);
            if (target)
            {
                var binding = view.Bindings.FirstOrDefault(b => b.Target == target);
                if (binding == null) window.Change(() => view.Bindings.Add(binding = new ViewBindingData { Target = target }));
                window.Refresh(); window._bindings.selectedIndex = window._visible.IndexOf(binding);
            }
        }
        public void CreateGUI()
        {
            rootVisualElement.Clear(); Undo.undoRedoPerformed -= Refresh;
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Package + "MvvmEditor.uxml").CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(Package + "MvvmEditor.uss"));
            _details = rootVisualElement.Q<ScrollView>("details"); _members = rootVisualElement.Q<ScrollView>("members");
            _sources = rootVisualElement.Q<ListView>("sources"); _bindings = rootVisualElement.Q<ListView>("bindings");
            _status = rootVisualElement.Q<Label>("status"); _search = rootVisualElement.Q<TextField>("search");
            var view = rootVisualElement.Q<ObjectField>("view"); view.objectType = typeof(MvvmBindings); view.RegisterValueChangedCallback(e => Select((MvvmBindings)e.newValue));
            _sources.makeItem = () => new Label(); _sources.bindItem = (row, i) => ((Label)row).text = _view.ViewModels[i].Name + " · " + _view.ViewModels[i].Creation;
            _sources.selectionChanged += _ => SourceDetails();
            _bindings.makeItem = () => { var row = new VisualElement(); row.AddToClassList("row"); row.Add(new Label()); row.Add(new Label()); row.Add(new Label()); return row; };
            _bindings.bindItem = (row, i) => { var b = _visible[i]; ((Label)row[0]).text = (b.Target ? b.Target.name : "选择控件") + " · " + b.Member; ((Label)row[1]).text = b.Source; ((Label)row[2]).text = b.Kind == ViewBindingKind.Property ? b.Mode.ToString() : b.Kind.ToString(); };
            _bindings.selectionChanged += _ => BindingDetails();
            _sources.itemIndexChanged += (a, b) => Reorder(_view.ViewModels, a, b);
            _bindings.itemIndexChanged += (a, b) => { if (_search.value.Length != 0) throw new InvalidOperationException("Clear search before reordering bindings."); Reorder(_view.Bindings, a, b); };
            _search.RegisterValueChangedCallback(_ => Refresh());
            rootVisualElement.Q<Button>("add-source").clicked += () => { Change(() => _view.ViewModels.Add(new ViewModelSourceData { Name = "Source" + (_view.ViewModels.Count + 1) })); Refresh(); _sources.selectedIndex = _view.ViewModels.Count - 1; };
            rootVisualElement.Q<Button>("delete-source").clicked += () => { if (_sources.selectedIndex >= 0) Change(() => _view.ViewModels.RemoveAt(_sources.selectedIndex)); Refresh(); };
            rootVisualElement.Q<Button>("add-binding").clicked += () => { Change(() => _view.Bindings.Add(new ViewBindingData())); Refresh(); _bindings.selectedIndex = _visible.Count - 1; };
            rootVisualElement.Q<Button>("delete-binding").clicked += () => { if (_bindings.selectedIndex >= 0) Change(() => _view.Bindings.Remove(_visible[_bindings.selectedIndex])); Refresh(); };
            rootVisualElement.Q<Button>("preview").clicked += () => { StopPreview(); _preview = new UnityBindingPreview(_view); _status.text = "预览中：仅刷新显示，不执行命令或写回资产"; };
            rootVisualElement.Q<Button>("stop-preview").clicked += StopPreview;
            _bindings.RegisterCallback<DragUpdatedEvent>(e => { if (DragAndDrop.GetGenericData("Mvvm.Path") is string) { DragAndDrop.visualMode = DragAndDropVisualMode.Copy; e.StopPropagation(); } });
            _bindings.RegisterCallback<DragPerformEvent>(e => { if (DragAndDrop.GetGenericData("Mvvm.Path") is string path) { DragAndDrop.AcceptDrag(); Change(() => _view.Bindings.Add(new ViewBindingData { Source = path })); Refresh(); _bindings.selectedIndex = _visible.Count - 1; } });
            Undo.undoRedoPerformed += Refresh;
            if (_view) Select(_view); else if (Selection.activeGameObject) Select(Selection.activeGameObject.GetComponentInParent<MvvmBindings>());
        }
        private void Select(MvvmBindings view)
        { StopPreview(); _view = view; _sources.ClearSelection(); _bindings.ClearSelection(); _details.Clear(); _members.Clear(); rootVisualElement.Q<ObjectField>("view")?.SetValueWithoutNotify(view); Refresh(); }
        private void Change(Action change)
        {
            if (!_view) throw new InvalidOperationException("Select a View with MvvmBindings.");
            StopPreview(); Undo.RecordObject(_view, "Edit View Binding"); change(); EditorUtility.SetDirty(_view);
            PrefabUtility.RecordPrefabInstancePropertyModifications(_view);
        }
        private void Reorder<T>(List<T> list, int oldIndex, int newIndex)
        { Change(() => { var value = list[oldIndex]; list.RemoveAt(oldIndex); list.Insert(newIndex, value); }); Refresh(); }
        private void Refresh()
        {
            if (_sources == null) return;
            _sourceRows.Clear(); if (_view) _sourceRows.AddRange(_view.ViewModels); _sources.itemsSource = _sourceRows; _sources.Rebuild(); _visible.Clear();
            if (_view) _visible.AddRange(_view.Bindings.Where(b => ((b.Target ? b.Target.name : "") + b.Member + b.Source).IndexOf(_search.value, StringComparison.OrdinalIgnoreCase) >= 0));
            _bindings.itemsSource = _visible; _bindings.reorderable = _search.value.Length == 0; _bindings.Rebuild();
            _status.text = _view ? _view.Bindings.Count + " 条绑定 · 配置随 Prefab / 场景保存" : "选择界面上的 MvvmBindings，或通过控件 Inspector 添加绑定。";
            var missing = _view ? _view.ViewModels.Where(s => s.Creation == ViewModelCreation.Manual && ((s.Template ? s.Template.GetType() : Type.GetType(s.TypeName)) is not Type type || !PreviewSources.Has(type))).Select(s => s.Name).ToArray() : new string[0];
            rootVisualElement.Q<Button>("preview").SetEnabled(_view && missing.Length == 0);
            if (missing.Length != 0) _status.text = "缺少预览来源：" + string.Join(", ", missing);
        }
        private void SourceDetails()
        {
            _details.Clear(); _members.Clear(); if (!_view || _sources.selectedIndex < 0) return;
            var source = _view.ViewModels[_sources.selectedIndex];
            Text("名称", source.Name, v => source.Name = v);
            var modes = new DropdownField("创建方式", Enum.GetNames(typeof(ViewModelCreation)).ToList(), (int)source.Creation); modes.RegisterValueChangedCallback(e => { Change(() => source.Creation = (ViewModelCreation)Enum.Parse(typeof(ViewModelCreation), e.newValue)); Refresh(); }); _details.Add(modes);
            var types = TypeCache.GetTypesWithAttribute<ViewModelAttribute>().Where(t => t.IsDefined(typeof(ViewModelAttribute), false) && !t.IsAbstract && !t.IsGenericType).OrderBy(t => t.FullName).ToArray();
            var type = new DropdownField("VM 类型", types.Select(t => t.FullName).ToList(), -1); type.SetValueWithoutNotify(Type.GetType(source.TypeName)?.FullName ?? "");
            type.RegisterValueChangedCallback(e => { Change(() => source.TypeName = types.Single(t => t.FullName == e.newValue).AssemblyQualifiedName); SourceDetails(); }); _details.Add(type);
            Object("初始值资产", typeof(ScriptableObject), source.Template, false, v => source.Template = (ScriptableObject)v);
            var vmType = source.Template ? source.Template.GetType() : Type.GetType(source.TypeName);
            if (vmType == null) return;
            foreach (var property in vmType.GetProperties().Where(p => p.IsDefined(typeof(FieldNotifyAttribute), true))) Member(source.Name + "." + property.Name, property.PropertyType.Name);
            foreach (var command in vmType.GetMethods().Where(m => m.IsDefined(typeof(CommandAttribute), true))) Member(source.Name + "." + command.Name, "Command");
        }
        private void Member(string path, string type)
        { var row = new Label(path + " : " + type); row.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; DragAndDrop.PrepareStartDrag(); DragAndDrop.SetGenericData("Mvvm.Path", path); DragAndDrop.StartDrag(path); }); _members.Add(row); }
        private void BindingDetails()
        {
            _details.Clear(); if (!_view || _bindings.selectedIndex < 0) return; var b = _visible[_bindings.selectedIndex];
            Object("目标控件", typeof(Component), b.Target, true, v => { b.Target = (Component)v; b.Member = TargetMembers(b.Target)[0]; });
            var members = TargetMembers(b.Target); var member = new DropdownField("目标属性", members, -1); member.SetValueWithoutNotify(b.Member); member.RegisterValueChangedCallback(e => Change(() => b.Member = e.newValue)); _details.Add(member);
            Text("来源路径", b.Source, v => b.Source = v);
            Choice("绑定类型", b.Kind, v => { b.Kind = v; BindingDetails(); });
            if (b.Kind == ViewBindingKind.Property)
            {
                Choice("方向", b.Mode, v => b.Mode = v);
                Converter("正向转换", b.Converter, b, false, v => b.Converter = v);
                if (b.Mode == BindingMode.TwoWay) Converter("反向转换", b.ConvertBack, b, true, v => b.ConvertBack = v);
                Toggle("显式空值显示", b.HasNullValue, v => b.HasNullValue = v); Text("空值内容", b.NullValue, v => b.NullValue = v);
            }
            if (b.Kind == ViewBindingKind.Collection) { Object("条目 Prefab", typeof(GameObject), b.ItemPrefab, false, v => b.ItemPrefab = (GameObject)v); Toggle("一次性展示", b.OneTimeCollection, v => b.OneTimeCollection = v); }
            Toggle("启用", b.Enabled, v => b.Enabled = v);
            _details.Add(new HelpBox("来源格式：数据源.成员，支持嵌套路径。双向绑定初始以 VM 为准。", HelpBoxMessageType.Info));
        }
        private void Converter(string label, string current, ViewBindingData binding, bool reverse, Action<string> set)
        {
            Type sourceType = null, targetType = null;
            var parts = binding.Source.Split('.'); var source = _view.ViewModels.FirstOrDefault(s => s.Name == parts[0]);
            if (source != null) { sourceType = source.Template ? source.Template.GetType() : Type.GetType(source.TypeName); foreach (var part in parts.Skip(1)) sourceType = sourceType?.GetProperty(part)?.PropertyType; }
            if (binding.Target && binding.Kind == ViewBindingKind.Property && TargetMembers(binding.Target).Contains(binding.Member)) { using (var target = UnityBindingTargets.Property(binding.Target, binding.Member)) targetType = target.ValueType; }
            var input = reverse ? targetType : sourceType; var output = reverse ? sourceType : targetType;
            var choices = new List<string> { "" }; choices.AddRange(Converters.Discover().Where(c => input != null && output != null && c.Input.IsAssignableFrom(input) && output.IsAssignableFrom(c.Output)).Select(c => c.Name));
            var field = new DropdownField(label, choices, -1); field.SetValueWithoutNotify(current); field.RegisterValueChangedCallback(e => Change(() => set(e.newValue))); _details.Add(field);
        }
        private static List<string> TargetMembers(Component target)
        {
            var names = new List<string>();
            if (target is UnityEngine.UI.Text || target is TMPro.TMP_Text || target is UnityEngine.UI.InputField || target is TMPro.TMP_InputField) names.Add("text");
            if (target is UnityEngine.UI.Slider) names.Add("value"); if (target is UnityEngine.UI.Toggle) names.Add("isOn");
            if (target is UnityEngine.UI.Image) names.Add("sprite"); if (target is UnityEngine.UI.RawImage) names.Add("texture"); if (target is UnityEngine.UI.Selectable) names.Add("interactable");
            names.Add("activeSelf"); return names;
        }
        private void Text(string label, string value, Action<string> set)
        { var field = new TextField(label) { value = value }; field.RegisterValueChangedCallback(e => { Change(() => set(e.newValue)); Refresh(); }); _details.Add(field); }
        private void Object(string label, Type type, UnityEngine.Object value, bool scene, Action<UnityEngine.Object> set)
        { var field = new ObjectField(label) { objectType = type, allowSceneObjects = scene, value = value }; field.RegisterValueChangedCallback(e => { Change(() => set(e.newValue)); Refresh(); }); _details.Add(field); }
        private void Toggle(string label, bool value, Action<bool> set)
        { var field = new Toggle(label) { value = value }; field.RegisterValueChangedCallback(e => Change(() => set(e.newValue))); _details.Add(field); }
        private void Choice<T>(string label, T value, Action<T> set) where T : Enum
        { var field = new DropdownField(label, Enum.GetNames(typeof(T)).ToList(), Convert.ToInt32(value)); field.RegisterValueChangedCallback(e => { Change(() => set((T)Enum.Parse(typeof(T), e.newValue))); Refresh(); }); _details.Add(field); }
        private void StopPreview() { _preview?.Dispose(); _preview = null; }
        private void OnDisable() { StopPreview(); Undo.undoRedoPerformed -= Refresh; }
    }
    [CustomEditor(typeof(MvvmBindings))]
    public sealed class MvvmBindingsInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        { var root = new VisualElement(); root.Add(new Button(() => MvvmEditorWindow.Inspect((MvvmBindings)target)) { text = "打开 Viewmodels / View Bindings" }); root.Add(new PropertyField(serializedObject.FindProperty("ViewModels"))); root.Add(new PropertyField(serializedObject.FindProperty("Bindings"))); return root; }
    }
    [InitializeOnLoad]
    internal static class ControlBindingEntry
    {
        static ControlBindingEntry() { Selection.selectionChanged += () => EditorApplication.delayCall += Install; }
        private static void Install()
        {
            var control = Selection.activeObject as Component;
            if (!control && Selection.activeGameObject) control = Selection.activeGameObject.GetComponent<UnityEngine.UI.Selectable>() ?? (Component)Selection.activeGameObject.GetComponent<UnityEngine.UI.Graphic>();
            if (!control) return;
            var view = control.GetComponentInParent<MvvmBindings>(); if (!view) return;
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w.GetType().Name == "InspectorWindow"))
            {
                var root = window.rootVisualElement; root.Q<Button>("mvvm-bind-entry")?.RemoveFromHierarchy();
                var target = control;
                root.Add(new Button(() => MvvmEditorWindow.Inspect(view, target)) { name = "mvvm-bind-entry", text = "建立 MVVM 绑定…" });
                root.Query<PropertyField>().ForEach(field => field.AddManipulator(new ContextualMenuManipulator(e => e.menu.AppendAction("MVVM / 建立绑定", _ => MvvmEditorWindow.Inspect(view, target)))));
            }
        }
    }
}
