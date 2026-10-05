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
public sealed class CompareView : EngineUI.UINode<CompareArgs>
    {
        private ViewBindings _bindings;
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this);
            this.GetControl<RectTransform>("Panel").anchoredPosition += Vector2.right * Parameters.Position * 370;
            _bindings.Text("Panel/Title", "装备对比 · " + (Parameters.Position + 1));
            _bindings.Text("Panel/Text", ClientStore.Items[Parameters.Item] + "\n\n攻击 +" + (80 + Parameters.Item * 15));
            _bindings.Button("Panel/Close", () => UnityClient.Instance.CloseComparisonAsync(this));
            return Task.CompletedTask;
        }
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
    }
}
