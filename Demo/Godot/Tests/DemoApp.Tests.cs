using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Generated;

namespace HierarchyDemo;

public partial class DemoApp
{
    public async Task CancelLoadingAsync()
    {
        // Cancellation is a normal, explicitly requested test outcome, not error suppression.
        var host = new GodotUIHost(GetViewport());
        var loading = new DelayedLoader(new GodotSceneLoader(host));
        var ui = GodotUI.Create(this, loader: loading);
        ui.Register<CardView>("res://Scenes/Inventory/Card.tscn");
        var panels = await ui.Root.CreateContainerAsync<PanelContainerNode>("CancellationCase", GetNode<Control>("FloatingMount"));
        var pending = panels.OpenAsync<CardView>(new CardArgs("CANCELED", Vector2.Zero));
        await ui.DisposeAsync();
        await ExpectCancellationAsync(pending);
        Check(loading.InstancesCreated == 0, "Cancellation created a late scene instance.");
        Status("CANCELLATION / Scope disposed. Pending load canceled. No late scene created.");
    }

    private sealed class DelayedLoader(IUIResourceLoader inner) : IUIResourceLoader
    {
        public int InstancesCreated;
        public async Task<IUIElement> LoadAsync(string resource, IUIElement? mount, CancellationToken token)
        {
            await Task.Delay(500, token);
            var display = await inner.LoadAsync(resource, mount, token); InstancesCreated++; return display;
        }
    }

    private static async Task ExpectCancellationAsync(Task pending)
    {
        try { await pending; }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Expected an explicitly canceled operation.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Demo validation failed: {message}");
    }

