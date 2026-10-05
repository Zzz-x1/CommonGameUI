using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Unity;
using UIFramework;
using UIFramework.Unity;
using UnityEngine;

namespace StarCourt.UnityDemo
{
    // Project recipes use the actual container context; no extra routing state.
    public static class ClientTransitions
    {
        public static IUITransition Page() => Transitions.Select(context =>
        {
            if (!UnityClient.Instance.Store.State.FullMotion) return EngineUI.Transitions.Fade(.12);
            if (context.Source is ItemView || context.Target is ItemView) return EngineUI.Transitions.Fade(.18);
            if (context.Direction == TransitionDirection.Backward) return EngineUI.Transitions.SlideHorizontal(.18);
            if (context.Entering && context.Source is LobbyView) return EngineUI.Transitions.ScaleFade(.35);
            var left = context.Source is ShopView && context.Target is InventoryView;
            var away = new EngineUI.TransitionFrame(relativeOffset: new Vector2((left ? -1 : 1) * (context.Entering ? 1 : -1), 0));
            return EngineUI.Transitions.Simple(context.Entering ? away : new EngineUI.TransitionFrame(),
                context.Entering ? new EngineUI.TransitionFrame() : away, .32);
        });
    }
}
