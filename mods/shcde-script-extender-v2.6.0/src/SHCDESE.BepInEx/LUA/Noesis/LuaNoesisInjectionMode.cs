using System;

namespace SHCDESE.LUA.Noesis;

public enum LuaNoesisInjectionMode
{
    /// <summary> Adds the new XAML as a child of the target element. </summary>
    Child = 0,
    /// <summary> Replaces the content of the target element. </summary>
    ReplaceContent = 1,
    /// <summary> Adds the new XAML to the PARENT of the target element. (Useful for unnamed root grids). </summary>
    AddToParent = 2
}
