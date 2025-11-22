using System;
using System.Collections.Generic;
/// <summary>
/// 可修饰的数值类型，支持动态添加/移除数值修饰符组
/// </summary>
/// <typeparam name="T">数值类型</typeparam>
public sealed class ModValue<T>:IModValue,IDescriptionR, IDirtyNotifiable,IDisposable,IModifier where T : struct
{
    // 存储所有修饰符组，key为组ID
    public SortedDictionary<int, ModGroup> AllGroups { get; } = new();
    private T _base;      // 基础值
    private T _cached;    // 缓存计算结果（可空）
    private bool _dirty;  // 标记是否需要重新计算
    private bool _disposed; // 标记是否已被释放
    
    private SafeEvent<(double Old, double New)> _valueChangedEvent;
    private SafeEvent _dirtyEvent;
    
    // TODO: 改成适配SafeEvent更加安全
    public event Action OnDirty; 
    public string Description { get; set; }
    
    /// <summary>
    /// 获取当前值（自动触发计算逻辑）
    /// </summary>
    public T Value
    {
        get
        {
            if (!_dirty) return _cached;
            var oldValue = _cached;
            _cached = Calculate(); // 重新计算数值
            _dirty = false;
            _dirtyEvent?.Invoke();

            if (!EqualityComparer<T>.Default.Equals(oldValue, _cached)) 
                Invoke(Convert.ToDouble(oldValue), Convert.ToDouble(_cached)); // 调用属性变化方法
        
            return _cached;
        }
    }
    
    /// <summary>
    /// 获取或设置基础值（修改会自动标记为脏数据）
    /// </summary>
    public T Base
    {
        get => _base;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_base, value)) return;
            _base = value;
            MarkDirty();
        }
    }

    /// <summary>
    /// 初始化数值实例
    /// </summary>
    public ModValue(T baseValue = default)
    {
        _base = baseValue;
        _cached = baseValue; // 缓存初始化优先等于_base
    } 

    /* ------------------------------------ 数值变化监听 --------------------------------------*/

    /// <summary>
    /// 添加数值变化监听事件
    /// </summary>
    public void AddListener(Action<double, double> handler)
    {
        _valueChangedEvent ??= new SafeEvent<(double Old, double New)>();
        _valueChangedEvent.Add(x => handler(x.Old, x.New));
    }
    /// <summary>
    /// 移除数值变化监听事件
    /// </summary>
    public void RemoveListener(Action<double, double> handler) =>
        _valueChangedEvent.Remove(x => handler(x.Old, x.New));
    
    public void AddListener(Action handler)
    {
        _dirtyEvent ??= new SafeEvent();
        _dirtyEvent.Add(handler);
    }

    public void RemoveListener(Action handler) =>
        _dirtyEvent.Remove(handler);

    public static ModValue<T> operator +(ModValue<T> vp, Action<double, double> handler) { vp.AddListener(handler); return vp; }
    public static ModValue<T> operator -(ModValue<T> vp, Action<double, double> handler) { vp.RemoveListener(handler); return vp; }
    
    /// <summary>
    /// 添加修饰符组
    /// </summary>
    public void AddGroup(int id, params Modifier[] mods)
    {
        var group = new ModGroup(); // 总是创建新组
        group.Add(mods);
        AllGroups[id] = group; // 直接覆盖
        group.OnDirty += MarkDirty;
        MarkDirty();
    }

    /// <summary>
    /// 移除指定修饰符组
    /// </summary>
    public void RemoveGroup(int id)
    {
        if (!AllGroups.TryGetValue(id, out var group)) return;
        group.Clear();
        AllGroups.Remove(id);
        MarkDirty();
    }

    /* ------------------------------------ 链式通知系统 --------------------------------------*/
    
    /// <summary>
    /// 直接设置基础数值，不依赖任何外部 ModValue
    /// </summary>
    public void SetBase(T value)
    {
        // 仅更新基础值，不覆盖 BaseSetter
        if (EqualityComparer<T>.Default.Equals(_base, value)) return;
        _base = value;
        MarkDirty();
    }

    /// <summary>
    /// 释放资源的标准方法
    /// </summary>
    public void Dispose()
    {
        Disposed();
        GC.SuppressFinalize(this); // 已手动释放则不需要析构函数再次调用
    }
    
    /* ------------------------------------ 语法糖 --------------------------------------*/
    public object BaseValue => _base;
    public object FinalValue => Value;

    public void SetDescription(string description) => Description = description; 

    public string GetDescription() => Description; 
    
    /* ------------------------------------- 私有方法 --------------------------------------*/
    
    // 计算最终值（当检测到数据变化时自动调用）
    private T Calculate()
    {
        double cur = Convert.ToDouble(_base);

        // --- 统一在 double 域执行 ---
        foreach (var g in AllGroups)
        {
            foreach (var m in g.Value.GetSortedMods())
                cur = m.Apply(cur);
            g.Value.ClearDirty();
        }

        // --- 类型 T 的最终转换 ---
        if (typeof(T) == typeof(int))
            return (T)(object)(int)cur;

        if (typeof(T) == typeof(float))
            return (T)(object)(float)cur;
        
        if (typeof(T) == typeof(double)) 
            return (T)(object)cur;

        throw new NotSupportedException();
    }


    // 事件触发逻辑 // BUG: 这里会不知为何多次Invoke所以有空要修
    private void Invoke(double oldVal, double newVal)
    {
        _valueChangedEvent.Invoke((oldVal, newVal)); 
        OnDirty?.Invoke();
    }
    
    // 安全脏标记方法
    public void MarkDirty()
    {
        // 如果已经在更新中，则跳过
        if (_dirty) return;
        _dirty = true;
    }
    
    /// <summary>
    /// 实际的资源释放逻辑
    /// </summary>
    private void Disposed()
    {
        if (_disposed) return;

        // 释放所有托管资源
        _valueChangedEvent.Dispose();
        AllGroups.Clear();
        _disposed = true;
    }
    
    /* ------------------------------------- 内置结构类 --------------------------------------*/
    
    /// <summary>
    /// 析构函数，作为兜底机制确保资源释放
    /// </summary>
    ~ModValue()
    {
        if (_disposed) return;
        try { Disposed(); }
        catch { /* 确保析构函数不会抛出异常 */ }
    }
    
    /// <summary>
    /// 通过隐式转换能够让使用者访问ModValue可以直接访问到.Value的数据
    /// </summary>
    public static implicit operator T(ModValue<T> modValue) => modValue?.Value ?? default;

    public override string ToString() => Value.ToString();
    public double GetDouble() => Convert.ToDouble(Value);
}

