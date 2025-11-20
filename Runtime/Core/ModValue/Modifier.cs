using System;
using UnityEngine;

public class Modifier
{
    private readonly ModType _type;
    private readonly IModifier _dynamicSource;
    private readonly double _staticValue;
    public int? Prio { get; }

    private IDirtyNotifiable _notifier;
    private ModGroup _parentGroup;
    
    public void SetParent(ModGroup parent)
        => _parentGroup = parent;
    
    /// <summary>静态值修饰器</summary>
    public Modifier(ModType type, double staticValue, int? prio = null)
    {
        _type = type;
        _staticValue = staticValue;
        _dynamicSource = null;
        Prio = prio;
    }

    /// <summary>动态值修饰器（支持 ModValue / ValuePair）</summary>
    public Modifier(ModType type, IModifier source, int? prio = null)
    {
        _type = type;
        _staticValue = 0;
        _dynamicSource = source;
        Prio = prio;
        
        // 动态来源 Dirty 时，冒泡给 ModGroup
        if (_dynamicSource is not IDirtyNotifiable d) return;
        _notifier = d;
        _notifier.OnDirty += HandleDirty;
    }
    
    private void HandleDirty()
    {
        _parentGroup.MarkDirty();
    }
    
    public void Dispose()
    {
        if (_notifier == null) return;
        _notifier.OnDirty -= HandleDirty;
        _notifier = null;
    }

    // -----------------------------
    // 统一 Apply 逻辑
    // -----------------------------
    private double ApplyDouble(double baseValue)
    {
        var v = GetValueDouble();

        return _type switch
        {
            ModType.Add => baseValue + v,
            ModType.Multiply => baseValue * v,
            ModType.Override => v,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    
    // --------------------------------
    // 对外暴露的具体类型版本
    // --------------------------------
    public int Apply(int baseValue) 
        => (int)ApplyDouble(baseValue);

    public float Apply(float baseValue)
        => (float)ApplyDouble(baseValue);

    public double Apply(double baseValue)
        => ApplyDouble(baseValue);
    
        
    public ModType GetModType() => _type;
    public double GetValue() => GetValueDouble();
    private double GetValueDouble()
        => _dynamicSource?.GetDouble() ?? _staticValue;
}


