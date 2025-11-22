using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[Serializable]
public abstract class AttributeEnv : IDisposable
{
    private readonly Dictionary<string, object> _allField = new(StringComparer.Ordinal);
    private List<IDisposable> _disposables;
    private bool _initialized;
    private Type _envType;
    
    public virtual string EnvID { get; }
    public Type EnvType {
        get
        {
            if (_envType != null) return _envType;
            _envType = GetType();
            return _envType;
        }
    }

    protected virtual void BindMap(){} // 用于初始化Bind方法
    protected abstract void InitValue(); // 用于初始化数据
    
    public virtual void BeforeLoad(){} // 用于反序列化解析前
    public virtual void AfterLoadPerLines(string line){} // 用于反序列化解析每行之后
    public virtual void AfterLoad(){} // 用于反序列化解析完成
    protected AttributeEnv()
    {
        InitValue();
        BindMap();
        EnvID = GetType().Name;
        EnsureInitialized();
    }

    // ============== 字符串名称访问 ==============

    public int AddModifiers<T>(string propertyName,int GroupId, params Modifier[] modifiers) where T : struct
    {
        if (modifiers == null) throw new ArgumentNullException(nameof(modifiers));
        TryGetModValue<T>(propertyName,out var value);
        value.AddGroup(GroupId, modifiers);
        return GroupId;
    }
    
    public bool TryGetModValue<T>(string propertyName, out ModValue<T> field) where T : struct
    {
        if (!_allField.TryGetValue(propertyName, out var fieldObj) || fieldObj is not ModValue<T> typedValue)
        {
            field = null;
            return false;
        }

        field = typedValue;
        return true;
    }
    public bool TryGetValuePair<T>(string propertyName, out ValuePair<T> field) where T : struct
    {
        if (!_allField.TryGetValue(propertyName, out var fieldObj) || fieldObj is not ValuePair<T> typedValue)
        {
            field = null;
            return false;
        }
        field = typedValue;
        return true;
    }
    public bool TryGetStr(string propertyName, out ExString field)
    {
        if (_allField.TryGetValue(propertyName, out var fieldObj) && fieldObj is  ExString typedValue)
        {
            field = typedValue;
            return true;
        }
        field = null;
        return false;
        
    }
    public bool TryGetExNum<T>(string key, out ExNum<T> ex) where T : struct
    {
        if (_allField.TryGetValue(key, out var obj) && obj is ExNum<T> casted)
        {
            ex = casted;
            return true;
        }
        ex = null;
        return false;
    }
    
    public ModValue<T> GetModValue<T>(string propertyName) where T : struct
    {
        if (!_allField.TryGetValue(propertyName, out var fieldObj))
            throw new KeyNotFoundException($"Property '{propertyName}' not found");
        
        if (fieldObj is not ModValue<T> typedValue)
            throw new InvalidCastException($"Property '{propertyName}' is not of type ModValue<{typeof(T)}>");
        
        return typedValue;
    }
    public ValuePair<T> GetValuePair<T>(string propertyName)where T : struct
    {
        if (!_allField.TryGetValue(propertyName, out var fieldObj))
            throw new KeyNotFoundException($"属性 '{propertyName}' 没找到");
        
        if (fieldObj is not ValuePair<T> typedValue)
            throw new InvalidCastException($"属性'{propertyName}'不是ValuePair<{typeof(T)}>");
        return typedValue;
    }

    
    public Dictionary<string, object> GetAllField()
    => _allField;  // 用于序列化
    
    
    private void EnsureInitialized()
    {
        if (_initialized) return;
        _disposables = new List<IDisposable>();

        var type = GetType();
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var field in fields)
        {
            var fieldType = field.FieldType;
            var value = field.GetValue(this);
            
            switch (fieldType.IsGenericType)
            {
                // ========== ④ ModValue<T> ==========
                case true when fieldType.GetGenericTypeDefinition() == typeof(ModValue<>):
                {
                    _allField[field.Name] = value ?? throw new InvalidOperationException($"ModValue '[{field.Name}]' 在使用前必须在InitValue初始化");

                    if (value is IDescriptionR desc)
                        desc.Description = AttrDecLib.Descriptions.GetValueOrDefault((type, field.Name), "");

                    if (value is IDisposable d)
                        _disposables.Add(d);

                    continue;
                }
                // ========== ⑤ ValuePair<T> ==========
                case true when fieldType.GetGenericTypeDefinition() == typeof(ValuePair<>):
                {
                    _allField[field.Name] = value ?? throw new InvalidOperationException($"ValuePair '[{field.Name}]' 在使用前必须在InitValue初始化");

                    if (value is IDescriptionR desc)
                        desc.Description = AttrDecLib.Descriptions.GetValueOrDefault((type, field.Name), "");
                    if (value is IDisposable d)
                        _disposables.Add(d);

                    continue;
                }
                // ========== ⑤ ExNum<T> ==========
                case true when fieldType.GetGenericTypeDefinition() == typeof(ExNum<>):
                {
                    _allField[field.Name] = value ?? throw new InvalidOperationException($"ExNum '[{field.Name}]' 在使用前必须在InitValue初始化");
                    if (value is IDescriptionR desc)
                        desc.Description = AttrDecLib.Descriptions.GetValueOrDefault((type, field.Name), "");
                    if (value is IDisposable d)
                        _disposables.Add(d);
                    continue;
                }
            }

            // ========== ⑥ ExString ==========
            if (fieldType == typeof(ExString))
            { _allField[field.Name] = value as ExString ?? new ExString(); }
            
        }
        _initialized = true;
    }
    
    /*----------------- 进行组合绑定 --------------------*/

    private readonly Dictionary<string, List<ModValue<int>>> _modValueGroup =
        new(StringComparer.OrdinalIgnoreCase);
    
    private readonly Dictionary<string, List<ValuePair<int>>> _valuePairGroup =
        new(StringComparer.OrdinalIgnoreCase);
 
    /// <summary>编辑器/初始化阶段调用即可</summary>
    public void BindGroup(string groupId, params ModValue<int>[] modifiers)
        => _modValueGroup[groupId] = new List<ModValue<int>>(modifiers); 
    
    public void BindGroup(string groupId, params ValuePair<int>[] modifiers)
        => _valuePairGroup[groupId] = new List<ValuePair<int>>(modifiers);
    
    /// <summary>运行时拿整组属性</summary>
    public List<ModValue<int>> GetModGroup(string groupId) 
        => _modValueGroup.TryGetValue(groupId, out var list) ? list : new List<ModValue<int>>();
    
    public List<ValuePair<int>> GetPairGroup(string groupId)
        => _valuePairGroup.TryGetValue(groupId, out var list) ? list : new List<ValuePair<int>>();
    
    /*---------------------------------------------------*/
    
    public virtual void Dispose()
    {
        if (!_initialized) return;
        GC.SuppressFinalize(this);
        
        foreach (var disposable in _disposables)
        {
            try
            { disposable?.Dispose(); }
            catch (Exception e)
            { Debug.LogError($"Dispose error: {e}"); }
        }
        
        _disposables?.Clear();
        _allField?.Clear();
        _initialized = false;
    }
    ~AttributeEnv() => Dispose(); 
}
