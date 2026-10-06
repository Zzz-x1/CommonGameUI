using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineMvvm = global::UIFramework.Mvvm.Unity;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;
using UnityEngine;
namespace StarCourt.UnityDemo
{
    [ViewModel]
    public class ClientInventoryVm
    { [FieldNotify] public ObservableCollection<ClientInventoryItemVm> Items { get; set; } = new ObservableCollection<ClientInventoryItemVm>(); }
    [ViewModel]
    public class ClientInventoryItemVm
    {
        public int Id { get; set; }
        [FieldNotify] public string Text { get; set; } = "";
        public Func<Task> Open { get; set; }
        [Command] public Task Select() => Open();
    }
    internal sealed class InventoryGridTarget : ICollectionTarget, IAsyncDisposable
    {
        private readonly EngineMvvm.CollectionTarget _items;
        private readonly RectTransform _mount;
        public InventoryGridTarget(IUINode owner, RectTransform mount, GameObject row) { _mount = mount; _items = new EngineMvvm.CollectionTarget(owner, mount, row); _items.Changed += Arrange; }
        public void Insert(int index, object item) { _items.Insert(index, item); Arrange(); }
        public void Remove(int index) { _items.Remove(index); Arrange(); }
        public void Move(int oldIndex, int newIndex) { _items.Move(oldIndex, newIndex); Arrange(); }
        public void Clear() => _items.Clear();
        public void Dispose() => _items.Dispose();
    public ValueTask DisposeAsync() => _items.DisposeAsync();
        private void Arrange() { for (int i = 0; i < _mount.childCount; i++) ((RectTransform)_mount.GetChild(i)).anchoredPosition = new Vector2(i % 4 * 370, -(i / 4) * 220); }
    }
}
