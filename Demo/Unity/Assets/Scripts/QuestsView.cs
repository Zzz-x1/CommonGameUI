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
public sealed class QuestsView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        { Bindings.Button("Panel/Claim", async () => { App.Store.ClaimQuest(); await App.RewardAsync("任务奖励\n金币 +800\n经验手记 +4"); }); return Task.CompletedTask; }
        protected override void Refresh()
        {
            Bindings.Text("Panel/Text", "整装待发\n\n在商店完成一次购买\n进度 " + Math.Min(1, Data.Purchases) + "/1\n\n奖励：800 金币、4 份经验手记" + (Data.QuestClaimed ? "\n已领取" : ""));
            this.GetControl<Button>("Panel/Claim").interactable = Data.Purchases >= 1 && !Data.QuestClaimed;
        }
    }
}
