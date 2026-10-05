using EngineUI = global::UIFramework.Unity;
using System.Collections.Generic;
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
    public RectTransform WindowsMount;
    public Button OpenWindowButton;

    private readonly List<Task> _tasks = new List<Task>();
    private UIContext _ui;
    private PanelContainerNode _windows;

    private void Start() => Observe(InitializeAsync());

    private async Task InitializeAsync()
    {
        _ui = EngineUI.UI.Create(Canvas, Events);
        _ui.BindLifetime(gameObject);
        _windows = await _ui.Root.CreateContainerAsync<PanelContainerNode>("Windows", WindowsMount);
        _ui.Register<HelloWindow>("UI/HelloWindow");
        await _windows.OpenAsync<HelloWindow>();
        OpenWindowButton.onClick.AddListener(OpenWindow);
        OpenWindowButton.interactable = true;
    }

    private void OpenWindow()
    {
        OpenWindowButton.interactable = false;
        Observe(OpenWindowAsync());
    }

    private async Task OpenWindowAsync()
    {
        await _windows.OpenAsync<HelloWindow>();
        OpenWindowButton.interactable = true;
    }

    private void Observe(Task task) => _tasks.Add(task);

    private void Update()
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
