using ARC.Client.Extensions;
using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages;
using ACE.Server.Network.Structure;
using AnimationPartChange = ACE.Entity.AnimationPartChange;
using ARC.Client.Entity.WorldObject;
using InboundMessage = ACE.Server.Network.ClientMessage;
using SubPalette = ACE.Entity.SubPalette;
using TextureMapChange = ACE.Entity.TextureMapChange;
using WorldObject = ARC.Client.Entity.WorldObject.WorldObject;
using ACE.Server.Physics.Common;

namespace ARC.Client.Network.GameMessages.Inbound;

public class ObjectCreate : InboundGameMessage
{
    public static new GameMessageOpcode Opcode = GameMessageOpcode.ObjectCreate;

    public WorldObject Object { get; private set; }

    /// <see cref="ACE.Server.Network.GameMessages.Messages.GameMessageCreateObject"/>
    public override void Handle(InboundMessage message, Session session)
    {
        var reader = new BinaryReader(message.Data);

        Object = new WorldObject();
        Object.Guid = reader.ReadGuid();

        // In the writer, there is a check (bool gameDataOnly) to see if these fields should be
        // written. gameDataOnly is false only for the GameEventType.ApproachVendor
        // GameEventMessage so here they should be set.
        deserializeModelData(reader);
        deserializePhysicsData(reader);
        deserializeWeenieData(reader);
    }

    /// <see cref="ACE.Server.WorldObjects.WorldObject.SerializeModelData"/>
    public void deserializeModelData(BinaryReader reader)
    {
        Object.Model = new WorldObjectModel();
        // Always set to 0x11
        reader.Skip(1);

        Object.Model.SubPaletteCount = reader.ReadByte();
        Object.Model.TextureMapChangeCount = reader.ReadByte();
        Object.Model.AnimationPartChangeCount = reader.ReadByte();

        if (Object.Model.SubPaletteCount > 0) {
            Object.Model.PaletteId = reader.ReadPackedDwordOfKnownType(0x4000000);
        }
        Object.Model.SubPalettes = new SubPalette[Object.Model.SubPaletteCount];
        for (int i = 0; i < Object.Model.SubPaletteCount; i++) {
            Object.Model.SubPalettes[i] = new SubPalette {
                SubID = reader.ReadPackedDwordOfKnownType(0x4000000),
                Offset = reader.ReadByte(),
                NumColors = reader.ReadByte()
            };
        }

        Object.Model.TextureMapChanges = new TextureMapChange[Object.Model.TextureMapChangeCount];
        for (int i = 0; i < Object.Model.TextureMapChangeCount; i++) {
            Object.Model.TextureMapChanges[i] = new TextureMapChange {
                PartIndex = reader.ReadByte(),
                OldTexture = reader.ReadPackedDwordOfKnownType(0x5000000),
                NewTexture = reader.ReadPackedDwordOfKnownType(0x5000000),
            };
        }

        Object.Model.AnimationPartChanges = new AnimationPartChange[Object.Model.AnimationPartChangeCount];
        for (int i = 0; i < Object.Model.AnimationPartChangeCount; i++) {
            Object.Model.AnimationPartChanges[i] = new AnimationPartChange {
                PartIndex = reader.ReadByte(),
                PartID = reader.ReadPackedDwordOfKnownType(0x1000000),
            };
        }

        reader.Align();
    }

