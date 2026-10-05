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
public sealed class CharactersView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        {
            for (int i = 0; i < 6; i++) { var id = i; Bindings.Button("Cards/C" + i, () => App.OpenHeroAsync(id)); }
            return Task.CompletedTask;
        }
        protected override void Refresh()
        { for (int i = 0; i < 6; i++) Bindings.Text("Cards/C" + i + "/Text", ClientStore.Heroes[i] + "\nLv." + Data.Levels[i]); }
    }
}