    private async Task RunSelfTestAsync()
    {
        await RunAdapterSelfTestAsync();
        Check(_ui.Root.Children.Count == 4, "Only project-declared mounts should exist.");
        await OpenPreferencesAsync();
        var tabs = (PreferencesView)_scenario!;
        var original = tabs.Tabs.Items["Audio"].Element;
        var childCount = tabs.Display.Get<Control>("TabContent").GetChildCount();
        var slider = tabs.Display.Get<HSlider>("TabContent/Graphics/Value");
        slider.Value = 82;
        await tabs.SelectAsync("Audio"); await tabs.SelectAsync("Graphics");
        Check(slider.Value == 82, "Embedded tab state was lost.");
        Check(ReferenceEquals(original, tabs.Tabs.Items["Audio"].Element), "Tab instance was cloned.");
        Check(tabs.Display.Get<Control>("TabContent").GetChildCount() == childCount, "Extra tab scene created.");
        Check(tabs.Tabs.Items["Graphics"].IsInteractive && !tabs.Tabs.Items["Audio"].IsInteractive, "Tab input state.");
        GD.Print("PASS embedded tabs: names, basic nodes, state, no clones");

        await OpenInventoryAsync();
        var inventory = (InventoryView)_inventoryStack!.Current!;
        await inventory.SelectAsync("Materials");
        var inspection = inventory.PreviewSeconds;
        await inventory.InspectAsync("Moonstone"); inspection = inventory.PreviewSeconds; inventory.Tick(1);
        Check(inventory.PreviewSeconds == inspection && !inventory.Tabs.Current!.IsActive, "Hidden inventory preview did not pause.");
        var details = (DetailsView)_inventoryStack.Current!;
        Check(details.GetControl<Label>("Title").Text == "MOONSTONE", "Selected item did not reach details.");
        details.GetControl<CheckButton>("Enabled").ButtonPressed = false;
        await _inventoryStack.PopAsync(); inventory.Tick(1);
        Check(!inventory.Favorites.Contains("Moonstone"), "Favorite edit was not retained by the project model.");
        inventory.GetControl<CheckButton>("TabContent/Materials/Enabled").ButtonPressed = true;
        Check(!inventory.GetControl<Button>("TabContent/Materials/Items/Item0").Visible, "Favorites filter did not apply.");
        inventory.GetControl<CheckButton>("TabContent/Materials/Enabled").ButtonPressed = false;
        Check(inventory.IsActive && inventory.PreviewSeconds > inspection, "Return lifecycle did not resume inventory.");
        Check(ReferenceEquals(inventory, _inventoryStack.Current) && inventory.Tabs.CurrentName == "Materials", "Stack/Tab restoration.");
        GD.Print("PASS stack + embedded tabs: original inventory restored");

        var inventoryWindow = (InventoryWindowView)_scenario!;
        var originalDisplay = inventoryWindow.GetControl();
        var windowTransition = inventoryWindow.TransitionState;
        await OpenInventoryAsync();
        Check(ReferenceEquals(inventoryWindow, _scenario) && ReferenceEquals(inventoryWindow.TransitionState, windowTransition),
            "Repeated backpack opening replayed entry or created another window.");
        await OpenHomeAsync(); inspection = inventory.PreviewSeconds; inventory.Tick(1);
        Check(!inventoryWindow.IsVisible && inventory.PreviewSeconds == inspection, "Stowed backpack kept updating.");
        Check(_stage.TryGet<InventoryWindowView>(out var hidden) && ReferenceEquals(hidden, inventoryWindow), "Hidden single window was lost.");
        await OpenInventoryAsync();
        Check(ReferenceEquals(originalDisplay, inventoryWindow.GetControl()) && ReferenceEquals(inventoryWindow.Inventory, inventory),
            "Restoring backpack loaded another scene or initialized again.");
        Check(ReferenceEquals(inventory, _inventoryStack.Current) && inventory.Tabs.CurrentName == "Materials",
            "Window restoration lost inner stack or tab.");
        await _stage.HideAsync(inventoryWindow); await _stage.ShowAsync(inventoryWindow);
        Check(inventory.IsActive, "Explicit Show did not reactivate backpack subtree.");
        GD.Print("PASS V3 single window: repeated open, retained hide/show, subtree pause/resume");

        await inventory.InspectAsync("Moonstone");
        var skippedDetails = (DetailsView)_inventoryStack.Current!;
        var notes = await _inventoryStack.PushProvenanceViewAsync();
        var detailsTransition = skippedDetails.TransitionState;
        await OpenHomeAsync(); await OpenInventoryAsync();
        Check(ReferenceEquals(notes, _inventoryStack.Current), "Stowing a nested page lost its history.");
        await _inventoryStack.PopToAsync(inventory);
        Check(ReferenceEquals(inventory, _inventoryStack.Current) && _inventoryStack.History.Count == 1,
            "Target return did not commit the original backpack.");
        Check(ReferenceEquals(skippedDetails.TransitionState, detailsTransition) && skippedDetails.State == NodeState.Disposed && notes.State == NodeState.Disposed,
            "Target return resumed an intermediate page or left it owned.");
        Check(inventory.SelectedItem == "Moonstone" && inventory.Tabs.CurrentName == "Materials", "Target return lost game UI state.");
        GD.Print("PASS V3 target return: field notes -> original backpack, intermediate pages released");

        var settingsButton = GetNode<Button>("Frame/Layout/Workspace/Sidebar/Settings");
        settingsButton.GrabFocus();
        var settings = await Modals.OpenSettingsWindowViewAsync();
        Check(settings.Display.Control.IsAncestorOf(GetViewport().GuiGetFocusOwner()), "Modal did not receive focus after input unlock.");
        Check(!_inventoryStack.IsInteractive, "Modal must block underlying input.");
        inspection = inventory.PreviewSeconds; inventory.Tick(1);
        Check(inventory.PreviewSeconds == inspection && inventory.IsVisible && !inventory.IsActive, "Modal cover must pause a visible inventory.");
        Check(!_menu.IsInteractive && settingsButton.FocusMode == FocusModeEnum.None, "Modal must block sidebar keyboard navigation.");
        var firstPage = settings.Body.Current;
        var graphics = await settings.Body.PushGraphicsViewAsync();
        graphics.GetControl<HSlider>("Value").Value = 74;
        await settings.Body.PopAsync();
        graphics = await settings.Body.PushGraphicsViewAsync();
        Check(graphics.GetControl<HSlider>("Value").Value == 74, "Project preferences did not survive recreated graphics page.");
        await settings.Body.PopAsync();
        Check(ReferenceEquals(firstPage, settings.Body.Current), "Settings page restoration.");
        Check(firstPage!.GetControl<Label>("Description").Text.StartsWith("Welcome back."), "Settings return did not refresh its business description.");
        Check(Modals.CurrentBackdrop != null, "Managed backdrop is missing.");
        await Modals.CloseAsync(settings);
        Check(_inventoryStack.IsInteractive, "Input was not restored.");
        Check(_menu.IsInteractive, "Sidebar input was not restored.");
        Check(GetViewport().GuiGetFocusOwner() == settingsButton, "Focus was not restored.");
        GD.Print("PASS settings modal: single shell, page return, focus");

        await OpenCardsAsync();
        var card1 = _cards.Children[0]; var card2 = _cards.Children[1];
        await _cards.CloseAsync(card1);
        Check(card2.State == NodeState.Ready && card2.IsVisible, "Independent close affected other card.");
        await _cards.CloseAsync(card2);
        GD.Print("PASS independent instances");

        var outerResult = Modals.ShowDialogAsync<ConfirmView, bool>();
        await WaitUntilAsync(() => Modals.Current is ConfirmView view && view.IsInteractive);
        var outer = (ConfirmView)Modals.Current!;
        var sharedBackdrop = Modals.CurrentBackdrop!;
        var outerFocus = GetViewport().GuiGetFocusOwner();
        var innerResult = Modals.ShowDialogAsync<ConfirmView, bool>();
        await WaitUntilAsync(() => Modals.Current is ConfirmView view && view != outer && view.IsInteractive);
        var inner = (ConfirmView)Modals.Current!;
        Check(ReferenceEquals(sharedBackdrop, Modals.CurrentBackdrop), "Nested modal created a second shared backdrop.");
        var dim = GetNode<Control>("ModalMount/ModalBackdrop");
        Check(dim.GetIndex() < inner.GetControl().GetIndex() && dim.GetIndex() > outer.GetControl().GetIndex(),
            "Shared backdrop must sit between the outer and inner dialogs.");
        Check(!outer.IsInteractive, "Nested modal did not block its parent.");
        dim.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });
        Check(!await innerResult, "Backdrop click must yield the normal false result.");
        Check(outer.IsInteractive, "Outer modal did not regain input.");
        Check(GetViewport().GuiGetFocusOwner() == outerFocus, "Nested modal focus was not restored.");
        outer.Complete(true); Check(await outerResult, "Outer result.");
        GD.Print("PASS nested modal results");
        Check(Modals.CurrentBackdrop == null, "Last dialog left a backdrop behind.");
        GD.Print("PASS V4 shared backdrop: one display, ordering, native click cancellation, final release");

        var clickSettings = await Modals.OpenSettingsWindowViewAsync();
        Check(await Modals.RequestDismissAsync(), "Settings did not handle its external click request.");
        Check(clickSettings.State == NodeState.Disposed && Modals.Current == null, "External click did not close settings.");
        GD.Print("PASS V4 settings external dismissal");

        Modals.ConfigureBackdrop(GodotBackdrops.Dim(Track, duration: 0.02), ModalBackdropMode.PerModal);
        var layeredOuter = await Modals.OpenAsync<ConfirmView>(); var outerBackdrop = Modals.CurrentBackdrop;
        var layeredInner = await Modals.OpenAsync<ConfirmView>(); var innerBackdrop = Modals.CurrentBackdrop;
        Check(!ReferenceEquals(outerBackdrop, innerBackdrop), "PerModal reused one backdrop.");
        var liveDims = GetNode<Control>("ModalMount").GetChildren().OfType<ColorRect>().Where(c => !c.IsQueuedForDeletion()).ToArray();
        Check(liveDims.Length == 2 && liveDims.All(c => c.Visible), "PerModal must own two visible dim layers.");
        Check((int)outerBackdrop!.CaptureOrder() < layeredOuter.GetControl().GetIndex()
            && layeredOuter.GetControl().GetIndex() < (int)innerBackdrop!.CaptureOrder()
            && (int)innerBackdrop.CaptureOrder() < layeredInner.GetControl().GetIndex(), "PerModal layer order is incorrect.");
        await Modals.CloseAsync(layeredInner); Check(ReferenceEquals(Modals.CurrentBackdrop, outerBackdrop) && layeredOuter.IsInteractive, "PerModal did not restore outer input/backdrop.");
        await layeredOuter.DisposeAsync(); Check(Modals.CurrentBackdrop == null && _menu.IsInteractive, "Forced modal disposal left its backdrop/input owner.");
        Modals.ConfigureBackdrop(GodotBackdrops.Dim(Track), ModalBackdropMode.Shared);
        GD.Print("PASS V4 layered backdrops: distinct displays, ordering, nested close and forced release");

        await OpenHomeAsync(); await _stage.CloseAsync(inventoryWindow);
        Check(inventoryWindow.State == NodeState.Disposed && inventory.State == NodeState.Disposed,
            "Closing hidden backpack did not release its subtree.");
        Check(!_stage.TryGet<InventoryWindowView>(out _), "Closed backpack remained in the instance index.");
        await OpenInventoryAsync();
        Check(!ReferenceEquals(inventoryWindow, _scenario) && ((InventoryWindowView)_scenario!).Body.Current is InventoryView,
            "Reopening a closed backpack reused a disposed instance.");
        GD.Print("PASS V3 hidden close: subtree released, next open creates fresh window");

        await RunTransitionSelectionSelfTestAsync();
        await RunAnimationCapabilitySelfTestAsync();
        await RunPartnerSelfTestAsync();
        var runtimeError = new IOException("Expected original async Demo error.");
        Track(Task.FromException(runtimeError));
        var observed = false;
        try { _Process(0); }
        catch (IOException ex) { Check(ReferenceEquals(ex, runtimeError), "Demo replaced the task error."); observed = true; }
        Check(observed, "Demo discarded an asynchronous failure.");
        GD.Print("PASS Demo task observation: original exception escapes the frame entry without manual handling");
        await CancelLoadingAsync();
        await _ui.DisposeAsync();
        Check(_ui.Root.Children.Count == 0, "Root cleanup left children.");
        GD.Print("GODOT DEMO SELF-TEST PASSED (22 scenarios)");
        _exiting = true;
        GetTree().Quit(0);
    }

    private async Task RunTransitionSelectionSelfTestAsync()
    {
        await OpenPreferencesAsync(); var preferences = (PreferencesView)_scenario!;
        var contexts = new List<TransitionContext>(); var tabRecipe = preferences.Tabs.ChildTransition!;
        preferences.Tabs.ChildTransition = Transitions.Select(c => { contexts.Add(c); return tabRecipe; });
        await preferences.SelectAsync("Audio", playAnimation: false);
        Check(contexts.Count == 0 && preferences.Tabs.CurrentName == "Audio", "Instant Tab switch invoked selection or failed to commit.");
        await preferences.SelectAsync("Graphics");
        Check(contexts.Select(c => c.Phase).SequenceEqual(new[] { TransitionPhase.Exit, TransitionPhase.Enter })
            && contexts.All(c => c.Source?.Name == "Audio" && c.Target?.Name == "Graphics"), "Tab selection context lost phases or names.");
        GD.Print("PASS V5 preferences: initial/instant pages, name-based selection, normal transitions");

        await OpenInventoryAsync(); var stack = ((InventoryWindowView)_scenario!).Body; var inventory = (InventoryView)stack.Current!;
        await inventory.SelectAsync("Materials");
        contexts.Clear(); var stackRecipe = stack.ChildTransition!;
        stack.ChildTransition = Transitions.Select(c => { contexts.Add(c); return stackRecipe; });
        await inventory.InspectAsync("Moonstone"); var details = stack.Current!;
        Check(contexts.Select(c => c.Phase).SequenceEqual(new[] { TransitionPhase.PushExit, TransitionPhase.PushEnter })
            && contexts.All(c => ReferenceEquals(c.Source, inventory) && ReferenceEquals(c.Target, details)), "Stack selection did not receive source/target phases.");
        contexts.Clear(); await stack.PushProvenanceViewAsync(); var notes = stack.Current!;
        Check(contexts.All(c => c.Source is DetailsView && c.Target is ProvenanceView), "Field note transition lost its business types.");
        contexts.Clear(); await stack.PopAsync();
        Check(contexts.Select(c => c.Phase).SequenceEqual(new[] { TransitionPhase.PopExit, TransitionPhase.PopEnter }), "Return selection phases.");
        contexts.Clear(); await stack.PopToAsync(inventory, playAnimation: false);
        Check(contexts.Count == 0 && ReferenceEquals(stack.Current, inventory) && details.State == NodeState.Disposed && notes.State == NodeState.Disposed,
            "Instant return selected animation or failed to release intermediate pages.");
        GD.Print("PASS V5 backpack: phase/type selection, reverse return, instant return cleanup");

        foreach (var mode in new[] { ModalBackdropMode.Shared, ModalBackdropMode.PerModal })
        {
            Modals.ConfigureBackdrop(GodotBackdrops.Dim(Track, duration: 2), mode);
            var opening = Modals.OpenSettingsWindowViewAsync(playAnimation: false);
            Check(opening.IsCompletedSuccessfully, "Instant modal waited for its node or dim animation."); var window = await opening;
            var dim = window.GetControl().GetParent().GetChildren().OfType<ColorRect>().Single(c => !c.IsQueuedForDeletion());
            Check(dim.Visible && dim.Modulate.A == 1 && Modals.Current == window && window.IsInteractive, "Instant backdrop/input final state.");
            var closing = Modals.CloseAsync(window, playAnimation: false); Check(closing.IsCompletedSuccessfully, "Instant close waited for animation.");
            await closing; Check(Modals.CurrentBackdrop == null && window.State == NodeState.Disposed && _menu.IsInteractive, "Instant modal cleanup or input restoration.");
        }
        Modals.ConfigureBackdrop(GodotBackdrops.Dim(Track));
        var dialog = Modals.ShowConfirmViewAsync(playAnimation: false); ((ConfirmView)Modals.Current!).Complete(false);
        Check(!await dialog && Modals.Current == null, "Generated instant dialog failed to return the explicit cancel result.");
        GD.Print("PASS V5 modal: instant shared/per-modal dim, lifecycle/input/cleanup, generated dialog result");
    }

    private async Task RunAnimationCapabilitySelfTestAsync()
    {
        await OpenPreferencesAsync(); var preferences = (PreferencesView)_scenario!;
        var audio = preferences.Tabs.Items["Audio"]; var control = audio.GetControl();
        var position = control.Position; var scale = control.Scale; var pivot = control.PivotOffset; var tint = control.Modulate;
        var neutral = new GodotTransitionFrame();
        var start = neutral with { Offset = new Vector2(0, 20), Scale = Vector2.One * .9f, Alpha = .2f };
        audio.Transition = GodotTransitions.Simple(start, neutral, duration: .4, delay: .2, transition: Tween.TransitionType.Linear);
        var states = new List<TransitionState>(); audio.TransitionChanged += states.Add;
        var switching = preferences.SelectAsync("Audio");
        Check(audio.IsTransitioning && audio.CurrentTransitionPhase == TransitionPhase.Enter
            && control.Position.IsEqualApprox(position + start.Offset) && control.Scale.IsEqualApprox(scale * .9f)
            && Mathf.IsEqualApprox(control.Modulate.A, tint.A * .2f), "Simple initial pose or public state.");
        await WaitUntilAsync(() => audio.TransitionProgress > 0);
        var active = Mathf.Clamp((audio.TransitionProgress * .6f - .2f) / .4f, 0, 1);
        Check(control.Position.IsEqualApprox(position + start.Offset * (1 - active))
            && control.Scale.IsEqualApprox(scale * Mathf.Lerp(.9f, 1, active))
            && Mathf.IsEqualApprox(control.Modulate.A, tint.A * Mathf.Lerp(.2f, 1, active)), "Delay/time/linear interpolation mismatch.");
        await switching;
        Check(!audio.IsTransitioning && audio.TransitionState!.Stage == TransitionStage.Completed && audio.TransitionProgress == 1
            && control.Position == position && control.Scale == scale && control.PivotOffset == pivot && control.Modulate == tint,
            "Simple animation did not restore authored presentation.");
        Check(states.Any(s => s.Stage == TransitionStage.Preparing) && states.Count(s => s.Stage == TransitionStage.Playing) > 2,
            "Animation state did not publish intermediate progress.");
        audio.TransitionChanged -= states.Add; audio.Transition = null;
        GD.Print("PASS animation parameters: delay, pixel offset, scale/alpha, linear progress and restoration");

        var frame = neutral with { RelativeOffset = new Vector2(-1, 1), Offset = new Vector2(3, -4), Scale = new Vector2(.8f, -1.1f), Alpha = .4f };
        var lifecycle = new LifecycleContext(audio, preferences.Tabs, LifecycleReason.Switch, null, audio);
        var instant = GodotTransitions.Simple(neutral, frame, duration: 0).Create(new(lifecycle, true, TransitionDirection.None));
        await instant.PlayAsync(CancellationToken.None);
        Check(control.Position.IsEqualApprox(position + frame.RelativeOffset * control.Size + frame.Offset)
            && control.Scale.IsEqualApprox(scale * frame.Scale) && Mathf.IsEqualApprox(control.Modulate.A, tint.A * frame.Alpha),
            "Simple final relative pose.");
        instant.Restore(); Check(control.Position == position && control.Scale == scale && control.Modulate == tint, "Simple endpoint restoration.");
        var delayed = GodotTransitions.Simple(neutral, frame, duration: 0, delay: .10).Create(new(lifecycle, true, TransitionDirection.None));
        var delayedPlay = delayed.PlayAsync(CancellationToken.None);
        Check(!delayedPlay.IsCompleted && control.Position == position, "Zero-duration animation ignored its explicit delay.");
        await delayedPlay; delayed.Restore();
        void Reject<T>(Action configure) where T : Exception
        {
            try { configure(); }
            catch (T) { return; } // The exact expected configuration error is asserted; the adapter already logs it.
            throw new InvalidOperationException($"Expected {typeof(T).Name} for invalid animation configuration.");
        }
        Reject<ArgumentOutOfRangeException>(() => GodotTransitions.Fade(duration: -1));
        Reject<ArgumentOutOfRangeException>(() => GodotTransitions.Fade(delay: double.NaN));
        Reject<ArgumentOutOfRangeException>(() => GodotTransitions.Fade(transition: (Tween.TransitionType)999));
        Reject<ArgumentNullException>(() => GodotTransitions.Simple(null!, neutral));
        Reject<ArgumentOutOfRangeException>(() => GodotTransitions.Simple(neutral with { RelativeOffset = new Vector2(float.PositiveInfinity, 0) }, neutral));
        Reject<ArgumentOutOfRangeException>(() => GodotTransitions.Simple(neutral with { Scale = new Vector2(float.NaN, 1) }, neutral));
        var easingChecks = 0;
        var eased = GodotTransitions.Simple(neutral with { Offset = new Vector2(100, 0) }, neutral,
            duration: .2, transition: Tween.TransitionType.Quart, ease: Tween.EaseType.Out);
        audio.Transition = eased;
        void VerifyEase(TransitionState s)
        {
            if (s.Stage != TransitionStage.Playing || s.Progress <= 0 || s.Progress >= 1) return;
            var factor = 1 - Mathf.Pow(1 - s.Progress, 4);
            // Native easing uses double time; public progress is float. Allow sub-pixel rounding, not a different curve.
            Check(control.Position.DistanceTo(position + new Vector2(100 * (1 - factor), 0)) < .001f,
                $"Selected easing mismatch: progress={s.Progress}, actual={control.Position}, expected={position + new Vector2(100 * (1 - factor), 0)}.");
            easingChecks++;
        }
        audio.TransitionChanged += VerifyEase; await preferences.SelectAsync("Graphics");
        audio.TransitionChanged -= VerifyEase; audio.Transition = null; Check(easingChecks > 0, "No native intermediate easing samples.");
        GD.Print("PASS animation frames: relative endpoints, zero duration, Quart Out easing and independent runs");

        var graphics = preferences.Tabs.Items["Graphics"]; var player = new AnimationPlayer { Name = "CapabilityAnimator" };
        graphics.GetControl().AddChild(player); var library = new AnimationLibrary();
        library.AddAnimation("enter", new Animation { Length = .12 }); library.AddAnimation("exit", new Animation { Length = .12 });
        player.AddAnimationLibrary("", library);
        graphics.Transition = GodotTransitions.Animation("CapabilityAnimator", "enter", "exit", _ => { });
        var clipStates = new List<TransitionState>(); graphics.TransitionChanged += clipStates.Add;
        await preferences.SelectAsync("Audio");
        Check(clipStates.Any(s => s.Stage == TransitionStage.Playing && s.Progress > 0 && s.Progress < 1)
            && graphics.TransitionState!.Stage == TransitionStage.Completed && !player.IsPlaying(), "AnimationPlayer progress/completion.");
        graphics.TransitionChanged -= clipStates.Add; graphics.Transition = null; player.QueueFree();
        var graphicsControl = graphics.GetControl(); var graphicsPosition = graphicsControl.Position;
        graphics.Transition = GodotTransitions.Simple(neutral with { Offset = new Vector2(0, 30), Alpha = 0 }, neutral, duration: .2, delay: 1);
        using (var cancel = new CancellationTokenSource())
        {
            var pending = preferences.SelectAsync("Graphics", token: cancel.Token); cancel.Cancel();
            await ExpectCancellationAsync(pending);
            Check(graphics.TransitionState!.Stage == TransitionStage.Canceled && !graphics.IsTransitioning
                && preferences.Tabs.CurrentName == "Audio" && audio.IsInteractive && graphicsControl.Position == graphicsPosition,
                "Canceled delayed animation leaked state, position or changed selection.");
        }
        graphics.Transition = null;
        GD.Print("PASS animation clips: native AnimationPlayer progress and observed completion");

        await OpenInventoryAsync(); var stack = ((InventoryWindowView)_scenario!).Body;
        var details = await stack.PushDetailsViewAsync(); var sorted = false;
        void VerifyOrder(TransitionState s)
        {
            if (s.Stage != TransitionStage.Playing || s.Context.Target is not ProvenanceView notes) return;
            Check(details.GetControl().GetIndex() > notes.GetControl().GetIndex(), "Detail page did not remain above entering field notes.");
            sorted = true;
        }
        details.TransitionChanged += VerifyOrder; var notesPage = await stack.PushProvenanceViewAsync();
        details.TransitionChanged -= VerifyOrder;
        Check(sorted && notesPage.GetControl().GetIndex() > details.GetControl().GetIndex(), "Temporary drawing order was not restored.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var noteControl = notesPage.GetControl(); var noteMount = noteControl.GetParent<Control>();
        Check(noteControl.Visible && noteControl.Modulate.A == 1 && noteControl.Position.IsEqualApprox(Vector2.Zero)
            && noteControl.Size.IsEqualApprox(noteMount.Size), "Anchored field notes drifted or expanded after animation.");
        Check(Mathf.IsEqualApprox(((InventoryWindowView)_scenario!).GetControl<Label>("Actions/Hint").Modulate.A, 1), "Exterior caption did not restore after progress animation.");
        await stack.PopToAsync(stack.History[0]);
        GD.Print("PASS animation drawing: detail reveals notes beneath, sibling and exterior caption restoration");
    }

    private async Task RunPartnerSelfTestAsync()
    {
        await OpenInventoryAsync();
        var stack = ((InventoryWindowView)_scenario!).Body;
        var inventory = (InventoryView)stack.Current!;
        var mount = stack.GetControl();
        foreach (var pair in new[] { ("Equipment", "Oakheart Bow"), ("Materials", "Iron Ore") })
        {
            await inventory.SelectAsync(pair.Item1);
            var samples = 0;
            void Verify(TransitionState state)
            {
                if (state.Stage != TransitionStage.Playing || state.Progress <= 0 || state.Progress >= 1) return;
                var details = (DetailsView)state.Context.Node;
                var small = inventory.SelectedIcon; var large = details.GetControl<TextureRect>("Artwork/Icon");
                var flying = mount.GetNode<TextureRect>("FlyingItemPortrait");
                Rect2 Rect(Control c)
                {
                    var t = mount.GetGlobalTransformWithCanvas().AffineInverse() * c.GetGlobalTransformWithCanvas();
                    return new Rect2(t * Vector2.Zero, t * c.Size - t * Vector2.Zero);
                }
                var start = Rect(state.Context.Entering ? small : large);
                var end = Rect(state.Context.Entering ? large : small);
                var eased = 1 - Mathf.Pow(1 - state.Progress, 3);
                Check(flying.Position.DistanceTo(start.Position.Lerp(end.Position, eased)) < .01f &&
                    flying.Size.DistanceTo(start.Size.Lerp(end.Size, eased)) < .01f, "Partner image did not interpolate the actual rectangles.");
                Check(small.SelfModulate.A == 0 && large.SelfModulate.A == 0 && ReferenceEquals(flying.Texture, small.Texture),
                    "Partner animation duplicated a portrait or changed its texture.");
                Check(flying.MouseFilter == MouseFilterEnum.Ignore && mount.GetChildren().Count(n => n.Name == "FlyingItemPortrait") == 1,
                    "Flying portrait intercepted input or was duplicated.");
                samples++;
            }
            void Hook(TransitionState state)
            {
                if (state.Stage == TransitionStage.Preparing && state.Context.Target is DetailsView details)
                    details.TransitionChanged += Verify;
            }
            inventory.TransitionChanged += Hook;
            await inventory.InspectAsync(pair.Item2);
            inventory.TransitionChanged -= Hook;
            var current = (DetailsView)stack.Current!;
            Check(samples > 0 && current.GetControl<TextureRect>("Artwork/Icon").SelfModulate.A == 1 &&
                inventory.SelectedIcon.SelfModulate.A == 1 && mount.GetNodeOrNull("FlyingItemPortrait") == null,
                "Completed partner entry leaked temporary presentation.");
            samples = 0;
            await stack.PopAsync();
            Check(samples > 0 && ReferenceEquals(stack.Current, inventory) && inventory.SelectedIcon.SelfModulate.A == 1 &&
                mount.GetNodeOrNull("FlyingItemPortrait") == null, "Partner return did not shrink/restore to the retained item.");
            current.TransitionChanged -= Verify;
        }
        GD.Print("PASS partner portraits: equipment/material textures, live rectangle expansion and reverse return, no duplicates");

        using var cancel = new CancellationTokenSource();
        DetailsView? canceledDetails = null;
        void CancelHook(TransitionState state)
        {
            if (state.Stage != TransitionStage.Preparing || state.Context.Target is not DetailsView details) return;
            canceledDetails = details;
            details.TransitionChanged += s => { if (s.Stage == TransitionStage.Playing && s.Progress > .2f) cancel.Cancel(); };
        }
        inventory.TransitionChanged += CancelHook;
        await ExpectCancellationAsync(inventory.InspectAsync("Iron Ore", token: cancel.Token));
        inventory.TransitionChanged -= CancelHook;
        Check(canceledDetails?.State == NodeState.Disposed && ReferenceEquals(stack.Current, inventory) &&
            inventory.SelectedIcon.SelfModulate.A == 1 && mount.GetNodeOrNull("FlyingItemPortrait") == null,
            "Canceled partner animation left an overlay, hidden item or history entry.");

        var original = new IOException("Expected partner animation observer failure.");
        DetailsView? failedDetails = null;
        void FailHook(TransitionState state)
        {
            if (state.Stage != TransitionStage.Preparing || state.Context.Target is not DetailsView details) return;
            failedDetails = details;
            details.TransitionChanged += s => { if (s.Stage == TransitionStage.Playing && s.Progress > .2f) throw original; };
        }
        inventory.TransitionChanged += FailHook;
        try { await inventory.InspectAsync("Iron Ore"); throw new InvalidOperationException("Partner fault unexpectedly succeeded."); }
        catch (IOException ex) { Check(ReferenceEquals(ex, original), "Partner failure replaced the original exception."); }
        finally { inventory.TransitionChanged -= FailHook; }
        Check(failedDetails?.State == NodeState.Disposed && ReferenceEquals(stack.Current, inventory) &&
            inventory.SelectedIcon.SelfModulate.A == 1 && mount.GetNodeOrNull("FlyingItemPortrait") == null,
            "Failed partner animation did not restore the retained inventory.");
        await inventory.InspectAsync("Iron Ore");
        var returningDetails = (DetailsView)stack.Current!;
        using var cancelReturn = new CancellationTokenSource();
        void CancelReturn(TransitionState state)
        { if (state.Stage == TransitionStage.Playing && state.Progress > .2f) cancelReturn.Cancel(); }
        returningDetails.TransitionChanged += CancelReturn;
        await ExpectCancellationAsync(stack.PopAsync(cancellationToken: cancelReturn.Token));
        returningDetails.TransitionChanged -= CancelReturn;
        Check(ReferenceEquals(stack.Current, returningDetails) && returningDetails.IsVisible && returningDetails.GetControl().Modulate.A == 1 &&
            returningDetails.GetControl<TextureRect>("Artwork/Icon").SelfModulate.A == 1 &&
            inventory.SelectedIcon.SelfModulate.A == 1 && mount.GetNodeOrNull("FlyingItemPortrait") == null,
            "Canceled return lost the open details or leaked a portrait.");
        await stack.PopAsync();
        await inventory.InspectAsync("Iron Ore", playAnimation: false);
        Check(((DetailsView)stack.Current!).GetControl<TextureRect>("Artwork/Icon").Texture == inventory.SelectedIcon.Texture &&
            mount.GetNodeOrNull("FlyingItemPortrait") == null, "Instant details skipped preparation or created an animation overlay.");
        await stack.PopAsync(playAnimation: false);
        GD.Print("PASS partner restoration: entry/return cancellation, original failure rollback and immediate display");
    }

    private async Task RunAdapterSelfTestAsync()
    {
        // Exercise the actual engine API, including borrowed-resource ownership and failures.
        var mount = new Control { Name = "AdapterTestMount" };
        var page = new Control { Name = "Page" };
        AddChild(mount); mount.AddChild(page);
        var ui = GodotUI.Create(this);
        try
        {
            Check(ui.Root.Children.Count == 0, "Adapter setup supplied project nodes.");
            var customLoader = new DelayedLoader(new GodotSceneLoader((GodotUIHost)ui.Host));
            await using (var custom = GodotUI.Create(this, loader: customLoader, logger: (IUILog)ui.Host))
                Check(ReferenceEquals(custom.Loader, customLoader) && ReferenceEquals(custom.Log, ui.Host), "Custom services were replaced.");
            var tabs = await ui.Root.CreateContainerAsync<TabContainerNode>("Tabs", mount);
            await tabs.BindAsync("Page", page);
            await tabs.SwitchAsync("Page");
            Check(tabs.Current!.GetType() == typeof(UINode), "Control-only binding requires a business View.");
            Check(ReferenceEquals(tabs.Current.GetControl(), page), "Embedded Control was cloned.");
            Check(ReferenceEquals(tabs.GetControl<Control>("Page"), page), "Typed Control lookup.");
            await VerifyNativeTransitionsAsync(tabs.Current!, page);
            await tabs.DisposeAsync();
            Check(GodotObject.IsInstanceValid(mount) && !mount.IsQueuedForDeletion(), "Borrowed mount was freed.");
            Check(GodotObject.IsInstanceValid(page) && !page.IsQueuedForDeletion(), "Borrowed page was freed.");

            var customNode = new DemoMenuNode();
            await customNode.AttachToAsync(ui.Root, "Custom", mount);
            Check(ReferenceEquals(ui.Root.GetChild("Custom"), customNode), "Attachment replaced the supplied node.");
            await customNode.DisposeAsync();
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await ExpectCancellationAsync(ui.Root.CreateContainerAsync<PanelContainerNode>("Canceled", mount, canceled.Token));
            Check(ui.Root.Children.Count == 0, "Canceled helper left an attached container.");

            var stack = await ui.Root.CreateContainerAsync<StackContainerNode>("TransitionRollback", mount);
            ui.Register<AdapterFadeNode>("res://Scenes/Settings/Graphics.tscn");
            var retained = await stack.PushAsync<AdapterFadeNode>();
            var transitionFailure = new InvalidOperationException("Expected adapter transition rollback failure.");
            ui.Register(() => new UINode { Transition = new AdapterFailingTransition(transitionFailure) }, "res://Scenes/Settings/Graphics.tscn");
            try
            {
                await stack.PushAsync<UINode>();
                throw new Exception("Transition should have failed.");
            }
            catch (InvalidOperationException ex)
            {
                Check(ReferenceEquals(ex, transitionFailure), "Transition replaced the original exception.");
                Check(ReferenceEquals(stack.Current, retained) && retained.IsVisible && retained.GetControl().Modulate.A == 1,
                    "Rollback restored visibility but left the previous fade transparent.");
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
        finally { await ui.DisposeAsync(); mount.QueueFree(); }
        GD.Print("GODOT ADAPTER SELF-TEST PASSED (setup, custom services, binding, ownership, cancellation, original failure)");
    }

    private async Task VerifyNativeTransitionsAsync(UINode node, Control page)
    {
        page.Size = new Vector2(100, 70); page.Position = new Vector2(5, 7);
        var tint = new Color(0.7f, 0.8f, 0.9f, 0.6f); page.Modulate = tint;
        var position = page.Position; var scale = new Vector2(1.1f, 0.9f); page.Scale = scale;
        var pivot = new Vector2(3, 4); page.PivotOffset = pivot;
        var lifecycle = new LifecycleContext(node, node.Parent as ContainerNode, LifecycleReason.Push, null, node);
        foreach (var direction in new[] { TransitionDirection.Forward, TransitionDirection.Backward })
        {
            var run = GodotTransitions.SlideHorizontal(0.02).Create(new(lifecycle, true, direction));
            var playing = run.PlayAsync(CancellationToken.None);
            var sign = direction == TransitionDirection.Forward ? 1 : -1;
            Check(page.Position.IsEqualApprox(position + new Vector2(sign * page.Size.X, 0)), "Slide did not honor the operation direction.");
            await playing; run.Restore(); Check(page.Position.IsEqualApprox(position), "Slide drifted after restoration.");
        }
        var fade = GodotTransitions.Fade(0.02).Create(new(lifecycle, true, TransitionDirection.None));
        await fade.PlayAsync(CancellationToken.None); fade.Restore(); Check(page.Modulate == tint, "Fade overwrote authored tint/alpha.");
        var zoom = GodotTransitions.ScaleFade(0.02).Create(new(lifecycle, true, TransitionDirection.None));
        await zoom.PlayAsync(CancellationToken.None); zoom.Restore();
        Check(page.Scale == scale && page.PivotOffset == pivot && page.Modulate == tint, "ScaleFade did not restore authored presentation.");
        using var canceled = new CancellationTokenSource();
        var rollback = GodotTransitions.ScaleFade(0.2).Create(new(lifecycle, false, TransitionDirection.None));
        var pending = rollback.PlayAsync(canceled.Token); canceled.Cancel();
        await ExpectCancellationAsync(pending); rollback.Restore(); Check(page.Scale == scale && page.Modulate == tint, "Canceled scale animation leaked state.");

        var player = new AnimationPlayer { Name = "Animator" }; page.AddChild(player);
        var library = new AnimationLibrary();
        library.AddAnimation("enter", new Animation { Length = 0.02 }); library.AddAnimation("exit", new Animation { Length = 0.02 });
        player.AddAnimationLibrary("", library);
        var restored = false;
        var authored = GodotTransitions.Animation("Animator", "enter", "exit", c => { c.Modulate = tint; restored = true; });
        var animation = authored.Create(new(lifecycle, true, TransitionDirection.None));
        await animation.PlayAsync(CancellationToken.None); animation.Restore(); Check(restored && !player.IsPlaying(), "AnimationPlayer restoration was not invoked.");
        GD.Print("PASS V4 native animations: slide directions, tint/position/scale restoration, cancellation, AnimationPlayer");
    }

    private sealed class AdapterFadeNode : UINode
    {
        protected override Task OnInitializeAsync(CancellationToken token)
        { Transition = GodotTransitions.Fade(0.02); return Task.CompletedTask; }
    }
    private sealed class AdapterFailingTransition(Exception original) : IUITransition, IUITransitionRun
    {
        public Task PlayAsync(CancellationToken token) => Task.FromException(original);
        public IUITransitionRun Create(TransitionContext context) => this; public void Restore() { }
    }

    private sealed class AdapterFailureNode(Exception original) : UINode
    {
        protected override Task OnInitializeAsync(CancellationToken token) => Task.FromException(original);
    }

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        var attempts = 0;
        while (!condition())
        {
            if (++attempts > 300) throw new TimeoutException("Demo operation did not reach its expected state.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

}
