using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Lua;

public static class LuaInterop
{
    private const string LIBRARY_NAME = "lua54.dll";

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void lua_pushcclosure(IntPtr luaState, IntPtr f, int n);

}