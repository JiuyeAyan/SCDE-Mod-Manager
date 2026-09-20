using SHCDESE.EventAPI.Units;
using SHCDESE.Lua.DocsGen;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to a wide range of unit-specific events.
/// </summary>
public static class UnitR3EventHooks
{
    /// <summary>
    /// Fired whenver the game interpolates chimps on the active game map.
    /// Not accessible from lua.
    /// </summary>
    public static readonly R3EventHook<UnitUnityVisualInterpolateEventArgs> OnUnitUnityVisualInterpolate = new();

    /// <summary>
    /// Fired whenver the game removes a chimp from the visual side (unity side)
    /// Not accessible from lua.
    /// </summary>
    public static readonly R3EventHook<UnitUnityVisualRemoveEventArgs> OnUnitUnityVisualRemove = new();

    /// <summary>
    /// Fired whenever the game updates or instantiates the visual sprite (Chimp) for a unit.
    /// Use this to attach/detach visual effects (Shields, Banners, Particles) ensuring they sync with Object Pooling.
    /// Not accessible from lua.
    /// </summary>
    public static readonly R3EventHook<UnitUnityVisualSpawnEventArgs> OnUnitUnityVisualSpawn = new();

    /// <summary>
    /// Fired each frame for a unit that is actively moving.
    /// </summary>
    [LuaApiExport("OnUnitMovement")]
    public static readonly R3EventHook<UnitMovementEventArgs> OnUnitMovement = new();

    /// <summary>
    /// Fired when a unit is deleted from the game using the low-level deletion function.
    /// </summary>
    [LuaApiExport("OnUnitDelete")]
    public static readonly R3EventHook<UnitDeleteEventArgs> OnUnitDelete = new();

    /// <summary>
    /// Fired when a new unit is spawned. The new unit's ID is available in the 'Post' phase via the ReturnValue.
    /// </summary>
    [LuaApiExport("OnUnitCreate")]
    public static readonly R3EventHook<UnitCreateEventArgs> OnUnitCreate = new();

    /// <summary>
    /// Fired at the moment a unit is killed by a projectile.
    /// </summary>
    [LuaApiExport("OnUnitKilledByProjectile")]
    public static readonly R3EventHook<UnitKilledByProjectileEventArgs> OnUnitKilledByProjectile = new();

    /// <summary>
    /// Fired at the moment a unit is killed by a melee attack.
    /// </summary>
    [LuaApiExport("OnUnitKilledByMelee")]
    public static readonly R3EventHook<UnitKilledByMeleeEventArgs> OnUnitKilledByMelee = new();

    /// <summary>
    /// Fired when a unit takes damage from a projectile. This precedes the kill event if the damage is fatal.
    /// NOTE: This hook also fires if projectiles just "touch" a unit, the game only later decides which unit in particular
    /// is supposed to be damaged, or if at all. If you want a hook that ONLY fires for actually damaged units,
    /// pleae prefer <see cref="OnUnitTakeProjectileDamageEx"/>
    /// </summary>
    [LuaApiExport("OnUnitTakeProjectileDamage")]
    public static readonly R3EventHook<UnitTakeDamageByProjectileEventArgs> OnUnitTakeProjectileDamage = new();

    /// <summary>
    /// Fired when a unit takes damage from a projectile. This precedes the kill event if the damage is fatal.
    /// This is an extended version that plays AFTER the original one, this variant allows damage manipulation.
    /// Specials restrictions apply:
    /// - Only Pre event is available.
    /// </summary>
    [LuaApiExport("OnUnitTakeProjectileDamageEx")]
    public static readonly R3EventHook<UnitTakeDamageByProjectileExEventArgs> OnUnitTakeProjectileDamageEx = new();

    /// <summary>
    /// Fired when a unit takes damage from a melee attack. This precedes the kill event if the damage is fatal.
    /// </summary>
    [LuaApiExport("OnUnitTakeMeleeDamage")]
    public static readonly R3EventHook<UnitTakeDamageByMeleeEventArgs> OnUnitTakeMeleeDamage = new();

