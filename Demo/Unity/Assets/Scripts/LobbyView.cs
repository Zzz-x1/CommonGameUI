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
public sealed class LobbyView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        {
            foreach (var route in new[] { "Inventory", "Characters", "Shop", "Team", "Quests", "Settings" })
            { var name = route; Bindings.Button("Entries/" + name, () => App.OpenAsync(name)); }
            var hero = this.GetControl<RectTransform>("Hero");
            var position = hero.anchoredPosition; var scale = hero.localScale;
            var groups = this.GetRoot().GetComponentsInChildren<CanvasGroup>(true).ToDictionary(g => g, g => g.alpha);
            Transition = Transitions.Select(c => c.Phase == TransitionPhase.PushEnter
                ? EngineUI.Transitions.Timeline("Transitions/Enter", "Transitions/Exit", root =>
                { hero.anchoredPosition = position; hero.localScale = scale; foreach (var pair in groups) pair.Key.alpha = pair.Value; })
                : EngineUI.Transitions.Fade(.2));
            return Task.CompletedTask;
        }
        protected override void Refresh() => Bindings.Text("Welcome/Text", "晨光已至，旅行者\n\n同行者正在等待下一次远征。\n金币 " + Data.Gold.ToString("N0") + " · 已购 " + Data.Purchases + " 次");
    }
}
