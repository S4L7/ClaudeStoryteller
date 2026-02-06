using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ClaudeStoryteller
{
    public static class SimpleJson
    {
        public static string Serialize(object obj)
        {
            if (obj == null) return "null";
            
            var type = obj.GetType();
            
            if (obj is string s)
                return "\"" + EscapeString(s) + "\"";
            
            if (obj is bool b)
                return b ? "true" : "false";
            
            if (obj is int || obj is float || obj is double || obj is long)
                return Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture);
            
            if (obj is IList list)
            {
                var sb = new StringBuilder("[");
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(Serialize(list[i]));
                }
                sb.Append("]");
                return sb.ToString();
            }
            
            if (obj is IDictionary dict)
            {
                var sb = new StringBuilder("{");
                bool first = true;
                foreach (DictionaryEntry entry in dict)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("\"" + entry.Key + "\":" + Serialize(entry.Value));
                }
                sb.Append("}");
                return sb.ToString();
            }
            
            // Object serialization
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var objSb = new StringBuilder("{");
            bool isFirst = true;
            
            foreach (var prop in props)
            {
                var value = prop.GetValue(obj);
                if (value == null) continue;
                
                if (!isFirst) objSb.Append(",");
                isFirst = false;
                
                string name = char.ToLower(prop.Name[0]) + prop.Name.Substring(1);
                name = ToSnakeCase(name);
                objSb.Append("\"" + name + "\":" + Serialize(value));
            }
            
            objSb.Append("}");
            return objSb.ToString();
        }

        public static T Deserialize<T>(string json) where T : new()
        {
            var obj = new T();
            var dict = ParseObject(json.Trim());
            
            foreach (var prop in typeof(T).GetProperties())
            {
                string snakeName = ToSnakeCase(prop.Name);
                if (dict.TryGetValue(snakeName, out string value))
                {
                    SetPropertyValue(obj, prop, value);
                }
            }
            
            return obj;
        }

        private static Dictionary<string, string> ParseObject(string json)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json) || json == "null") return result;
            
            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}")) return result;
            
            json = json.Substring(1, json.Length - 2);
            
            int depth = 0;
            int start = 0;
            string currentKey = null;
            bool inString = false;
            bool inKey = true;
            
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                
                if (c == '"' && (i == 0 || json[i-1] != '\\'))
                {
                    inString = !inString;
                    if (inKey && !inString)
                    {
                        currentKey = json.Substring(start + 1, i - start - 1);
                    }
                }
                else if (!inString)
                {
                    if (c == ':' && depth == 0 && inKey)
                    {
                        inKey = false;
                        start = i + 1;
                    }
                    else if (c == '{' || c == '[') depth++;
                    else if (c == '}' || c == ']') depth--;
                    else if (c == ',' && depth == 0)
                    {
                        if (currentKey != null)
                        {
                            result[currentKey] = json.Substring(start, i - start).Trim();
                        }
                        inKey = true;
                        start = i + 1;
                        currentKey = null;
                    }
                }
            }
            
            if (currentKey != null)
            {
                result[currentKey] = json.Substring(start).Trim();
            }
            
            return result;
        }

        private static void SetPropertyValue(object obj, PropertyInfo prop, string value)
        {
            value = value.Trim();
            
            if (prop.PropertyType == typeof(string))
            {
                if (value.StartsWith("\"") && value.EndsWith("\""))
                    value = value.Substring(1, value.Length - 2);
                prop.SetValue(obj, UnescapeString(value));
            }
            else if (prop.PropertyType == typeof(int))
            {
                if (int.TryParse(value, out int i))
                    prop.SetValue(obj, i);
            }
            else if (prop.PropertyType == typeof(float))
            {
                if (float.TryParse(value, System.Globalization.NumberStyles.Float, 
                    System.Globalization.CultureInfo.InvariantCulture, out float f))
                    prop.SetValue(obj, f);
            }
            else if (prop.PropertyType == typeof(bool))
            {
                prop.SetValue(obj, value == "true");
            }
            else if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
            {
                var nestedObj = Activator.CreateInstance(prop.PropertyType);
                var nestedDict = ParseObject(value);
                foreach (var nestedProp in prop.PropertyType.GetProperties())
                {
                    string snakeName = ToSnakeCase(nestedProp.Name);
                    if (nestedDict.TryGetValue(snakeName, out string nestedValue))
                    {
                        SetPropertyValue(nestedObj, nestedProp, nestedValue);
                    }
                }
                prop.SetValue(obj, nestedObj);
            }
        }

        private static string ToSnakeCase(string str)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                if (char.IsUpper(str[i]) && i > 0)
                    sb.Append('_');
                sb.Append(char.ToLower(str[i]));
            }
            return sb.ToString();
        }

        private static string EscapeString(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        private static string UnescapeString(string s)
        {
            return s.Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\t", "\t")
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
        }
    }
}
