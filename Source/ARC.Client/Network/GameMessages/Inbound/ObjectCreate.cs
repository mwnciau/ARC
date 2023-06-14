using ACE.Common.Extensions;
using ACE.DatLoader.Entity;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages;
using ARC.Client.Extensions;
using Position = ARC.Client.Entity.Position;
using InboundMessage = ACE.Server.Network.ClientMessage;
using ACE.Server.Network.Structure;
using System.Numerics;
using ACE.Server.Network.Sequence;
using ACE.Server.WorldObjects;
using Google.Protobuf.WellKnownTypes;
using WorldObject = ARC.Client.Entity.WorldObject.WorldObject;
using ACE.Server.Entity;
using ARC.Client.Entity.WorldObject;
using SubPalette = ACE.Entity.SubPalette;

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

        int subPaletteCount = reader.ReadByte();
        int textureChanges = reader.ReadByte();
        int animPartChanges = reader.ReadByte();

        if (subPaletteCount > 0) {
            Object.Model.PaletteId = reader.ReadPackedDwordOfKnownType(0x4000000);
            Object.Model.SubPalettes = new();
        }
        for (int i = 0; i < subPaletteCount; i++) {
            Object.Model.SubPalettes.Add(new SubPalette {
                SubID = reader.ReadPackedDwordOfKnownType(0x4000000),
                Offset = reader.ReadByte(),
                NumColors = reader.ReadByte()
            });
        }

        if (textureChanges > 0) {
            Object.Model.TextureMapChanges = new();
        }
        for (int i = 0; i < textureChanges; i++) {
            Object.Model.TextureMapChanges.Add(new ACE.Entity.TextureMapChange {
                PartIndex = reader.ReadByte(),
                OldTexture = reader.ReadPackedDwordOfKnownType(0x5000000),
                NewTexture = reader.ReadPackedDwordOfKnownType(0x5000000),
            });
        }

        if (animPartChanges > 0) {
            Object.Model.AnimationPartChanges = new();
        }
        for (int i = 0; i < animPartChanges; i++) {
            Object.Model.AnimationPartChanges.Add(new ACE.Entity.AnimationPartChange {
                PartIndex = reader.ReadByte(),
                PartID = reader.ReadPackedDwordOfKnownType(0x1000000),
            });
        }

        reader.Align();
    }

    /// <see cref="ACE.Server.WorldObjects.WorldObject.SerializePhysicsData"/>
    public void deserializePhysicsData(BinaryReader reader)
    {
        var physicsDescriptionFlag = (PhysicsDescriptionFlag)reader.ReadUInt32();
        var physicsState = (PhysicsState)reader.ReadUInt32();

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Movement) != 0) {
            int movementDataLength = (int)reader.ReadUInt32();
            if (movementDataLength > 0) {
                byte[] movementData = reader.ReadBytes(movementDataLength);
                bool isAutonomous = Convert.ToBoolean(reader.ReadUInt32());
            }
        } else if ((physicsDescriptionFlag & PhysicsDescriptionFlag.AnimationFrame) != 0) {
            var placement = (Placement)reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Position) != 0) {
            var position = Position.Deserialize(reader);
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.MTable) != 0) {
            uint motionTableId = reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.STable) != 0) {
            uint soundTableId = reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.PeTable) != 0) {
            uint physicsTableId = reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.CSetup) != 0) {
            uint setupTableId = reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Parent) != 0) {
            uint wielderId = reader.ReadUInt32();
            var parentLocation = (ParentLocation)reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Children) != 0) {
            int childrenCount = reader.ReadInt32();
            for (int i = 0; i < childrenCount; i++) {
                uint childGuid = reader.ReadUInt32();
                int locationId = reader.ReadInt32();
            }
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.ObjScale) != 0) {
            float objScale = reader.ReadSingle();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Friction) != 0) {
            float friction = reader.ReadSingle();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Elasticity) != 0) {
            float elasticity = reader.ReadSingle();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Translucency) != 0) {
            float translucency = reader.ReadSingle();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Acceleration) != 0) {
            Vector3 velocity = reader.ReadVector3();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Velocity) != 0) {
            Vector3 acceleration = reader.ReadVector3();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.Omega) != 0) {
            Vector3 omega = reader.ReadVector3();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.DefaultScript) != 0) {
            uint defaultScriptId = reader.ReadUInt32();
        }

        if ((physicsDescriptionFlag & PhysicsDescriptionFlag.DefaultScriptIntensity) != 0) {
            float defaultScriptIntensity = reader.ReadSingle();
        }
        
        ushort objectPosition = BitConverter.ToUInt16(reader.ReadBytes(2));
        ushort objectMovement = BitConverter.ToUInt16(reader.ReadBytes(2));
        ushort objectState = BitConverter.ToUInt16(reader.ReadBytes(2));
        ushort objectVector = BitConverter.ToUInt16(reader.ReadBytes(2));
        ushort objectTeleport = BitConverter.ToUInt16(reader.ReadBytes(2));
        ushort objectServerControl = BitConverter.ToUInt16(reader.ReadBytes(2));
        ushort objectForcePosition = BitConverter.ToUInt16(reader.ReadBytes(2));
        ushort objectVisualDesc = BitConverter.ToUInt16(reader.ReadBytes(2));
        ushort objectInstance = BitConverter.ToUInt16(reader.ReadBytes(2));

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
        return $@"

        <<< GameMessage: CreateObject [0x{(int)Opcode:X4}:{Opcode}]
            Guid:      {Object.Guid}

        ";
    }
}
