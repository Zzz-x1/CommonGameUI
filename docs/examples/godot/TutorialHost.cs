using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Godot;

public partial class TutorialHost : Control
{
    private readonly List<Task> _tasks = new();
    private Button _openWindowButton = null!;
    private PanelContainerNode _windows = null!;

    public UIContext UI { get; private set; } = null!;
    public Task Initialization { get; private set; } = null!;

    public override void _Ready()
    {
        Initialization = InitializeAsync();
        Observe(Initialization);
    }

    private async Task InitializeAsync()
    {
        _openWindowButton = GetNode<Button>("OpenWindow");
        UI = GodotUI.Create(this);
        UI.BindLifetime(this);
        _windows = await UI.Root.CreateContainerAsync<PanelContainerNode>("Windows", GetNode<Control>("Windows"));
        UI.Register(() => new HelloWindow(_windows, Observe), "res://HelloWindow.tscn");
        await _windows.OpenAsync<HelloWindow>();
        _openWindowButton.Pressed += OpenWindow;
        _openWindowButton.Disabled = false;
    }

    private void OpenWindow()
    {
        _openWindowButton.Disabled = true;
        Observe(OpenWindowAsync());
    }

    private async Task OpenWindowAsync()
    {
        await _windows.OpenAsync<HelloWindow>();
        _openWindowButton.Disabled = false;
    }

    private void Observe(Task task) => _tasks.Add(task);

    public override void _Process(double delta)
    {
        for (var i = _tasks.Count - 1; i >= 0; i--)
        {
            var task = _tasks[i];
            if (!task.IsCompleted) continue;
            _tasks.RemoveAt(i);
            task.GetAwaiter().GetResult();
        }
    }
}