    /// <summary>
    /// Fired when a "move to location" order is issued to an individual unit.
    /// </summary>
    [LuaApiExport("OnUnitMoveHere")]
    public static readonly R3EventHook<UnitMoveHereEventArgs> OnUnitMoveHere = new();

    /// <summary>
    /// Fired when a unit gets healed by a bedouin healer.
    /// </summary>
    [LuaApiExport("OnUnitHealByBedouinHealer")]
    public static readonly R3EventHook<UnitHealByBedouinHealerEventArgs> OnUnitHealByBedouinHealer = new();

    //
    // Worker - Related
    //
    /// <summary>Fired when a Woodcutter picks up wood from their hut.</summary>
    [LuaApiExport("OnWoodcutterPickUpPlanks")]
    public static readonly R3EventHook<UnitWoodcutterPickUpPlanksEventArgs> OnWoodcutterPickUpPlanks = new();
    /// <summary>Fired when a Woodcutter drops off wood at the stockpile.</summary>
    [LuaApiExport("OnWoodcutterDropOffPlanks")]
    public static readonly R3EventHook<UnitWoodcutterDropOffPlanksEventArgs> OnWoodcutterDropOffPlanks = new();

    /// <summary>Fired when a Cattle Farmer picks up cheese from their dairy farm.</summary>
    [LuaApiExport("OnCattleFarmerPickUpCheese")]
    public static readonly R3EventHook<UnitCattleFarmerPickUpCheeseEventArgs> OnCattleFarmerPickUpCheese = new();
    /// <summary>Fired when a Cattle Farmer drops off cheese at the granary.</summary>
    [LuaApiExport("OnCattleFarmerDropOffCheese")]
    public static readonly R3EventHook<UnitCattleFarmerDropOffCheeseEventArgs> OnCattleFarmerDropOffCheese = new();

    /// <summary>Fired when an Apple Farmer picks up apples from their orchard.</summary>
    [LuaApiExport("OnAppleFarmerPickUpApple")]
    public static readonly R3EventHook<UnitAppleFarmerPickUpAppleEventArgs> OnAppleFarmerPickUpApple = new();
    /// <summary>Fired when an Apple Farmer drops off apples at the granary.</summary>
    [LuaApiExport("OnAppleFarmerDropOffApple")]
    public static readonly R3EventHook<UnitAppleFarmerDropOffAppleEventArgs> OnAppleFarmerDropOffApple = new();

    /// <summary>Fired when a Hops Farmer picks up hops from their hops farm.</summary>
    [LuaApiExport("OnHempFarmerPickUpHemp")]
    public static readonly R3EventHook<UnitHempFarmerPickUpHempEventArgs> OnHempFarmerPickUpHemp = new();
    /// <summary>Fired when a Hops Farmer drops off hops at the stockpile.</summary>
    [LuaApiExport("OnHempFarmerDropOffHemp")]
    public static readonly R3EventHook<UnitHempFarmerDropOffHempEventArgs> OnHempFarmerDropOffHemp = new();

    /// <summary>Fired when a Wheat Farmer picks up wheat from their wheat farm.</summary>
    [LuaApiExport("OnWheatFarmerPickUpWheat")]
    public static readonly R3EventHook<UnitWheatFarmerPickUpWheatEventArgs> OnWheatFarmerPickUpWheat = new();
    /// <summary>Fired when a Wheat Farmer drops off wheat at the stockpile.</summary>
    [LuaApiExport("OnWheatFarmerDropOffWheat")]
    public static readonly R3EventHook<UnitWheatFarmerDropOffWheatEventArgs> OnWheatFarmerDropOffWheat = new();

    /// <summary>Fired when a Baker picks up finished bread from their bakery.</summary>
    [LuaApiExport("OnBakerPickUpBread")]
    public static readonly R3EventHook<UnitBakerPickUpBreadEventArgs> OnBakerPickUpBread = new();
    /// <summary>Fired when a Baker drops off bread at the granary.</summary>
    [LuaApiExport("OnBakerDropOffBread")]
    public static readonly R3EventHook<UnitBakerDropOffBreadEventArgs> OnBakerDropOffBread = new();
    /// <summary>Fired when a Baker picks up flour from the stockpile to begin baking.</summary>
    [LuaApiExport("OnBakerPickUpFlour")]
    public static readonly R3EventHook<UnitBakerPickUpFlourEventArgs> OnBakerPickUpFlour = new();

