using Godot;
using UIFramework.Godot;
namespace UIFramework.Mvvm.Godot;
public static class GodotBindingTargets
{
    public static IBindingTarget TextTarget(this Label label) => new Target<string>(label, () => label.Text, v => label.Text = v);
    public static IBindingTarget TextTarget(this LineEdit edit) => new Target<string>(edit, () => edit.Text, v => edit.Text = v, a => { void Changed(string _) => a(); edit.TextChanged += Changed; return () => edit.TextChanged -= Changed; });
    public static IBindingTarget ValueTarget(this global::Godot.Range range) => new Target<float>(range, () => (float)range.Value, v => range.SetValueNoSignal(v), a => { void Changed(double _) => a(); range.ValueChanged += Changed; return () => range.ValueChanged -= Changed; });
    public static IBindingTarget ToggleTarget(this BaseButton button) => new Target<bool>(button, () => button.ButtonPressed, button.SetPressedNoSignal, a => { void Changed(bool _) => a(); button.Toggled += Changed; return () => button.Toggled -= Changed; });
    public static ICommandTarget ClickTarget(this BaseButton button) => new ButtonTarget(button);
    public static IBindingTarget Property(Control control, string member) => member switch
    {
        "Text" => control is Label label ? label.TextTarget() : ((LineEdit)control).TextTarget(),
        "Value" => ((global::Godot.Range)control).ValueTarget(),
        "ButtonPressed" => ((BaseButton)control).ToggleTarget(),
        "Visible" => new Target<bool>(control, () => control.Visible, v => control.Visible = v),
        "Disabled" => new Target<bool>(control, () => ((BaseButton)control).Disabled, v => ((BaseButton)control).Disabled = v),
        "Texture" => new Target<Texture2D?>(control, () => ((TextureRect)control).Texture, v => ((TextureRect)control).Texture = v),
        _ => throw new MissingMemberException(control.GetType().Name, member)
    };
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
