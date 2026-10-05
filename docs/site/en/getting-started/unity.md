# Unity Getting Started

## Scenario and validation status

Build the same menu, settings categories, confirmation and return flow as the Godot tutorial. Adapter source has been compiled against Unity 6000.3.24f1 and uGUI 2.0.0. The latest adapter fixes and this tutorial have offline compilation only, without native runtime verification. The game Unity Demo still has pending timeline asset verification. This is not a claim of support tested on every Unity platform.

## Install

Download this repository and install the .NET 10 SDK. From the repository root run `./scripts/Run-UnityDemo.ps1 -PrepareOnly`. It prepares the core DLL in the local package without starting an editor. In your own project, use Package Manager → Add package from disk and select `src/UIFramework.Unity/package.json`. The package depends on uGUI and the Animation module. The core DLL is a build artifact; prepare it before the first import.

This tutorial needs neither Source Generator nor Unity CLI. Copy `docs/examples/unity/TutorialHost.cs` into Assets/Scripts. Set Active Input Handling to Input Manager (Old) or Both; Esc below uses the old Input API. Use an EventSystem with StandaloneInputModule, Submit mapped to Return and Cancel to Escape. Replace the host key check and input module when using the new input system.

## Resources and Inspector

```text
Scene
├── Canvas (Screen Space Overlay, CanvasScaler, GraphicRaycaster, TutorialHost)
│   ├── PagesMount (RectTransform, Stretch)
│   └── DialogsMount (RectTransform, Stretch, after PagesMount)
└── EventSystem (StandaloneInputModule)
Assets/Resources/UI/Menu.prefab: RectTransform → Content → Settings, Quit
Assets/Resources/UI/Settings.prefab: RectTransform → Content (Image)
  ├── AudioTab, GraphicsTab, Close (Button)
  └── Body → Audio/Volume (Slider), Graphics/Quality (Toggle)
Assets/Resources/UI/Confirm.prefab: RectTransform → Content (Image)
  └── Question (Text), Yes, No (Button)
```

Use Scale With Screen Size at 1000×700 on CanvasScaler. Every Prefab root is a stretched RectTransform. Center Content at roughly 440×380. Author both Tab pages under Body in the same display area; runtime binding initially selects Audio. Enable button navigation, disable label Raycast Target, and keep panel/button graphics raycastable. Start Volume at .65. Quality is a regular Toggle; both controls demonstrate UI state only.

Do not place raycastable full-screen Images on the mounts or Prefab roots. The framework creates the modal backdrop. Assign Canvas, Events, PagesMount and DialogsMount to TutorialHost fields. Do not duplicate the button listeners in the Inspector.

## Complete host and nodes

```csharp
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
```

## Try the flow and shut down

Settings→Graphics→Audio retains the Slider value. Back returns to the menu. Quit→Cancel keeps it; Quit→Confirm disposes the Context then requests exit. Esc handles the current modal first. Enter submits the EventSystem selection. Application.Quit does not stop Play Mode in the editor; manual Stop is not an awaited shutdown. Before unloading a scene, await ShutdownAsync from project code and replace the exit action as needed. Do not use async void cleanup in OnDestroy.

Update observes tasks and propagates original errors. A project Switching state disables selectors and Close together; containers still manage their own input locks. External closure of an unanswered dialog cancels instead of returning false. This tutorial has offline compilation only, not native runtime verification.

Buttons update their availability synchronously in OnApplyState, before framework focus restoration. Controls are not bound during initialization, so updates apply only in Ready state. Switching and answered flags update immediately without polling button state every frame.

## Continue

[Unity integration](../integrations/unity.md) · [Result dialogs](../manual/modals-and-dialogs.md) · [Input and focus](../manual/input-and-focus.md) · [Next steps](next-steps.md)
