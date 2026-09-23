using SHCDESE.Interop.Enums;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct AISellCategoryPair
{
    public eGoods32 item;
    public AISellCategory category;
}
