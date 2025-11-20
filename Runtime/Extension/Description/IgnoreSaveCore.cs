using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class IgnoreSaveAttribute : Attribute { }
public static class IgnoreSaveCore
{
    private static readonly HashSet<(Type, string)> Ign
        = new(AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(AttributeEnv)))
            .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.GetCustomAttribute<IgnoreSaveAttribute>() != null)
                .Select(f => (t, f.Name))));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Skip(Type t, string n) => Ign.Contains((t, n));
}


