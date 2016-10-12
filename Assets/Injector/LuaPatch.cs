// /*
// * ==============================================================================
// *
// * Description: Lua补丁执行逻辑
// *
// * Version: 1.0
// * Created: 2016-9-17 17:07
// *
// * Author: Surui (76963802@qq.com)
// * Company: WYD
// *
// * ==============================================================================
// */

using System;
using UnityEngine;

public class LuaPatch
{
    public static bool HasPatch(string luaFile, string luaFunc)
    {
        // TODO 此处写你的lua函数存在判断, 建议你把判断结果缓存起来
        return false;
    }

    public static object CallPatch(string luaFile, string luaFunc, params object[] args)
    {
        Debug.Log(string.Format("Do Lua Patch: {0}:{1}", luaFile, luaFunc));
        // TODO 此处写你的lua函数调用, 并传入参数.
//        return Lua.DoFile(luaFile).Call(luaFunc, args);
        return null;
    }
}