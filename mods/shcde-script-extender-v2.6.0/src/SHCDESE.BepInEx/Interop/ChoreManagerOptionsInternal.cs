using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;
/*
[StructLayout(LayoutKind.Explicit)]
public unsafe struct ChoreManagerOptionsInternal
{
    [FieldOffset(2590432)] public int StartingGameSpeed;
    [FieldOffset(2590436)] public int StartingGoodsLevel;
    [FieldOffset(2590616)] public int WinCondition;
    [FieldOffset(2590620)] public int UnknownFlag_647655;
    [FieldOffset(2590624)] public int AllowAutoTrading;
    [FieldOffset(2590640)] public int Fairness;


    // --- Advanced / Game Settings Block ---
    [FieldOffset(2596740)] public int Peacetime;
    [FieldOffset(2596760)] public int Autosave;
    [FieldOffset(2596780)] public int NoKnockdownWalls;
    [FieldOffset(2596788)] public int NoCows;
    [FieldOffset(2596792)] public int NoDogs;
    [FieldOffset(2596804)] public int ExtremePowers;
    [FieldOffset(2596808)] public int ExtremePowersAroundLord;
    [FieldOffset(2596812)] public int AllowOutposts;
    [FieldOffset(2596816)] public int AdvancedOptions;
    [FieldOffset(2596820)] public int AdvancedSkirmishOptions;


    // --- Advanced Options Toggles ---
    [FieldOffset(2596824)] public int AdvOpt_PreBuild;
    [FieldOffset(2596828)] public int AdvOpt_ImprovedArabSwordsmen;
    [FieldOffset(2596832)] public int AdvOpt_ImprovedLaddermen;
    [FieldOffset(2596836)] public int AdvOpt_ImprovedSpearmen;
    [FieldOffset(2596840)] public int AdvOpt_RebalancedHorseArchers;
    [FieldOffset(2596844)] public int AdvOpt_ImprovedFletchers;
    [FieldOffset(2596848)] public int AdvOpt_UncappedPeasants;
    [FieldOffset(2596852)] public int AdvOpt_FasterPeasants;

    [FieldOffset(2596856)] public int AdvOpt_Healers;
    [FieldOffset(2596860)] public int AdvOpt_Eunuchs;
    [FieldOffset(2596864)] public int AdvOpt_NoGold;

    [FieldOffset(2596868)] public int AdvOpt_EnemyHPS;


    // --- MP Availability Arrays ---
    [FieldOffset(2596872)] public fixed int MP_BuildingsAvailable[13];
    [FieldOffset(2596924)] public fixed int MP_GoodsAvailable[25];
    [FieldOffset(2597024)] public fixed int MP_TroopsAvailable[32];

    // --- Location and AI Logic ---
    [FieldOffset(2602972)] public fixed byte StartKeepLocationOrder[8];
    [FieldOffset(2602980)] public fixed int PreferredAIVs[8];
}*/
public readonly unsafe struct ChoreManagerOptionsInternal
{
    internal readonly byte* _base;

    public ChoreManagerOptionsInternal(void* basePtr)
    {
        _base = (byte*)basePtr;
    }

    public bool IsValid() => _base != null;

    // ---------------------------------------------------------------------
    // Low-level helpers
    // ---------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref int Int32At(int offset) => ref *(int*)(_base + offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<int> Int32SpanAt(int offset, int length) => new Span<int>(_base + offset, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<byte> ByteSpanAt(int offset, int length) => new Span<byte>(_base + offset, length);

    // ---------------------------------------------------------------------
    // Basic / Game Settings
    // ---------------------------------------------------------------------

    public ref int StartingGameSpeed => ref Int32At(2590432);
    public ref int StartingGoodsLevel => ref Int32At(2590436);
    public ref int WinCondition => ref Int32At(2590616);
    public ref int UnknownFlag_647655 => ref Int32At(2590620);
    public ref int AllowAutoTrading => ref Int32At(2590624);
    public ref int Fairness => ref Int32At(2590640);

    // ---------------------------------------------------------------------
    // Advanced / Game Settings Block
    // ---------------------------------------------------------------------

    public ref int Peacetime => ref Int32At(2596740);
    public ref int Autosave => ref Int32At(2596760);
    public ref int NoKnockdownWalls => ref Int32At(2596780);
    public ref int NoCows => ref Int32At(2596788);
    public ref int NoDogs => ref Int32At(2596792);
    public ref int ExtremePowers => ref Int32At(2596804);
    public ref int ExtremePowersAroundLord => ref Int32At(2596808);
    public ref int AllowOutposts => ref Int32At(2596812);
    public ref int AdvancedOptions => ref Int32At(2596816);
    public ref int AdvancedSkirmishOptions => ref Int32At(2596820);

    // ---------------------------------------------------------------------
    // Advanced Options Toggles
    // ---------------------------------------------------------------------

    public ref int AdvOpt_PreBuild => ref Int32At(2596824);
    public ref int AdvOpt_ImprovedArabSwordsmen => ref Int32At(2596828);
    public ref int AdvOpt_ImprovedLaddermen => ref Int32At(2596832);
    public ref int AdvOpt_ImprovedSpearmen => ref Int32At(2596836);
    public ref int AdvOpt_RebalancedHorseArchers => ref Int32At(2596840);
    public ref int AdvOpt_ImprovedFletchers => ref Int32At(2596844);
    public ref int AdvOpt_UncappedPeasants => ref Int32At(2596848);
    public ref int AdvOpt_FasterPeasants => ref Int32At(2596852);
    public ref int AdvOpt_Healers => ref Int32At(2596856);
    public ref int AdvOpt_Eunuchs => ref Int32At(2596860);
    public ref int AdvOpt_NoGold => ref Int32At(2596864);
    public ref int AdvOpt_EnemyHPS => ref Int32At(2596868);

    // ---------------------------------------------------------------------
    // MP Availability Arrays
    // ---------------------------------------------------------------------

    public Span<int> MP_BuildingsAvailable => Int32SpanAt(2596872, 13);

    public Span<int> MP_GoodsAvailable => Int32SpanAt(2596924, 25);

    public Span<int> MP_TroopsAvailable => Int32SpanAt(2597024, 32);

    // ---------------------------------------------------------------------
    // Location and AI Logic
    // ---------------------------------------------------------------------

    public Span<byte> StartKeepLocationOrder => ByteSpanAt(2602972, 8);

    public Span<int> PreferredAIVs => Int32SpanAt(2602980, 8);
}