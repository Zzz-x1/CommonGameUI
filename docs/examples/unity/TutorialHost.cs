using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TutorialHost : MonoBehaviour
{
    public Canvas Canvas;
    public EventSystem Events;
    public RectTransform PagesMount, DialogsMount;
    private readonly List<Task> _tasks = new List<Task>();
    private UIContext _ui;
    private StackContainerNode _pages;
    private ModalContainerNode _dialogs;
    private Task _ready;
    private bool _stopping;
    private bool _shutdownComplete;
    private void Start() { _ready = InitializeAsync(); Observe(_ready); }
    private async Task InitializeAsync()
    {
        _ui = UnityUI.Create(Canvas, Events);
        _pages = await _ui.Root.CreateContainerAsync<StackContainerNode>("Pages", PagesMount);
        _dialogs = await _ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", DialogsMount);
        _dialogs.ConfigureBackdrop(UnityBackdrops.Dim(Observe));
        _ui.Register(() => new MenuView(OpenSettingsAsync, AskQuitAsync, Observe), "UI/Menu");
        _ui.Register(() => new SettingsView(_dialogs, Observe), "UI/Settings");
        _ui.Register<ConfirmView>("UI/Confirm");
        await _pages.PushAsync<MenuView>();
    }
    private void Observe(Task task) { _tasks.Add(task); }
    private void Update()
    {
        for (int i = _tasks.Count - 1; i >= 0; i--)
        {
            var task = _tasks[i];
            if (!task.IsCompleted) continue;
            _tasks.RemoveAt(i);
            task.GetAwaiter().GetResult();
        }
        if (_stopping || _ready == null || !_ready.IsCompletedSuccessfully || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (_dialogs.Current is ConfirmView confirm && confirm.IsInteractive) confirm.Answer(false);
        else if (_dialogs.Current is SettingsView settings && settings.IsInteractive && !settings.Switching) Observe(_dialogs.CloseAsync(settings));
        else if (_dialogs.Current == null && _pages.IsInteractive) Observe(AskQuitAsync());
    }
    private Task OpenSettingsAsync() => _dialogs.OpenAsync<SettingsView>();
    private async Task AskQuitAsync()
    { if (await _dialogs.ShowDialogAsync<ConfirmView, bool>()) await ShutdownAsync(); }
    public async Task ShutdownAsync()
    {
        _stopping = true;
        await _ui.DisposeAsync();
        _shutdownComplete = true;
        Application.Quit();
    }
    private bool OnApplicationWantsToQuit()
    {
        if (_stopping) return _shutdownComplete;
        if (_ready != null && _ready.IsCompletedSuccessfully && _dialogs.Current == null && _pages.IsInteractive) Observe(AskQuitAsync());
        return false;
    }
    private void OnEnable() { Application.wantsToQuit += OnApplicationWantsToQuit; }
    private void OnDisable() { Application.wantsToQuit -= OnApplicationWantsToQuit; }
}

public sealed class MenuView : UINode
{
    private readonly Func<Task> _settings, _quit;
    private readonly Action<Task> _observe;
    private Button _settingsButton, _quitButton;
    public MenuView(Func<Task> settings, Func<Task> quit, Action<Task> observe)
    { _settings = settings; _quit = quit; _observe = observe; }
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        _settingsButton = this.GetComponent<Button>("Content/Settings"); _quitButton = this.GetComponent<Button>("Content/Quit");
        _settingsButton.onClick.AddListener(() => _observe(_settings()));
        _quitButton.onClick.AddListener(() => _observe(_quit()));
        return Task.CompletedTask;
    }
    protected override void OnApplyState() { if (State == NodeState.Ready) RefreshInput(); }
    public void RefreshInput() { _settingsButton.interactable = IsInteractive; _quitButton.interactable = IsInteractive; }
}

public sealed class SettingsView : UINode
{
    private readonly ModalContainerNode _dialogs;
    private readonly Action<Task> _observe;
    private TabContainerNode _tabs;
    private Button _audio, _graphics, _close;
    public bool Switching { get; private set; }
    public SettingsView(ModalContainerNode dialogs, Action<Task> observe) { _dialogs = dialogs; _observe = observe; }
    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        _audio = this.GetComponent<Button>("Content/AudioTab"); _graphics = this.GetComponent<Button>("Content/GraphicsTab");
        _close = this.GetComponent<Button>("Content/Close");
        _tabs = await this.CreateContainerAsync<TabContainerNode>("Categories", (RectTransform)this.GetRect().Find("Content/Body"), token);
        await _tabs.BindAsync("Audio", (RectTransform)this.GetRect().Find("Content/Body/Audio"), token);
        await _tabs.BindAsync("Graphics", (RectTransform)this.GetRect().Find("Content/Body/Graphics"), token);
        await _tabs.SwitchAsync("Audio", playAnimation: false, cancellationToken: token);
        _audio.onClick.AddListener(() => _observe(SwitchAsync("Audio")));
        _graphics.onClick.AddListener(() => _observe(SwitchAsync("Graphics")));
        _close.onClick.AddListener(() => _observe(_dialogs.CloseAsync(this)));
    }
    public async Task SwitchAsync(string name)
    {
        Switching = true; RefreshInput();
        try { if (_tabs.CurrentName != name) await _tabs.SwitchAsync(name); }
        finally { Switching = false; if (State == NodeState.Ready) RefreshInput(); }
    }
    protected override void OnApplyState() { if (State == NodeState.Ready) RefreshInput(); }
    public void RefreshInput()
    { bool enabled = IsInteractive && !Switching; _audio.interactable = enabled; _graphics.interactable = enabled; _close.interactable = enabled; }
}

public sealed class ConfirmView : DialogNode<bool>
{
    private Button _yes, _no;
    private bool _answered;
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        _yes = this.GetComponent<Button>("Content/Yes"); _no = this.GetComponent<Button>("Content/No");
        _yes.onClick.AddListener(() => Answer(true)); _no.onClick.AddListener(() => Answer(false));
        return Task.CompletedTask;
    }
    public void Answer(bool value) { Complete(value); _answered = true; RefreshInput(); }
    protected override void OnApplyState() { if (State == NodeState.Ready) RefreshInput(); }
    public void RefreshInput() { _yes.interactable = IsInteractive && !_answered; _no.interactable = IsInteractive && !_answered; }
}
