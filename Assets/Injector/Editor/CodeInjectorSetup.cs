// /*
// * ==============================================================================
// *
// * Description: IL注入核心处理, 可根据实际需求修改 DoInjectMethod 中的IL指令
// *
// * https://github.com/rayosu/UnityDllInjector
// *
// * Version: 1.0
// * Created: 2016-10-12 17:50
// *
// * Author: Surui (76963802@qq.com)
// * Company: WYD
// *
// * ==============================================================================
// */

using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using OpCodes = Mono.Cecil.Cil.OpCodes;

public class CodeInjectorSetup
{
    private readonly List<string> enginePaths = new List<string>();
    private readonly List<string> assemblys = new List<string>();
    public string BuildTarget;
    public string OutputDirectory;

    public void AddAssemblySearchDirectory(string enginePath)
    {
        enginePaths.Add(enginePath);
    }

    public void AddAssembly(string scriptPath)
    {
        assemblys.Add(scriptPath);
    }

    public void Run()
    {
        foreach (var path in assemblys)
        {
            var assembly = ReadAssembly(path);
            var result = DoInjector(assembly);
            if (result)
            {
                SaveAssembly(path, assembly);
            }
        }
    }

    private AssemblyDefinition ReadAssembly(string path)
    {
        Debug.Log(string.Format("ReadAssembly: {0}", path));
        var assemblyResolver = new DefaultAssemblyResolver();
        foreach (var enginePath in enginePaths)
        {
            Debug.Log(string.Format("AddSearchDirectory: {0}", enginePath));
            assemblyResolver.AddSearchDirectory(enginePath);
        }
        var readerParameters = new ReaderParameters
        {
            AssemblyResolver = assemblyResolver,
            ReadingMode = ReadingMode.Immediate,
            ReadSymbols = true
        };
        var assembly = AssemblyDefinition.ReadAssembly(path, readerParameters);
        return assembly;
    }

    private void SaveAssembly(string path, AssemblyDefinition assembly)
    {
        var outPath = Path.Combine(OutputDirectory, Path.GetFileName(path));
        Debug.Log(string.Format("WriteAssembly: {0}", outPath));

        var writerParameters = new WriterParameters { WriteSymbols = true };
        assembly.Write(outPath, writerParameters);
    }

    private static bool DoInjector(AssemblyDefinition assembly)
    {
        var modified = false;
        foreach (var type in assembly.MainModule.Types)
        {
            if (type.HasCustomAttribute<LuaInjectorAttribute>())
            {
                foreach (var method in type.Methods)
                {
                    if (method.HasCustomAttribute<LuaInjectorIgnoreAttribute>()) continue;

                    DoInjectMethod(assembly, method, type);
                    modified = true;
                }
            }
            else
            {
                foreach (var method in type.Methods)
                {
                    if (!method.HasCustomAttribute<LuaInjectorAttribute>()) continue;

                    DoInjectMethod(assembly, method, type);
                    modified = true;
                }
            }
        }
        return modified;
    }

    private static void DoInjectMethod(AssemblyDefinition assembly, MethodDefinition method, TypeDefinition type)
    {
        if (method.Name.Equals(".ctor") || !method.HasBody) return;

        var firstIns = method.Body.Instructions.First();
        var worker = method.Body.GetILProcessor();

        // bool result = LuaPatch.HasPatch(type.Name)
        var hasPatchRef = assembly.MainModule.Import(typeof(LuaPatch).GetMethod("HasPatch"));
        var current = InsertBefore(worker, firstIns, worker.Create(OpCodes.Ldstr, type.Name));
        current = InsertAfter(worker, current, worker.Create(OpCodes.Ldstr, method.Name));
        current = InsertAfter(worker, current, worker.Create(OpCodes.Call, hasPatchRef));

        // if(result == false) jump to the under code
        current = InsertAfter(worker, current, worker.Create(OpCodes.Brfalse, firstIns));

        // else LuaPatch.CallPatch(type.Name, method.Name, args)
        var callPatchMethod = typeof(LuaPatch).GetMethod("CallPatch");
        var callPatchRef = assembly.MainModule.Import(callPatchMethod);
        current = InsertAfter(worker, current, worker.Create(OpCodes.Ldstr, type.Name));
        current = InsertAfter(worker, current, worker.Create(OpCodes.Ldstr, method.Name));
        var paramsCount = method.Parameters.Count;
        // 创建 args参数 object[] 集合
        current = InsertAfter(worker, current, worker.Create(OpCodes.Ldc_I4, paramsCount));
        current = InsertAfter(worker, current, worker.Create(OpCodes.Newarr, assembly.MainModule.Import(typeof(object))));
        for (int index = 0; index < paramsCount; index++)
        {
            var argIndex = method.IsStatic ? index : index + 1;
            // 压入参数
            current = InsertAfter(worker, current, worker.Create(OpCodes.Dup));
            current = InsertAfter(worker, current, worker.Create(OpCodes.Ldc_I4, index));
            var paramType = method.Parameters[index].ParameterType;
            // 获取参数类型定义, 用来区分是否枚举类 [若你所使用的类型不在本assembly, 则此处需要遍历其他assembly以取得TypeDefinition]
            var paramTypeDef = assembly.MainModule.GetType(paramType.FullName);
            // 这里很重要, 需要判断出 值类型数据(不包括枚举) 是不需要拆箱的
            if (paramType.IsValueType && (paramTypeDef == null || !paramTypeDef.IsEnum))
            {
                current = InsertAfter(worker, current, worker.Create(OpCodes.Ldarg, argIndex));
            }
            else
            {
                current = InsertAfter(worker, current, worker.Create(OpCodes.Ldarg, argIndex));
                current = InsertAfter(worker, current, worker.Create(OpCodes.Box, paramType));
            }
            current = InsertAfter(worker, current, worker.Create(OpCodes.Stelem_Ref));
        }
        current = InsertAfter(worker, current, worker.Create(OpCodes.Call, callPatchRef));
        var methodReturnVoid = method.ReturnType.FullName.Equals("System.Void");
        var patchCallReturnVoid = callPatchMethod.ReturnType.FullName.Equals("System.Void");
        // LuaPatch.CallPatch()有返回值时
        if (!patchCallReturnVoid)
        {
            // 方法无返回值, 则需先Pop出栈区中CallPatch()返回的结果
            if (methodReturnVoid) current = InsertAfter(worker, current, worker.Create(OpCodes.Pop));
            // 方法有返回值时, 返回值进行拆箱
            else current = InsertAfter(worker, current, worker.Create(OpCodes.Unbox_Any, method.ReturnType));
        }
        // return
        InsertAfter(worker, current, worker.Create(OpCodes.Ret));

        // 重新计算语句位置偏移值
        ComputeOffsets(method.Body);
    }

    /// <summary>
    ///     语句前插入Instruction, 并返回当前语句
    /// </summary>
    private static Instruction InsertBefore(ILProcessor worker, Instruction target, Instruction instruction)
    {
        worker.InsertBefore(target, instruction);
        return instruction;
    }

    /// <summary>
    ///     语句后插入Instruction, 并返回当前语句
    /// </summary>
    private static Instruction InsertAfter(ILProcessor worker, Instruction target, Instruction instruction)
    {
        worker.InsertAfter(target, instruction);
        return instruction;
    }

    private static void ComputeOffsets(MethodBody body)
    {
        var offset = 0;
        foreach (var instruction in body.Instructions)
        {
            instruction.Offset = offset;
            offset += instruction.GetSize();
        }
    }
}