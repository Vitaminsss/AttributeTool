using System;
using UnityEngine;

[Serializable]
public sealed class ValuePair<T> : IReadOnlyValuePair,IDescriptionR,IDirtyNotifiable,IDisposable,IModifier where T : struct
{
    [SerializeField]
    private T baseMin,baseMax,current,recovery;
    private ModValue<T> BindMinRef,BindMaxRef,BindRecRef;
    
    private readonly object _bindingLock = new();

    private readonly SafeEvent<(double Old, double New)> _valueChangedEvent = new();
    private readonly SafeEvent<T> _onReachMaxEvent = new();
    private readonly SafeEvent _dirtyEvent = new();
    public string Description { get; set; }
    public event Action OnDirty;

    #region 结构函数
    public ValuePair(T max) : this(default, max ,max) { }
    
    public ValuePair(T cur, T max) : this(default, max, cur) { }

    public ValuePair(T min, T max, T current, T recovery = default)
    {
        baseMin = Math.Min(min, max);
        baseMax = max;
        this.current = Math.Clamp(current, baseMin, baseMax);
        this.recovery = recovery;
    }
        
    object IReadOnlyValuePair.Current => current;
    object IReadOnlyValuePair.Min     => Min;
    object IReadOnlyValuePair.Max     => Max;
    
    /// <summary>
    /// 隐式转换为当前值 (Current)，实现无缝使用
    /// </summary>
    public static implicit operator T(ValuePair<T> pair) { return pair?.Current ?? default; }

    public override string ToString() => $"[{Current}] [{Min}→{Max}] (+{Recovery}/s)";

    #endregion
    
    #region 绑定方法

    public void BindMinTo(ModValue<T> modValue)
    {
        if (modValue == null) return;
        BindMinRef = modValue;
        MarkDirty();
        Current = current;
    }
    public void BindMaxTo(ModValue<T> modValue, bool adjustCurrent = false)
    {
        if (modValue == null) return;
    
        if (adjustCurrent && BindMaxRef != null)
        {
            var oldMax = Max;
            BindMaxRef = modValue;
            var newMax = Max;
        
            if (Math.GreaterThan(newMax, oldMax))
            {
                var difference = Math.Subtract(newMax, oldMax);
                Current = Math.Add(Current, difference);
            }
        }
        else
        { BindMaxRef = modValue; }
    
        MarkDirty();
        Current = current;
    }

    public void BindRecTo(ModValue<T> modValue)
    {
        if (modValue == null) return;
        BindRecRef = modValue;
        MarkDirty();
        Current = current;
    }

    public void SetDescription(string description) => Description = description; 

    public string GetDescription() => Description;

    private void Invoke(T oldVal, T newVal)
    {
        _valueChangedEvent.Invoke((Convert.ToDouble(oldVal), Convert.ToDouble(newVal)));
        _dirtyEvent.Invoke(); // 通知监听器变化
        OnDirty?.Invoke(); // 用于当被用于ModValue动态修改值触发
        
        // 如果本次赋值后才第一次达到 Max，则触发一次 ReachMax
        if (!Math.Equal(oldVal, Max)                   // 原来是未满
            && Math.Equal(newVal, Max))             // 现在刚满
        { _onReachMaxEvent.Invoke(newVal); }
    }

    #endregion

    #region 属性外部接口
    
    public T Current
    {
        get => current;
        set
        {
            var clamped = Math.Clamp(value, Min, Max);
            if (current.Equals(clamped)) return;
            var old     = current;
            current = clamped;
            Invoke(old, clamped);

        }
    } 
    public T Min
    {
        get
        {
            lock (_bindingLock)
            {
                if (BindMinRef != null && !baseMin.Equals(BindMinRef.Value)) { baseMin = BindMinRef.Value;}
                return BindMinRef?.Value ?? baseMin;
            }
        }
        set
        {
            lock (_bindingLock)
            {
                baseMin = Math.Min(value, baseMax);
                current = Math.Max(current, baseMin);
            }
        }
    }
    public T Max
    {
        get
        {
            lock (_bindingLock)
            {
                if (BindMaxRef != null &&!baseMax.Equals(BindMaxRef.Value)) { baseMax = BindMaxRef.Value;}
                return BindMaxRef?.Value ?? baseMax;
            }
        }
        set
        {
            lock (_bindingLock)
            {
                baseMax = Math.Max(value, baseMin);
                current = Math.Min(current, baseMax);
            }
        }
    }
    public T Recovery
    {
        get
        {
            lock (_bindingLock)
            {
                if (BindRecRef != null &&!recovery.Equals(BindRecRef.Value)) { recovery = BindRecRef.Value;}
                return BindRecRef?.Value ?? recovery;
            }
        }
        set
        {
            lock (_bindingLock)
            { recovery = value; }
        }
    }
    
