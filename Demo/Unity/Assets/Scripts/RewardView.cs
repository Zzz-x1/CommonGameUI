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
public sealed class RewardView : EngineUI.UINode<string>
    {
        private ViewBindings _bindings;
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this);
            _bindings.Text("Panel/Text", Parameters);
            _bindings.Button("Panel/Close", UnityClient.Instance.BackAsync);
            var rects = this.GetRoot().GetComponentsInChildren<RectTransform>(true)
                .ToDictionary(r => r, r => (r.anchoredPosition, r.localScale, r.localRotation));
            var groups = this.GetRoot().GetComponentsInChildren<CanvasGroup>(true).ToDictionary(g => g, g => g.alpha);
            Transition = Transitions.Select(c => c.Entering && c.Source is PurchaseView && UnityClient.Instance.Store.State.FullMotion
                ? EngineUI.Transitions.Timeline("Transitions/Enter", "Transitions/Exit", root =>
                {
                    foreach (var pair in rects)
                    { pair.Key.anchoredPosition = pair.Value.anchoredPosition; pair.Key.localScale = pair.Value.localScale; pair.Key.localRotation = pair.Value.localRotation; }
                    foreach (var pair in groups) pair.Key.alpha = pair.Value;
                })
                : EngineUI.Transitions.ScaleFade(.15, "Panel"));
            DismissRequested = _ => UnityClient.Instance.Modals.CloseAsync(this);
            return Task.CompletedTask;
        }
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
    }
}
