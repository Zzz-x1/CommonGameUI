using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Unity;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StarCourt.UnityDemo
{
    public sealed class ViewBindings : IDisposable
    {
        private readonly IUINode _node;
        private readonly List<Action> _disconnect = new List<Action>();
        public ViewBindings(IUINode node) => _node = node;
        public void Text(string path, string value) => _node.GetControl<Text>(path).text = value;
        public void Button(string path, Func<Task> action)
        {
            var button = _node.GetControl<Button>(path);
            UnityAction click = () => UnityClient.Instance.Track(action());
            button.onClick.AddListener(click);
            _disconnect.Add(() => button.onClick.RemoveListener(click));
        }
        public void Listen(Action refresh)
        {
            UnityClient.Instance.Store.Changed += refresh;
            _disconnect.Add(() => UnityClient.Instance.Store.Changed -= refresh);
        }
        public void Dropdown(string path, UnityAction<int> changed)
        {
            var dropdown = _node.GetControl<Dropdown>(path);
            dropdown.onValueChanged.AddListener(changed);
            _disconnect.Add(() => dropdown.onValueChanged.RemoveListener(changed));
        }
        public void Toggle(string path, UnityAction<bool> changed)
        {
            var toggle = _node.GetControl<Toggle>(path);
            toggle.onValueChanged.AddListener(changed);
            _disconnect.Add(() => toggle.onValueChanged.RemoveListener(changed));
        }
        public void Dispose() { foreach (var disconnect in _disconnect) disconnect(); }
    }
}
