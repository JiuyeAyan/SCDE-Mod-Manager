using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop.VTables;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public unsafe struct UnitFunctionsVTable
{
    // 0 - 10
    public delegate* unmanaged[Cdecl]<Int64> NullUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeasantUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BurningManUpdate;
    public delegate* unmanaged[Cdecl]<Int64> WoodcutterUpdate;
    public delegate* unmanaged[Cdecl]<Int64> FletcherUpdate;
    public delegate* unmanaged[Cdecl]<Int64> TunnelerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> HunterUpdate;
    public delegate* unmanaged[Cdecl]<Int64> QuarryMasonUpdate;
    public delegate* unmanaged[Cdecl]<Int64> QuarryGruntUpdate;
    public delegate* unmanaged[Cdecl]<Int64> QuarryOxUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PitchmanUpdate;

    // 11 - 20
    public delegate* unmanaged[Cdecl]<Int64> FarmerWheatUpdate;
    public delegate* unmanaged[Cdecl]<Int64> FarmerHopsUpdate;
    public delegate* unmanaged[Cdecl]<Int64> FarmerAppleUpdate;
    public delegate* unmanaged[Cdecl]<Int64> FarmerCattleUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MillerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BakerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BrewerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PoleturnerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BlacksmithUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ArmourerUpdate;

    // 21 - 30
    public delegate* unmanaged[Cdecl]<Int64> TannerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ArcherUpdate;
    public delegate* unmanaged[Cdecl]<Int64> XbowmanUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SpearmanUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PikemanUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MacemanUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SwordsmanUpdate;
    public delegate* unmanaged[Cdecl]<Int64> KnightUpdate;
    public delegate* unmanaged[Cdecl]<Int64> LaddermanUpdate;
    public delegate* unmanaged[Cdecl]<Int64> EngineerUpdate;

    // 31 - 40
    public delegate* unmanaged[Cdecl]<Int64> Miner1Update;
    public delegate* unmanaged[Cdecl]<Int64> Miner2Update;
    public delegate* unmanaged[Cdecl]<Int64> PriestUpdate;
    public delegate* unmanaged[Cdecl]<Int64> HealerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> DrunkardUpdate;
    public delegate* unmanaged[Cdecl]<Int64> InnkeeperUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MonkUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ArcherDebugUpdate;
    public delegate* unmanaged[Cdecl]<Int64> CatapultUpdate;
    public delegate* unmanaged[Cdecl]<Int64> TrebuchetUpdate;

    // 41 - 50
    public delegate* unmanaged[Cdecl]<Int64> MangonelUpdate;
    public delegate* unmanaged[Cdecl]<Int64> TraderUpdate;
    public delegate* unmanaged[Cdecl]<Int64> TraderHorseUpdate;
    public delegate* unmanaged[Cdecl]<Int64> DeerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> LionUpdate;
    public delegate* unmanaged[Cdecl]<Int64> RabbitUpdate;
    public delegate* unmanaged[Cdecl]<Int64> CamelUpdate;
    public delegate* unmanaged[Cdecl]<Int64> CrowUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SeagullUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SiegeTentUpdate;

    // 51 - 60
    public delegate* unmanaged[Cdecl]<Int64> CowUpdate;
    public delegate* unmanaged[Cdecl]<Int64> DogUpdate;
    public delegate* unmanaged[Cdecl]<Int64> FiremanUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GhostUpdate;
    public delegate* unmanaged[Cdecl]<Int64> LordUpdate;
    public delegate* unmanaged[Cdecl]<Int64> LadyUpdate;
    public delegate* unmanaged[Cdecl]<Int64> JesterUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SiegeTowerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BatteringRamUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PortableShieldUpdate;

    // 61 - 70
    public delegate* unmanaged[Cdecl]<Int64> BallistaUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ChickenUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MotherUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ChildUpdate;
    public delegate* unmanaged[Cdecl]<Int64> JugglerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> FireeaterUpdate;
    public delegate* unmanaged[Cdecl]<Int64> WarDogUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BurningAnimalBigUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BurningAnimalSmallUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ArabBowUpdate;

    // 71 - 80
    public delegate* unmanaged[Cdecl]<Int64> ArabSlaveUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ArabSlingerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ArabAssasinUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ArabHorsemanUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ArabSwordsmanUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ArabGrenadierUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ArabBallistaUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BedouinCamelLancerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BedouinHealerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BedouinEunuchUpdate;

    // 81 - 88
    public delegate* unmanaged[Cdecl]<Int64> BedouinAmbusherUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BedouinSkirmisherUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BedouinHeavyCamelUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BedouinSapperUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BedouinDemolisherUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GoatUpdate;
    public delegate* unmanaged[Cdecl]<Int64> HyenaUpdate;
    public delegate* unmanaged[Cdecl]<Int64> CrocodileUpdate;
}