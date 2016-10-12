在对Unity项目后期需要对c#实现的代码添加lua补丁, 这里介绍一种不污染老代码的lua补丁方案

##1.我为何有IL[^1]代码注入的想法
[^1]:Unity中不管使用C#还是其他语言, 都会编译为IL代码存放为dll形式, iOS打包会进行IL2Cpp转换为C++代码, 所以此处对IL这一中间代码(dll文件)的修改, 可以达成注入的目的.

- Unity项目如果初期没有很好的规划代码热更, 基本都会选择C#作为开发语言, 那么项目后期引入lua机制, 把旧模块用lua重写并非很好的方案, 此时更希望是给旧代码留一个lua热更入口.
- 为了减少重复代码, 借鉴J2EE领域中AOP[^2]实现思路, 应用到此次需求上.
[^2]:IL代码注入只是AOP的一种实现方案, AOP(面向切面编程)的思想源自GOF设计模式, 你可以理解为: 用横向的思考角度, 来统一切入一类相同逻辑的某个"切面"(Aspect), 让使用者(逻辑程序员)无需重复关注这个"横向面"需要做的工作.这里的切面就是"判断是否有对应Lua补丁"

##2.lua补丁代码雏形
```
public class FooBar
{
    public void Foo(string params1, int params2, Action params3)
    {
        if(LuaPatch.HasPatch("path/to/lua/file", "luaFuncName"))
        {
            LuaPatch.CallPatch("path/to/lua/file", "luaFuncName", params1, params2, params3);
            return;
        }
        // the old code here
        Debug.Log("这里是原来的逻辑代码, 无返回值");
    }
    public Vector2 Bar(string params1, int params2, Action params3)
    {
        if (LuaPatch.HasPatch("path/to/lua/file", "luaFuncName"))
        {
            return (Vector2)LuaPatch.CallPatch("path/to/lua/file", "luaFuncName", params1, params2, params3);
        }
        // the old code here
        Debug.Log("这里是原来的逻辑代码, 有返回值");
        return Vector2.one;
    }
}
```
至于是使用sLua或者toLua方案, 大家各自根据项目需要自由选择.
>https://github.com/pangweiwei/slua
https://github.com/topameng/tolua
如果没有使用lua做大量向量,三角函数运算, 两个方案没有太大差异

##3.初识IL
>IL语法参考文章:http://www.cnblogs.com/Jax/archive/2009/05/29/1491523.html

上面LuaPatch判断那一段先使用IL语法重新书写
由于大家时间都很宝贵, 为了节省时间这里不精通IL语法也行, 这里有一个取巧的方法

- 请自行下载利器: **.NET Reflector**
- 我们使用Reflector打开Unity工程下\Library\ScriptAssemblies\Assembly-CSharp.dll
找到你事先写好的希望注入到代码模板, 这里我以上面Foobar.cs为例

![Paste_Image.png](http://upload-images.jianshu.io/upload_images/3160167-c1673fc637a51043.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

- 篇幅限制, 我把核心的IL代码贴出并加上注释, 大家根据具体情况自行使用Reflector获取
```
# 代码后附带MSDN文档链接
L_0000: ldstr "path/to/lua/file"    -- 压入string参数
L_0005: ldstr "luaFuncName"
L_000a: call bool LuaPatch::HasPatch (string, string) -- 调用方法, 并指定参数形式
L_000f: brfalse L_0040              -- 相当于 if(上述返回值为false) jump L_0040行
L_0014: ldstr "path/to/lua/file"    -- 同样压入参数
L_0019: ldstr "luaFuncName"
L_001e: ldc.i4.3                    -- 对应params不定参数, 需要根据具体不定参个数声明对应数组, 这里newarr object, 长度为3
L_001f: newarr object
L_0024: dup                         -- 复制栈顶(数组)的引用并压入计算堆栈中
L_0025: ldc.i4.0                    -- 0下标存放本函数传入第一个参数的引用
L_0026: ldarg.1                     -- #这里要注意static方法ldarg.0是第一个参数, 非static的ldarg.0存放的是"this"
L_0027: stelem.ref                  -- 声明上述传入数组的参数为其对象的引用
L_0028: dup                         -- 作用同上一个dup
L_0029: ldc.i4.1                    
L_002a: ldarg.2
L_002b: box int32
L_0030: stelem.ref
L_0031: dup
L_0032: ldc.i4.2
L_0033: ldarg.3
L_0034: stelem.ref
L_0035: call object LuaPatch ::CallPatch (string, string, object[])
L_003a: unbox.any [UnityEngine]UnityEngine.Vector2
L_003f: ret
```
**对IL语法有个大致理解, 有助于稍后用C#进行代码注入, 对于指令可以参考msdn的[OpCodes文档](https://msdn.microsoft.com/zh-cn/library/system.reflection.emit.opcodes(v=vs.110).aspx).**

##4.Mono.Ceil库
1. 能够标记需要注入的类或者方法
利用C#的 **特性(Attribute)**
1)声明特性如下:
```
using System;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class LuaInjectorAttribute : Attribute
{
}
[AttributeUsage(AttributeTargets.Method)]
public class LuaInjectorIgnoreAttribute : Attribute
{
}
```
2)使用特性进行标记
```
[LuaInjector]
public class CatDog
{
    public void Cat()
    {
        // 这个类所有函数都会被注入
    }
    [LuaInjectorIgnore]
    public static void Dog()
    {
        // 只有LuaInjectorIgnore标记的会被忽略
    }
}
```
*上述作为实现参考, 当然你也可以对Namespace, cs代码目录进行遍历, 或者通过代码主动Add(Type targetType)等方式来进行注入标记.*
3)遍历dll中所有的类型
```
var assembly = AssemblyDefinition.ReadAssembly("path/to/Library/ScriptAssemblies/Assembly-CSharp.dll");
foreach (var type in assembly.MainModule.Types)
{
  // 判断Attribute是否LuaInjector等等
}
```

