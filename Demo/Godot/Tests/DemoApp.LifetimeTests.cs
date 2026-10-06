using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Mvvm;
using UIFramework.Godot;
using UIFramework.Mvvm.Godot;

namespace HierarchyDemo;
public partial class DemoApp
{
    private sealed class LifetimeWindow : UINode { }
    private sealed class LifetimeFailure : UINode
    {
        internal readonly Exception Failure = new IOException("Expected lifetime cleanup failure.");
        protected override Task OnDisposeAsync() => Task.FromException(Failure);
    }
    private sealed class DisconnectingLifetime : UINode
    {
        internal EngineUI.LifetimeBinding Binding = null!;
        protected override Task OnDisposeAsync() { Binding.Dispose(); return Task.CompletedTask; }
    }
    private sealed class LifetimeGate : UINode
    {
        internal readonly TaskCompletionSource<bool> Gate = new();
        protected override Task OnDisposeAsync() => Gate.Task;
    }
    private sealed class LifetimeLog : IUILog
    {
        public readonly List<Exception> Errors = new();
        public void Error(Exception exception, string context) => Errors.Add(exception);
    }
    private async Task RunLifetimeTestsAsync(Action<bool, string> pass)
    {
        // Scene-scoped modals, including every per-modal backdrop, disappear together.
        var scene = new Control { Name = "LifetimeScene" }; AddChild(scene);
        var context = EngineUI.UI.Create(this);
        context.Register(() => new LifetimeWindow(), "res://Scenes/Client/Modal.tscn", WindowInstanceMode.Multiple);
        var dialogs = await context.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", scene);
        var observed = new List<Task>();
        dialogs.ConfigureBackdrop(EngineUI.Backdrops.Dim(observed.Add), ModalBackdropMode.PerModal);
        var first = await dialogs.OpenAsync<LifetimeWindow>(playAnimation: false);
        var second = await dialogs.OpenAsync<LifetimeWindow>(playAnimation: false);
        var lifetime = context.BindLifetime(scene);
        scene.QueueFree(); await lifetime.Completion;
        pass(context.Root.State == NodeState.Disposed && first.State == NodeState.Disposed && second.State == NodeState.Disposed,
            "scene lifetime releases context, nested modals and per-modal backdrops");
        await Task.WhenAll(observed);

        // Actual Demo views own their controls; disposal never re-queries freed widgets.
        scene = new Control(); AddChild(scene); context = EngineUI.UI.Create(this);
        context.Register<CompareView>("res://Scenes/Client/Compare.tscn");
        context.Register<ClientModalView>("res://Scenes/Client/Modal.tscn");
        var panels = await context.Root.CreateContainerAsync<PanelContainerNode>("ActualWindows", scene);
        var compare = await panels.OpenAsync<CompareView>(new CompareArgs("eq0", 0), playAnimation: false);
        var settings = await panels.OpenAsync<ClientModalView>(new ModalArgs("Settings"), playAnimation: false);
        lifetime = context.BindLifetime(scene); scene.Free(); await lifetime.Completion;
        pass(compare.State == NodeState.Disposed && settings.State == NodeState.Disposed, "actual comparison and settings views release after native scene destruction");

        // Inactive owners still have a lifetime. Disconnecting never closes the node.
        var owner = new Node { Name = "InactiveLifetime", ProcessMode = ProcessModeEnum.Disabled }; AddChild(owner);
        context = EngineUI.UI.Create(this);
        var node = new UINode(); await node.AttachAsync(context.Root, "Scope");
        var child = new UINode(); await child.AttachAsync(node, "Child");
        lifetime = node.BindLifetime(owner); owner.Free(); await lifetime.Completion;
        pass(node.State == NodeState.Disposed && child.State == NodeState.Disposed && context.Root.State == NodeState.Ready,
            "inactive owner closes only its bound subtree");
        owner = new Node(); AddChild(owner);
        node = new UINode(); await node.AttachAsync(context.Root, "Disconnected");
        lifetime = node.BindLifetime(owner); lifetime.Dispose(); owner.Free(); await lifetime.Completion;
        pass(node.State == NodeState.Ready, "disconnect preserves node");
        await context.DisposeAsync();

        // Normal node disposal removes a scene binding without destroying its supplied owner.
        owner = new Node(); AddChild(owner); context = EngineUI.UI.Create(this);
        node = new UINode(); await node.AttachAsync(context.Root, "Explicit");
        lifetime = node.BindLifetime(owner); await node.DisposeAsync(); await lifetime.Completion;
        pass(GodotObject.IsInstanceValid(owner), "normal disposal preserves supplied engine owner");
        owner.Free(); await context.DisposeAsync();

        owner = new Node(); AddChild(owner); context = EngineUI.UI.Create(this);
        var gated = new LifetimeGate(); await gated.AttachAsync(context.Root, "AsyncCleanup");
        lifetime = gated.BindLifetime(owner); owner.Free();
        while (gated.State != NodeState.Disposing) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        pass(!lifetime.Completion.IsCompleted, "binding waits for async cleanup after native owner disappears");
        gated.Gate.SetResult(true); await lifetime.Completion; await context.DisposeAsync();

        owner = new Node(); AddChild(owner); context = EngineUI.UI.Create(this);
        var rootBinding = context.BindLifetime(owner);
        var nested = new DisconnectingLifetime(); await nested.AttachAsync(context.Root, "Nested"); nested.Binding = nested.BindLifetime(owner);
        owner.Free(); await rootBinding.Completion; await nested.Binding.Completion;
        pass(nested.State == NodeState.Disposed, "overlapping bindings may disconnect during ancestor cleanup");

        foreach (bool timeline in new[] { false, true })
        {
            scene = new Control(); AddChild(scene); context = EngineUI.UI.Create(this);
            var tabs = await context.Root.CreateContainerAsync<TabContainerNode>("Animated", scene);
            var page = new Control(); scene.AddChild(page);
            using var animation = timeline ? new Animation { Length = .5 } : null;
            using var library = timeline ? new AnimationLibrary() : null;
            if (timeline)
            {
                var player = new AnimationPlayer { Name = "AnimationPlayer" }; page.AddChild(player);
                int track = animation!.AddTrack(Animation.TrackType.Value);
                animation.TrackSetPath(track, ".:modulate");
                animation.TrackInsertKey(track, 0, Colors.Transparent); animation.TrackInsertKey(track, .5, Colors.White);
                library!.AddAnimation("enter", animation); library.AddAnimation("exit", animation);
                player.AddAnimationLibrary("", library);
            }
            node = new UINode { Transition = timeline
                ? EngineUI.Transitions.Animation("AnimationPlayer", "enter", "exit", _ => throw new Exception("Destroyed timeline must not be restored."))
                : EngineUI.Transitions.Fade(.5) };
            await tabs.BindAsync("Page", node, page);
            lifetime = tabs.BindLifetime(scene);
            var pending = tabs.SwitchAsync("Page"); scene.Free();
            await ExpectCancellationAsync(pending); await lifetime.Completion;
            pass(tabs.State == NodeState.Disposed && node.State == NodeState.Disposed,
                timeline ? "timeline destruction cancels playback and releases subtree" : "fade destruction cancels playback and releases subtree");
            await context.DisposeAsync();
        }

        foreach (bool cinematic in new[] { false, true })
        {
            scene = new Control(); AddChild(scene); context = EngineUI.UI.Create(this);
            IUINode? created = null;
            if (cinematic) context.Register<InventoryView>("res://Scenes/Client/Inventory.tscn", view => created = view);
            else context.Register<ClientModalView>("res://Scenes/Client/Modal.tscn", view => created = view);
            var windows = await context.Root.CreateContainerAsync<PanelContainerNode>("ProjectAnimation", scene);
            lifetime = context.BindLifetime(scene);
            Task pending = cinematic ? windows.OpenAsync<InventoryView>() : windows.OpenAsync<ClientModalView>(new ModalArgs("Mail"));
            while (created?.TransitionState?.Stage != TransitionStage.Playing) await ((EngineUI.UIHost)context.Host).NextFrameAsync();
            var material = cinematic ? null : created!.GetControl<Control>("Center").Material;
            scene.Free(); await ExpectCancellationAsync(pending); await lifetime.Completion;
            pass(created.State == NodeState.Disposed && (material == null || !GodotObject.IsInstanceValid(material)), cinematic ? "project cinematic closes with destroyed scene" : "project shader reveal releases material after destroyed scene");
        }

        // The persistent observer reports failure; callers retain the very same exception.
        owner = new Node(); AddChild(owner); var failureLog = new LifetimeLog(); context = EngineUI.UI.Create(this, logger: failureLog);
        var failing = new LifetimeFailure(); await failing.AttachAsync(context.Root, "Failure");
        lifetime = failing.BindLifetime(owner); owner.Free();
        try { await lifetime.Completion; throw new Exception("Expected cleanup failure."); }
        catch (IOException error) { pass(ReferenceEquals(error, failing.Failure) && failureLog.Errors.Contains(error), "bound cleanup preserves original exception"); }
        await context.DisposeAsync();

        // Without an explicit lifetime binding, premature display destruction still fails.
        scene = new Control(); AddChild(scene); var log = new LifetimeLog(); context = EngineUI.UI.Create(this, logger: log);
        node = new UINode(); await node.AttachToAsync(context.Root, "Unbound", scene); scene.Free();
        try { await node.DisposeAsync(); throw new Exception("Expected missing display error."); }
        catch (ObjectDisposedException error) { pass(log.Errors.Contains(error), "unbound destroyed display preserves original failure"); }
        await context.DisposeAsync();
    }
}
