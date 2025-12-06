using System;
using UnityEngine;

/// <summary>
/// 数值运算接口（恢复乘法运算）
/// </summary>
public interface IArithmetic<T> where T : struct
{
    T Add(T a, T b);
    T Subtract(T a, T b);
    T Multiply(T a, T b);
    T Divide(T a, T b);
    T Min(T a, T b);
    T Max(T a, T b);
    T Clamp(T value, T min, T max);
    bool LessThan(T a, T b);
    bool GreaterThan(T a, T b);
    bool GreaterEThan(T a, T b);
    
    bool Equal(T a, T b);
    float ToFloat(T value);
}

/// <summary>
/// 算术运算工厂
/// </summary>
public static class Arithmetic<T> where T : struct
{
    private static readonly IArithmetic<T> _instance;

    static Arithmetic()
    {
        if (typeof(T) == typeof(int))
            _instance = new IntArithmetic() as IArithmetic<T>;
        else if (typeof(T) == typeof(float))
            _instance = new FloatArithmetic() as IArithmetic<T>;
        else
            throw new NotSupportedException($"Unsupported type: {typeof(T)}");
    }

    public static IArithmetic<T> Instance => _instance;
    
    /// <summary>
    /// 静态乘法快捷方法
    /// </summary>
    public static T Multiply(T a, T b) => _instance.Multiply(a, b);
}

public struct IntArithmetic : IArithmetic<int>
{
    public int Add(int a, int b) => a + b;
    public int Subtract(int a, int b) => a - b;
    public int Multiply(int a, int b) => a * b;
    public int Divide(int a, int b) => b == 0 ? 0 : a / b;
    public int Min(int a, int b) => Math.Min(a, b);
    public int Max(int a, int b) => Math.Max(a, b);
    public int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
    public bool LessThan(int a, int b) => a < b;
    public bool GreaterThan(int a, int b) => a > b;
    public bool GreaterEThan(int a, int b) => a >= b;

    public bool Equal(int a, int b) => a == b;
    public float ToFloat(int value) => value;
}

public struct FloatArithmetic : IArithmetic<float>
{
    public float Add(float a, float b) => a + b;
    public float Subtract(float a, float b) => a - b;
    public float Multiply(float a, float b) => a * b;
    public float Divide(float a, float b) => Mathf.Approximately(b, 0) ? 0 : a / b;
    public float Min(float a, float b) => Mathf.Min(a, b);
    public float Max(float a, float b) => Mathf.Max(a, b);
    public float Clamp(float value, float min, float max) => Mathf.Clamp(value, min, max);
    public bool LessThan(float a, float b) => a < b;
    public bool GreaterThan(float a, float b) => a > b;
    public bool GreaterEThan(float a, float b) => a >= b;
    public bool Equal(float a, float b) => Mathf.Approximately(a, b);
    public float ToFloat(float value) => value;
}
