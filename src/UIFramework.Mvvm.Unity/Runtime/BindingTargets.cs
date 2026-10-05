using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UIFramework.Mvvm.Unity
{
    public static class BindingTargets
    {
        public static IBindingTarget TextTarget(this Text text) => new Target<string>(text, () => text.text, v => text.text = v);
        public static IBindingTarget TextTarget(this TMP_Text text) => new Target<string>(text, () => text.text, v => text.text = v);
        public static IBindingTarget ValueTarget(this Slider slider) => new Target<float>(slider, () => slider.value, slider.SetValueWithoutNotify, callback => { UnityEngine.Events.UnityAction<float> listener = _ => callback(); slider.onValueChanged.AddListener(listener); return () => slider.onValueChanged.RemoveListener(listener); });
        public static IBindingTarget ToggleTarget(this Toggle toggle) => new Target<bool>(toggle, () => toggle.isOn, toggle.SetIsOnWithoutNotify, callback => { UnityEngine.Events.UnityAction<bool> listener = _ => callback(); toggle.onValueChanged.AddListener(listener); return () => toggle.onValueChanged.RemoveListener(listener); });
        public static IBindingTarget TextTarget(this InputField input) => new Target<string>(input, () => input.text, input.SetTextWithoutNotify, callback => { UnityEngine.Events.UnityAction<string> listener = _ => callback(); input.onValueChanged.AddListener(listener); return () => input.onValueChanged.RemoveListener(listener); });
        public static IBindingTarget TextTarget(this TMP_InputField input) => new Target<string>(input, () => input.text, input.SetTextWithoutNotify, callback => { UnityEngine.Events.UnityAction<string> listener = _ => callback(); input.onValueChanged.AddListener(listener); return () => input.onValueChanged.RemoveListener(listener); });
        public static ICommandTarget ClickTarget(this Button button) => new ButtonTarget(button);
        public static IBindingTarget Property(Component component, string member)
        {
            switch (member)
            {
                case "text":
                    if (component is Text text) return text.TextTarget(); if (component is TMP_Text tmp) return tmp.TextTarget();
                    if (component is InputField input) return input.TextTarget(); return ((TMP_InputField)component).TextTarget();
                case "value": return ((Slider)component).ValueTarget();
                case "isOn": return ((Toggle)component).ToggleTarget();
                case "interactable": return new Target<bool>(component, () => ((Selectable)component).interactable, v => ((Selectable)component).interactable = v);
                case "activeSelf": return new Target<bool>(component, () => component.gameObject.activeSelf, v => component.gameObject.SetActive(v));
                case "sprite": return new Target<Sprite>(component, () => ((Image)component).sprite, v => ((Image)component).sprite = v);
                case "texture": return new Target<Texture>(component, () => ((RawImage)component).texture, v => ((RawImage)component).texture = v);
                default: throw new MissingMemberException(component.GetType().FullName, member);
            }
        }
        private sealed class Target<T> : IBindingTarget
        {
            private readonly UnityEngine.Object _owner; private readonly Func<T> _read; private readonly Action<T> _write; private readonly Action _disconnect;
            public Target(UnityEngine.Object owner, Func<T> read, Action<T> write, Func<Action, Action> subscribe = null)
            { _owner = owner; _read = read; _write = write; _disconnect = subscribe?.Invoke(() => Changed?.Invoke()); }
            public Type ValueType => typeof(T); public object Read() => _read(); public void Write(object value) => _write((T)value); public event Action Changed;
            public void Dispose() { if (_owner) _disconnect?.Invoke(); }
        }
        private sealed class ButtonTarget : ICommandTarget
        {
            private readonly Button _button; public ButtonTarget(Button button) { _button = button; button.onClick.AddListener(Invoke); }
            private void Invoke() => Invoked?.Invoke(); public event Action Invoked; public void SetEnabled(bool enabled) => _button.interactable = enabled;
            public void Dispose() { if (_button) _button.onClick.RemoveListener(Invoke); }
        }
    }
}
