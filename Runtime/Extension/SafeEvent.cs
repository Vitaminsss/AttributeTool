using System;
using System.Collections.Generic;

public sealed class SafeEvent<T> : IDisposable
{
    /* ======================================== */
    private readonly object            _lock     = new();
    private readonly List<(Action<T> act, int order)> _actions = new();
    /* ======================================== */

    public void Add(Action<T> h, int order = int.MaxValue)
    {
        if (h == null) throw new ArgumentNullException(nameof(h));
        lock (_lock)
        {
            _actions.Add((h, order));
            _actions.Sort((a, b) => a.order.CompareTo(b.order));    
        }
    }
    
    public void Remove(Action<T> h)
    {
        if (h == null) throw new ArgumentNullException(nameof(h));
        lock (_lock)
        {
            _actions.RemoveAll(t => t.act == h); 
        }
    }
    
    public void Remove(int order = int.MinValue)
    {
        lock (_lock)
        {
            // 如果为 MinValue则移除所有绑定 
            if(order == int.MinValue) _actions.Clear();
            // 正常则移除所有对应Order的所有action
            _actions.RemoveAll(t => t.order == order); 
        }
    }

    public void Invoke(T arg)
    {
        List<(Action<T> act, int _) > snap;
        lock (_lock) snap = _actions;   // 已排好序
        foreach (var (act, _) in snap) act(arg);
    }

    public void Dispose()
    { lock (_lock) _actions.Clear(); }
    
    public static SafeEvent<T> operator +(SafeEvent<T> e, Action<T> h) { e.Add(h); return e; }
    public static SafeEvent<T> operator -(SafeEvent<T> e, Action<T> h) { e.Remove(h); return e; }
}

public sealed class SafeEvent : IDisposable
{
    private readonly object _lock = new();
    private readonly List<(Action act, int order)> _actions = new();

    public void Add(Action h, int order = int.MaxValue)
    {
        if (h == null) throw new ArgumentNullException(nameof(h));
        lock (_lock)
        {
            _actions.Add((h, order));
            _actions.Sort((a, b) => a.order.CompareTo(b.order));
        }
    }

    public void Remove(Action h)
    {
        if (h == null) throw new ArgumentNullException(nameof(h));
        lock (_lock) { _actions.RemoveAll(t => t.act == h); }
    }
    
    public void Remove(int order = int.MinValue)
    {
        lock (_lock)
        {
            // 如果为 MinValue则移除所有绑定 
            if(order == int.MinValue) _actions.Clear();
            // 正常则移除所有对应Order的所有action
            _actions.RemoveAll(t => t.order == order); 
        }
    }

    public void Invoke()
    {
        lock (_lock)
        { foreach (var (act, _) in _actions) act(); }
    }

    public void Dispose() { lock (_lock) _actions.Clear(); }
    
    public static SafeEvent operator +(SafeEvent e, Action h) { e.Add(h); return e; }
    public static SafeEvent operator -(SafeEvent e, Action h) { e.Remove(h); return e; }
}
