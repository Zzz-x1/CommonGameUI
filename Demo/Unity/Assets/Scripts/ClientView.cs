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
public abstract class ClientView : EngineUI.UINode
    {
        protected UnityClient App => UnityClient.Instance;
        protected PlayerState Data => App.Store.State;
        protected ViewBindings Bindings;
        protected override async Task OnInitializeAsync(CancellationToken token)
        {
            Bindings = new ViewBindings(this);
            Bindings.Listen(Refresh);
            await InitializeViewAsync(token);
            Refresh();
        }
        protected virtual Task InitializeViewAsync(CancellationToken token) => Task.CompletedTask;
        protected virtual void Refresh() { }
        protected override Task OnDisposeAsync() { Bindings.Dispose(); return Task.CompletedTask; }
        protected override void OnActivated(StateChangeContext context) => Refresh();
    }
}
