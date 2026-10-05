using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineMvvm = global::UIFramework.Mvvm.Godot;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;

namespace HierarchyDemo
{
public abstract partial class ClientView : EngineUI.UINode
{
    protected DemoApp App => DemoApp.Instance;
    protected PlayerState Data => App.Store.State;
    protected Control Root => ((EngineUI.UIElement)Element!).Control;

    private readonly List<Action> _disconnect = new();
    private readonly Dictionary<Button, Tween> _feedback = new();
    protected T Get<T>(string path)
        where T : Godot.Node => Root.GetNode<T>(path);
    public void Text(string path, string text) => Get<Label>(path).Text = text;
    protected void Wire(string path, Func<Task> action)
    {
        var button = Get<Button>(path);
        void Click() => App.Track(action());
        button.Pressed += Click;
    }

    protected void Listen(Action connect, Action disconnect)
    {
        connect();
        _disconnect.Add(disconnect);
    }

    protected void Feedback()
    {
        foreach (var button in Descendants(Root).OfType<Button>())
        {
            var original = button.SelfModulate;
            void Animate(Color color)
            {
                if (_feedback.Remove(button, out var previous))
                    previous.Kill();
                var tween = Root.CreateTween();
                tween.TweenProperty(button, "self_modulate", color, .12);
                _feedback[button] = tween;
            }

            void Enter() => Animate(original * new Color(1.02f, 1.02f, .92f));
            void Leave() => Animate(original);
            button.MouseEntered += Enter;
            button.MouseExited += Leave;
        }
    }

    public static IEnumerable<Godot.Node> Descendants(Godot.Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }

    protected override Task OnDisposeAsync()
    {
        foreach (var disconnect in _disconnect)
            disconnect();
        // Owned scenes release native signals and node-bound tweens when freed.
        _disconnect.Clear();
        _feedback.Clear();
        return Task.CompletedTask;
    }

    protected override void OnActivated(StateChangeContext context) => Refresh();
    public virtual void Refresh()
    {
    }

    protected Texture2D HeroTexture(int i) => GD.Load<Texture2D>($"res://Assets/Client/hero{i}.png");
    protected Texture2D ItemTexture(string id) => GD.Load<Texture2D>($"res://Assets/Items/{Catalog.Item(id).Icon}.svg");
}
}
