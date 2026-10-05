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
public sealed class ItemView : EngineUI.UINode<int>
    {
        private ViewBindings _bindings;
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this);
            var vm = this.CreateViewModel<ClientItemVm>("Item"); vm.Initialize(UnityClient.Instance.Store, Parameters);
            this.Bind("Item.Name", this.GetControl<Text>("Panel/Title").TextTarget());
            this.Bind("Item.Description", this.GetControl<Text>("Panel/Text").TextTarget());
            _bindings.Button("Panel/Compare", () => UnityClient.Instance.CompareAsync(Parameters));
            _bindings.Button("Panel/Back", UnityClient.Instance.BackAsync);
            return Task.CompletedTask;
        }
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
        public void RefreshComparison() => this.GetControl<Button>("Panel/Compare").interactable = UnityClient.Instance.Windows.Children.Count < 2;
    }
}
