using Godot;
using GodotRange = global::Godot.Range;

namespace UIFramework.Mvvm.Godot;

public static class BindingTargets
{
    public static IBindingTarget Property(Control control, string member, BindingMode mode = BindingMode.OneWay) =>
        BindingTargetRegistry.Find(control.GetType(), member).CreateTarget(control, mode);
}

[ControlAdapter(typeof(Control))]
[BindProperty(nameof(Control.Visible))]
[BindProperty(nameof(Control.Modulate))]
[BindProperty(nameof(Control.SelfModulate))]
internal static class ControlAdapterDefinition { }

[ControlAdapter(typeof(Label))]
[BindProperty(nameof(Label.Text))]
internal static class LabelAdapterDefinition { }

[ControlAdapter(typeof(LineEdit))]
[BindProperty(nameof(LineEdit.Text), Changed = nameof(LineEdit.TextChanged))]
internal static class LineEditAdapterDefinition { }

[ControlAdapter(typeof(GodotRange))]
[BindProperty(nameof(GodotRange.Value), Read = nameof(ReadValue), Write = nameof(WriteValue), Changed = nameof(GodotRange.ValueChanged))]
internal static class RangeAdapterDefinition
{
    internal static float ReadValue(GodotRange range) => (float)range.Value;
    internal static void WriteValue(GodotRange range, float value) => range.SetValueNoSignal(value);
}

[ControlAdapter(typeof(TextureRect))]
[BindProperty(nameof(TextureRect.Texture))]
internal static class TextureRectAdapterDefinition { }

[ControlAdapter(typeof(BaseButton))]
[BindProperty(nameof(BaseButton.Disabled))]
[BindProperty(nameof(BaseButton.ButtonPressed), Write = nameof(BaseButton.SetPressedNoSignal), Changed = nameof(BaseButton.Toggled))]
[BindCommand("Click", Factory = nameof(CreateClick))]
internal static class BaseButtonAdapterDefinition
{
    internal static ICommandTarget CreateClick(BaseButton button) => new ButtonTarget(button);

    private sealed class ButtonTarget : ICommandTarget
    {
        private readonly BaseButton _button;

        public ButtonTarget(BaseButton button)
        {
            _button = button;
            _button.Pressed += Invoke;
        }

        public event Action? Invoked;
        private void Invoke() => Invoked?.Invoke();
        public void SetEnabled(bool enabled) => _button.Disabled = !enabled;

        public void Dispose()
        {
            if (GodotObject.IsInstanceValid(_button)) _button.Pressed -= Invoke;
        }
    }
}