    private float Percent => Math.GreaterThan(Max, default) ? Math.ToFloat(Math.Divide(current, Max)) : 0f;
    public float GetPercent() => Percent;
    public void MarkDirty()
    { OnDirty?.Invoke(); }

    #endregion
    
    // 只会在不绑定的时候影响最大最小值
    #region 数值设定方法
    public void AddCurrent(T amount) => Current = Math.Add(current, amount);
    public void SubCurrent(T amount) => Current = Math.Subtract(current, amount);
    public void SetCurrent(T amount) => Current = amount;
    
    public void ToMin() => Current = Min;
    public void ToMax() => Current = Max;
    
    public void AddMax(T amount) => Max = Math.Add(baseMax, amount);
    public void SubMax(T amount) => Max = Math.Subtract(baseMax, amount);
    public void SetMax(T amount) => Max = amount;

    public void AddMin(T amount) => Min = Math.Add(baseMin, amount);
    public void SubMin(T amount) => Min = Math.Subtract(baseMin, amount);
    public void SetMin(T amount) => Min = amount;

    public void AddRecovery(T amount) => recovery = Math.Add(recovery, amount);
    public void SubRecovery(T amount) => recovery = Math.Subtract(recovery, amount);
    public void SetRecovery(T amount) => recovery = amount;

    public void Rec() => AddCurrent(Recovery);

    public void MultiplyCurrent(T factor) => Current = Arithmetic<T>.Multiply(current, factor);
    public void MultiplyMax(T factor) => Max = Arithmetic<T>.Multiply(baseMax, factor);
    public void MultiplyMin(T factor) => Min = Arithmetic<T>.Multiply(baseMin, factor);
    public void MultiplyRecovery(T factor) => recovery = Arithmetic<T>.Multiply(recovery, factor);
    private static readonly IArithmetic<T> Math = Arithmetic<T>.Instance;
    
    #endregion

    #region 生命周期
    /// <summary>
    /// 监听 Current 值的变化。支持 lambda/方法组；
    /// </summary>
    public void AddListener(Action<double,double> handler) => _valueChangedEvent.Add(h => handler(h.Old, h.New));
 
    /// <summary>
    /// 移除监听 —— Lambada函数无法被移除
    /// </summary>
    public void RemoveListener(Action<double,double> handler) => _valueChangedEvent.Remove(h => handler(h.Old, h.New));
    
    public void AddListener(Action handler) =>
        _dirtyEvent.Add(handler);
    public void RemoveListener(Action handler) =>
        _dirtyEvent.Remove(handler);



    public void AddReachMaxListener(Action<T> handler)  => _onReachMaxEvent .Add(handler);
    public void RemoveReachMaxListener(Action<T> handler)=> _onReachMaxEvent .Remove(handler);
    
    public static ValuePair<T> operator +(ValuePair<T> vp, Action<double,double> handler) { vp.AddListener(handler); return vp; }
    public static ValuePair<T> operator -(ValuePair<T> vp, Action<double,double> handler) { vp.RemoveListener(handler); return vp; }
    
    public static ValuePair<T> operator +(ValuePair<T> vp, Action handler) { vp.AddListener(handler); return vp; }
    public static ValuePair<T> operator -(ValuePair<T> vp, Action handler) { vp.RemoveListener(handler); return vp; }
    
    ~ValuePair() => Dispose();
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        lock (_bindingLock)
        {
            BindMinRef = null;
            BindMaxRef = null;
            BindRecRef = null;
            _valueChangedEvent.Dispose();
        }
    }
    
    #endregion

    #region 辅助方法

    public bool IsEnough(T  amount)
    => Math.GreaterThan(Current, amount); 

    public T GetRemaining()
    => Math.Subtract(Max, Current);

    public double GetDouble()
    => Convert.ToDouble(Current);
    #endregion
}

public interface IReadOnlyValuePair
{
    object Current { get; }
    object Min     { get; }
    object Max     { get; }
}