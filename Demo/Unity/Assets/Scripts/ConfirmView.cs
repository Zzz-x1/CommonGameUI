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
        public string Message { get; set; } = "";
        private ViewBindings _bindings;
        
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this);
            _bindings.Text("Panel/Text", Message);
            _bindings.Button("Panel/Yes", () => { Complete(true); return Task.CompletedTask; });
            _bindings.Button("Panel/No", () => { Complete(false); return Task.CompletedTask; });
            return Task.CompletedTask;
        }
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
    }
}
