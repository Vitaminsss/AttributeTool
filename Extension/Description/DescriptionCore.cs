using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class DescAttribute : Attribute
{
    public readonly string Text;
    public DescAttribute(string text) => Text = text;
}

public static class AttrDecLib
{
    private static readonly Dictionary<(Type envType,string field), string> _dict = new();
    public static IReadOnlyDictionary<(Type,string), string> Descriptions => _dict;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ScanAllEnv()
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies();      // 全部托管程序集
        foreach (var a in asm)
        {
            foreach (var t in a.GetTypes())
            {
                if (!t.IsSubclassOf(typeof(AttributeEnv))) continue;
                if (t.IsAbstract) continue;     // 只取可实例化的

                foreach (var f in t.GetFields(
                             BindingFlags.Instance |
                             BindingFlags.Public   |
                             BindingFlags.NonPublic))
                {
                    var dec = f.GetCustomAttribute<DescAttribute>();
                    if (dec == null) continue;
                    _dict[(t, f.Name)] = dec.Text;
                }
            }
        }
    }
    
    // 直接通过AttrDecLib解析对应类的简介
    public static string GetDescription<T>(string fieldName)
    {
        Descriptions.TryGetValue((typeof(T), fieldName), out var desc);
        return desc;
    }
    
    // 实例类的Description索引获取
    public static string GetDescription<T>(
        this T _, string fieldName) where T : AttributeEnv
    { return Descriptions[(typeof(T), fieldName)] ?? ""; }
}