2. C#进行IL代码注入的核心代码
```
    // 代码片段
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
        // 忽略构造器
        if (method.Name.Equals(".ctor") || !method.HasBody) return;

        var firstIns = method.Body.Instructions.First();
        var worker = method.Body.GetILProcessor();

        // 下列代码对照IL语法进行编写
        // bool result = LuaPatch.HasPatch(type.Name)
        var hasPatchRef = assembly.MainModule.Import(typeof (LuaPatch).GetMethod("HasPatch"));
        var current = InsertBefore(worker, firstIns, worker.Create(OpCodes.Ldstr, type.Name));
        current = InsertAfter(worker, current, worker.Create(OpCodes.Ldstr, method.Name));
        current = InsertAfter(worker, current, worker.Create(OpCodes.Call, hasPatchRef));

        // if(result == false) jump to the under code
        current = InsertAfter(worker, current, worker.Create(OpCodes.Brfalse, firstIns));

        // else LuaPatch.CallPatch(type.Name, method.Name, args)
        var callPatchRef = assembly.MainModule.Import(typeof (LuaPatch).GetMethod("CallPatch"));
        current = InsertAfter(worker, current, worker.Create(OpCodes.Ldstr, type.Name));
        current = InsertAfter(worker, current, worker.Create(OpCodes.Ldstr, method.Name));
        var paramsCount = method.Parameters.Count;
        // 创建 args参数 object[] 集合
        current = InsertAfter(worker, current, worker.Create(OpCodes.Ldc_I4, paramsCount));
        current = InsertAfter(worker, current, worker.Create(OpCodes.Newarr, assembly.MainModule.Import(typeof (object))));
        for (int index = 0; index < paramsCount; index++)
        {
            var argIndex = method.IsStatic ? index : index + 1;
            // 压入参数
            current = InsertAfter(worker, current, worker.Create(OpCodes.Dup));
            current = InsertAfter(worker, current, worker.Create(OpCodes.Ldc_I4, index));
            current = InsertAfter(worker, current, worker.Create(OpCodes.Ldarg, argIndex));
            current = InsertAfter(worker, current, worker.Create(OpCodes.Stelem_Ref));
        }
        current = InsertAfter(worker, current, worker.Create(OpCodes.Call, callPatchRef));
        // 方法有返回值时
        if (!method.ReturnType.FullName.Equals("System.Void"))
        {
            current = InsertAfter(worker, current, worker.Create(OpCodes.Unbox_Any, method.ReturnType));
        }
        // return
        InsertAfter(worker, current, worker.Create(OpCodes.Ret));

        // 重新计算语句位置偏移值
        ComputeOffsets(method.Body);
    }
    /// <summary>
    /// 语句前插入Instruction, 并返回当前语句
    /// </summary>
    private static Instruction InsertBefore(ILProcessor worker, Instruction target, Instruction instruction)
    {
        worker.InsertBefore(target, instruction);
        return instruction;
    }

    /// <summary>
    /// 语句后插入Instruction, 并返回当前语句
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
```
3. 能够在Unity打包时自动执行IL注入
使用特性[PostProcessScene](https://docs.unity3d.com/ScriptReference/Callbacks.PostProcessSceneAttribute.html)进行标记, 不过注意如果你的项目中有多个Scene需要打包, 这里避免重复调用, 需要添加一个_hasMidCodeInjectored用来标记, 达到只在一个场景时机执行注入处理.
```
    // 代码片段
    [PostProcessScene]
    private static void MidCodeInjectoring()
    {
        if (_hasMidCodeInjectored) return;
        D.Log("PostProcessBuild::OnPostProcessScene");

        // Don't CodeInjector when in Editor and pressing Play
        if (Application.isPlaying || EditorApplication.isPlaying) return;
        //if (!EditorApplication.isCompiling) return;

        BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;

        if (buildTarget == BuildTarget.Android)
        {
            if (DoCodeInjectorBuild("Android"))
            {
                _hasMidCodeInjectored = true;
            }
            else
            {
                D.LogWarning("CodeInjector: Failed to inject Android build!");
            }
        }
        else if (buildTarget == BuildTarget.iPhone)
        {
            if (DoCodeInjectorBuild("iOS"))
            {
                _hasMidCodeInjectored = true;
            }
            else
            {
                D.LogWarning("CodeInjector: Failed to inject iOS build!");
            }
        }
    }
```