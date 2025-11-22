using System;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
public static class AttributeDatabase
{

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
        
        root.BeforeLoad(); // 加载前方法
        var IsDataArea = false;
        var lines = data.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("[/") && line.EndsWith("]"))
            { break; /* 此处为结束标记 */}
            
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                IsDataArea = true;
                continue;
            }

            if (!IsDataArea) continue;
            var ok = line.Length > 0 && line[0] switch
            {
                '*' => DeserializeVp(root, line),
                '#' => DeserializeStr(root, line),
                '^' => DeserializeMod(root, line),
                '@' => DeserializeExNum(root, line),
                _ => false
            };

            if (!ok) Debug.LogWarning($"读取行解析失败: \n\t {line}");

            // 触发每行方法
            try { root.AfterLoadPerLines(line); }
            catch { /* 忽略错误 */ }
        }
        try { root.AfterLoad(); }
        catch { /* 忽略错误 */ }
        return true;
    }
    private static string SerializeAllField(this AttributeEnv env)
    {
        if(env == null) return string.Empty;
        var type   = env.EnvType;
        var sb = new StringBuilder(1024); // 预分配缓冲
        foreach (var (name, mod) in env.GetAllField())
        {
            if (IgnoreSaveCore.Skip(type, name)) continue;
            switch (mod)
            {
                case IModValue tv: SerModValue(tv, sb, name); break;
                case IReadOnlyValuePair vp: SerVp(vp, sb, name); break;
                case ExString ex when string.IsNullOrEmpty(ex): continue;
                case ExString ex:
                    sb.Append($"#{name}") // 字段名
                        .Append(':')
                        .Append(ex)
                        .Append('\n');
                    break;
                case IReadOnlyExNum nx: SerNum(nx, sb, name); break;
            }
        } 
        return sb.ToString();
    }
    private static void SerNum(IReadOnlyExNum nx, StringBuilder sb, string name)
    {
        var typeChar = GetTypeChar(nx.Current);

        sb.Append($"@{name}")           // '@' 表示 ExNum
            .Append(':')
            .Append(typeChar)
            .Append('(')
            .Append(ConvertToString(nx.Current))
            .Append(")\n");
    }
    private static void SerVp(IReadOnlyValuePair vp, StringBuilder sb, string name)
    {
        var typeChar = GetTypeChar(vp.Current);
        sb.Append($"*{name}") // 为Vp加上特殊标识用于和ModValue进行区分
            .Append(':')
            .Append(typeChar)
            .Append($"({ConvertToString(vp.Current)},{ConvertToString(vp.Min)},{ConvertToString(vp.Max)})") // 数据封装
            .Append('\n');
    }
    private static void SerModValue(IModValue tv, StringBuilder sb, string name)
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
                        return true;
                    }
                    Debug.LogError($"没有对应的Key[{key}] 请检查是否配置正确"); 
                    return false;

                case "F":
                    var valueFloat = float.Parse(value);
                    if (env.TryGetModValue<float>(key, out var modFloat))
                    { 
                        modFloat.Base = valueFloat; 
                        return true;
                    }
                    Debug.LogError($"没有对应的Key[{key}] 请检查是否配置正确"); 
                    return false;

                case "D":
                    var valueDouble = double.Parse(value);
                    if (env.TryGetModValue<double>(key, out var modDouble))
                    { 
                        modDouble.Base = valueDouble; 
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

    private static char GetTypeChar(object value)
    {
        char typeChar = 'D';
        if (value is int) typeChar = 'I';
        if (value is float) typeChar = 'F';
        return typeChar;
    }
    
    // 递归序列化
    private static void SerializeInternal(AttributeEnv env, StringBuilder sb, string path)
    {
        // section 标头
        sb.Append('[').Append(path).Append(']').Append('\n');
        // 当前 env 的字段
        sb.Append(env.SerializeAllField());
        // section 结尾
        sb.Append("[/").Append(path).Append(']').Append('\n');
    }

    public static T LoadEnv<T>(this string data) where T : AttributeEnv,new()
    {
        var Env = new T();
        Env.LoadFrom(data);
        return Env;
    }
}
