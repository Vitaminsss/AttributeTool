using System;
using UnityEngine;

[Serializable]
public sealed class ExNum<T> : IDescriptionR,IReadOnlyExNum, IDisposable where T : struct
{
    [SerializeField] 
    private T current;

    private readonly SafeEvent<(double Old, double New)> _valueChanged = new();

    public string Description { get; set; }

    private static readonly IArithmetic<T> Math = Arithmetic<T>.Instance;

    public ExNum() { current = default; }
    public ExNum(T value) { current = value; }

    public T Current
    {
        get => current;
        set
        {
            if (current.Equals(value)) return;

            var old = current;
            current = value;

            _valueChanged.Invoke((Convert.ToDouble(old), Convert.ToDouble(value)));
        }
    }

    //=== 数学操作 ===//
    public void Add(T amount) => Current = Math.Add(current, amount);
    public void Sub(T amount) => Current = Math.Subtract(current, amount);
    public void Mul(T factor) => Current = Math.Multiply(current, factor);
    public void Div(T divisor) => Current = Math.Divide(current, divisor);

    public void Set(T value) => Current = value;

    //=== 事件 ===//
    public void AddListener(Action<double,double> h) => _valueChanged.Add(v => h(v.Old, v.New));
    public void RemoveListener(Action<double,double> h) => _valueChanged.Remove(v => h(v.Old, v.New));

    // 运算符重载
    public static ExNum<T> operator +(ExNum<T> n, T value) { n.Add(value); return n; }
    public static ExNum<T> operator -(ExNum<T> n, T value) { n.Sub(value); return n; }
    public static ExNum<T> operator *(ExNum<T> n, T value) { n.Mul(value); return n; }
    public static ExNum<T> operator /(ExNum<T> n, T value) { n.Div(value); return n; }

    public static implicit operator T(ExNum<T> n) => n.current;

    public void Dispose()
    { _valueChanged?.Dispose(); }

    object IReadOnlyExNum.Current => Current;

    // public override string ToString() => current?.ToString() ?? "null";
}
public interface IReadOnlyExNum
{
    object Current { get; }
}
