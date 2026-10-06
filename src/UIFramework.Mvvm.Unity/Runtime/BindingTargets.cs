using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[assembly: UIFramework.Mvvm.BindingTargetProvider(typeof(UIFramework.Mvvm.Unity.BuiltinBindingTargets))]

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
        public static IBindingTarget Property(Component component, string member, BindingMode mode = BindingMode.OneWay) =>
            BindingTargetRegistry.Find(component.GetType(), member).CreateTarget(component, mode);
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

namespace UIFramework.Mvvm.Unity
{
    public sealed class BuiltinBindingTargets : IBindingTargetProvider
    {
        public System.Collections.Generic.IEnumerable<BindingTargetDefinition> GetTargets()
        {
            yield return BindingTargetDefinition.Create<Text, string>("text", t => t.text, (t,v) => t.text = v);
            yield return BindingTargetDefinition.Create<TMP_Text, string>("text", t => t.text, (t,v) => t.text = v);
            yield return BindingTargetDefinition.Create<InputField, string>("text", t => t.text, (t,v) => t.SetTextWithoutNotify(v), (t,a) => { UnityEngine.Events.UnityAction<string> h = _ => a(); t.onValueChanged.AddListener(h); return () => { if(t) t.onValueChanged.RemoveListener(h); }; });
            yield return BindingTargetDefinition.Create<TMP_InputField, string>("text", t => t.text, (t,v) => t.SetTextWithoutNotify(v), (t,a) => { UnityEngine.Events.UnityAction<string> h = _ => a(); t.onValueChanged.AddListener(h); return () => { if(t) t.onValueChanged.RemoveListener(h); }; });
            yield return BindingTargetDefinition.Create<Slider, float>("value", t => t.value, (t,v) => t.SetValueWithoutNotify(v), (t,a) => { UnityEngine.Events.UnityAction<float> h = _ => a(); t.onValueChanged.AddListener(h); return () => { if(t) t.onValueChanged.RemoveListener(h); }; });
            yield return BindingTargetDefinition.Create<Toggle, bool>("isOn", t => t.isOn, (t,v) => t.SetIsOnWithoutNotify(v), (t,a) => { UnityEngine.Events.UnityAction<bool> h = _ => a(); t.onValueChanged.AddListener(h); return () => { if(t) t.onValueChanged.RemoveListener(h); }; });
            yield return BindingTargetDefinition.Create<Selectable, bool>("interactable", t => t.interactable, (t,v) => t.interactable = v);
            yield return BindingTargetDefinition.Create<Component, bool>("activeSelf", t => t.gameObject.activeSelf, (t,v) => t.gameObject.SetActive(v));
            yield return BindingTargetDefinition.Create<Image, Sprite>("sprite", t => t.sprite, (t,v) => t.sprite = v);
            yield return BindingTargetDefinition.Create<RawImage, Texture>("texture", t => t.texture, (t,v) => t.texture = v);
            yield return BindingTargetDefinition.Create<Behaviour, bool>("enabled", t => t.enabled, (t,v) => t.enabled = v);
            yield return BindingTargetDefinition.Create<Graphic, Color>("color", t => t.color, (t,v) => t.color = v);
            yield return BindingTargetDefinition.Create<CanvasGroup, float>("alpha", t => t.alpha, (t,v) => t.alpha = v);
        }
    }
}
