// /*
// * ==============================================================================
// *
// * Description: Lua×¢ÈëÌØÐÔ
// *
// * Version: 1.0
// * Created: 2016-09-17 11:44
// *
// * Author: Surui (76963802@qq.com)
// * Company: WYD
// *
// * ==============================================================================
// */

using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class LuaInjectorAttribute : Attribute
{
}
[AttributeUsage(AttributeTargets.Method)]
public class LuaInjectorIgnoreAttribute : Attribute
{
}