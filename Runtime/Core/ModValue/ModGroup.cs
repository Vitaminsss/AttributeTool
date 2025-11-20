using System;
using System.Collections.Generic;

// 修饰符组内部容器 - 可以当作是一套完整的计算 - 例如可以当作是装备的强化之类的
public class ModGroup:IDisposable
{
    private readonly SortedList<int, List<Modifier>> _mods = new();
    private readonly List<Modifier> _noPrio = new();
    public event Action OnDirty; // 用于冒泡提醒ModValueMarkDirty用的事件
    private bool _isDirty;
    
    public void Add(IEnumerable<Modifier> mods)
    {
        foreach (var mod in mods)
        {
            mod.SetParent(this);
            if (mod.Prio.HasValue)
            {
                var p = mod.Prio.Value;
                if (!_mods.TryGetValue(p, out var list))
                {
                    list = new List<Modifier>();
                    _mods.Add(p, list);
                }
                list.Add(mod);
            }
            else
            { _noPrio.Add(mod); }
        }
    }
    
    public IEnumerable<Modifier> GetSortedMods()
    {
        foreach (var kv in _mods)
        foreach (var mod in kv.Value)
            yield return mod;

        foreach (var mod in _noPrio)
            yield return mod;
    }
    
    public void MarkDirty()
    {
        if (_isDirty) return;
        _isDirty = true;
        OnDirty?.Invoke();
    }
    
    public void ClearDirty()
    { _isDirty = false; }

    public void Clear()
    {
        OnDirty =  null;
        Dispose();
    }

    public void Dispose()
    {
        foreach (var m in _mods)
            foreach (var mod in m.Value)
                mod.Dispose();
        _mods.Clear();
    }
}