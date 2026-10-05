using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UIFramework.Unity
{
    public static class UnityUI
    {
        public static UIContext Create(Canvas canvas, EventSystem events, IUIResourceLoader loader = null, IUILog logger = null)
        {
            try
            {
                if (!canvas || !canvas.isActiveAndEnabled) throw new ArgumentException("An active scene Canvas is required.", nameof(canvas));
                var frames = UnityLifetimeRunner.Get().Frames;
                var host = new UnityUIHost(events, frames);
                return new UIContext(host, loader ?? new UnityPrefabLoader(host), logger ?? host);
            }
            catch (Exception ex) { Report(ex, "Create Unity UI context"); throw; }
        }
        internal static void Report(Exception exception, string operation)
        {
            const string logged = "UIFramework.Logged";
            if (exception.Data.Contains(logged)) return;
            exception.Data[logged] = true;
            try { Debug.LogError("[UI] " + operation); Debug.LogException(exception); }
            catch (Exception loggingFailure) { AddSecondary(exception, loggingFailure); Console.Error.WriteLine(loggingFailure); }
        }
        private static void AddSecondary(Exception primary, Exception secondary)
        {
            if (!(primary.Data[UIFailure.SecondaryExceptionsKey] is List<Exception> failures))
                primary.Data[UIFailure.SecondaryExceptionsKey] = failures = new List<Exception>();
            failures.Add(secondary);
        }
        internal static void Cleanup(Exception primary, Action cleanup)
        {
            try { cleanup(); }
            catch (Exception ex)
            {
                Report(ex, "Restore Unity display");
                if (primary == null) throw;
                AddSecondary(primary, ex);
            }
        }
        internal static T Configure<T>(Func<T> configure)
        { try { return configure(); } catch (Exception ex) { Report(ex, "Configure Unity effect"); throw; } }
    }
    public static class UnityUIExtensions
    {
        private static UnityUIHost Host(UINode node)
        {
            var host = node.Context.Host as UnityUIHost ?? throw new InvalidOperationException("Expected a Unity UI context.");
            host.VerifyAccess();
            return host;
        }
        public static Task AttachToAsync(this UINode node, UINode parent, string name, RectTransform rect, CancellationToken token = default)
        {
            try { return node.AttachAsync(parent, name, new UnityUIElement(Host(parent), rect), token); }
            catch (Exception ex) { UnityUI.Report(ex, "Attach Unity node " + name); throw; }
        }
        public static async Task<T> CreateContainerAsync<T>(this UINode parent, string name, RectTransform mount, CancellationToken token = default) where T : ContainerNode, new()
        {
            try { var node = new T(); await node.AttachToAsync(parent, name, mount, token); return node; }
            catch (Exception ex) { UnityUI.Report(ex, "Create Unity container " + name); throw; }
        }
        public static Task BindAsync(this TabContainerNode tabs, string name, RectTransform page, CancellationToken token = default) => tabs.BindAsync(name, new UINode(), page, token);
        public static Task BindAsync(this TabContainerNode tabs, string name, UINode node, RectTransform page, CancellationToken token = default)
        {
            try { return tabs.BindAsync(name, node, new UnityUIElement(Host(tabs), page), token); }
            catch (Exception ex) { UnityUI.Report(ex, "Bind Unity Tab " + name); throw; }
        }
        public static RectTransform GetRect(this UINode node)
        {
            try
            {
                Host(node);
                if (node.State == NodeState.Disposing || node.State == NodeState.Disposed) throw new ObjectDisposedException(node.Name);
                var element = node.Element as UnityUIElement ?? throw new InvalidOperationException("Node has no Unity display.");
                if (!element.Rect) throw new ObjectDisposedException(node.Name);
                return element.Rect;
            }
            catch (Exception ex) { UnityUI.Report(ex, "Get Unity RectTransform"); throw; }
        }
        public static T GetComponent<T>(this UINode node, string path) where T : Component
        {
            try
            {
                var child = node.GetRect().Find(path);
                if (!child) throw new KeyNotFoundException("Unity UI path not found: " + path);
                var component = child.GetComponent<T>();
                return component ? component : throw new InvalidOperationException("Expected " + typeof(T).Name + " at " + path);
            }
            catch (Exception ex) { UnityUI.Report(ex, "Get Unity component " + path); throw; }
        }
    }
}
