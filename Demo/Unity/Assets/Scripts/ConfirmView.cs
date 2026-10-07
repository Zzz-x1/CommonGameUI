using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Unity;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Unity;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace StarCourt.UnityDemo
{
public sealed class ConfirmView : EngineUI.DialogNode<bool>
    {
        public string Message { get; private set; } = "";
    protected override void OnSetParameters(object args) => Message = args is string message
        ? message : throw new ArgumentException("Confirmation requires a message.", nameof(args));
        private ViewBindings _bindings;
        
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this);
            _bindings.Button("Panel/Yes", () => { Complete(true); return Task.CompletedTask; });
            _bindings.Button("Panel/No", () => { Complete(false); return Task.CompletedTask; });
            return Task.CompletedTask;
        }
        protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token)
        { _bindings.Text("Panel/Text", Message); return Task.CompletedTask; }
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
    }
}
