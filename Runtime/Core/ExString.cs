using System;

public sealed class ExString : IDescriptionR, IDisposable
{
    private string _value;
    private readonly SafeEvent<(string Old, string New)> _onValueChanged = new();
    public string Description { get; set; }

    public ExString(string initialValue = "")
    { _value = initialValue ?? string.Empty; }

    // 可以直接访问到的隐式转换
    public static implicit operator string(ExString exString) => exString?.Value ?? string.Empty;
    public override string ToString() => Value;

    public string Value
    {
        get => _value;
        set
        {
            if (this._value == value) return;
            
            var old = this._value;
            this._value = value ?? string.Empty;
            _onValueChanged.Invoke((old, this._value));
        }
    }

    /// <summary>
    /// 设置新值并触发值变更事件
    /// </summary>
    public void Set(string newValue)
    {
        Value = newValue;  // 通过属性设置器触发事件
    }
    
    // 字符串操作扩展方法
    public void Append(string text) => Value += text;
    public void Clear() => Value = string.Empty;
    public void Trim() => Value = Value.Trim();
    public void ToUpper() => Value = Value.ToUpper();
    public void ToLower() => Value = Value.ToLower();
    
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public bool IsNullOrWhiteSpace => string.IsNullOrWhiteSpace(Value);
    public int Length => Value.Length;

    // 事件监听
    public void AddListener(Action<string, string> handler) => 
        _onValueChanged.Add(h => handler(h.Old, h.New));
    
    public void AddListener(Action handler) => 
        _onValueChanged.Add(_ => handler());

    public void RemoveListener(Action handler) => 
        _onValueChanged.Add(_ => handler());
    
    public void RemoveListener(Action<string, string> handler) => 
        _onValueChanged.Remove(h => handler(h.Old, h.New));

    /// <summary>
    /// 使用 += 操作符追加字符串
    /// </summary>
    public static ExString operator +(ExString exString, string appendText)
    {
        if (exString != null)
        { exString.Value += appendText; }
        return exString;
    }

    /// <summary>
    /// 使用 -= 操作符从末尾移除指定字符串
    /// </summary>
    public static ExString operator -(ExString exString, string removeText)
    {
        if (exString != null && !string.IsNullOrEmpty(removeText) && 
            exString.Value.EndsWith(removeText))
        { exString.Value = exString.Value[..^removeText.Length]; }
        return exString;
    }

    /// <summary>
    /// 使用 -= 操作符移除指定长度的末尾字符
    /// </summary>
    public static ExString operator -(ExString exString, int removeLength)
    {
        if (exString != null && removeLength > 0 && exString.Value.Length >= removeLength)
        { exString.Value = exString.Value[..^removeLength]; }
        return exString;
    }

    /// <summary>
    /// 允许将 string 隐式转换为 ExString 如若有Event的情况下不要使用这个饮食转换 请用Set
    /// </summary>
    public static implicit operator ExString(string str) => new ExString(str);
    
    
    public void Dispose()
    {
        _onValueChanged.Dispose();
        GC.SuppressFinalize(this);
    }
    ~ExString()
    { Dispose(); }
}