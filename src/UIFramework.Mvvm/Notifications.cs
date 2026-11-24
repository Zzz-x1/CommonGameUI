using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace UIFramework.Mvvm;

public static class MvvmNotifications
{
    /// <summary>Notify bindings of a FieldNotify property and its declared dependants without calling its setter.</summary>
    public static void NotifyChanged<TVm>(this TVm vm, string propertyName) where TVm : class
    {
        if (vm == null) throw new ArgumentNullException(nameof(vm));
        if (vm is not IMvvmObject model)
            throw new InvalidOperationException($"Cannot notify '{propertyName}' on {vm.GetType().FullName}: the object does not implement IMvvmObject.");
        model.NotifyChanged(propertyName);
    }

    public static bool Different<T>(T previous, T current) => !EqualityComparer<T>.Default.Equals(previous, current);
    public static void Raise(PropertyChangedEventHandler? handlers, object owner, string property)
    {
        Exception? first = null;
        foreach (PropertyChangedEventHandler handler in handlers?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try { handler(owner, new PropertyChangedEventArgs(property)); }
            catch (Exception error)
            {
                if (first == null) first = error;
                else
                {
                    if (first.Data[UIFailure.SecondaryExceptionsKey] is not List<Exception> secondary)
                        first.Data[UIFailure.SecondaryExceptionsKey] = secondary = new List<Exception>();
                    secondary.Add(error);
                }
            }
        }
        if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
    }
}
