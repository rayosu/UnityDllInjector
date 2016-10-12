// /*
// * ==============================================================================
// *
// * Description: IL代码注入工具扩展
// *
// * Version: 1.0
// * Created: 2016-10-12 16:49
// *
// * Author: Surui (76963802@qq.com)
// * Company: WYD
// *
// * ==============================================================================
// */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;

public static class AssemblyDefinitionEx
{
    public static List<TypeDefinition> FindTypesByAttribute<T>(this AssemblyDefinition assembly)
    {
        var targetTypes = new List<TypeDefinition>();
        foreach (var type in assembly.MainModule.Types)
        {
            if (type.HasCustomAttributes)
            {
                foreach (var customAttribute in type.CustomAttributes)
                {
                    if (customAttribute.AttributeType.FullName.Equals(typeof (T).FullName))
                    {
                        targetTypes.Add(type);
                    }
                }
            }
        }
        return targetTypes;
    }

    public static List<MethodDefinition> FindMethodsByAttribute<T>(this AssemblyDefinition assembly)
    {
        var targetMethods = new List<MethodDefinition>();
        foreach (var type in assembly.MainModule.Types)
        {
            foreach (var method in type.Methods)
            {
                if (method.HasCustomAttributes)
                {
                    foreach (var customAttribute in method.CustomAttributes)
                    {
                        if (customAttribute.AttributeType.FullName.Equals(typeof (T).FullName))
                        {
                            targetMethods.Add(method);
                        }
                    }
                }
            }
        }
        return targetMethods;
    }

    public static bool HasCustomAttribute<T>(this MethodDefinition method)
    {
        if (method.HasCustomAttributes)
        {
            foreach (var customAttribute in method.CustomAttributes)
            {
                if (customAttribute.AttributeType.FullName.Equals(typeof (T).FullName))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool HasCustomAttribute<T>(this TypeDefinition type)
    {
        if (type.HasCustomAttributes)
        {
            foreach (var customAttribute in type.CustomAttributes)
            {
                if (customAttribute.AttributeType.FullName.Equals(typeof (T).FullName))
                {
                    return true;
                }
            }
        }
        return false;
    }
}

public static class PathEx
{
    private static char SpaceCharacter = ' ';
    private static char SeparatorWin = '\\';
    private static char SeparatorMac = '/';

    private static char[] SpecifiSymbol = {SpaceCharacter, SeparatorWin, SeparatorMac};

    private static string _Combine(string path1, string path2)
    {
        path1 = path1.TrimEnd(SpecifiSymbol);
        path1 = path1 + SeparatorMac;
        path2 = path2.TrimStart(SpecifiSymbol);
        path2 = path2.TrimStart(SpecifiSymbol);

        return path1 + path2;
    }

    /// <summary>
    ///     多路径合并
    /// </summary>
    /// <param name="path1"></param>
    /// <param name="paths"></param>
    /// <returns></returns>
    public static string Combine(string path1, params string[] paths)
    {
        var path = path1;
        foreach (string subPath in paths)
        {
            path = _Combine(path, subPath);
        }
        return Normalize(path);
    }

    /// <summary>
    ///     此方法区别于Path.GetFileNameWithoutExtension:
    ///     原来什么路径就还是什么路径, 只去掉后缀
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string GetFilePathWithoutExtension(string path)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext))
            return path;
        // 去掉后缀
        return path.Substring(0, path.Length - ext.Length);
    }

    public static string Normalize(string path)
    {
        var normalized = path;
        normalized = Regex.Replace(normalized, @"/\./", "/");
        if (normalized.Contains(".."))
        {
            var list = new List<string>();
            var paths = normalized.Split('/');
            foreach (var name in paths)
            {
                // 首位是".."无法处理的
                if (name.Equals("..") && list.Count > 0)
                    list.RemoveAt(list.Count - 1);
                else
                    list.Add(name);
            }
            normalized = list.Join("/");
        }
        if (path.Contains("\\"))
        {
            normalized = normalized.Replace("\\", "/");
        }
        return normalized;
    }

    public static string Join<T>(this IEnumerable<T> source, string sp)
    {
        var result = new StringBuilder();
        var first = true;
        foreach (T item in source)
        {
            if (first)
            {
                first = false;
                result.Append(item);
            }
            else
            {
                result.Append(sp).Append(item);
            }
        }
        return result.ToString();
    }
}