using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;

namespace HierarchyDemo;
public partial class DemoApp
{
    private async Task RunRetainedPartnerTestsAsync(Action<bool, string> pass)
    {
        await OpenAsync("Inventory");
        var inventory = (InventoryView)Shell.Pages.Current!;
        await PressAsync(inventory.GetControl<Button>("Pages/装备/C0"));
        string itemId = ((ItemDetailsView)Shell.Pages.Current!).Parameters;
        await Shell.Pages.PopAsync();
        var stack = Shell.Pages;
        using var cancel = new CancellationTokenSource();
        ItemDetailsView? canceledDetails = null;
        void Hook(TransitionState state)
        {
            if (state.Stage != TransitionStage.Preparing || state.Context.Target is not ItemDetailsView details)
                return;
            canceledDetails = details;
            details.TransitionChanged += s =>
            {
                if (s.Stage == TransitionStage.Playing && s.Progress > .2f)
                    cancel.Cancel();
            };
        }

        inventory.TransitionChanged += Hook;
        await ExpectCancellationAsync(stack.PushAsync<ItemDetailsView>(itemId, cancellationToken: cancel.Token));
        inventory.TransitionChanged -= Hook;
        pass(canceledDetails!.State == NodeState.Disposed && stack.Current == inventory && inventory.SelectedIcon.SelfModulate.A == 1 && stack.GetRoot().GetNodeOrNull("FlyingItemPortrait") == null, "partner entry cancellation restores retained icon");
        var original = new IOException("Expected partner observer failure.");
        void FailHook(TransitionState state)
        {
            if (state.Stage != TransitionStage.Preparing || state.Context.Target is not ItemDetailsView details)
                return;
            details.TransitionChanged += s => { if (s.Stage == TransitionStage.Playing && s.Progress > .2f) throw original; };
        }
        inventory.TransitionChanged += FailHook;
        try
        {
            await stack.PushAsync<ItemDetailsView>(itemId);
            throw new InvalidOperationException("Expected partner failure.");
        }
        catch (IOException ex)
        {
            pass(ReferenceEquals(ex, original) && stack.Current == inventory && inventory.SelectedIcon.SelfModulate.A == 1 && stack.GetRoot().GetNodeOrNull("FlyingItemPortrait") == null, "partner raw failure restores source and cleans overlay");
        }
        finally
        {
            inventory.TransitionChanged -= FailHook;
        }
        var retained = await stack.PushAsync<ItemDetailsView>(itemId);
        using var cancelReturn = new CancellationTokenSource();
        void CancelReturn(TransitionState state)
        {
            if (state.Stage == TransitionStage.Playing && state.Progress > .2f)
                cancelReturn.Cancel();
        }

        retained.TransitionChanged += CancelReturn;
        await ExpectCancellationAsync(stack.PopAsync(cancellationToken: cancelReturn.Token));
        retained.TransitionChanged -= CancelReturn;
        pass(stack.Current == retained && retained.GetRoot().Modulate.A == 1 && retained.GetControl<TextureRect>("Icon").SelfModulate.A == 1 && stack.GetRoot().GetNodeOrNull("FlyingItemPortrait") == null, "partner return cancellation restores detail");
        await stack.PopAsync(playAnimation: false);
        var instant = await stack.PushAsync<ItemDetailsView>(itemId, playAnimation: false);
        pass(instant.GetControl<TextureRect>("Icon").Texture == inventory.SelectedIcon.Texture && stack.GetRoot().GetNodeOrNull("FlyingItemPortrait") == null, "instant partner prepares texture without overlay");
        await stack.PopAsync(playAnimation: false);
        await OpenAsync("Home");
        var host = new EngineUI.UIHost(GetViewport());
        var loader = new DelayedLoader(new EngineUI.UIResourceLoader(host));
        await using var ui = EngineUI.UI.Create(this, loader: loader);
        ui.Register(() => new UINode(), "res://Scenes/Client/Compare.tscn");
        var windows = await ui.Root.CreateContainerAsync<PanelContainerNode>("LoadingCancellation", GetNode<Control>("FloatingMount"));
        var pending = windows.OpenAsync<UINode>();
        await ui.DisposeAsync();
        await ExpectCancellationAsync(pending);
        pass(loader.InstancesCreated == 0, "context cancellation creates no late native instance");
    }

