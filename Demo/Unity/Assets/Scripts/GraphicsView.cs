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
public sealed class GraphicsView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        {
            Bindings.Dropdown("Panel/Quality", value => App.Store.Change(s => s.Quality = value));
            Bindings.Button("Panel/Close", App.BackAsync);
            DismissRequested = _ => App.Modals.CloseAsync(this);
            return Task.CompletedTask;
        }
        protected override void Refresh() => this.GetControl<Dropdown>("Panel/Quality").SetValueWithoutNotify(Data.Quality);
    }
}
