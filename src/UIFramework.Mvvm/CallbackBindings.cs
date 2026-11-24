namespace UIFramework.Mvvm;

public static partial class MvvmExtensions
{
    public static IDisposable Bind<T>(this IUINode node, string path, Action<T> apply,
        BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame) =>
        StartCallback(node, path, apply, timing);

    public static IDisposable Bind<T1, T2>(this IUINode node, string path1, string path2, Action<T1, T2> apply,
        BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame) =>
        StartCallback(node, path1, path2, apply, timing);

    public static IDisposable Bind<T1, T2, T3>(this IUINode node, string path1, string path2, string path3, Action<T1, T2, T3> apply,
        BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame) =>
        StartCallback(node, path1, path2, path3, apply, timing);

    public static IDisposable Bind<T1, T2, TResult>(this IUINode node, string path1, string path2,
        IBindingTarget target, Func<T1, T2, TResult> project, BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame) =>
        Scope(node.Node).BindCallback(new[] { path1, path2 }, new[] { typeof(T1), typeof(T2) }, null, timing,
            target, typeof(TResult), values => project((T1)values[0]!, (T2)values[1]!));

    public static IDisposable Bind<T1, T2, T3, TResult>(this IUINode node, string path1, string path2, string path3,
        IBindingTarget target, Func<T1, T2, T3, TResult> project, BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame) =>
        Scope(node.Node).BindCallback(new[] { path1, path2, path3 }, new[] { typeof(T1), typeof(T2), typeof(T3) }, null, timing,
            target, typeof(TResult), values => project((T1)values[0]!, (T2)values[1]!, (T3)values[2]!));

    public static IDisposable BindTwoWay<T>(this IUINode node, string path, Action<T> apply,
        Func<Action<T>, Action> subscribe, BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame) =>
        StartCallback(node, path, apply, timing, binding => binding.ConnectInput<T>(subscribe, binding.WriteValue), writeBack: true);

    public static IDisposable BindTwoWay<T1, TEdit>(this IUINode node, string path, Action<T1> apply,
        Func<Action<TEdit>, Action> subscribe, Action<TEdit> commit, BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame) =>
        StartCallback(node, path, apply, timing, binding => binding.ConnectInput(subscribe, commit));

    public static IDisposable BindTwoWay<T1, T2, TEdit>(this IUINode node, string path1, string path2,
        Action<T1, T2> apply, Func<Action<TEdit>, Action> subscribe, Action<TEdit> commit,
        BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame) =>
        StartCallback(node, path1, path2, apply, timing, binding => binding.ConnectInput(subscribe, commit));

    public static IDisposable BindTwoWay<T1, T2, T3, TEdit>(this IUINode node, string path1, string path2, string path3,
        Action<T1, T2, T3> apply, Func<Action<TEdit>, Action> subscribe, Action<TEdit> commit,
        BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame) =>
        StartCallback(node, path1, path2, path3, apply, timing, binding => binding.ConnectInput(subscribe, commit));

    private static IDisposable StartCallback<T>(IUINode node, string path, Action<T> apply, BindingUpdateTiming timing,
        Action<BindingScope.CallbackBinding>? connect = null, bool writeBack = false) =>
        Scope(node.Node).BindCallback(new[] { path }, new[] { typeof(T) }, values => apply((T)values[0]!), timing,
            connect: connect, writeBack: writeBack);
    private static IDisposable StartCallback<T1, T2>(IUINode node, string path1, string path2, Action<T1, T2> apply, BindingUpdateTiming timing,
        Action<BindingScope.CallbackBinding>? connect = null) =>
        Scope(node.Node).BindCallback(new[] { path1, path2 }, new[] { typeof(T1), typeof(T2) },
            values => apply((T1)values[0]!, (T2)values[1]!), timing, connect: connect);
    private static IDisposable StartCallback<T1, T2, T3>(IUINode node, string path1, string path2, string path3, Action<T1, T2, T3> apply, BindingUpdateTiming timing,
        Action<BindingScope.CallbackBinding>? connect = null) =>
        Scope(node.Node).BindCallback(new[] { path1, path2, path3 }, new[] { typeof(T1), typeof(T2), typeof(T3) },
            values => apply((T1)values[0]!, (T2)values[1]!, (T3)values[2]!), timing, connect: connect);
}
