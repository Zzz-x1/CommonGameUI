using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Mvvm;
using UIFramework.Godot;

namespace HierarchyDemo;
public partial class DemoApp
{
    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException("Client test: " + description);
    }

    private static void Throws<T>(Action action)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private async Task RunClientTestsAsync()
    {
        int checks = 0;
        void Pass(bool b, string s)
        {
            Check(b, s);
            checks++;
            GD.Print("PASS " + s);
        }

        var directory = ProjectSettings.GlobalizePath("user://client-test");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "model.json");
        if (File.Exists(path))
            File.Delete(path);
        var model = new ClientStore(path);
        int gold = model.State.Gold;
        model.Buy("eq0", 2);
        Pass(model.State.Gold == gold - Catalog.Item("eq0").Price * 2 && model.State.Inventory["eq0"] == 4, "purchase state");
        var previous = model.State;
        Throws<ArgumentOutOfRangeException>(() => model.Buy("eq11", 100));
        Pass(ReferenceEquals(previous, model.State), "invalid purchase unchanged");
        model.Train(0);
        model.Equip(0, "eq2");
        model.SetTeam(2, 2);
        Pass(model.State.Levels[0] == 13 && model.State.Equipment[0] == "eq2" && model.State.Team[2] == 2, "train equip team");
        var equipped = model.State;
        Throws<InvalidOperationException>(() => model.Equip(0, "eq2"));
        Pass(ReferenceEquals(equipped, model.State), "same equipment cannot advance goals");
        model.Equip(0, "");
        Pass(model.State.Equipment[0] == "", "unequip persists");
        model.Equip(0, "eq2");
        Throws<InvalidOperationException>(() => model.SetTeam(3, 2));
        checks++;
        model.Count("login");
        model.ClaimGoal(false, 0);
        Throws<InvalidOperationException>(() => model.ClaimGoal(false, 0));
        checks++;
        model.ClaimMail(new[] { 0, 1, 1 });
        Pass(model.State.Counts["mail"] == 2, "mail deduplication");
        Throws<InvalidOperationException>(() => model.ClaimMail(new[] { 0, 1 }));
        checks++;
        model.ClaimEvent(1);
        Throws<InvalidOperationException>(() => model.ClaimEvent(1));
        checks++;
        var loaded = new ClientStore(path);
        Pass(loaded.State.Gold == model.State.Gold && loaded.State.Equipment[0] == "eq2" && loaded.State.Claimed.SetEquals(model.State.Claimed), "reload state");
        File.WriteAllText(Path.Combine(directory, "broken.json"), "{bad");
        Throws<System.Text.Json.JsonException>(() => new ClientStore(Path.Combine(directory, "broken.json")));
        checks++;
        File.WriteAllText(Path.Combine(directory, "incomplete.json"), "{}");
        Throws<System.Text.Json.JsonException>(() => new ClientStore(Path.Combine(directory, "incomplete.json")));
        checks++;
        var corrupt = System.Text.Json.JsonSerializer.SerializeToNode(model.State)!;
        corrupt["Inventory"]!["unknown"] = 1;
        File.WriteAllText(Path.Combine(directory, "unknown-item.json"), corrupt.ToJsonString());
        Throws<InvalidDataException>(() => new ClientStore(Path.Combine(directory, "unknown-item.json")));
        checks++;
        var failurePath = Path.Combine(directory, "write-failure");
        var failed = new ClientStore(failurePath);
        Directory.CreateDirectory(failurePath);
        var old = failed.State;
        Throws<UnauthorizedAccessException>(() => failed.Count("login"));
        Pass(ReferenceEquals(old, failed.State), "failed save not published");
        var balance = model.State.Gold;
        model.Change(s => s.Gold = 0);
        var broke = model.State;
        Throws<InvalidOperationException>(() => model.Buy("eq0", 1));
        Pass(ReferenceEquals(broke, model.State), "insufficient balance changes nothing");
        model.Change(s => s.Gold = balance);
        var purchases = model.State.Counts["purchase"];
        var materials = model.State.Inventory["mat6"];
        model.Buy("mat0", 2, true);
        Pass(model.State.Counts["purchase"] == purchases + 1 && model.State.Inventory["mat6"] == materials + 6, "bundle is one committed purchase");
        Store.Change(s => s.FullMotion = false);
        await MouseClickAsync(_login!.GetControl<Button>("LoginCard/Enter"));
        Pass(Shell.Pages.Current == Shell.Home && Shell.Home.IsInteractive, "client entry");
        await MouseClickAsync(Shell.GetControl<Button>("Bottom/Shop"));
        Pass(Shell.Pages.Current is ShopView, "mouse reaches lobby navigation through empty mounts");
        await MouseClickAsync(Shell.GetControl<Button>("Bottom/Home"));
        foreach (var module in new[]
        {
            "Characters",
            "Inventory",
            "Team",
            "Stages",
            "Shop",
            "Tasks",
            "Achievements",
            "Mail",
            "Friends",
            "Events"
        })
        {
            await OpenAsync(module);
            Pass(Shell.Pages.Current!.IsInteractive, "module " + module);
            await OpenAsync("Home");
            Pass(Shell.Pages.Current == Shell.Home, "return " + module);
        }

        await OpenAsync("Inventory");
        var inventory = (InventoryView)Shell.Pages.Current!;
        await inventory.SelectTabAsync("材料");
        await BackAsync();
        var reusedInventory = await Shell.Pages.PushAsync<InventoryView>();
        Pass(ReferenceEquals(inventory, reusedInventory) && inventory.Tabs.CurrentName == "材料", "closed inventory preserves embedded tab");
        await OpenAsync("Home");
        await OpenAsync("Inventory");
        inventory = (InventoryView)Shell.Pages.Current!;
        await inventory.SelectTabAsync("装备");
        await PressAsync(inventory.GetRoot().GetNode<Button>("Pages/装备/C0"));
        var item = (ItemDetailsView)Shell.Pages.Current!;
        Pass(!inventory.IsInteractive && item.IsInteractive, "partner activation");
        Pass(!ClientView.Descendants(GetNode<Control>("ShellMount")).Any(n => n.Name == "FlyingItemPortrait"), "partner cleanup");
        await item.CompareAsync();
        await item.CompareAsync();
        await ((ClientModalView)Modals.Current!).PickAsync(3);
        Pass(Floating.Children.Count == 2, "multi instance comparison");
        await BackAsync();
        Pass(Floating.Children.Count == 0, "comparison back priority");
        await Shell.Pages.PopAsync();
        Pass(ReferenceEquals(Shell.Pages.Current, inventory), "original inventory returned");
        Pass(item.IsCached, "closed item leaves history and enters cache");
        var reusedItem = await Shell.Pages.PushAsync<ItemDetailsView>("eq1");
        Pass(ReferenceEquals(item, reusedItem) && reusedItem.GetViewModel<ClientItemVm>("Item").Name == Catalog.Item("eq1").Name, "cached item applies new parameters and refreshes VM");
        await Shell.Pages.PopAsync();
        await OpenAsync("Shop");
        await ModalAsync(new ModalArgs("Shop", 0));
        var detail = (ClientModalView)Modals.Current!;
        await detail.ConfirmAsync();
        var confirmation = (ClientModalView)Modals.Current!;
        int owned = Store.State.Inventory["eq0"];
        await PressAsync(confirmation.GetControl<Button>("Center/Confirm"));
        Pass(Store.State.Inventory["eq0"] == owned + 1 && Modals.Current is RewardView, "purchase reward");
        Pass(confirmation.State == NodeState.Disposed && detail.State == NodeState.Ready && !detail.IsInteractive, "close to target");
        await BackAsync();
        Pass(ReferenceEquals(Modals.Current, detail) && detail.IsInteractive, "detail restored");
        await BackAsync();
        await ModalAsync(new ModalArgs("Shop", 1));
        await ((ClientModalView)Modals.Current!).ConfirmAsync();
        owned = Store.State.Inventory["eq1"];
        await BackAsync();
        Pass(Store.State.Inventory["eq1"] == owned, "cancel purchase");
        await BackAsync();
        await OpenAsync("Characters");
        var equippedHero = await Shell.Pages.PushAsync<HeroDetailsView>(2);
        await ModalAsync(new ModalArgs("Equip", 2));
        await ((ClientModalView)Modals.Current!).PickAsync(0);
        Pass(Store.State.Equipment[2] == "eq0" && equippedHero.GetControl<Label>("Stats").Text.Contains("280"), "purchased equipment updates visible attributes");
        await ModalAsync(new ModalArgs("Equip", 3));
        await ((ClientModalView)Modals.Current!).PickAsync(0);
        await OpenAsync("Tasks");
        await ModalAsync(new ModalArgs("Tasks", 2));
        await ((ClientModalView)Modals.Current!).ConfirmAsync();
        Pass(Store.State.Claimed.Contains("t2") && Modals.Current is RewardView, "equipment task reward closes loop");
        await BackAsync();
        await OpenAsync("Achievements");
        await ModalAsync(new ModalArgs("Achievements", 2));
        await ((ClientModalView)Modals.Current!).ConfirmAsync();
        Pass(Store.State.Claimed.Contains("a2") && Modals.Current is RewardView, "equipment achievement reward closes loop");
        await BackAsync();
        await ModalAsync(new ModalArgs("Settings"));
        Pass(!Shell.Pages.Current!.IsInteractive && Modals.Current!.IsInteractive, "modal input ownership");
        var settingsView = (ClientModalView)Modals.Current;
        settingsView.GetControl<HSlider>("Center/Pages/Settings/SettingPages/声音/Volume").Value = 15;
        await PressAsync(settingsView.GetControl<Button>("Center/Pages/Settings/Tab1"));
        var quality = settingsView.GetControl<OptionButton>("Center/Pages/Settings/SettingPages/画面/Quality");
        quality.Select(1);
        quality.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
        settingsView.GetControl<CheckButton>("Center/Pages/Settings/SettingPages/画面/Fullscreen").ButtonPressed = true;
        await PressAsync(settingsView.GetControl<Button>("Center/Pages/Settings/Tab2"));
        settingsView.GetControl<CheckButton>("Center/Pages/Settings/SettingPages/演出/FullMotion").ButtonPressed = false;
        Pass(Store.State.Volume == 15 && Store.State.Quality == 1 && Store.State.Fullscreen && !Store.State.FullMotion, "settings controls commit state");
        var reset = settingsView.ConfirmAsync();
        while (Modals.Current is not ConfirmView || !Modals.Current.IsInteractive)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ((ConfirmView)Modals.Current).Complete(true);
        await reset;
        Pass(Store.State.Volume == 70 && Store.State.Quality == 2 && !Store.State.Fullscreen && Store.State.FullMotion, "settings restore defaults");
        await BackAsync();
        Store.Change(s => s.FullMotion = false);
        Pass(Shell.Pages.Current.IsInteractive, "modal input restored");
        await OpenAsync("Mail");
        Store.Change(s => s.FullMotion = true);
        await ModalAsync(new ModalArgs("Mail", 0));
        var mailDetail = Modals.Current;
        var claimSingle = ((ClientModalView)mailDetail!).ConfirmAsync();
        await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
        Pass(Modals.Children.OfType<RewardView>().Single().GetControl<Control>("Center").Material is ShaderMaterial && SkipCinematic == null,
            "actual mail claim uses source-dependent reward reveal");
        await claimSingle;
        await BackAsync();
        Pass(ReferenceEquals(Modals.Current, mailDetail) && Store.State.Claimed.Contains("m0"), "mail reward returns to retained claimed letter");
        await BackAsync();
        Store.Change(s => s.FullMotion = false);
        await PressAsync(Shell.Pages.Current!.GetControl<Button>("ClaimAll"));
        Pass(Store.State.Counts["mail"] == 8 && Modals.Current is RewardView, "batch mail grants all once");
        await BackAsync();
        Pass(Shell.Pages.Current!.GetControl<Button>("ClaimAll").Disabled, "batch mail cannot repeat");
        await OpenAsync("Events");
        var events = (EventsView)Shell.Pages.Current!;
        await events.Tabs.HideAsync();
        Pass(events.Tabs.Current == null, "embedded hide");
        await events.SelectAsync("签到");
        await events.ClaimAsync(1);
        Pass(Store.State.Claimed.Contains("e1"), "event reward persisted");
        await BackAsync();
        await OpenAsync("Home");
        await OpenAsync("Characters");
        var hero = await Shell.Pages.PushAsync<HeroDetailsView>(0);
        int level = Store.State.Levels[0];
        await hero.TrainAsync();
        Pass(Store.State.Levels[0] == level + 1 && Modals.Current is TrainingView, "training timeline");
        Pass(hero.GetControl<Label>("Stats").Text.EndsWith("战力 " + Store.State.Power(0)), "training counter resumes latest VM value");
        await BackAsync();
        var question = AskAsync("测试确认");
        while (Modals.Current is not ConfirmView || !Modals.Current.IsInteractive)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ((ConfirmView)Modals.Current).Complete(true);
        Pass(await question, "dialog result");
        // Actual AnimationPlayer skip must finish the framework wait and leave no gate.
        Store.Change(s => s.FullMotion = true);
        var reward = RewardAsync("跳过时间轴");
        while (SkipCinematic == null)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        int modalCount = Modals.Children.Count;
        _Notification((int)NotificationWMCloseRequest);
        Pass(Modals.Children.Count == modalCount, "cinematic admits only skip");
        SkipCinematic();
        await reward;
        Pass(SkipCinematic == null && Modals.Current!.IsInteractive, "animation skip");
        await BackAsync();
        var saved = new ClientStore(ProjectSettings.GlobalizePath("user://client-test/save.json"));
        Pass(saved.State.Gold == Store.State.Gold && saved.State.Levels[0] == Store.State.Levels[0], "client persisted");
        await RunAnimationBoundaryTestsAsync(Pass);
        await RunInputAnimationTestsAsync(Pass);
        await RunTabInputTestsAsync(Pass);
        await RunRevealTestsAsync(Pass);
        await RunSourceBranchTestsAsync(Pass);
        await RunAdapterSelfTestAsync();
        await RunLifetimeTestsAsync(Pass);
        Pass(true, "retained native adapter and transition coverage");
        await RunResourceSelfTestAsync();
        Pass(true, "retained native resource ownership coverage");
        await RunRetainedPartnerTestsAsync(Pass);
        await OpenAsync("Home");
        int showcase = Store.State.Showcase;
        var switchHero = Shell.Home.SwitchHeroAsync();
        Pass(Busy && Shell.Home.GetControl<Button>("Welcome/NextHero").Disabled
            && Shell.Home.GetControl<Button>("Quest/Tasks").MouseFilter == Control.MouseFilterEnum.Ignore,
            "portrait switch disables repeated input and other lobby actions");
        await switchHero;
        Pass(Store.State.Showcase == (showcase + 1) % 6 && !Busy && !Shell.Home.GetControl<Button>("Welcome/NextHero").Disabled, "portrait switch restores input");
        GD.Print($"CLIENT TESTS PASSED: {checks}");
        await _ui.DisposeAsync();
        GetTree().Quit();
    }

    private async Task VerifyClientSaveAsync()
    {
        // Separate process, same isolated file; never recreates a preset archive.
        Check(Store.State.Gold == 14520 && Store.State.Gems == 650 && Store.State.Inventory["eq0"] == 3, "restart currencies and inventory");
        Check(Store.State.Levels[0] == 13 && Store.State.Equipment[2] == "eq0" && Store.State.Showcase == 1, "restart heroes and profile");
        Check(Store.State.Counts["mail"] == 8 && Store.State.Claimed.Contains("t2") && Store.State.Claimed.Contains("a2") && Store.State.Claimed.Contains("e1"), "restart rewards cannot repeat");
        await EnterAsync();
        Check(Shell.GetControl<Label>("Top/Wallet").Text.Contains("14,520"), "restart visible wallet");
        GD.Print("CLIENT RESTART TESTS PASSED: 4");
        await _ui.DisposeAsync();
        GetTree().Quit();
    }

    private async Task MouseClickAsync(Button button)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var position = button.GetGlobalTransformWithCanvas() * (button.Size / 2);
        int before = _tasks.Count;
        foreach (bool pressed in new[] { true, false })
            GetViewport().PushInput(new InputEventMouseButton
            {
                Position = position,
                GlobalPosition = position,
                ButtonIndex = MouseButton.Left,
                Pressed = pressed
            }, true);
        Check(_tasks.Count > before, $"鼠标点击未到达 {button.GetPath()}");
        await Task.WhenAll(Task.WhenAll(_tasks.Skip(before).ToArray()), _ui.WhenMvvmIdleAsync());
    }

    private sealed class TestLog : IUILog
    {
        public readonly List<Exception> Errors = new();
        public void Error(string message) => new ConsoleUILog().Error(message);
        public void Exception(Exception exception, string context) => Errors.Add(exception);
    }

    private async Task RunAnimationBoundaryTestsAsync(Action<bool, string> pass)
    {
        var mount = new Control();
        AddChild(mount);
        mount.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var log = new TestLog();
        var fixture = EngineUI.UI.Create(this, logger: log);
        fixture.Register<RewardView>("res://Scenes/Client/Reward.tscn");
        var windows = await fixture.Root.CreateContainerAsync<PanelContainerNode>("AnimationTests", mount);
        using var preload = await fixture.PreloadAsync<RewardView>();
        var complete = windows.OpenAsync<RewardView>(new PresentationArgs("多阶段奖励"));
        var preparing = windows.Children.Single().GetRoot();
        pass(preparing.IsVisibleInTree() && preparing.Modulate.A == 0 && SkipCinematic == null,
            "timeline layout frame cannot draw authored end state");
        while (SkipCinematic == null)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        pass(preparing.Modulate.A == 1 && preparing.GetNode<Control>("Center/Paper").Modulate.A == 0,
            "timeline first key is sampled before layout mask is removed for drawing");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var display = windows.Children.Single().GetRoot();
        var timeline = display.GetNode<AnimationPlayer>("AnimationPlayer");
        timeline.Seek(.8, update: true);
        pass(display.GetNode<Control>("Center/Crest").Modulate.A > .1f && display.GetNode<Control>("Center/Close").Modulate.A == 0,
            "timeline has distinct crest and result phases");
        SkipCinematic!();
        var completed = await complete;
        var card = display.GetNode<Control>("Center/Cards/C0");
        pass(card.Scale == Vector2.One && card.PivotOffset == Vector2.Zero && display.GetNode<Control>("Center/Crest").Modulate.A == 0
            && completed.IsInteractive && SkipCinematic == null, "timeline restores transforms and input after skip");
        await windows.CloseAsync(completed);
        using var cancel = new CancellationTokenSource();
        var pending = windows.OpenAsync<RewardView>(new PresentationArgs("取消动画"), cancellationToken: cancel.Token);
        while (SkipCinematic == null)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        cancel.Cancel();
        try
        {
            await pending;
            throw new InvalidOperationException("Expected cancellation.");
        }
        catch (OperationCanceledException)
        {
        }

        pass(SkipCinematic == null && windows.Children.Count == 0 && mount.GetChildCount() == 0, "cancel timeline cleans display and gate");
        pending = windows.OpenAsync<RewardView>(new PresentationArgs("原始动画错误"));
        while (SkipCinematic == null)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        windows.Children.Single().GetRoot().GetNode<AnimationPlayer>("AnimationPlayer").Stop();
        Exception original;
        try
        {
            await pending;
            throw new IOException("Expected AnimationPlayer error.");
        }
        catch (InvalidOperationException ex)
        {
            original = ex;
        }

        pass(log.Errors.Contains(original) && SkipCinematic == null && mount.GetChildCount() == 0, "raw animation failure cleanup");
        pending = windows.OpenAsync<RewardView>(new PresentationArgs("销毁中的动画"));
        while (SkipCinematic == null)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await fixture.DisposeAsync();
        try
        {
            await pending;
            throw new InvalidOperationException("Expected disposal cancellation.");
        }
        catch (OperationCanceledException)
        {
        }

        pass(SkipCinematic == null && mount.GetChildCount() == 0, "dispose during timeline");
        RemoveChild(mount);
        mount.QueueFree();
        await OpenAsync("Home");
        var settings = Shell.GetControl<Button>("Top/Settings");
        settings.GrabFocus();
        await ModalAsync(new ModalArgs("Settings"));
        pass(GetViewport().GuiGetFocusOwner() != settings, "modal captures focus");
        await BackAsync();
        pass(GetViewport().GuiGetFocusOwner() == settings, "modal restores original focus");
        await OpenAsync("Inventory");
        var inventory = (InventoryView)Shell.Pages.Current!;
        await PressAsync(inventory.GetControl<Button>("Pages/装备/C0"));
        await OpenAsync("Home");
        pass(Shell.Pages.Current == Shell.Home && !ClientView.Descendants(GetNode<Control>("ShellMount")).Any(n => n.Name == "FlyingItemPortrait"), "item detail direct home uses explicit transition");
    }
}
