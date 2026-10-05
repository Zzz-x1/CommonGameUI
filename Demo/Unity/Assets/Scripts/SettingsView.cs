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
public sealed class SettingsView : ClientView
    {
        public TabContainerNode Tabs { get; private set; }
        protected override async Task InitializeViewAsync(CancellationToken token)
        {
            Bindings.Button("Panel/Close", App.BackAsync);
            Bindings.Button("Panel/Pages/画面/Graphics", () => App.Modals.OpenAsync<GraphicsView>());
            Bindings.Button("Panel/Defaults", async () =>
            {
                if (await App.AskAsync("恢复默认画质与演出设置？")) App.Store.Change(s => { s.FullMotion = true; s.Quality = 2; });
            });
            Tabs = await this.CreateContainerAsync<TabContainerNode>("Categories", this.GetControl<RectTransform>("Panel/Pages"), token);
            foreach (var name in new[] { "画面", "演出" })
            {
                var item = name;
                await Tabs.BindAsync(name, new UINode { Transition = EngineUI.Transitions.Fade(.18) }, this.GetControl<RectTransform>("Panel/Pages/" + name), token);
                Bindings.Button("Panel/Tabs/" + name, () => Tabs.CurrentName == item ? Task.CompletedTask : Tabs.SwitchAsync(item));
            }
            var vm = this.CreateViewModel<ClientSettingsVm>("Settings"); vm.Initialize(App.Store);
            this.Bind("Settings.FullMotion", this.GetControl<Toggle>("Panel/Pages/演出/Motion").ToggleTarget(), BindingMode.TwoWay);
            await Tabs.SwitchAsync("画面", false, token);
            DismissRequested = _ => App.Modals.CloseAsync(this);
        }
        protected override void Refresh()
        {
            this.GetControl<Toggle>("Panel/Pages/演出/Motion").SetIsOnWithoutNotify(Data.FullMotion);
            Bindings.Text("Panel/Pages/画面/Text", "画质档位：" + new[] { "流畅", "均衡", "精致" }[Data.Quality] + "\n\n这是纯 UI 客户端，画质档位保存为本地偏好。");
        }
    }
}