    /// <summary>Fired when a Miller drops off flour at the stockpile.</summary>
    [LuaApiExport("OnMillerDropOffFlour")]
    public static readonly R3EventHook<UnitMillerDropOffFlourEventArgs> OnMillerDropOffFlour = new();
    /// <summary>Fired when a Miller picks up finished flour from their mill.</summary>
    [LuaApiExport("OnMillerPickUpFlour")]
    public static readonly R3EventHook<UnitMillerPickUpFlourEventArgs> OnMillerPickUpFlour = new();
    /// <summary>Fired when a Miller picks up wheat from the stockpile to begin milling.</summary>
    [LuaApiExport("OnMillerPickUpWheat")]
    public static readonly R3EventHook<UnitMillerPickUpWheatEventArgs> OnMillerPickUpWheat = new();

    /// <summary>Fired when a Brewer picks up hops from the stockpile.</summary>
    [LuaApiExport("OnBrewerPickUpHemp")]
    public static readonly R3EventHook<UnitBrewerPickUpHempEventArgs> OnBrewerPickUpHemp = new();
    /// <summary>Fired when a Brewer drops off hops at their brewery.</summary>
    [LuaApiExport("OnBrewerDropOffHemp")]
    public static readonly R3EventHook<UnitBrewerDropOffHempEventArgs> OnBrewerDropOffHemp = new();
    /// <summary>Fired when a Brewer picks up finished ale from their brewery.</summary>
    [LuaApiExport("OnBrewerPickUpAle")]
    public static readonly R3EventHook<UnitBrewerPickUpAleEventArgs> OnBrewerPickUpAle = new();
    /// <summary>Fired when a Brewer drops off ale at the stockpile.</summary>
    [LuaApiExport("OnBrewerDropOffAle")]
    public static readonly R3EventHook<UnitBrewerDropOffAleEventArgs> OnBrewerDropOffAle = new();
    /// <summary>Fired when a Brewer's production cycle finishes and ale is produced.</summary>
    [LuaApiExport("OnBrewerProduceAle")]
    public static readonly R3EventHook<UnitBrewerProducedAleEventArgs> OnBrewerProduceAle = new();

    /// <summary>Fired when an Innkeeper picks up ale from the stockpile.</summary>
    [LuaApiExport("OnInnkeperPickUpAle")]
    public static readonly R3EventHook<UnitInnkeeperPickUpAleEventArgs> OnInnkeperPickUpAle = new();
    /// <summary>Fired when an Innkeeper drops off ale at their inn.</summary>
    [LuaApiExport("OnInnkeeperDropOffAle")]
    public static readonly R3EventHook<UnitInnkeeperDropOffAleEventArgs> OnInnkeeperDropOffAle = new();

    /// <summary>Fired when a Fletcher picks up wood from the stockpile.</summary>
    [LuaApiExport("OnFletcherPickUpPlanks")]
    public static readonly R3EventHook<UnitFletcherPickUpPlanksEventArgs> OnFletcherPickUpPlanks = new();
    /// <summary>Fired when a Fletcher drops off wood at their workshop.</summary>
    [LuaApiExport("OnFletcherDropOffPlanks")]
    public static readonly R3EventHook<UnitFletcherDropOffPlanksEventArgs> OnFletcherDropOffPlanks = new();
    /// <summary>Fired when a Fletcher drops off a finished bow or crossbow at the armoury.</summary>
    [LuaApiExport("OnFletcherDropOffProduce")]
    public static readonly R3EventHook<UnitFletcherDropOffProduceEventArg> OnFletcherDropOffProduce = new();

