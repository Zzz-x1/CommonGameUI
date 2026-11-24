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

    public static IDisposable BindTwoWay<T>(this IUINode node, string path, Action<T> apply,
        Func<Action<T>, Action> subscribe) =>
        StartCallback(node, path, apply, binding => binding.ConnectInput<T>(subscribe, binding.WriteValue), writeBack: true);

    public static IDisposable BindTwoWay<T1, TEdit>(this IUINode node, string path, Action<T1> apply,
        Func<Action<TEdit>, Action> subscribe, Action<TEdit> commit) =>
        StartCallback(node, path, apply, binding => binding.ConnectInput(subscribe, commit));

    public static IDisposable BindTwoWay<T1, T2, TEdit>(this IUINode node, string path1, string path2,
        Action<T1, T2> apply, Func<Action<TEdit>, Action> subscribe, Action<TEdit> commit) =>
        StartCallback(node, path1, path2, apply, binding => binding.ConnectInput(subscribe, commit));

    public static IDisposable BindTwoWay<T1, T2, T3, TEdit>(this IUINode node, string path1, string path2, string path3,
        Action<T1, T2, T3> apply, Func<Action<TEdit>, Action> subscribe, Action<TEdit> commit) =>
        StartCallback(node, path1, path2, path3, apply, binding => binding.ConnectInput(subscribe, commit));

    private static IDisposable StartCallback<T>(IUINode node, string path, Action<T> apply,
        Action<BindingScope.CallbackBinding>? connect = null, bool writeBack = false) =>
        Scope(node.Node).BindCallback(new[] { path }, new[] { typeof(T) }, values => apply((T)values[0]!),
            connect: connect, writeBack: writeBack);
    private static IDisposable StartCallback<T1, T2>(IUINode node, string path1, string path2, Action<T1, T2> apply,
        Action<BindingScope.CallbackBinding>? connect = null) =>
        Scope(node.Node).BindCallback(new[] { path1, path2 }, new[] { typeof(T1), typeof(T2) },
            values => apply((T1)values[0]!, (T2)values[1]!), connect: connect);
    private static IDisposable StartCallback<T1, T2, T3>(IUINode node, string path1, string path2, string path3, Action<T1, T2, T3> apply,
        Action<BindingScope.CallbackBinding>? connect = null) =>
        Scope(node.Node).BindCallback(new[] { path1, path2, path3 }, new[] { typeof(T1), typeof(T2), typeof(T3) },
            values => apply((T1)values[0]!, (T2)values[1]!, (T3)values[2]!), connect: connect);
}
