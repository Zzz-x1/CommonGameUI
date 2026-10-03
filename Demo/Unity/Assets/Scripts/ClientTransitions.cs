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
            if (!UnityClient.Instance.Store.State.FullMotion) return UnityTransitions.Fade(.12);
            if (context.Source is ItemView || context.Target is ItemView) return UnityTransitions.Fade(.18);
            if (context.Direction == TransitionDirection.Backward) return UnityTransitions.SlideHorizontal(.18);
            if (context.Entering && context.Source is LobbyView) return UnityTransitions.ScaleFade(.35);
            var left = context.Source is ShopView && context.Target is InventoryView;
            var away = new UnityTransitionFrame(relativeOffset: new Vector2((left ? -1 : 1) * (context.Entering ? 1 : -1), 0));
            return UnityTransitions.Simple(context.Entering ? away : new UnityTransitionFrame(),
                context.Entering ? new UnityTransitionFrame() : away, .32);
        });
    }
}
