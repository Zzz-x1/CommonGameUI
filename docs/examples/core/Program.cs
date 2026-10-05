using UIFramework;

// Engine-free checks for the Manual's container/resource scenarios.
int checks = 0;
void Check(bool condition)
{ if (!condition) throw new InvalidOperationException("Documentation scenario failed."); checks++; }
await using var ui = new UIContext(new MemoryHost(), new MemoryLoader());
ui.Register(() => new LobbyView(), "lobby");
ui.Register(() => new InventoryView(), "inventory");
ui.Register(() => new ItemDetailsView(), "details");
ui.Register(() => new CompareView(), "compare", windowMode: WindowInstanceMode.Multiple);
ui.Register(() => new ConfirmView(), "confirm");
var pages = new StackContainerNode();
var windows = new PanelContainerNode();
var tabs = new TabContainerNode();
var modals = new ModalContainerNode();
await pages.AttachAsync(ui.Root, "Pages", new MemoryElement());
await windows.AttachAsync(ui.Root, "Windows", new MemoryElement());
await tabs.AttachAsync(ui.Root, "Tabs", new MemoryElement());
await modals.AttachAsync(ui.Root, "Dialogs", new MemoryElement());
Check(ui.Root.GetChild("Pages") == pages);
var prepared = await ui.PreloadAsync<InventoryView>();
var inventory = await windows.OpenAsync<InventoryView>();
prepared.Dispose();
await windows.HideAsync(inventory);
Check(!inventory.IsVisible && inventory.State == NodeState.Ready);
Check(await windows.OpenAsync<InventoryView>() == inventory);
await windows.HideAsync(inventory);
await windows.ShowAsync(inventory);
Check(windows.TryGet<InventoryView>(out var existing) && existing == inventory);
await windows.CloseAsync(inventory);
Check(inventory.State == NodeState.Disposed);
var left = await windows.OpenAsync<CompareView>(new ItemArgs("sword"));
var right = await windows.OpenAsync<CompareView>(new ItemArgs("shield"));
Check(left != right && left.Parameters.ItemId == "sword");
await windows.CloseAsync(left);
await windows.CloseAsync(right);
var lobby = await pages.PushAsync<LobbyView>();
var backpack = await pages.PushAsync<InventoryView>();
var details = await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword"));
Check(!backpack.IsVisible && details.Parameters.ItemId == "sword");
await pages.PopAsync();
Check(pages.Current == backpack && details.State == NodeState.Disposed);
await pages.PushAsync<ItemDetailsView>(new ItemArgs("shield"));
await pages.PopToAsync(lobby);
Check(pages.Current == lobby && pages.History.Count == 1 && backpack.State == NodeState.Disposed);
await tabs.BindAsync("Audio", new UINode(), new MemoryElement());
await tabs.BindAsync("Graphics", new UINode(), new MemoryElement());
await tabs.SwitchAsync("Audio", playAnimation: false);
var audio = tabs.Current;
await tabs.SwitchAsync("Graphics");
await tabs.HideAsync();
Check(tabs.Current == null && tabs.Items.Count == 2);
await tabs.SwitchAsync("Audio");
Check(tabs.Current == audio);
var settings = await modals.OpenAsync<InventoryView>();
var graphics = await modals.OpenAsync<ItemDetailsView>(new ItemArgs("quality"));
await modals.CloseAsync(graphics);
Check(modals.Current == settings && settings.IsInteractive);
var answer = modals.ShowDialogAsync<ConfirmView, bool>();
var confirm = (ConfirmView)modals.Current!;
confirm.Complete(false);
Check(!await answer && confirm.State == NodeState.Disposed && modals.Current == settings);
await modals.OpenAsync<ItemDetailsView>(new ItemArgs("purchase"));
await modals.OpenAsync<InventoryView>();
await modals.CloseToAsync(settings);
Check(modals.Current == settings);
pages.ChildTransition = Transitions.Select(context => context.Entering ? new InstantTransition() : new InstantTransition());
await pages.PushAsync<InventoryView>();
await pages.PopAsync(playAnimation: false);
await ui.DisposeAsync();
Check(ui.Root.State == NodeState.Disposed);
Console.WriteLine($"CORE DOCUMENTATION CHECKS PASSED: {checks}");

public sealed class LobbyView : UINode { }
public sealed class InventoryView : UINode { }
public sealed record ItemArgs(string ItemId);
public sealed class ItemDetailsView : UINode<ItemArgs> { }
public sealed class CompareView : UINode<ItemArgs> { }
public sealed class ConfirmView : DialogNode<bool> { }
sealed class MemoryHost : IUIHost
{
    public void VerifyAccess() { }
    public object? CaptureFocus() => null;
    public void Focus(IUIElement? element) { }
    public void RestoreFocus(object? focus) { }
}
sealed class MemoryLoader : IUIResourceLoader
{
    public Task<IUIResource> LoadAsync(string resource, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult<IUIResource>(new MemoryResource()); }
}
sealed class MemoryResource : IUIResource
{
    public IUIElement Instantiate(IUIElement? mount) => new MemoryElement();
    public void Dispose() { }
}
sealed class MemoryElement : IUIElement
{
    public void SetVisible(bool visible) { }
    public void SetInteractive(bool interactive) { }
    public void BringToFront() { }
    public object CaptureOrder() => 0;
    public void RestoreOrder(object order) { }
    public ValueTask DisposeAsync() => default;
}
