using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineMvvm = global::UIFramework.Mvvm.Godot;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;
namespace HierarchyDemo.Tests;
public partial class MvvmVerification : global::Godot.Node
{
    private Task _test = null!;
    private int _checks;
    private Action? _frameEdit;
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _test = Run();
    }
    public override void _Process(double delta)
    {
        var edit = _frameEdit;
        _frameEdit = null;
        edit?.Invoke();
        if (!_test.IsCompleted) return; SetProcess(false);
        try { _test.GetAwaiter().GetResult(); GD.Print($"MVVM GODOT: {_checks} checks passed"); GetTree().Quit(); }
        catch (Exception error) { GD.PrintErr(error); GetTree().Quit(1); }
    }
    private void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); _checks++; }
    private async Task Run()
    {
        var template = new NativePlayerVm(); template.Other = template;
        var view = new Control { Name = "View" }; AddChild(view); var label = new Label { Name = "Gold", Text = "original" }; view.AddChild(label);
        var button = new Button { Name = "Buy" }; view.AddChild(button); var slider = new HSlider { Name = "Amount", MinValue = 0, MaxValue = 200 }; view.AddChild(slider);
        var config = new MvvmBindings { Name = "MvvmBindings" }; view.AddChild(config); config.Owner = view; label.Owner = view; button.Owner = view; slider.Owner = view;
        config.ViewModels.Add(new ViewModelSourceResource { Name = "Player", Creation = ViewModelCreation.SharedAsset, Template = template });
        config.AddBinding(view.GetNode<Control>("Gold"), new ViewBindingResource { Member = "Text", Source = "Player.Gold", Converter = "FormatNumber" });
        config.AddBinding(view.GetNode<Control>("Buy"), new ViewBindingResource { Kind = ViewBindingKind.Command, Source = "Player.Buy" });
        config.AddBinding(view.GetNode<Control>("Amount"), new ViewBindingResource { Member = "Value", Source = "Player.Gold", Mode = BindingMode.TwoWay, Converter = "IntToFloat", ConvertBack = "FloatToInt", UpdateTiming = BindingUpdateTiming.Immediate });
        var runtime = new MvvmRuntime(); var ui = EngineUI.UI.Create(this); ui.ConfigureMvvm(runtime); var node = new UINode(); await node.AttachToAsync(ui.Root, "View", view);
        var vm = node.GetViewModel<NativePlayerVm>("Player"); Check(vm != template, "Original asset was used"); Check(ReferenceEquals(vm, vm.Other), "Asset cycle not preserved"); Check(label.Text == "100", "Initial display missing");
        button.EmitSignal(BaseButton.SignalName.Pressed); Check(vm.Gold == 90 && template.Gold == 100, "Purchase modified template");
        Check(slider.Value == 90 && label.Text == "100", "Immediate slider and deferred label observe the same purchase");
        slider.Value = 70; Check(vm.Gold == 70 && label.Text == "100", "Two-way editor writes synchronously");
        await NextUIFrameAsync();
        Check(label.Text == "70", "Deferred label reads the latest value");
        vm.Items.Add(2); Check(template.Items.Count == 1, "Typed arrays shared storage");
        node.PauseBinding(nameof(NativePlayerVm.Gold)); vm.Gold = 55; slider.Value = 33;
        Check(vm.Gold == 55 && label.Text == "70", "Paused GUI bindings suppress both directions");
        node.ResumeBinding("Player.Gold"); Check(label.Text == "55" && slider.Value == 55, "Resume GUI group from latest VM value");
        await VerifySharedFrameAsync(ui, vm);
        vm.Gold = 70;
        await node.DisposeAsync(); vm.Gold = 40;
        await NextUIFrameAsync();
        Check(label.Text == "46" && slider.Value == 70, "Closing removes queued display and immediate input subscriptions");
        await ui.DisposeAsync(); await runtime.DisposeAsync();
        await using (var preview = new EngineMvvm.BindingPreview(config)) { Check(label.Text == "100", "Preview initial value"); Check(slider.Value == 100, "Preview slider value"); button.EmitSignal(BaseButton.SignalName.Pressed); Check(template.Gold == 100, "Preview command changed asset"); }
        Check(label.Text == "46" && slider.Value == 70, "Preview did not restore controls");
        template.Other = null; var path = "user://mvvm-config-verification.tscn";
        using (var packed = new PackedScene()) { Check(packed.Pack(view) == Error.Ok, "Pack view"); Check(ResourceSaver.Save(packed, path) == Error.Ok, "Save configuration"); }
        using var saved = ResourceLoader.Load<PackedScene>(path, cacheMode: ResourceLoader.CacheMode.Ignore); var restored = saved.Instantiate<Control>(); var restoredConfig = restored.GetNode<MvvmBindings>("MvvmBindings");
        Check(restoredConfig.ViewModels.Count == 1 && restoredConfig.Bindings.Count == 3, "Configuration was not serialized");
        Check(restoredConfig.Bindings[0].UpdateTiming == BindingUpdateTiming.EndOfFrame &&
            restoredConfig.Bindings[2].UpdateTiming == BindingUpdateTiming.Immediate, "Binding timing survives scene save and reopen");
        restored.Free(); DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        view.QueueFree(); template.Dispose(); await VerifyCollectionAsync();
    }
    private async Task NextUIFrameAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0), SceneTreeTimer.SignalName.Timeout);
    }
    private async Task VerifySharedFrameAsync(UIContext ui, NativePlayerVm vm)
    {
        await using var shared = EngineUI.UI.Create(this);
        shared.ShareMvvm(ui);
        var owner = new UINode();
        await owner.AttachAsync(shared.Root, "Wallet");
        owner.SetViewModel("Player", vm);
        var paints = new List<int>();
        owner.Bind<int>("Player.Gold", gold =>
        {
            paints.Add(gold);
            if (gold == 45) vm.Gold = 46;
        });
        var tree = GetTree();
        tree.Paused = true;
        try
        {
            // A normal game node can still edit a VM in _Process before the UI frame boundary.
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            int paintsDuringEdit = 0;
            _frameEdit = () =>
            {
                vm.Gold = 42;
                vm.Gold = 45;
                paintsDuringEdit = paints.Count;
            };
            await ToSignal(tree.CreateTimer(0), SceneTreeTimer.SignalName.Timeout);
            Check(paintsDuringEdit == 1, "Notifications during _Process do not run the callback");
            Check(paints.SequenceEqual(new[] { 55, 45 }), "Shared runtime flushes once in a paused frame");
            await NextUIFrameAsync();
            Check(paints.SequenceEqual(new[] { 55, 45, 46 }), "A write from the callback refreshes on the following frame");
        }
        finally { tree.Paused = false; }
    }
    private async Task VerifyCollectionAsync()
    {
        var mount = new Control { Name = "Items" }; AddChild(mount); var ui = EngineUI.UI.Create(this);
        var owner = new UINode(); await owner.AttachToAsync(ui.Root, "Items", mount);
        var player = new ClientShopVm(); owner.SetViewModel("Player", player);
        var template = new CollectionRowNode(); var scene = new PackedScene(); Check(scene.Pack(template) == Error.Ok, "Pack native row"); template.Free();
        var target = new EngineMvvm.CollectionTarget(owner, mount, scene, sources: new[] { new CollectionSourceMapping { ParentSource = "Player", ItemSource = "Player" } });
        owner.BindCollection("Player.Equipment", target); var first = new ClientInventoryItemVm { Id = 1 }; var second = new ClientInventoryItemVm { Id = 2 };
        player.Equipment.Add(first); player.Equipment.Add(second); player.Equipment.Move(0, 1); await ui.WhenMvvmIdleAsync();
        var row = mount.GetChild<CollectionRowNode>(0); Check(ReferenceEquals(row.ItemAtInitialize, second), "Native row keeps pending move order"); Check(ReferenceEquals(row.PlayerAtInitialize, player), "Additional source arrives before initialization"); Check(row.State == NodeState.Ready, "Async row initialized");
        var select = new Button();
        row.AddChild(select);
        int selected = 0;
        second.Open = () => { selected = second.Id; return Task.CompletedTask; };
        row.BindCommand("Item.Select", select.ClickTarget());
        player.Equipment.ApplySnapshot(new[] { 1, 2 }, item => item.Id, id => id,
            id => new ClientInventoryItemVm { Id = id }, (item, id) => item.Title = "Item " + id);
        await ui.WhenMvvmIdleAsync();
        Check(ReferenceEquals(row, mount.GetChild<CollectionRowNode>(1)), "Snapshot keeps the native row");
        select.EmitSignal(BaseButton.SignalName.Pressed);
        await ui.WhenMvvmIdleAsync();
        Check(selected == 2 && second.Title == "Item 2", "Reordered row selects the refreshed item");
        player.Equipment.Remove(second); await ui.WhenMvvmIdleAsync(); Check(mount.GetChildCount() == 1, "Native row removal cleanup");
        player.Equipment.Add(second); await owner.DisposeAsync(); Check(mount.GetChildCount() == 0, "Close cancels pending native row"); await ui.DisposeAsync(); scene.Dispose(); mount.QueueFree();
    }

}