    /// <see cref="ACE.Server.WorldObjects.WorldObject.SerializePhysicsData"/>
    public void deserializePhysicsData(BinaryReader reader)
    {
        Object.Physics = new();
        var physicsDescriptionFlag = (PhysicsDescriptionFlag)reader.ReadUInt32();
        Object.Physics.PhysicsState = (PhysicsState)reader.ReadUInt32();

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Movement) != 0) {
            Object.Physics.MovementDataLength = (int)reader.ReadUInt32();
            if (Object.Physics.MovementDataLength > 0) {
                Object.Physics.MovementData = reader.ReadBytes((int)Object.Physics.MovementDataLength);
                Object.Physics.IsAutonomous = Convert.ToBoolean(reader.ReadUInt32());
            }
        } else if ((physicsDescriptionFlag & PhysicsDescriptionFlag.AnimationFrame) != 0) {
            Object.Physics.Placement = (Placement)reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Position) != 0) {
            Object.Physics.Position = new ACE.Entity.Position(reader);
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.MTable) != 0) {
            Object.Physics.MotionTableId = reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.STable) != 0) {
            Object.Physics.SoundTableId = reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.PeTable) != 0) {
            Object.Physics.PhysicsTableId = reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.CSetup) != 0) {
            Object.Physics.SetupTableId = reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Parent) != 0) {
            Object.Physics.WielderId = reader.ReadUInt32();
            Object.Physics.ParentLocation = (ParentLocation)reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Children) != 0) {
            Object.Physics.ChildrenCount = reader.ReadInt32();
            Object.Physics.Children = new WorldObjectChildren[(int)Object.Physics.ChildrenCount];
            for (int i = 0; i < Object.Physics.ChildrenCount; i++) {
                Object.Physics.Children[i] = new WorldObjectChildren() {
                    Guid = reader.ReadUInt32(),
                    LocationId = reader.ReadInt32(),
                };
            }
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.ObjScale) != 0) {
            Object.Physics.Scale = reader.ReadSingle();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Friction) != 0) {
            Object.Physics.Friction = reader.ReadSingle();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Elasticity) != 0) {
            Object.Physics.Elasticity = reader.ReadSingle();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Translucency) != 0) {
            Object.Physics.Translucency = reader.ReadSingle();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Acceleration) != 0) {
            Object.Physics.Velocity = reader.ReadVector3();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Velocity) != 0) {
            Object.Physics.Acceleration = reader.ReadVector3();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Omega) != 0) {
            Object.Physics.Omega = reader.ReadVector3();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.DefaultScript) != 0) {
            Object.Physics.DefaultScriptId = reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.DefaultScriptIntensity) != 0) {
            Object.Physics.DefaultScriptIntensity = reader.ReadSingle();
        }
        
        Object.Physics.ObjectPosition = BitConverter.ToUInt16(reader.ReadBytes(2));
        Object.Physics.ObjectMovement = BitConverter.ToUInt16(reader.ReadBytes(2));
        Object.Physics.ObjectState = BitConverter.ToUInt16(reader.ReadBytes(2));
        Object.Physics.ObjectVector = BitConverter.ToUInt16(reader.ReadBytes(2));
        Object.Physics.ObjectTeleport = BitConverter.ToUInt16(reader.ReadBytes(2));
        Object.Physics.ObjectServerControl = BitConverter.ToUInt16(reader.ReadBytes(2));
        Object.Physics.ObjectForcePosition = BitConverter.ToUInt16(reader.ReadBytes(2));
        Object.Physics.ObjectVisualDesc = BitConverter.ToUInt16(reader.ReadBytes(2));
        Object.Physics.ObjectInstance = BitConverter.ToUInt16(reader.ReadBytes(2));

        reader.Align();
    }

    public void deserializeWeenieData(BinaryReader reader)
    {
        var weenieFlags = (WeenieHeaderFlag)reader.ReadUInt32();
        WeenieHeaderFlag2 weenieFlags2 = WeenieHeaderFlag2.None;
        Object.Name = reader.ReadString16L();
        Object.ClassId = reader.ReadPackedDword();
        Object.IconId = reader.ReadPackedDwordOfKnownType(0x6000000);
        Object.ItemType = (ItemType)reader.ReadUInt32();
        var objectDescriptionFlags = (ObjectDescriptionFlag)reader.ReadUInt32();
        reader.Align();

        if ((objectDescriptionFlags & ObjectDescriptionFlag.IncludesSecondHeader) != 0) {
            weenieFlags2 = (WeenieHeaderFlag2)reader.ReadUInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.PluralName) != 0) {
            Object.PluralName = reader.ReadString16L();
        }

        if ((weenieFlags & WeenieHeaderFlag.ItemsCapacity) != 0) {
            Object.ItemCapacity = reader.ReadByte();
        }

        if ((weenieFlags & WeenieHeaderFlag.ContainersCapacity) != 0) {
            Object.ContainerCapacity = reader.ReadByte();
        }

        if ((weenieFlags & WeenieHeaderFlag.AmmoType) != 0) {
            Object.AmmoType = (AmmoType)reader.ReadUInt16();
        }

        if ((weenieFlags & WeenieHeaderFlag.Value) != 0) {
            Object.Value = reader.ReadInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.Usable) != 0) {
            Object.Usable = (Usable)reader.ReadUInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.UseRadius) != 0) {
            Object.UseRadius = reader.ReadSingle();
        }

        if ((weenieFlags & WeenieHeaderFlag.TargetType) != 0) {
            Object.TargetType = (ItemType)reader.ReadUInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.UiEffects) != 0) {
            Object.UiEffects = (UiEffects)reader.ReadUInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.CombatUse) != 0) {
            Object.CombatUse = (CombatUse)reader.ReadSByte();
        }

        if ((weenieFlags & WeenieHeaderFlag.Structure) != 0) {
            Object.Structure = reader.ReadUInt16();
        }

        if ((weenieFlags & WeenieHeaderFlag.MaxStructure) != 0) {
            Object.MaxStructure = reader.ReadUInt16();
        }

        if ((weenieFlags & WeenieHeaderFlag.StackSize) != 0) {
            Object.StackSize = reader.ReadUInt16();
        }

        if ((weenieFlags & WeenieHeaderFlag.MaxStackSize) != 0) {
            Object.MaxStackSize = reader.ReadUInt16();
        }

        if ((weenieFlags & WeenieHeaderFlag.Container) != 0) {
            Object.ContainerId = reader.ReadUInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.Wielder) != 0) {
            Object.WielderId = reader.ReadUInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.ValidLocations) != 0) {
            Object.ValidLocations = (EquipMask)reader.ReadUInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.CurrentlyWieldedLocation) != 0) {
            Object.CurrentWieldedLocation = (EquipMask)reader.ReadUInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.Priority) != 0) {
            Object.ClothingPriority = (CoverageMask)reader.ReadUInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.RadarBlipColor) != 0) {
            Object.adarColor = (RadarColor)reader.ReadByte();
        }

        if ((weenieFlags & WeenieHeaderFlag.RadarBehavior) != 0) {
            Object.adarBehavior = (RadarBehavior)reader.ReadByte();
        }

        if ((weenieFlags & WeenieHeaderFlag.PScript) != 0) {
            Object.PScript = reader.ReadUInt16();
        }

        if ((weenieFlags & WeenieHeaderFlag.Workmanship) != 0) {
            Object.Workmanship = reader.ReadSingle();
        }

        if ((weenieFlags & WeenieHeaderFlag.Burden) != 0) {
            Object.Burden = reader.ReadUInt16();
        }

        if ((weenieFlags & WeenieHeaderFlag.Spell) != 0) {
            Object.SpellDID = reader.ReadUInt16();
        }

        if ((weenieFlags & WeenieHeaderFlag.HouseOwner) != 0) {
            Object.HouseOwner = reader.ReadUInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.HouseRestrictions) != 0) {
            Object.HouseRestrictions = reader.ReadRestrictionDB();
        }

        if ((weenieFlags & WeenieHeaderFlag.HookItemTypes) != 0) {
            Object.HookItemType = (int)reader.ReadUInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.Monarch) != 0) {
            Object.MonarchId = reader.ReadUInt32();
        }

        if ((weenieFlags & WeenieHeaderFlag.HookType) != 0) {
            Object.HookType = reader.ReadUInt16();
        }

        if ((weenieFlags & WeenieHeaderFlag.IconOverlay) != 0) {
            Object.IconOverlayId = reader.ReadPackedDwordOfKnownType(0x6000000);
        }

        if ((weenieFlags2 & WeenieHeaderFlag2.IconUnderlay) != 0) {
            Object.IconUnderlayId = reader.ReadPackedDwordOfKnownType(0x6000000);
        }

        if ((weenieFlags & WeenieHeaderFlag.MaterialType) != 0) {
            Object.MaterialType = (MaterialType)reader.ReadUInt32();
        }

        if ((weenieFlags2 & WeenieHeaderFlag2.Cooldown) != 0) {
            Object.CooldownId = reader.ReadInt32();
        }

        if ((weenieFlags2 & WeenieHeaderFlag2.CooldownDuration) != 0) {
            Object.CooldownDuration = reader.ReadDouble();
        }

        if ((weenieFlags2 & WeenieHeaderFlag2.PetOwner) != 0) {
            Object.PetOwner = reader.ReadUInt32();
        }

        reader.Align();
    }

    public override string ToString()
    {
        string location = "Unknown location";

        if (Object.Physics.Position != null) {
            location = $"At coordinates {Object.Physics.Position.ToString()}";
        } else if (Object.WielderId != null) {
            location = $"Wielded by {Object.WielderId}";
        } else if (Object.ContainerId != null) {
            location = $"In container {Object.ContainerId}";
        }

        return $@"

        <<< GameMessage: CreateObject [0x{(int)Opcode:X4}:{Opcode}]
            Guid:      {Object.Guid}
            Name:      {Object.Name}
            Location:  {location}
        ";
    }
}
