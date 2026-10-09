using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UIFramework.Mvvm.Unity;

namespace UIFramework.Mvvm.EditorUnity
{
    [CustomEditor(typeof(GlobalViewModelCatalog))]
    public sealed class GlobalViewModelCatalogInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("保存到 Resources/UIFramework/Mvvm 下。框架初始化时自动注册目录中的 VM；首次使用时创建 Context 独立实例。", HelpBoxMessageType.Info));
            root.Add(new PropertyField(serializedObject.FindProperty("ViewModels"), "SO ViewModels"));
            return root;
        }
    }
}
