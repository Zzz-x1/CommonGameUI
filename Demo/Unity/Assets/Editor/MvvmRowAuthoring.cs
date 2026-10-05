using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;
namespace StarCourt.UnityDemo.Editor
{
    public static class MvvmRowAuthoring
    {
        public static void Build()
        {
            var inventory = PrefabUtility.LoadPrefabContents("Assets/Resources/UI/Inventory.prefab");
            var row = UnityEngine.Object.Instantiate(inventory.transform.Find("Pages/装备/C0").gameObject); row.name = "C";
            var config = row.AddComponent<MvvmBindings>(); config.ViewModels.Add(new ViewModelSourceData { Name = "Item", TypeName = typeof(ClientInventoryItemVm).AssemblyQualifiedName, Creation = ViewModelCreation.Manual });
            config.Bindings.Add(new ViewBindingData { Target = row.transform.Find("Text").GetComponent<Text>(), Source = "Item.Text" });
            config.Bindings.Add(new ViewBindingData { Target = row.GetComponent<Button>(), Source = "Item.Select", Kind = ViewBindingKind.Command });
            PrefabUtility.SaveAsPrefabAsset(row, "Assets/Resources/MVVM/InventoryRow.prefab"); UnityEngine.Object.DestroyImmediate(row); PrefabUtility.UnloadPrefabContents(inventory);
            Debug.Log("MVVM inventory row authored");
        }
    }
}
