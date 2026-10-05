using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UIFramework;
using EngineUI = UIFramework.Unity;

namespace UIFramework.Tests.Unity
{
    public sealed class NativeWindow : EngineUI.UINode
    {
        [SerializeField] private UnityEngine.UI.Button closeButton = null;
        public UnityEngine.UI.Button CloseControl => closeButton;
        [System.NonSerialized] public int AwakeCount, Initialized, Disposals;
        [System.NonSerialized] public bool Configured;
        private void Awake() { AwakeCount++; }
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            if (AwakeCount != 1 || !Configured) throw new System.InvalidOperationException("Native preparation and configuration must precede UI initialization.");
            Initialized++;
            closeButton.onClick.AddListener(() => Observe(((PanelContainerNode)Parent).CloseAsync(this)));
            return Task.CompletedTask;
        }
        protected override Task OnDisposeAsync() { Disposals++; return Task.CompletedTask; }
    }
}
