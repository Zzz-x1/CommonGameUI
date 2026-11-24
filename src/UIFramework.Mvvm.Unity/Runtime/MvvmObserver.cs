using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace UIFramework.Mvvm.Unity
{
    internal static class MvvmObserver
    {
        private static MvvmFrameDriver _driver = new MvvmFrameDriver();

        internal static void Attach(MvvmRuntime runtime) => _driver.Attach(runtime);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeCanvasUpdate()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            int phase = Array.FindIndex(loop.subSystemList, system => system.type == typeof(PostLateUpdate));
            if (phase < 0) throw new InvalidOperationException("The Unity PlayerLoop is missing PostLateUpdate.");
            var systems = new List<PlayerLoopSystem>(loop.subSystemList[phase].subSystemList);
            // The Editor can retain a custom loop across Play Mode sessions without domain reload.
            systems.RemoveAll(system => system.type == typeof(MvvmObserver));
            int canvas = systems.FindIndex(system => system.type == typeof(PostLateUpdate.PlayerUpdateCanvases));
            if (canvas < 0) throw new InvalidOperationException("The Unity PlayerLoop is missing PlayerUpdateCanvases.");
            systems.Insert(canvas, new PlayerLoopSystem { type = typeof(MvvmObserver), updateDelegate = Tick });
            loop.subSystemList[phase].subSystemList = systems.ToArray();
            PlayerLoop.SetPlayerLoop(loop);
            Application.quitting -= EndSession;
            Application.quitting += EndSession;
        }

        private static void Tick() => _driver.Tick();

        private static void EndSession()
        {
            Application.quitting -= EndSession;
            _driver = new MvvmFrameDriver();
        }
    }
}
