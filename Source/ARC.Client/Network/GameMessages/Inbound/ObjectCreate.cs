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

namespace ARC.Client.Network.GameMessages.Inbound;

public class ObjectCreate : InboundGameMessage
{
    public static new GameMessageOpcode Opcode = GameMessageOpcode.ObjectCreate;

    public ObjectGuid Guid{ get; private set; }


    /// <see cref="ACE.Server.Network.GameMessages.Messages.GameMessageCreateObject"/>
    public override void Handle(InboundMessage message, Session session)
    {
        var reader = new BinaryReader(message.Data);

        Guid = reader.ReadGuid();

        // In the writer, there is a check (bool gameDataOnly) to see if these fields should be
        // written. gameDataOnly is false only for the GameEventType.ApproachVendor
        // GameEventMessage so here they should be set.
        deserializeModelData(reader);
        deserializePhysicsData(reader);
        desrializeWeenieData(reader);
    }

    /// <see cref="ACE.Server.WorldObjects.WorldObject.SerializeModelData"/>
    public void deserializeModelData(BinaryReader reader)
    {
        // Always set to 0x11
        reader.Skip(1);

        int subPaletteCount = reader.ReadByte();
        int textureChanges = reader.ReadByte();
        int animPartChanges = reader.ReadByte();

        uint paletteId;
        if (subPaletteCount > 0) {
            paletteId = reader.ReadPackedDwordOfKnownType(0x4000000);
        }
        for (int i = 0; i < subPaletteCount; i++) {
            uint subPaletteId = reader.ReadPackedDwordOfKnownType(0x4000000);
            ushort subPaletteOffset = reader.ReadByte();
            ushort subPaletteLength = reader.ReadByte();
        }

        for (int i = 0; i < textureChanges; i++) {
            byte texturePartIndex = reader.ReadByte();
            uint oldTexture = reader.ReadPackedDwordOfKnownType(0x5000000);
            uint newTexture = reader.ReadPackedDwordOfKnownType(0x5000000);
        }

        for (int i = 0; i < animPartChanges; i++) {
            byte modelIndex = reader.ReadByte();
            uint animationId = reader.ReadPackedDwordOfKnownType(0x1000000);
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
        string name = reader.ReadString16L();
        uint classId = reader.ReadPackedDword();
        uint iconId = reader.ReadPackedDwordOfKnownType(0x6000000);
        var itemType = (ItemType)reader.ReadUInt32();
        var objectDescriptionFlags = (ObjectDescriptionFlag)reader.ReadUInt32();
        reader.Align();
    }

    public override string ToString()
    {
        return $@"

        <<< GameMessage: CreateObject [0x{(int)Opcode:X4}:{Opcode}]
            Guid:      {Guid}

        ";
    }
}
