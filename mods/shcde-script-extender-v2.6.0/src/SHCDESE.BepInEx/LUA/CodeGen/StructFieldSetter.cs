using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SHCDESE.Lua.CodeGen;

public static class StructFieldSetter
{
    /// <summary>
    /// Set a field value on a struct instance by field name, only if the field is marked with a specific attribute.
    /// </summary>
    /// <typeparam name="T">Struct type</typeparam>
    /// <param name="pStruct">Reference to the struct instance</param>
    /// <param name="field">Field name</param>
    /// <param name="value">Value to set</param>
    /// <param name="attributeType">Attribute type to check (e.g. typeof(LUAExposedAttribute))</param>
    public static unsafe void SetField<T>(T* pStruct, string field, object value, Type attributeType) where T : unmanaged
    {
        FieldInfo info = typeof(T).GetField(field, BindingFlags.Public | BindingFlags.Instance);
        if (info == null || !Attribute.IsDefined(info, attributeType)) return;
        int offset = Marshal.OffsetOf(typeof(T), field).ToInt32();

        byte* fieldPtr = (byte*)pStruct + offset;
        Type fieldType = info.FieldType;
        if (fieldType == typeof(float))
            *(float*)fieldPtr = Convert.ToSingle(value);
        else if (fieldType == typeof(double))
            *(double*)fieldPtr = Convert.ToDouble(value);
        else if (fieldType == typeof(byte))
            *(byte*)fieldPtr = Convert.ToByte(value);
        else if (fieldType == typeof(sbyte))
            *(sbyte*)fieldPtr = Convert.ToSByte(value);
        else if (fieldType == typeof(short))
            *(short*)fieldPtr = Convert.ToInt16(value);
        else if (fieldType == typeof(ushort))
            *(ushort*)fieldPtr = Convert.ToUInt16(value);
        else if (fieldType == typeof(int))
            *(int*)fieldPtr = Convert.ToInt32(value);
        else if (fieldType == typeof(uint))
            *(uint*)fieldPtr = Convert.ToUInt32(value);
        else if (fieldType == typeof(long))
            *(long*)fieldPtr = Convert.ToInt64(value);
        else if (fieldType == typeof(ulong))
            *(ulong*)fieldPtr = Convert.ToUInt64(value);
        else if (fieldType.IsEnum)
        {
            Type underlyingType = Enum.GetUnderlyingType(fieldType);
            object enumValue = Convert.ChangeType(value, underlyingType);
            if (underlyingType == typeof(byte))
                *(byte*)fieldPtr = (byte)enumValue;
            else if (underlyingType == typeof(sbyte))
                *(sbyte*)fieldPtr = (sbyte)enumValue;
            else if (underlyingType == typeof(short))
                *(short*)fieldPtr = (short)enumValue;
            else if (underlyingType == typeof(ushort))
                *(ushort*)fieldPtr = (ushort)enumValue;
            else if (underlyingType == typeof(int))
                *(int*)fieldPtr = (int)enumValue;
            else if (underlyingType == typeof(uint))
                *(uint*)fieldPtr = (uint)enumValue;
            else if (underlyingType == typeof(long))
                *(long*)fieldPtr = (long)enumValue;
            else if (underlyingType == typeof(ulong))
                *(ulong*)fieldPtr = (ulong)enumValue;
        }
    }
}
