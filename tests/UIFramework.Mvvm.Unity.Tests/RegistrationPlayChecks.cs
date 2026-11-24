using System;
using UIFramework.Mvvm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NativeMvvm.Tests
{
    // Batch entry point for the isolated converter test project.
    public static class RegistrationPlayChecks
    {
        private static int _sessions;
        private static bool _leaving;
        private static double _deadline;
        private static bool _enabled;
        private static EnterPlayModeOptions _options;
        private static BindingTargetDefinition _preview;

        public static void Run()
        {
            _preview = BindingTargetRegistry.Find(typeof(TargetPresentation), "Delay");
            _enabled = EditorSettings.enterPlayModeOptionsEnabled;
            _options = EditorSettings.enterPlayModeOptions;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            _deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                    throw new TimeoutException("Timed out cycling Play Mode for MVVM registration.");
                if (!_leaving && EditorApplication.isPlaying)
                {
                    if (!ReferenceEquals(_preview, BindingTargetRegistry.Find(typeof(TargetPresentation), "Delay")))
                        throw new InvalidOperationException("Play Mode lost the target definition registered for preview.");
                    _sessions++;
                    _leaving = true;
                    EditorApplication.ExitPlaymode();
                }
                else if (_leaving && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if (_sessions == 2) Finish(0);
                    else
                    {
                        _leaving = false;
                        EditorApplication.EnterPlaymode();
                    }
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            EditorSettings.enterPlayModeOptions = _options;
            EditorSettings.enterPlayModeOptionsEnabled = _enabled;
            Debug.Log("MVVM registration Play Mode sessions: " + _sessions);
            EditorApplication.Exit(code);
        }
    }
}
