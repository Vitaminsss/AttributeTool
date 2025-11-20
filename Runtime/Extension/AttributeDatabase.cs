using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
public static class AttributeDatabase
{
    private static readonly Regex GroupRegex = new(@"\[(\d+)@([^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex ModRegex   = new(@"([A-Za-z]+)\(([^\)]+)\)\^?", RegexOptions.Compiled);

    public static string Serialize(this AttributeEnv env)
    {
        var sb = new StringBuilder(2048);
        SerializeInternal(env, sb, env.EnvID);  // 根环境 section 名就是类名
        return sb.ToString();
    }
    
    public static bool LoadFrom(this AttributeEnv root, string data)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (string.IsNullOrEmpty(data)) return false;

        var lines = data.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        AttributeEnv current = root;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                var section = line.Substring(1, line.Length - 2);
                var resolved = ResolveEnvPath(root, section);
                if (resolved == null)
                {
                    Debug.LogWarning($"[LoadEnv] 无法解析节: {section}，将忽略该节内行直到下一节。");
                    current = null;
                }
                else current = resolved;
                continue;
            }

            if (current == null)
            {
                // 当前没有目标 env（上一节解析失败），跳过行
                continue;
            }

            var ok = line.Length > 0 && line[0] switch
            {
                '*' => DeserializeVp(current, line),
                '#' => DeserializeStr(current, line),
                '^' => DeserializeMod(current, line),
                '@' => DeserializeExNum(current, line),
                _ => throw new ArgumentOutOfRangeException()
            };

            if (!ok) Debug.LogWarning($"[LoadEnv] 行解析失败: \n\t {line}");

            // 触发每行方法
            try { current.AfterLoadPerLines(line); }         
            catch { /* 忽略错误 */ }
        }
        try { root.AfterLoad(); }
        catch { /* 忽略错误 */ }
        return true;
    }
    
    
    
    public static string SerializeModValue(this AttributeEnv env)
    {
        if(env == null) return string.Empty;
        var type   = env.EnvType;
        var sb = new StringBuilder(1024); // 预分配缓冲
        foreach (var (name, mod) in env.GetAllModValues())
        {
            if (IgnoreSaveCore.Skip(type, name)) continue;
            if (mod is IModValue tv)
            {
                var typeChar = GetTypeChar(tv.BaseValue);
                
                sb.Append($"^{name}")
                    .Append(':')
                    .Append(typeChar) // 数据类型
                    .Append($"({ConvertToString(tv.BaseValue)})") //数据
                    .Append(':')
                    // .Append(SerializationModGroup(tv)) // 处理null
                    .Append('\n');
            }
        } 
        return sb.ToString();
    }
    
    public static string SerializeValuePair(this AttributeEnv env)
    {
        if(env == null) return string.Empty;
        var type   = env.EnvType;
        var sb = new StringBuilder(1024); // 预分配缓冲
        foreach (var (name, mod) in env.GetAllValuePairs())
        {
            if (IgnoreSaveCore.Skip(type, name)) continue;
            if (mod is IReadOnlyValuePair vp)
            {
                var typeChar = GetTypeChar(vp.Current);

                sb.Append($"*{name}") // 为Vp加上特殊标识用于和ModValue进行区分
                    .Append(':')
                    .Append(typeChar)
                    .Append($"({ConvertToString(vp.Current)},{ConvertToString(vp.Min)},{ConvertToString(vp.Max)})") // 数据封装
                    .Append('\n');
            }
        }
        return sb.ToString();
    }
    

    
    public static string SerializeString(this AttributeEnv env)
    {
        if(env == null) return string.Empty;
        var type   = env.EnvType;
        var sb = new StringBuilder(1024); // 预分配缓冲
        foreach (var (name, str) in env.GetAllString())
        {
            if (IgnoreSaveCore.Skip(type, name) || string.IsNullOrEmpty(str)) continue;
            sb.Append($"#{name}") // 字段名
                .Append(':')
                .Append(str)
                .Append('\n');
        }
        return sb.ToString();
    }
    
    public static string SerializeExNum(this AttributeEnv env)
    {
        if (env == null) return string.Empty;
        var type = env.EnvType;
        var sb   = new StringBuilder(512);

        foreach (var (name, ex) in env.GetAllExNum()) // 假设你有这个方法
        {
            if (IgnoreSaveCore.Skip(type, name)) continue;

            if (ex is IReadOnlyExNum nx) // 你可能需要定义一个只读接口
            {
                var typeChar = GetTypeChar(nx.Current);

                sb.Append($"@{name}")           // '@' 表示 ExNum
                    .Append(':')
                    .Append(typeChar)
                    .Append('(')
                    .Append(ConvertToString(nx.Current))
                    .Append(")\n");
            }
        }

        return sb.ToString();
    }

    
    public static bool DesModValue(this AttributeEnv env,string data)
    {
        if (string.IsNullOrEmpty(data)) return false;
        string[] lines = data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        { DeserializeMod(env, line); }
        return true;
    }
    public static bool DesValuePair(this AttributeEnv env, string data)
    {
        if (string.IsNullOrEmpty(data)) return false;
        
        string[] lines = data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        { DeserializeVp(env, line); }
        return true;
    }
    
    
    // TODO：后期需要在这几个泛型类的地方做泛用工具类目前的重复度过高
    private static bool DeserializeMod(AttributeEnv env, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;

        string[] parts = line.Split(':', 3);
        if (parts.Length < 2) return false; // 最少需要2部分
        
        string key = parts[0];
        if(key[..1] != "^") return false;
        key = key[1..]; // 获取正确的key
        string baseValue = parts[1];
        
        // 检查baseValue格式
        if (baseValue.Length < 3) return false; // 最小长度 "F(1)"
        if (baseValue[1] != '(' || baseValue[^1] != ')') return false;
        
        string valueType = baseValue[..1];
        string value = baseValue[2..^1];
        string extras = parts.Length > 2 ? parts[2] : "";
        
        if (string.IsNullOrWhiteSpace(value)) return false;

        try
        {
            switch (valueType)
            {
                case "I":
                    var valueInt = int.Parse(value);
                    if (env.TryGetModValue<int>(key, out var modInt))
                    { 
                        modInt.Base = valueInt; 
                        LoadModifier(extras, modInt);
                        return true;
                    }
                    Debug.LogError($"没有对应的Key[{key}] 请检查是否配置正确"); 
                    return false;

                case "F":
                    var valueFloat = float.Parse(value);
                    if (env.TryGetModValue<float>(key, out var modFloat))
                    { 
                        modFloat.Base = valueFloat; 
                        LoadModifier(extras, modFloat);
                        return true;
                    }
                    Debug.LogError($"没有对应的Key[{key}] 请检查是否配置正确"); 
                    return false;

                case "D":
                    var valueDouble = double.Parse(value);
                    if (env.TryGetModValue<double>(key, out var modDouble))
                    { 
                        modDouble.Base = valueDouble; 
                        LoadModifier(extras, modDouble);
                        return true;
                    }
                    Debug.LogError($"没有对应的Key[{key}] 请检查是否配置正确"); 
                    return false;

                default: 
                    Debug.LogError($"未知数据类型: {valueType}"); 
                    return false;
            }
        }
        catch (FormatException)
        {
            Debug.LogError($"数值格式错误: {line}");
            return false;
        }
        catch (OverflowException)
        {
            Debug.LogError($"数值溢出: {line}");
            return false;
        }
    }
    private static bool DeserializeVp(AttributeEnv env, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        
        string[] parts = line.Split(':', 2); // 每行按冒号分割成两个部分
        if (parts.Length < 2) return false; // 数组长度检查
        
        string key = parts[0];      // 属性ID 如 "MaxHealth"
        if(key[..1] != "*") return false;
        key = key[1..]; // 获取正确的key
        
        string baseValue = parts[1]; // 基础数据 如 "F(1.25,5,2)"
        if (baseValue.Length < 5) return false; // 最小长度检查

        
        string valueType = baseValue[..1];  // 数据类型 I F D
        string value     = baseValue[2..^1]; // 数据
        string[] values = value.Split(',');
        
        // 检查数据对应长度
        if (values.Length < 3) return false;
        // 检查数值有效性 如果数据无效则忽略
        if (values.Any(string.IsNullOrWhiteSpace)) return false;
        // 处理数据
        try
        {
            switch (valueType)
            {
                case "I":
                {
                    if (env.TryGetValuePair<int>(key, out var vp))
                    {
                        vp.Min = int.Parse(values[1]);
                        vp.Max = int.Parse(values[2]);
                        vp.Current = int.Parse(values[0]);
                    }
                    else
                    { Debug.LogError($"没有对应的Key{key} 请检查是否配置正确"); return false; }
                    break;
                }
                case "F":
                {
                    if (env.TryGetValuePair<float>(key, out var vp))
                    {
                        vp.Min = float.Parse(values[1]);
                        vp.Max = float.Parse(values[2]);
                        vp.Current = float.Parse(values[0]);
                    }
                    else
                    { Debug.LogError($"没有对应的Key{key} 请检查是否配置正确"); return false; }
                    break;
                }
                case "D":
                {
                    if (env.TryGetValuePair<double>(key, out var vp))
                    {
                        vp.Min = double.Parse(values[1]);
                        vp.Max = double.Parse(values[2]);
                        vp.Current = double.Parse(values[0]);
                    }
                    else
                    { Debug.LogError($"没有对应的Key{key} 请检查是否配置正确"); return false; }
                    break;
                }
                default: Debug.LogError($"未知数据类型: {valueType}"); return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"数据转换失败: {e.Message}");
            return false;
        }
        return true;
    }
    private static bool DeserializeExNum(AttributeEnv env, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;

        string[] parts = line.Split(':', 2);
        if (parts.Length < 2) return false;

        string key = parts[0];
        if (key[..1] != "@") return false; // 不是 ExNum
        key = key[1..]; // 去掉前缀@

        string baseValue = parts[1]; // 如 "F(3.5)"
        if (baseValue.Length < 4) return false; // 至少 "X()" 4 字符

        string valueType = baseValue[..1];   // I / F / D
        string valueStr  = baseValue[2..^1]; // 括号内的内容

        if (string.IsNullOrWhiteSpace(valueStr)) return false;

        try
        {
            switch (valueType)
            {
                case "I":
                {
                    if (env.TryGetExNum<int>(key, out var ex))
                    {
                        ex.Current = int.Parse(valueStr);
                    }
                    else
                    {
                        Debug.LogError($"没有对应的 ExNum Key: {key}");
                        return false;
                    }
                    break;
                }

                case "F":
                {
                    if (env.TryGetExNum<float>(key, out var ex))
                    {
                        ex.Current = float.Parse(valueStr);
                    }
                    else
                    {
                        Debug.LogError($"没有对应的 ExNum Key: {key}");
                        return false;
                    }
                    break;
                }

                case "D":
                {
                    if (env.TryGetExNum<double>(key, out var ex))
                    {
                        ex.Current = double.Parse(valueStr);
                    }
                    else
                    {
                        Debug.LogError($"没有对应的 ExNum Key: {key}");
                        return false;
                    }
                    break;
                }

                default:
                    Debug.LogError($"未知 ExNum 数据类型: {valueType}");
                    return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"ExNum 数据转换失败: {e.Message}");
            return false;
        }
        return true;
    }
    private static bool DeserializeStr(AttributeEnv env, string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            string[] parts = line.Split(':', 2);
            if (parts.Length < 2) return false;
            string key = parts[0];
            if(key[..1] != "#") return false;
            key = key[1..];
            var value = parts[1];
            if (!env.TryGetStr(key, out var field)) return false;
            field.Value = value; return true;
        }
    
    private static void LoadModifier(string data, IModValue value)
    {
        // if (string.IsNullOrEmpty(data)) return;
        // var matches = GroupRegex.Matches(data);
        // foreach (Match match in matches)
        // {
        //     string idPart = match.Groups[1].Value;
        //     string content = match.Groups[2].Value;
        //
        //     var modifiers = content.Split(';')
        //         .Select(modStr => 
        //         {
        //             var modMatch = ModRegex.Match(modStr);
        //             return new Modifier(
        //                 type: DeserializationModType(modMatch.Groups[1].Value),
        //                 value: double.Parse(modMatch.Groups[2].Value),
        //                 prio: int.TryParse(modMatch.Groups[3].Value, out int prio) ? prio : null
        //             );
        //         })
        //         .ToArray();
        //
        //     value.AddGroup(int.Parse(idPart), modifiers);
        // }
    }
    // 序列化ModGroup的方法 也就是增强属性的序列化方法
    private static string SerializationModGroup(IModValue modValue)
    {
        var ModGroupList = new List<string>();
        foreach (var (index,modGroup) in modValue.AllGroups)
        {
            var data = modGroup.GetSortedMods();
            var ModifierList = new List<string>();
            foreach (var modifier in data)
            {
                var Type =  SerializationModType(modifier.GetModType());
                var Value = modifier.GetValue();
                var ModifierData = $"{Type}({Value})^{modifier.Prio}";
                ModifierList.Add(ModifierData);
            }
            var ModGroupData = $"[{index}@{string.Join(";", ModifierList)}]";
            ModGroupList.Add(ModGroupData);
        }
        return ModGroupList.Count > 0 ? string.Join("||", ModGroupList) : null;
    }
    
    // 用于保证存储的数字类型不会被语言影响了形式永远保证是类似数字是1.2 而非变成德语之类的1,2
    private static string ConvertToString(object value)
    {
        return value switch
        {
            int i => i.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() // 其他类型保持原样
        };
    }
    private static string SerializationModType(ModType type)
    {
        return type switch
        {
            ModType.Add => "A",
            ModType.Multiply => "M",
            ModType.Override => "O",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
    private static ModType DeserializationModType(string type)
        {
            return type switch
            {
                "A" => ModType.Add,
                "M" => ModType.Multiply,
                "O" => ModType.Override,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    private static char GetTypeChar(object value)
    {
        char typeChar = 'D';
        if (value is int) typeChar = 'I';
        if (value is float) typeChar = 'F';
        return typeChar;
    }
    
    
    private static AttributeEnv ResolveEnvPath(AttributeEnv root, string path)
    {
        var parts = path.Split('.');
        var current = root;

        // 从 1 开始，因为第 0 个是 TypeName
        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];

            // 判断 name[index]
            var lb = part.IndexOf('[');
            if (lb >= 0)
            {
                var key = part[..lb];
                var rb = part.IndexOf(']', lb + 1);
                var index = int.Parse(part.Substring(lb + 1, rb - lb - 1));

                var container = current.AllEnv[key];

                // 优先数组
                if (container is Array arr)
                {
                    current = (AttributeEnv)arr.GetValue(index);
                    continue;
                }

                // 其次列表
                current = (AttributeEnv)((IList)container)[index];
                continue;
            }
            // 普通字段（必须是子环境）
            current = (AttributeEnv)current.AllEnv[part];
        }

        return current;
    }
    
    
    // 递归序列化
    private static void SerializeInternal(AttributeEnv env, StringBuilder sb, string path)
    {
        // section 标头
        sb.Append('[').Append(path).Append(']').Append('\n');

        // 当前 env 的字段
        sb.Append(env.SerializeString());
        sb.Append(env.SerializeExNum());
        sb.Append(env.SerializeModValue());
        sb.Append(env.SerializeValuePair());
        SerializeEnv(env, sb, path);
    }

    private static void SerializeEnv(AttributeEnv env, StringBuilder sb, string path)
    {
        // 遍历子环境
        foreach (var (key, obj) in env.AllEnv)
        {
            // 忽略不需要的环境
            if(IgnoreSaveCore.Skip(env.EnvType, key)) continue;
            switch (obj)
            {
                // 普通子环境
                case AttributeEnv childEnv:
                    SerializeInternal(childEnv, sb, $"{path}.{key}");
                    break;

                // 数组 (Array)
                case Array { Length: > 0 } arr when arr.GetValue(0) is AttributeEnv:
                    for (var i = 0; i < arr.Length; i++)
                    {
                        if (arr.GetValue(i) is AttributeEnv e)
                            SerializeInternal(e, sb, $"{path}.{key}[{i}]");
                    }
                    break;

                // IList（List<T>） 
                case IList { Count: > 0 } list when list[0] is AttributeEnv:
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i] is AttributeEnv e)
                            SerializeInternal(e, sb, $"{path}.{key}[{i}]");
                    }
                    break;
            }
        }
    }


    // private static string Serialize(this AttributeEnv env)
    // { 
    //     var sb = new StringBuilder();
    //     sb.Append(SerializeString(env));
    //     sb.Append(SerializeModValue(env));
    //     sb.Append(SerializeValuePair(env));
    //     return sb.ToString();
    // }
    // private static bool LoadFrom(this AttributeEnv env, string data)
    // {
    //     if (string.IsNullOrEmpty(data)) return false;
    //     
    //     var lines = data.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    //     var result = true;
    //     foreach (var raw in lines)
    //     {
    //         if (string.IsNullOrWhiteSpace(raw)) continue;
    //         var line = raw.TrimEnd(); // 去掉末尾回车符等
    //
    //         result &= line[..1] switch
    //         {
    //             "*" => DeserializeVp(env, line),
    //             "#" => DeserializeStr(env, line),
    //             _   => DeserializeMod(env, line)
    //         };
    //         // 每行读取完毕后回调 - 可用于初始化数据
    //         env.AfterLoadPerLines(line);
    //     }
    //     env.AfterLoad(); // 整体读取完的后处理
    //     return result;
    // }
}
