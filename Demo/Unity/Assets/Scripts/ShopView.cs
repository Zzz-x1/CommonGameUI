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
public sealed class ShopView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        { for (int i = 0; i < 8; i++) { var id = i; Bindings.Button("Cards/C" + i, () => App.OpenPurchaseAsync(id)); } return Task.CompletedTask; }
        protected override void Refresh()
        { for (int i = 0; i < 8; i++) Bindings.Text("Cards/C" + i + "/Text", ClientStore.Items[i] + "\n" + ClientStore.Prices[i] + " 金币\n查看与购买"); }
    }
}