    /// <summary>Fired when a Poleturner picks up wood from the stockpile.</summary>
    [LuaApiExport("OnPoleturnerPickUpPlanks")]
    public static readonly R3EventHook<UnitPoleturnerPickUpPlanksEventArgs> OnPoleturnerPickUpPlanks = new();
    /// <summary>Fired when a Poleturner drops off wood at their workshop.</summary>
    [LuaApiExport("OnPoleturnerDropOffPlanks")]
    public static readonly R3EventHook<UnitPoleturnerDropOffPlanksEventArgs> OnPoleturnerDropOffPlanks = new();
    /// <summary>Fired when a Poleturner drops off a finished pike or spear at the armoury.</summary>
    [LuaApiExport("OnPoleturnerDropOffProduce")]
    public static readonly R3EventHook<UnitPoleturnerDropOffProduceEventArgs> OnPoleturnerDropOffProduce = new();

    /// <summary>Fired when a Blacksmith picks up iron from the stockpile.</summary>
    [LuaApiExport("OnBlacksmithPickUpIron")]
    public static readonly R3EventHook<UnitBlacksmithPickUpIronEventArgs> OnBlacksmithPickUpIron = new();
    /// <summary>Fired when a Blacksmith drops off iron at their workshop.</summary>
    [LuaApiExport("OnBlacksmithDropOffIron")]
    public static readonly R3EventHook<UnitBlacksmithDropOffIronEventArgs> OnBlacksmithDropOffIron = new();
    /// <summary>Fired when a Blacksmith's production cycle finishes and a mace or sword is produced.</summary>
    [LuaApiExport("OnBlacksmithProduce")]
    public static readonly R3EventHook<UnitBlacksmithProduceEventArgs> OnBlacksmithProduce = new();
    /// <summary>Fired when a Blacksmith drops off a finished mace or sword at the armoury.</summary>
    [LuaApiExport("OnBlacksmithDropOffProduce")]
    public static readonly R3EventHook<UnitBlacksmithDropOffProduceEventArgs> OnBlacksmithDropOffProduce = new();

    /// <summary>Fired when a Tanner stores cow hides at their workshop.</summary>
    [LuaApiExport("OnTannerStoreCowHides")]
    public static readonly R3EventHook<UnitTannerStoreCowHidesEventArgs> OnTannerStoreCowHides = new();
    /// <summary>Fired when a Tanner's production cycle finishes and leather armour is produced.</summary>
    [LuaApiExport("OnTannerProduce")]
    public static readonly R3EventHook<UnitTannerProduceEventArgs> OnTannerProduce = new();
    /// <summary>Fired when a Tanner drops off finished leather armour at the armoury.</summary>
    [LuaApiExport("OnTannerDropOffCowHides")]
    public static readonly R3EventHook<UnitTannerDropOffCowHidesEventArgs> OnTannerDropOffCowHides = new();

    /// <summary>Fired when an Armourer picks up iron from the stockpile.</summary>
    [LuaApiExport("OnArmourerPickUpIron")]
    public static readonly R3EventHook<UnitArmourerPickUpIronEventArgs> OnArmourerPickUpIron = new();
    /// <summary>Fired when an Armourer drops off iron at their workshop.</summary>
    [LuaApiExport("OnArmourerDropOffIron")]
    public static readonly R3EventHook<UnitArmourerDropOffIronEventArgs> OnArmourerDropOffIron = new();
    /// <summary>Fired when an Armourer's production cycle finishes and metal armour is produced.</summary>
    [LuaApiExport("OnArmourerProduce")]
    public static readonly R3EventHook<UnitArmourerProduceEventArgs> OnArmourerProduce = new();
    /// <summary>Fired when an Armourer picks up finished metal armour from their workshop.</summary>
    [LuaApiExport("OnArmourerPickUpProduce")]
    public static readonly R3EventHook<UnitArmourerPickUpProduceEventArgs> OnArmourerPickUpProduce = new();
    /// <summary>Fired when an Armourer drops off finished metal armour at the armoury.</summary>
    [LuaApiExport("OnArmourerDropOffProduce")]
    public static readonly R3EventHook<UnitArmourerDropOffProduceEventArgs> OnArmourerDropOffProduce = new();
    /// <summary>Fired when an Armourer stores finished metal armour at their workshop.</summary>
    [LuaApiExport("OnArmourerStoreProduce")]
    public static readonly R3EventHook<UnitArmourerStoreProduceEventArgs> OnArmourerStoreProduce = new();

