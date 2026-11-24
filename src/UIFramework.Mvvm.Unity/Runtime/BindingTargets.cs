using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UIFramework.Mvvm.Unity
{
    public static class BindingTargets
    {
        public static IBindingTarget Property(Component component, string member, BindingMode mode = BindingMode.OneWay) =>
            BindingTargetRegistry.Find(component.GetType(), member).CreateTarget(component, mode);
    }

    [ControlAdapter(typeof(Component))]
    [BindProperty(nameof(GameObject.activeSelf), Read = nameof(ReadActiveSelf), Write = nameof(WriteActiveSelf))]
    internal static class ComponentAdapter
    {
        internal static bool ReadActiveSelf(Component component) => component.gameObject.activeSelf;
        internal static void WriteActiveSelf(Component component, bool value) => component.gameObject.SetActive(value);
    }

    [ControlAdapter(typeof(Behaviour))]
    [BindProperty(nameof(Behaviour.enabled))]
    internal static class BehaviourAdapter { }

    [ControlAdapter(typeof(CanvasGroup))]
    [BindProperty(nameof(CanvasGroup.alpha))]
    internal static class CanvasGroupAdapter { }

    [ControlAdapter(typeof(Graphic))]
    [BindProperty(nameof(Graphic.color))]
    internal static class GraphicAdapter { }

    [ControlAdapter(typeof(Text))]
    [BindProperty(nameof(Text.text))]
    internal static class TextAdapter { }

    [ControlAdapter(typeof(Image))]
    [BindProperty(nameof(Image.sprite))]
    internal static class ImageAdapter { }

    [ControlAdapter(typeof(RawImage))]
    [BindProperty(nameof(RawImage.texture))]
    internal static class RawImageAdapter { }

    [ControlAdapter(typeof(Selectable))]
    [BindProperty(nameof(Selectable.interactable))]
    internal static class SelectableAdapter { }

    [ControlAdapter(typeof(Slider))]
    [BindProperty(nameof(Slider.value), Write = nameof(Slider.SetValueWithoutNotify), Changed = nameof(Slider.onValueChanged))]
    internal static class SliderAdapter { }

    [ControlAdapter(typeof(Toggle))]
    [BindProperty(nameof(Toggle.isOn), Write = nameof(Toggle.SetIsOnWithoutNotify), Changed = nameof(Toggle.onValueChanged))]
    internal static class ToggleAdapter { }

    [ControlAdapter(typeof(InputField))]
    [BindProperty(nameof(InputField.text), Write = nameof(InputField.SetTextWithoutNotify), Changed = nameof(InputField.onValueChanged))]
    internal static class InputFieldAdapter { }

    [ControlAdapter(typeof(Button))]
    [BindCommand("Click", Factory = nameof(CreateClick))]
    internal static class ButtonAdapter
    {
        internal static ICommandTarget CreateClick(Button button) => new ButtonTarget(button);

        private sealed class ButtonTarget : ICommandTarget
        {
            private readonly Button _button;
            private UnityEvent _click;
            private Action _invoked;

            internal ButtonTarget(Button button) => _button = button;

            public event Action Invoked
            {
                add
                {
                    if (_invoked == null)
                    {
                        _click = _button.onClick;
                        _click.AddListener(Invoke);
                    }
                    _invoked += value;
                }
                remove
                {
                    _invoked -= value;
                    if (_invoked == null) Dispose();
                }
            }

            private void Invoke() => _invoked?.Invoke();
            public void SetEnabled(bool enabled) => _button.interactable = enabled;

            public void Dispose()
            {
                _click?.RemoveListener(Invoke);
                _click = null;
                _invoked = null;
            }
        }
    }
}

namespace UIFramework.Mvvm.Unity.TMP
{
    [ControlAdapter(typeof(TMP_Text))]
    [BindProperty(nameof(TMP_Text.text))]
    internal static class TmpTextAdapter { }

    [ControlAdapter(typeof(TMP_InputField))]
    [BindProperty(nameof(TMP_InputField.text), Write = nameof(TMP_InputField.SetTextWithoutNotify), Changed = nameof(TMP_InputField.onValueChanged))]
    internal static class TmpInputFieldAdapter { }
}
