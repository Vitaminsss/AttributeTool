using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SafeEvent<T> : IDisposable
{
    /* ======================================== */
    private readonly object            _lock     = new();
    private readonly HashSet<Action<T>> _actions = new();
    /* ======================================== */

    public void Add(Action<T> h)
    {
        if (h == null) throw new ArgumentNullException(nameof(h));
        lock (_lock) _actions.Add(h);
    }

    public void Remove(Action<T> h)
    {
        if (h == null) throw new ArgumentNullException(nameof(h));
        lock (_lock) _actions.Remove(h);
    }

    public void Invoke(T arg)
    {
        Action<T>[] snapshot;
        lock (_lock) snapshot = _actions.ToArray();
        foreach (var h in snapshot) h(arg);
    }

    public void Dispose()
    { lock (_lock) _actions.Clear(); }
}

public sealed class SafeEvent : IDisposable
{
    /* ======================================== */
    private readonly object _lock = new();
    private readonly HashSet<Action> _actions = new();
    /* ======================================== */

    public void Add(Action handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        lock (_lock) _actions.Add(handler);
    }
    public void Remove(Action handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        lock (_lock) _actions.Remove(handler);
    }
    
    public void Invoke()
    {
        Action[] snapshot;
        lock (_lock) snapshot = _actions.ToArray();
        foreach (var handler in snapshot) handler();
    }
    public void Dispose()
    {
        lock (_lock) _actions.Clear();
    }
}