    /// <summary>Fired when a Hunter picks up meat from a slain animal.</summary>
    [LuaApiExport("OnHunterPickUpMeat")]
    public static readonly R3EventHook<UnitHunterPickUpMeatEventArgs> OnHunterPickUpMeat = new();
    /// <summary>Fired when a Hunter drops off meat at the granary.</summary>
    [LuaApiExport("OnHunterDropOffMeat")]
    public static readonly R3EventHook<UnitHunterDropOffMeatEventArgs> OnHunterDropOffMeat = new();

    /// <summary>Fired when a Quarry Grunt picks up stone from their quarry.</summary>
    [LuaApiExport("OnQuarryGruntPickUpStone")]
    public static readonly R3EventHook<UnitQuarryGruntPickUpStoneEventArgs> OnQuarryGruntPickUpStone = new();
    /// <summary>Fired when a Quarry Grunt drops off stone at the stockpile.</summary>
    [LuaApiExport("OnQuarryGruntDropOffStone")]
    public static readonly R3EventHook<UnitQuarryGruntDropOffStoneEventArgs> OnQuarryGruntDropOffStone = new();

    /// <summary>Fired when a Quarry Ox begins its trip from the quarry to the stockpile.</summary>
    [LuaApiExport("OnQuarryOxDepart")]
    public static readonly R3EventHook<UnitQuarryOxDepartEventArgs> OnQuarryOxDepart = new();
    /// <summary>Fired when a Quarry Ox drops off stone at the stockpile.</summary>
    [LuaApiExport("OnQuarryOxDropOffStone")]
    public static readonly R3EventHook<UnitQuarryOxDropOffStoneEventArgs> OnQuarryOxDropOffStone = new();

    /// <summary>Fired when a Miner picks up iron from an iron mine.</summary>
    [LuaApiExport("OnMiner2PickUpIron")]
    public static readonly R3EventHook<UnitMiner2PickUpIronEventArgs> OnMiner2PickUpIron = new();
    /// <summary>Fired when a Miner drops off iron at the stockpile.</summary>
    [LuaApiExport("OnMiner2DropOffIron")]
    public static readonly R3EventHook<UnitMiner2DropOffIronEventArgs> OnMiner2DropOffIron = new();

    /// <summary>Fired when a Pitch Digger picks up raw pitch from a pitch rig.</summary>
    [LuaApiExport("OnPitcherPickUpRawPitch")]
    public static readonly R3EventHook<UnitPitcherPickUpRawPitchEventArgs> OnPitcherPickUpRawPitch = new();
    /// <summary>Fired when a Pitch Digger drops off raw pitch at the stockpile.</summary>
    [LuaApiExport("OnPitcherDropOffRawPitch")]
    public static readonly R3EventHook<UnitPitcherDropOffRawPitchEventArgs> OnPitcherDropOffRawPitch = new();

    /// <summary>
    /// Fired when the game calculates a worker's bonus resource yield (e.g., from inn coverage).
    /// </summary>
    [LuaApiExport("OnCalculateBonusYield")]
    public static readonly R3EventHook<UnitCalculateBonusYieldEventArgs> OnCalculateBonusYield = new();

    /// <summary>
    /// Fired when a unit enters a tile where a killing pit is placed.
    /// </summary>
    [LuaApiExport("OnUnitEnterKillingPit")]
    public static readonly R3EventHook<UnitEnterKillingPitEventArgs> OnUnitEnterKillingPit = new();

    /// <summary>
    /// Fired when a hunter starts to query for valid targets
    /// </summary>
    [LuaApiExport("OnUnitHunterQueryTarget")]
    public static readonly R3EventHook<UnitHunterQueryTargetEventArgs> OnUnitHunterQueryTarget = new();

    /// <summary>
    /// Fired when a unit changes its AI State
    /// </summary>
    [LuaApiExport("OnUnitAIStateChange")]
    public static readonly R3EventHook<UnitAIStateEventArgs> OnUnitAIStateChange = new();

    /// <summary>
    /// Fired when a unit (e.g.: a peasant) transforms into a new unit, such as a soldier of any kind, or a worker unit.
    /// </summary>
    [LuaApiExport("OnUnitTransition")]
    public static readonly R3EventHook<UnitTransitionEventArgs> OnUnitTransition = new();
}