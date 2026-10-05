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
public sealed class TeamView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        {
            for (int i = 0; i < 4; i++)
            {
                var slot = i;
                Bindings.Button("Slots/S" + i + "/Assign", () =>
                {
                    var hero = this.GetControl<Dropdown>("Slots/S" + slot + "/Hero").value - 1;
                    App.Store.Change(s => s.Team[slot] = hero); return Task.CompletedTask;
                });
                Bindings.Dropdown("Slots/S" + i + "/Hero", _ => Refresh());
            }
            return Task.CompletedTask;
        }
        protected override void Refresh()
        {
            for (int i = 0; i < 4; i++)
            {
                var hero = Data.Team[i]; var choice = this.GetControl<Dropdown>("Slots/S" + i + "/Hero").value - 1;
                Bindings.Text("Slots/S" + i + "/Text", "位置 " + (i + 1) + "\n" + (hero < 0 ? "空位" : ClientStore.Heroes[hero]));
                this.GetControl<Button>("Slots/S" + i + "/Assign").interactable = choice < 0 || !Data.Team.Contains(choice) || hero == choice;
            }
        }
    }
}
