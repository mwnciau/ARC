using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Network.Structure;

namespace ARC.Client.Entity.WorldObject;
public class WorldObject
{
    public ObjectGuid Guid;
    public string Name;
    public uint ClassId;
    public uint IconId;
    public ItemType ItemType;

    public WorldObjectModel? Model;
    public WorldObjectPhysics? Physics;

    public string? PluralName;
    public int? ItemCapacity;
    public int? ContainerCapacity;
    public AmmoType? AmmoType;
    public int? Value;
    public Usable? Usable;
    public float? UseRadius;
    public ItemType? TargetType;
    public UiEffects? UiEffects;
    public CombatUse? CombatUse;
    public ushort? Structure;
    public ushort? MaxStructure;
    public ushort? StackSize;
    public ushort? MaxStackSize;
    public uint? ContainerId;
    public uint? WielderId;
    public EquipMask? ValidLocations;
    public EquipMask? CurrentWieldedLocation;
    public CoverageMask? ClothingPriority;
    public RadarColor? adarColor;
    public RadarBehavior? adarBehavior;
    public ushort? PScript;
    public float? Workmanship;
    public ushort? Burden;
    public ushort? SpellDID;
    public uint? HouseOwner;
    public RestrictionDB? HouseRestrictions;
    public int? HookItemType;
    public uint? MonarchId;
    public ushort? HookType;
    public uint? IconOverlayId;
    public uint? IconUnderlayId;
    public MaterialType? MaterialType;
    public int? CooldownId;
    public double? CooldownDuration;
    public uint? PetOwner;
}
