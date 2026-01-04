using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SafeEvent<T> : IDisposable
{
    /* ======================================== */
    private readonly List<(Action<T> act, int order, string group)> _actions = new();
    /* ======================================== */

    public void Add(Action<T> h, int order = int.MaxValue, string group = null)
    {
        if (h == null) throw new ArgumentNullException(nameof(h));
        _actions.Add((h, order, group));
        _actions.Sort((a, b) => a.order.CompareTo(b.order));
    }
    
    public void Add(Action<T> h, string group)
    => Add(h, int.MaxValue, group);

    
    // 此方法为失效方法只是暂时没有时间去修改优化 让ValuePair和ModValue进行优化适配所以暂时搁置 如果要Remove 请尽量使用 RemoveGroup
    public void Remove(Action<T> h)
    {
        if (h == null) throw new ArgumentNullException(nameof(h));
        _actions.RemoveAll(t => t.act == h);
    }

    public void Remove(int order = int.MinValue)
    {
        if (order == int.MinValue) _actions.Clear();
        _actions.RemoveAll(t => t.order == order);
    }

    /// <summary>
    /// 移除指定组内的所有回调
    /// </summary>
    public void ClearGroup(string group)
    {
        if (string.IsNullOrEmpty(group))
        {
            Debug.LogWarning("[SafeEvent] ClearGroup: 组名不能为空");
            return;
        }
        
        int removed = _actions.RemoveAll(t => t.group == group);
        if (removed > 0)
        { Debug.LogWarning($"[SafeEvent] 已清理组 '{group}' 中的 {removed} 个回调"); }
    }

    /// <summary>
    /// 移除多个组内的所有回调
    /// </summary>
    public void ClearGroups(params string[] groups)
    {
        if (groups == null || groups.Length == 0) return;
        
        foreach (var group in groups)
        { ClearGroup(group); }
    }

    public void Invoke(T arg)
    {
        // 创建副本避免迭代中修改集合
        var snap = new List<(Action<T> act, int order, string group)>(_actions);
        List<Action<T>> toRemove = new();

        foreach (var (act, order, group) in snap)
        {
            try
            { act(arg); }
            catch (MissingReferenceException ex)
            {
                Debug.LogWarning($"[SafeEvent] 检测到丢失引用，已自动移除回调 (Order: {order}, Group: {group ?? "None"})\n{ex.Message}");
                toRemove.Add(act);
            }
            catch (NullReferenceException ex)
            {
                Debug.LogWarning($"[SafeEvent] 检测到空引用，已自动移除回调 (Order: {order}, Group: {group ?? "None"})\n{ex.Message}");
                toRemove.Add(act);
            }
            catch (Exception ex)
            {
                // 其他异常仍然记录，但不自动移除（可能是临时错误）
                Debug.LogError($"[SafeEvent] 回调执行异常 (Order: {order}, Group: {group ?? "None"}):\n{ex}");
            }
        }

        // 批量移除有问题的回调
        if (toRemove.Count <= 0) return;
        foreach (var act in toRemove)
        { _actions.RemoveAll(t => t.act == act); }
    }

    public void Dispose()
    { _actions.Clear(); }

    public static SafeEvent<T> operator +(SafeEvent<T> e, Action<T> h) { e.Add(h); return e; }
    public static SafeEvent<T> operator -(SafeEvent<T> e, Action<T> h) { e.Remove(h); return e; }
}


public sealed class SafeEvent : IDisposable
{
    /* ======================================== */
    private readonly List<(Action act, int order, string group)> _actions = new();
    /* ======================================== */

    public void Add(Action h, int order = int.MaxValue, string group = null)
    {
        if (h == null) throw new ArgumentNullException(nameof(h));
        _actions.Add((h, order, group));
        _actions.Sort((a, b) => a.order.CompareTo(b.order));
    }

    public void Add(Action h, string group)
        => Add(h, int.MaxValue, group);

    public void Remove(Action h)
    {
        if (h == null) throw new ArgumentNullException(nameof(h));
        _actions.RemoveAll(t => t.act == h);
    }

    public void Remove(int order = int.MinValue)
    {
        if (order == int.MinValue) _actions.Clear();
        _actions.RemoveAll(t => t.order == order);
    }

    /// <summary>
    /// 移除指定组内的所有回调
    /// </summary>
    public void ClearGroup(string group)
    {
        if (string.IsNullOrEmpty(group))
        {
            Debug.LogWarning("[SafeEvent] ClearGroup: 组名不能为空");
            return;
        }
        
        int removed = _actions.RemoveAll(t => t.group == group);
        if (removed > 0)
        { Debug.Log($"[SafeEvent] 已清理组 '{group}' 中的 {removed} 个回调"); }
    }

    /// <summary>
    /// 移除多个组内的所有回调
    /// </summary>
    public void ClearGroups(params string[] groups)
    {
        if (groups == null || groups.Length == 0) return;
        
        foreach (var group in groups)
        { ClearGroup(group); }
    }

    public void Invoke()
    {
        // 创建副本避免迭代中修改集合
        var snap = new List<(Action act, int order, string group)>(_actions);
        List<Action> toRemove = new();

        foreach (var (act, order, group) in snap)
        {
            try
            {
                act();
            }
            catch (MissingReferenceException ex)
            {
                Debug.LogWarning($"[SafeEvent] 检测到丢失引用，已自动移除回调 (Order: {order}, Group: {group ?? "None"})\n{ex.Message}");
                toRemove.Add(act);
            }
            catch (NullReferenceException ex)
            {
                Debug.LogWarning($"[SafeEvent] 检测到空引用，已自动移除回调 (Order: {order}, Group: {group ?? "None"})\n{ex.Message}");
                toRemove.Add(act);
            }
            catch (Exception ex)
            {
                // 其他异常仍然记录，但不自动移除（可能是临时错误）
                Debug.LogError($"[SafeEvent] 回调执行异常 (Order: {order}, Group: {group ?? "None"}):\n{ex}");
            }
        }

        // 批量移除有问题的回调
        if (toRemove.Count > 0)
        {
            foreach (var act in toRemove)
            { _actions.RemoveAll(t => t.act == act); }
        }
    }

    public void Dispose()
    { _actions.Clear(); }

    public static SafeEvent operator +(SafeEvent e, Action h) { e.Add(h); return e; }
    public static SafeEvent operator -(SafeEvent e, Action h) { e.Remove(h); return e; }
}
