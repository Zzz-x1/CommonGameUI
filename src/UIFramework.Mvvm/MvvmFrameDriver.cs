using System.Runtime.ExceptionServices;

namespace UIFramework.Mvvm;

/// <summary>Advances all MVVM runtimes attached to one engine UI frame loop.</summary>
public sealed class MvvmFrameDriver
{
    private readonly List<MvvmRuntime> _runtimes = new();

    public bool IsEmpty => _runtimes.Count == 0;

    public void Attach(MvvmRuntime runtime)
    {
        if (runtime == null) throw new ArgumentNullException(nameof(runtime));
        if (!_runtimes.Contains(runtime)) _runtimes.Add(runtime);
    }

    public void Tick()
    {
        Exception? first = null;
        // A binding can open another context. Its new runtime starts on the next tick.
        int index = 0;
        for (int remaining = _runtimes.Count; remaining > 0; remaining--)
        {
            var runtime = _runtimes[index];
            if (!runtime.IsDisposed)
                try { runtime.FlushBindings(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            try { runtime.ObserveTasks(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            if (runtime.IsDisposed) _runtimes.RemoveAt(index);
            else index++;
        }
        if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
    }
}
