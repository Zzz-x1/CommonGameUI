namespace UIFramework.Mvvm;

public static partial class MvvmExtensions
{
    public static IDisposable Bind<T>(this IUINode node, string path, Action<T> apply) =>
        StartCallback(node, path, apply);

    public static IDisposable Bind<T1, T2>(this IUINode node, string path1, string path2, Action<T1, T2> apply) =>
        StartCallback(node, path1, path2, apply);

    public static IDisposable Bind<T1, T2, T3>(this IUINode node, string path1, string path2, string path3, Action<T1, T2, T3> apply) =>
        StartCallback(node, path1, path2, path3, apply);

    public static IDisposable Bind<T1, T2, TResult>(this IUINode node, string path1, string path2,
        IBindingTarget target, Func<T1, T2, TResult> project) =>
        Scope(node.Node).BindCallback(new[] { path1, path2 }, new[] { typeof(T1), typeof(T2) },
            values => target.Write(project((T1)values[0]!, (T2)values[1]!)), target, typeof(TResult));

    public static IDisposable Bind<T1, T2, T3, TResult>(this IUINode node, string path1, string path2, string path3,
        IBindingTarget target, Func<T1, T2, T3, TResult> project) =>
        Scope(node.Node).BindCallback(new[] { path1, path2, path3 }, new[] { typeof(T1), typeof(T2), typeof(T3) },
            values => target.Write(project((T1)values[0]!, (T2)values[1]!, (T3)values[2]!)), target, typeof(TResult));

    private static BindingScope.CallbackBinding StartCallback<T>(IUINode node, string path, Action<T> apply) =>
        Scope(node.Node).BindCallback(new[] { path }, new[] { typeof(T) }, values => apply((T)values[0]!));
    private static BindingScope.CallbackBinding StartCallback<T1, T2>(IUINode node, string path1, string path2, Action<T1, T2> apply) =>
        Scope(node.Node).BindCallback(new[] { path1, path2 }, new[] { typeof(T1), typeof(T2) },
            values => apply((T1)values[0]!, (T2)values[1]!));
    private static BindingScope.CallbackBinding StartCallback<T1, T2, T3>(IUINode node, string path1, string path2, string path3, Action<T1, T2, T3> apply) =>
        Scope(node.Node).BindCallback(new[] { path1, path2, path3 }, new[] { typeof(T1), typeof(T2), typeof(T3) },
            values => apply((T1)values[0]!, (T2)values[1]!, (T3)values[2]!));
}
