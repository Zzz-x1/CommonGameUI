using Godot;
using UIFramework.Godot;
[assembly: UIFramework.Mvvm.BindingTargetProvider(typeof(UIFramework.Mvvm.Godot.BuiltinBindingTargets))]
namespace UIFramework.Mvvm.Godot;
public static class BindingTargets
{
    public static IBindingTarget TextTarget(this Label label) => new Target<string>(label, () => label.Text, v => label.Text = v);
    public static IBindingTarget TextTarget(this LineEdit edit) => new Target<string>(edit, () => edit.Text, v => edit.Text = v, a => { void Changed(string _) => a(); edit.TextChanged += Changed; return () => edit.TextChanged -= Changed; });
    public static IBindingTarget ValueTarget(this global::Godot.Range range) => new Target<float>(range, () => (float)range.Value, v => range.SetValueNoSignal(v), a => { void Changed(double _) => a(); range.ValueChanged += Changed; return () => range.ValueChanged -= Changed; });
    public static IBindingTarget ToggleTarget(this BaseButton button) => new Target<bool>(button, () => button.ButtonPressed, button.SetPressedNoSignal, a => { void Changed(bool _) => a(); button.Toggled += Changed; return () => button.Toggled -= Changed; });
    public static ICommandTarget ClickTarget(this BaseButton button) => new ButtonTarget(button);
    public static IBindingTarget Property(Control control, string member, BindingMode mode = BindingMode.OneWay) =>
        BindingTargetRegistry.Find(control.GetType(), member).CreateTarget(control, mode);
    private sealed class Target<T> : IBindingTarget
    {
        private readonly GodotObject _owner; private readonly Func<T> _read; private readonly Action<T> _write; private readonly Action? _disconnect;
        public Target(GodotObject owner, Func<T> read, Action<T> write, Func<Action, Action>? subscribe = null)
        { _owner = owner; _read = read; _write = write; _disconnect = subscribe?.Invoke(() => Changed?.Invoke()); }
        public Type ValueType => typeof(T); public object? Read() => _read(); public void Write(object? value) => _write((T)value!); public event Action? Changed;
        public void Dispose() { if (GodotObject.IsInstanceValid(_owner)) _disconnect?.Invoke(); }
    }
    private sealed class ButtonTarget : ICommandTarget
    {
        private readonly BaseButton _button; public ButtonTarget(BaseButton button) { _button = button; button.Pressed += Invoke; }
        private void Invoke() => Invoked?.Invoke(); public event Action? Invoked;
        public void SetEnabled(bool enabled) => _button.Disabled = !enabled;
        public void Dispose() { if (GodotObject.IsInstanceValid(_button)) _button.Pressed -= Invoke; }
    }
}

public sealed class BuiltinBindingTargets : IBindingTargetProvider
{
    public IEnumerable<BindingTargetDefinition> GetTargets()
    {
        yield return BindingTargetDefinition.Create<Label, string>("Text", t => t.Text, (t,v) => t.Text = v);
        yield return BindingTargetDefinition.Create<LineEdit, string>("Text", t => t.Text, (t,v) => t.Text = v, (t,a) => { void H(string _) => a(); t.TextChanged += H; return () => { if (GodotObject.IsInstanceValid(t)) t.TextChanged -= H; }; });
        yield return BindingTargetDefinition.Create<global::Godot.Range, float>("Value", t => (float)t.Value, (t,v) => t.SetValueNoSignal(v), (t,a) => { void H(double _) => a(); t.ValueChanged += H; return () => { if (GodotObject.IsInstanceValid(t)) t.ValueChanged -= H; }; });
        yield return BindingTargetDefinition.Create<BaseButton, bool>("ButtonPressed", t => t.ButtonPressed, (t,v) => t.SetPressedNoSignal(v), (t,a) => { void H(bool _) => a(); t.Toggled += H; return () => { if (GodotObject.IsInstanceValid(t)) t.Toggled -= H; }; });
        yield return BindingTargetDefinition.Create<Control, bool>("Visible", t => t.Visible, (t,v) => t.Visible = v);
        yield return BindingTargetDefinition.Create<BaseButton, bool>("Disabled", t => t.Disabled, (t,v) => t.Disabled = v);
        yield return BindingTargetDefinition.Create<TextureRect, Texture2D?>("Texture", t => t.Texture, (t,v) => t.Texture = v);
        yield return BindingTargetDefinition.Create<Control, Color>("Modulate", t => t.Modulate, (t,v) => t.Modulate = v);
        yield return BindingTargetDefinition.Create<Control, Color>("SelfModulate", t => t.SelfModulate, (t,v) => t.SelfModulate = v);
    }
}
