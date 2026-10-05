using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Unity;
using System;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using StarCourt.UnityDemo;
using StarCourt.UnityDemo.Editor;
using UIFramework;
using UIFramework.Unity;
using UnityEngine;

public static class ClientCaptures
{
    public static Task At1280() => Run(1280,720);
    public static Task At1600() => Run(1600,900);
    public static Task At1920() => Run(1920,1080);
    private static async Task Run(int width, int height)
    {
        var app = UnityClient.Instance;
        await app.Ready;
        ClientSceneAuthoring.SetResolution(width,height);
        var host = (EngineUI.UIHost)app.UI.Host;
        var captureType = Type.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands, Unity.Pipeline.Editor", true);
        var capture = captureType.GetMethod("CaptureGameView", BindingFlags.Public | BindingFlags.Static);
        async Task Save(string name)
        {
            await host.NextFrameAsync(); await host.NextFrameAsync();
            if (Screen.width != width || Screen.height != height) throw new InvalidOperationException("Game View did not adopt the requested native resolution.");
            capture.Invoke(null,new object[] {width,height,null,"Temp/Captures/"+width+"/"+name+".png",false,0,"screen"});
        }
        if (!(app.Pages.Current is LoginView)) { await app.Pages.PopAsync(); app.Navigation.SetActive(false); }
        await Save("login");
        var entering = app.EnterAsync();
        while (!entering.IsCompleted && !app.Pages.Children.OfType<LobbyView>().Any(node => node.TransitionState?.Stage == TransitionStage.Playing && node.TransitionState.Progress >= .35))
            await host.NextFrameAsync();
        await Save("lobby-middle"); await entering; await Save("lobby");
        await app.OpenAsync("Inventory"); var inventory=(InventoryView)app.Pages.Current;
        await Save("inventory"); await inventory.Tabs.SwitchAsync("材料"); await Save("materials"); await inventory.Tabs.SwitchAsync("装备");
        await app.OpenItemAsync(0); await Save("item"); await app.CompareAsync(0); await app.CompareAsync(0); await Save("comparison");
        await app.BackAsync(); await app.BackAsync();
        await app.OpenAsync("Characters"); await Save("characters"); await app.OpenHeroAsync(0); await Save("hero");
        await app.OpenAsync("Shop"); await Save("shop"); await app.OpenPurchaseAsync(0); await Save("purchase");
        var confirmation = app.AskAsync("确认购买这份旅途补给？");
        while (!(app.Modals.Current is ConfirmView) || !app.Modals.Current.IsInteractive) await host.NextFrameAsync();
        await Save("confirm"); ((ConfirmView)app.Modals.Current).Complete(false); await confirmation;
        entering=app.RewardAsync(ClientStore.Items[0]+" × 1\n补给已收入背包");
        while (!entering.IsCompleted && !app.Modals.Children.OfType<RewardView>().Any(n => n.TransitionState?.Stage == TransitionStage.Playing && n.TransitionState.Progress >= .45))
            await host.NextFrameAsync();
        await Save("reward-middle"); await entering; await Save("reward"); await app.BackAsync(); await app.BackAsync();
        await app.OpenAsync("Quests"); await Save("quests"); await app.OpenAsync("Team"); await Save("team");
        await app.OpenAsync("Settings"); var settings=(SettingsView)app.Modals.Current; await Save("settings");
        await app.Modals.OpenAsync<GraphicsView>(); await Save("graphics"); await app.BackAsync();
        await settings.Tabs.SwitchAsync("演出"); await Save("effects"); await app.BackAsync(); await app.OpenAsync("Home");
    }
}
