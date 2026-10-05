using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace UIFramework.Mvvm;

public static class MvvmNotifications
{
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
