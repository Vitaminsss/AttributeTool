using System;
using System.Collections.Generic;

public enum ModType 
{ 
    Add ,        // 加法修饰
    Multiply ,   // 乘法修饰
    Override     // 覆盖修饰
}

public interface IDirtyNotifiable
{
    void MarkDirty();
    event Action OnDirty;
}

public interface IModValue
{
    public SortedDictionary<int, ModGroup> AllGroups { get; }
    public void AddGroup(int id, params Modifier[] mods);
    public void RemoveGroup(int id);
    object BaseValue { get; }
    object FinalValue { get; }
}

public interface IDescriptionR
{ public string Description { get; set; } }

public interface IModifier
{
    double GetDouble();
}