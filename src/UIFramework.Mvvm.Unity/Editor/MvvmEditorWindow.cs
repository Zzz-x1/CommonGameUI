using EngineMvvm = global::UIFramework.Mvvm.Unity;
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
        private BindingPreview _preview;
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
                if (binding == null) { window.AppendBinding(target); binding = view.Bindings.Last(); }
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
            _sources.itemIndexChanged += (a, b) => Reorder("ViewModels", a, b);
            _bindings.itemIndexChanged += (a, b) => { if (_search.value.Length != 0) throw new InvalidOperationException("Clear search before reordering bindings."); Reorder("Bindings", a, b); };
            _search.RegisterValueChangedCallback(_ => Refresh());
            rootVisualElement.Q<Button>("add-source").clicked += () => { Edit("ViewModels", array => { var index = array.arraySize; array.InsertArrayElementAtIndex(index); var row = array.GetArrayElementAtIndex(index); row.FindPropertyRelative("Name").stringValue = "Source" + (index + 1); row.FindPropertyRelative("Creation").intValue = 0; row.FindPropertyRelative("Template").objectReferenceValue = null; }); Refresh(); _sources.selectedIndex = _view.ViewModels.Count - 1; };
            rootVisualElement.Q<Button>("delete-source").clicked += () => { if (_sources.selectedIndex >= 0) Edit("ViewModels", array => array.DeleteArrayElementAtIndex(_sources.selectedIndex)); Refresh(); };
            rootVisualElement.Q<Button>("add-binding").clicked += () => { AppendBinding(); Refresh(); _bindings.selectedIndex = _visible.Count - 1; };
            rootVisualElement.Q<Button>("delete-binding").clicked += () => { if (_bindings.selectedIndex >= 0) Edit("Bindings", array => array.DeleteArrayElementAtIndex(_view.Bindings.IndexOf(_visible[_bindings.selectedIndex]))); Refresh(); };
            rootVisualElement.Q<Button>("preview").clicked += () => { StopPreview(); _preview = new BindingPreview(_view); _status.text = "预览中：仅刷新显示，不执行命令或写回资产"; };
            rootVisualElement.Q<Button>("stop-preview").clicked += StopPreview;
            _bindings.RegisterCallback<DragUpdatedEvent>(e => { if (DragAndDrop.GetGenericData("Mvvm.Path") is string) { DragAndDrop.visualMode = DragAndDropVisualMode.Copy; e.StopPropagation(); } });
            _bindings.RegisterCallback<DragPerformEvent>(e => { if (DragAndDrop.GetGenericData("Mvvm.Path") is string path) { DragAndDrop.AcceptDrag(); AppendBinding(source: path); Refresh(); _bindings.selectedIndex = _visible.Count - 1; } });
            Undo.undoRedoPerformed += Refresh;
            if (_view) Select(_view); else if (Selection.activeGameObject) Select(Selection.activeGameObject.GetComponentInParent<MvvmBindings>());
        }
        private void Select(MvvmBindings view)
        { StopPreview(); _view = view; _sources.ClearSelection(); _bindings.ClearSelection(); _details.Clear(); _members.Clear(); rootVisualElement.Q<ObjectField>("view")?.SetValueWithoutNotify(view); Refresh(); }
        private void Edit(string path, Action<SerializedProperty> write)
        {
            if (!_view) throw new InvalidOperationException("Select a View with MvvmBindings.");
            StopPreview();
            using (var serialized = new SerializedObject(_view))
            {
                serialized.Update(); write(serialized.FindProperty(path)); serialized.ApplyModifiedProperties();
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(_view);
        }
        private void Binding(ViewBindingData binding, string member, Action<SerializedProperty> write) =>
            Edit("Bindings.Array.data[" + _view.Bindings.IndexOf(binding) + "]." + member, write);
        private void Source(ViewModelSourceData source, string member, Action<SerializedProperty> write) =>
            Edit("ViewModels.Array.data[" + _view.ViewModels.IndexOf(source) + "]." + member, write);
        private void Reorder(string path, int oldIndex, int newIndex) { Edit(path, array => array.MoveArrayElement(oldIndex, newIndex)); Refresh(); }
        private void AppendBinding(Component target = null, string source = "")
        {
            Edit("Bindings", array =>
            {
                var index = array.arraySize; array.InsertArrayElementAtIndex(index); var row = array.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("Enabled").boolValue = true;
                row.FindPropertyRelative("Target").objectReferenceValue = target;
                row.FindPropertyRelative("Source").stringValue = source; row.FindPropertyRelative("Member").stringValue = "text";
                row.FindPropertyRelative("Mode").intValue = (int)BindingMode.OneWay; row.FindPropertyRelative("Kind").intValue = 0;
                row.FindPropertyRelative("Converter").stringValue = ""; row.FindPropertyRelative("ConvertBack").stringValue = "";
                row.FindPropertyRelative("ConverterInput").intValue = 0; row.FindPropertyRelative("ConverterConstant").stringValue = "";
                row.FindPropertyRelative("HasNullValue").boolValue = false; row.FindPropertyRelative("NullValue").stringValue = "";
                row.FindPropertyRelative("ItemPrefab").objectReferenceValue = null; row.FindPropertyRelative("OneTimeCollection").boolValue = false;
            });
        }
        private void Refresh()
        {
            if (_sources == null) return;
            _sourceRows.Clear(); if (_view) _sourceRows.AddRange(_view.ViewModels); _sources.itemsSource = _sourceRows; _sources.Rebuild(); _visible.Clear();
            if (_view) _visible.AddRange(_view.Bindings.Where(b => ((b.Target ? b.Target.name : "") + b.Member + b.Source).IndexOf(_search.value, StringComparison.OrdinalIgnoreCase) >= 0));
            _bindings.itemsSource = _visible; _bindings.reorderable = _search.value.Length == 0; _bindings.Rebuild();
            _status.text = _view ? _view.Bindings.Count + " 条绑定 · 配置随 Prefab / 场景保存" : "选择界面上的 MvvmBindings，或通过控件 Inspector 添加绑定。";
            var missing = _view ? _view.ViewModels.Where(source => !source.Template).Select(source => source.Name).ToArray() : new string[0];
            rootVisualElement.Q<Button>("preview").SetEnabled(_view && missing.Length == 0);
            if (missing.Length != 0) _status.text = "缺少 VM 资产：" + string.Join(", ", missing);
        }
        private void SourceDetails()
        {
            _details.Clear(); _members.Clear(); if (!_view || _sources.selectedIndex < 0) return;
            var source = _view.ViewModels[_sources.selectedIndex];
            Text("名称", source.Name, v => Source(source, "Name", property => property.stringValue = v));
            var modes = new DropdownField("创建方式", Enum.GetNames(typeof(ViewModelCreation)).ToList(), (int)source.Creation); modes.RegisterValueChangedCallback(e => { Source(source, "Creation", property => property.intValue = (int)Enum.Parse(typeof(ViewModelCreation), e.newValue)); Refresh(); }); _details.Add(modes);
            var assets = AssetDatabase.FindAssets("t:ScriptableObject").Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<ScriptableObject>()
                .Where(asset => asset.GetType().IsDefined(typeof(ViewModelAttribute), false)).ToArray();
            var search = new TextField("搜索 VM 资产"); _details.Add(search);
            var picker = new DropdownField("VM 资产", assets.Select(AssetDatabase.GetAssetPath).ToList(), -1);
            search.RegisterValueChangedCallback(e => picker.choices = assets.Where(asset => AssetDatabase.GetAssetPath(asset).IndexOf(e.newValue, StringComparison.OrdinalIgnoreCase) >= 0).Select(AssetDatabase.GetAssetPath).ToList());
            picker.SetValueWithoutNotify(source.Template ? AssetDatabase.GetAssetPath(source.Template) : "");
            picker.RegisterValueChangedCallback(e => { Source(source, "Template", property => property.objectReferenceValue = assets.Single(asset => AssetDatabase.GetAssetPath(asset) == e.newValue)); SourceDetails(); }); _details.Add(picker);
            if (!source.Template) return;
            _details.Add(new Button(() => EditorGUIUtility.PingObject(source.Template)) { text = "定位资产" });
            var vmType = source.Template.GetType();
            foreach (var property in vmType.GetProperties().Where(p => p.IsDefined(typeof(FieldNotifyAttribute), true))) Member(source.Name + "." + property.Name, property.PropertyType.Name);
            foreach (var command in vmType.GetMethods().Where(m => m.IsDefined(typeof(CommandAttribute), true))) Member(source.Name + "." + command.Name, "Command");
        }
        private void Member(string path, string type)
        { var row = new Label(path + " : " + type); row.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; DragAndDrop.PrepareStartDrag(); DragAndDrop.SetGenericData("Mvvm.Path", path); DragAndDrop.StartDrag(path); }); _members.Add(row); }
        private void BindingDetails()
        {
            _details.Clear(); if (!_view || _bindings.selectedIndex < 0) return; var b = _visible[_bindings.selectedIndex];
            Object("目标控件", typeof(Component), b.Target, true, v => { Binding(b, "Target", property => property.objectReferenceValue = v); Binding(b, "Member", property => property.stringValue = TargetMembers((Component)v).FirstOrDefault() ?? ""); BindingDetails(); });
            var members = TargetMembers(b.Target); var member = new DropdownField("目标属性", members, -1); member.SetValueWithoutNotify(b.Member); member.RegisterValueChangedCallback(e => Binding(b, "Member", property => property.stringValue = e.newValue)); _details.Add(member);
            if (b.Kind != ViewBindingKind.Property || b.ConverterInput == ConverterInput.ViewModel)
                Text("来源路径", b.Source, v => Binding(b, "Source", property => property.stringValue = v));
            Choice("绑定类型", b.Kind, v => { Binding(b, "Kind", property => property.intValue = (int)v); BindingDetails(); });
            if (b.Kind == ViewBindingKind.Property)
            {
                Choice("方向", b.Mode, v => { Binding(b, "Mode", property => property.intValue = (int)v); if (v is BindingMode.TwoWay or BindingMode.OneWayToViewModel) Binding(b, "ConverterInput", property => property.intValue = (int)ConverterInput.ViewModel); BindingDetails(); });
                if (b.Mode is BindingMode.OneWay or BindingMode.OneTime)
                    Choice("转换器输入", b.ConverterInput, v => { Binding(b, "ConverterInput", property => property.intValue = (int)v); BindingDetails(); });
                Converter("正向转换", b.Converter, b, false, v => Binding(b, "Converter", property => property.stringValue = v));
                if (b.Converter.Length != 0)
                {
                    var info = Converters.Discover().Single(c => c.Name == b.Converter);
                    _details.Add(new Label("输入：" + info.Input.Name + " → 输出：" + info.Output.Name));
                    if (b.ConverterInput == ConverterInput.Constant) Text("输入常量", b.ConverterConstant, v => Binding(b, "ConverterConstant", property => property.stringValue = v));
                    else _details.Add(new Label("输入参数绑定到上述来源路径；成员变化时重新转换。"));
                }
                if (b.Mode == BindingMode.TwoWay) Converter("反向转换", b.ConvertBack, b, true, v => Binding(b, "ConvertBack", property => property.stringValue = v));
                Toggle("显式空值显示", b.HasNullValue, v => Binding(b, "HasNullValue", property => property.boolValue = v)); Text("空值内容", b.NullValue, v => Binding(b, "NullValue", property => property.stringValue = v));
            }
            if (b.Kind == ViewBindingKind.Collection) { Object("条目 Prefab", typeof(GameObject), b.ItemPrefab, false, v => Binding(b, "ItemPrefab", property => property.objectReferenceValue = v)); Toggle("一次性展示", b.OneTimeCollection, v => Binding(b, "OneTimeCollection", property => property.boolValue = v)); }
            Toggle("启用", b.Enabled, v => Binding(b, "Enabled", property => property.boolValue = v));
            _details.Add(new HelpBox("来源格式：数据源.成员，支持嵌套路径。双向绑定初始以 VM 为准。", HelpBoxMessageType.Info));
        }
        private void Converter(string label, string current, ViewBindingData binding, bool reverse, Action<string> set)
        {
            Type sourceType = null, targetType = null;
            var parts = binding.Source.Split('.'); var source = _view.ViewModels.FirstOrDefault(s => s.Name == parts[0]);
            if (source != null) { sourceType = source.Template ? source.Template.GetType() : null; foreach (var part in parts.Skip(1)) sourceType = sourceType?.GetProperty(part)?.PropertyType; }
            if (binding.Target && binding.Kind == ViewBindingKind.Property && TargetMembers(binding.Target).Contains(binding.Member)) { targetType = BindingTargetRegistry.Find(binding.Target.GetType(), binding.Member).ValueType; }
            var input = reverse ? targetType : sourceType; var output = reverse ? sourceType : targetType;
            var choices = new List<string> { "" }; choices.AddRange(Converters.Discover().Where(c => (input != null || (!reverse && binding.ConverterInput == ConverterInput.Constant)) && output != null && (!reverse && binding.ConverterInput == ConverterInput.Constant ? Converters.SupportsConstant(c.Input) : c.Input.IsAssignableFrom(input)) && output.IsAssignableFrom(c.Output)).Select(c => c.Name));
            var field = new DropdownField(label, choices, -1); field.SetValueWithoutNotify(current); field.RegisterValueChangedCallback(e => { set(e.newValue); BindingDetails(); }); _details.Add(field);
        }
        private static List<string> TargetMembers(Component target) => target
            ? BindingTargetRegistry.Members(target.GetType()).Select(d => d.Member).ToList() : new List<string>();
        private void Text(string label, string value, Action<string> set)
        { var field = new TextField(label) { value = value }; field.RegisterValueChangedCallback(e => { set(e.newValue); Refresh(); }); _details.Add(field); }
        private void Object(string label, Type type, UnityEngine.Object value, bool scene, Action<UnityEngine.Object> set)
        { var field = new ObjectField(label) { objectType = type, allowSceneObjects = scene, value = value }; field.RegisterValueChangedCallback(e => { set(e.newValue); Refresh(); }); _details.Add(field); }
        private void Toggle(string label, bool value, Action<bool> set)
        { var field = new Toggle(label) { value = value }; field.RegisterValueChangedCallback(e => set(e.newValue)); _details.Add(field); }
        private void Choice<T>(string label, T value, Action<T> set) where T : Enum
        { var field = new DropdownField(label, Enum.GetNames(typeof(T)).ToList(), Convert.ToInt32(value)); field.RegisterValueChangedCallback(e => { set((T)Enum.Parse(typeof(T), e.newValue)); Refresh(); }); _details.Add(field); }
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
            var view = control.GetComponentInParent<MvvmBindings>();
            var node = control.GetComponentInParent<global::UIFramework.Unity.UINode>();
            var owner = view ? view.gameObject : node ? node.gameObject : null;
            if (!owner) return;
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w.GetType().Name == "InspectorWindow"))
            {
                var root = window.rootVisualElement; root.Q<Button>("mvvm-bind-entry")?.RemoveFromHierarchy();
                var target = control;
                root.Add(new Button(() => MvvmEditorWindow.Inspect(owner.GetComponent<MvvmBindings>() ?? Undo.AddComponent<MvvmBindings>(owner), target)) { name = "mvvm-bind-entry", text = "建立 MVVM 绑定…" });
                root.Query<PropertyField>().ForEach(field => field.AddManipulator(new ContextualMenuManipulator(e => e.menu.AppendAction("MVVM / 建立绑定", _ => MvvmEditorWindow.Inspect(owner.GetComponent<MvvmBindings>() ?? Undo.AddComponent<MvvmBindings>(owner), target)))));
            }
        }
    }
}
