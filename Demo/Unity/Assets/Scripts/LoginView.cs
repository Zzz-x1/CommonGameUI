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
public sealed class LoginView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        { Bindings.Button("Panel/Enter", App.EnterAsync); return Task.CompletedTask; }
    }
}