    private sealed class DelayedLoader(IUIResourceLoader inner) : IUIResourceLoader
    {
        public int InstancesCreated;
        public async Task<IUIResource> LoadAsync(string resource, CancellationToken token)
        {
            await Task.Delay(500, token);
            return new Resource(this, await inner.LoadAsync(resource, token));
        }

        private sealed class Resource(DelayedLoader owner, IUIResource inner) : IUIResource
        {
            public IUIElement Instantiate(IUIElement? mount)
            {
                owner.InstancesCreated++;
                return inner.Instantiate(mount);
            }

            public void Dispose() => inner.Dispose();
        }
    }

    private static async Task ExpectCancellationAsync(Task pending)
    {
        try
        {
            await pending;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException("Expected an explicitly canceled operation.");
    }

    private sealed class CountingLoader(IUIResourceLoader inner) : IUIResourceLoader
    {
        public int Loads, Instances, Releases;
        public async Task<IUIResource> LoadAsync(string resource, CancellationToken token)
        {
            Loads++;
            return new Resource(this, await inner.LoadAsync(resource, token));
        }

        private sealed class Resource(CountingLoader owner, IUIResource inner) : IUIResource
        {
            public IUIElement Instantiate(IUIElement? mount)
            {
                owner.Instances++;
                return inner.Instantiate(mount);
            }

            public void Dispose()
            {
                owner.Releases++;
                inner.Dispose();
            }
        }
    }

    private async Task RunResourceSelfTestAsync()
    {
        var host = new EngineUI.UIHost(GetViewport());
        var loader = new CountingLoader(new EngineUI.UIResourceLoader(host));
        await using var ui = EngineUI.UI.Create(this, loader: loader);
        ui.Register(() => new UINode(), "res://Scenes/Client/Compare.tscn");
        var first = ui.PreloadAsync<UINode>();
        var second = ui.PreloadAsync<UINode>();
        var a = await first;
        var b = await second;
        Check(loader.Loads == 1 && loader.Instances == 0 && ui.Root.Children.Count == 0, "Preload did not share a pure resource acquisition.");
        var panels = await ui.Root.CreateContainerAsync<PanelContainerNode>("ResourceCase", GetNode<Control>("FloatingMount"));
        var window = await panels.OpenAsync<UINode>(playAnimation: false);
        a.Dispose();
        b.Dispose();
        Check(loader.Loads == 1 && loader.Instances == 1 && loader.Releases == 0 && window.GetRoot().IsInsideTree(), "Releasing preload damaged the opened window or reloaded its resource.");
        await panels.CloseAsync(window, playAnimation: false);
        Check(loader.Releases == 1, "Closing the last instance kept its resource hold.");
        var remaining = await ui.PreloadAsync<UINode>();
        await ui.DisposeAsync();
        remaining.Dispose();
        Check(loader.Loads == 2 && loader.Releases == 2 && loader.Instances == 1, "Context retained a preload or instantiated it.");
        GD.Print("PASS resources: threaded acquisition, pure/shared preload, independent instance hold, explicit/context release");
    }

    private async Task RunAdapterSelfTestAsync()
    {
        // Exercise the actual engine API, including borrowed-resource ownership and failures.
        var mount = new Control
        {
            Name = "AdapterTestMount"
        };
        var page = new Control
        {
            Name = "Page"
        };
        AddChild(mount);
        mount.AddChild(page);
        var ui = EngineUI.UI.Create(this, logger: new TestLog());
        try
        {
            Check(ui.Root.Children.Count == 0, "Adapter setup supplied project nodes.");
            var customLoader = new DelayedLoader(new EngineUI.UIResourceLoader((EngineUI.UIHost)ui.Host));
            await using (var custom = EngineUI.UI.Create(this, loader: customLoader, logger: ui.Log))
                Check(ReferenceEquals(custom.Loader, customLoader) && ReferenceEquals(custom.Log, ui.Log), "Custom services were replaced.");
            var tabs = await ui.Root.CreateContainerAsync<TabContainerNode>("Tabs", mount);
            await tabs.BindAsync("Page", page);
            await tabs.SwitchAsync("Page");
            Check(tabs.Current!.GetType() == typeof(UINode), "Control-only binding requires a business View.");
            Check(ReferenceEquals(tabs.Current.GetRoot(), page), "Embedded Control was cloned.");
            Check(ReferenceEquals(tabs.GetControl<Control>("Page"), page), "Typed Control lookup.");
            await VerifyNativeTransitionsAsync(tabs.Current!, page);
            await tabs.DisposeAsync();
            Check(GodotObject.IsInstanceValid(mount) && !mount.IsQueuedForDeletion(), "Borrowed mount was freed.");
            Check(GodotObject.IsInstanceValid(page) && !page.IsQueuedForDeletion(), "Borrowed page was freed.");
            var customNode = new UINode();
            await customNode.AttachToAsync(ui.Root, "Custom", mount);
            Check(ReferenceEquals(ui.Root.GetChild("Custom"), customNode), "Attachment replaced the supplied node.");
            await customNode.DisposeAsync();
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await ExpectCancellationAsync(ui.Root.CreateContainerAsync<PanelContainerNode>("Canceled", mount, canceled.Token));
            Check(ui.Root.Children.Count == 0, "Canceled helper left an attached container.");
            var stack = await ui.Root.CreateContainerAsync<StackContainerNode>("TransitionRollback", mount);
            ui.Register(() => new AdapterFadeNode(), "res://Scenes/Client/Compare.tscn");
            var retained = await stack.PushAsync<AdapterFadeNode>();
            var transitionFailure = new InvalidOperationException("Expected adapter transition rollback failure.");
            ui.Register(() => new UINode { Transition = new AdapterFailingTransition(transitionFailure) }, "res://Scenes/Client/Compare.tscn");
            try
            {
                await stack.PushAsync<UINode>();
                throw new Exception("Transition should have failed.");
            }
            catch (InvalidOperationException ex)
            {
                Check(ReferenceEquals(ex, transitionFailure), "Transition replaced the original exception.");
                Check(ReferenceEquals(stack.Current, retained) && retained.IsVisible && retained.GetRoot().Modulate.A == 1, "Rollback restored visibility but left the previous fade transparent.");
            }

            await stack.DisposeAsync();
            // Godot frees QueueFree-owned scenes at frame end before this borrowed mount is rebound.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var original = new InvalidOperationException("Expected adapter attachment initialization failure.");
            var failing = new AdapterFailureNode(original);
            try
            {
                await failing.AttachToAsync(ui.Root, "Failing", mount);
                throw new Exception("Adapter initialization should have failed.");
            }
            catch (InvalidOperationException ex)
            {
                Check(ReferenceEquals(ex, original), "Adapter replaced the original initialization exception.");
                Check(failing.State == NodeState.Disposed && ui.Root.Children.Count == 0, "Failed attachment was not cleaned.");
                Check(!mount.IsQueuedForDeletion(), "Failed attachment destroyed its borrowed Control.");
            }
        }
        finally
        {
            await ui.DisposeAsync();
            mount.QueueFree();
        }

        GD.Print("GODOT ADAPTER SELF-TEST PASSED (setup, custom services, binding, ownership, cancellation, original failure)");
    }

    private async Task VerifyNativeTransitionsAsync(IUINode node, Control page)
    {
        page.Size = new Vector2(100, 70);
        page.Position = new Vector2(5, 7);
        var tint = new Color(0.7f, 0.8f, 0.9f, 0.6f);
        page.Modulate = tint;
        var position = page.Position;
        var scale = new Vector2(1.1f, 0.9f);
        page.Scale = scale;
        var pivot = new Vector2(3, 4);
        page.PivotOffset = pivot;
        var lifecycle = new LifecycleContext(node, node.Parent as ContainerNode, LifecycleReason.Push, null, node);
        foreach (var direction in new[]
        {
            TransitionDirection.Forward,
            TransitionDirection.Backward
        }

        )
        {
            var run = EngineUI.Transitions.SlideHorizontal(0.02).Create(new(lifecycle, true, direction));
            var playing = run.PlayAsync(CancellationToken.None);
            var sign = direction == TransitionDirection.Forward ? 1 : -1;
            Check(page.Position.IsEqualApprox(position + new Vector2(sign * page.Size.X, 0)), "Slide did not honor the operation direction.");
            await playing;
            run.Restore();
            Check(page.Position.IsEqualApprox(position), "Slide drifted after restoration.");
        }

        var fade = EngineUI.Transitions.Fade(0.02).Create(new(lifecycle, true, TransitionDirection.None));
        await fade.PlayAsync(CancellationToken.None);
        fade.Restore();
        Check(page.Modulate == tint, "Fade overwrote authored tint/alpha.");
        var zoom = EngineUI.Transitions.ScaleFade(0.02).Create(new(lifecycle, true, TransitionDirection.None));
        await zoom.PlayAsync(CancellationToken.None);
        zoom.Restore();
        Check(page.Scale == scale && page.PivotOffset == pivot && page.Modulate == tint, "ScaleFade did not restore authored presentation.");
        using var canceled = new CancellationTokenSource();
        var rollback = EngineUI.Transitions.ScaleFade(0.2).Create(new(lifecycle, false, TransitionDirection.None));
        var pending = rollback.PlayAsync(canceled.Token);
        canceled.Cancel();
        await ExpectCancellationAsync(pending);
        rollback.Restore();
        Check(page.Scale == scale && page.Modulate == tint, "Canceled scale animation leaked state.");
        var player = new AnimationPlayer
        {
            Name = "Animator"
        };
        page.AddChild(player);
        var library = new AnimationLibrary();
        var enterClip = new Animation { Length = 0.02 };
        var alphaTrack = enterClip.AddTrack(Animation.TrackType.Value);
        enterClip.TrackSetPath(alphaTrack, new NodePath(".:modulate"));
        enterClip.TrackInsertKey(alphaTrack, 0, new Color(tint.R, tint.G, tint.B, 0));
        enterClip.TrackInsertKey(alphaTrack, .02, tint);
        library.AddAnimation("enter", enterClip);
        library.AddAnimation("exit", new Animation { Length = 0.02 });
        player.AddAnimationLibrary("", library);
        var restored = false;
        var authored = EngineUI.Transitions.Animation("Animator", "enter", "exit", c =>
        {
            c.Modulate = tint;
            restored = true;
        });
        var animation = authored.Create(new(lifecycle, true, TransitionDirection.None));
        var timelinePlaying = animation.PlayAsync(CancellationToken.None);
        Check(page.Modulate.A == 0, "AnimationPlayer exposed the authored state before sampling its first key.");
        await timelinePlaying;
        animation.Restore();
        Check(restored && !player.IsPlaying(), "AnimationPlayer restoration was not invoked.");
        GD.Print("PASS V4 native animations: slide directions, tint/position/scale restoration, cancellation, AnimationPlayer");
    }

    private sealed class AdapterFadeNode : UINode
    {
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            Transition = EngineUI.Transitions.Fade(0.02);
            return Task.CompletedTask;
        }
    }

    private sealed class AdapterFailingTransition(Exception original) : IUITransition, IUITransitionRun
    {
        public Task PlayAsync(CancellationToken token) => Task.FromException(original);
        public IUITransitionRun Create(TransitionContext context) => this;
        public void Restore()
        {
        }
    }

    private sealed class AdapterFailureNode(Exception original) : UINode
    {
        protected override Task OnInitializeAsync(CancellationToken token) => Task.FromException(original);
    }
}
