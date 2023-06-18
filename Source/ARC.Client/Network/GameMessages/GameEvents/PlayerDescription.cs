using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameEvent;
using ACE.Common.Extensions;
using ARC.Client.Network.GameMessages.Inbound;

namespace ARC.Client.Network.GameMessages.GameEvents;
public class PlayerDescription : InboundGameEvent
{
    public static GameEventType GameEventType = GameEventType.PlayerDescription;

    /// <see cref="ACE.Server.Network.GameEvent.Events.GameEventPlayerDescription.DescriptionPropertyFlag"/>
    [Flags]
    private enum DescriptionPropertyFlag
    {
        None = 0x0000,
        PropertyInt32 = 0x0001,
        PropertyBool = 0x0002,
        PropertyDouble = 0x0004,
        PropertyDid = 0x0008,
        PropertyString = 0x0010,
        Position = 0x0020,
        PropertyIid = 0x0040,
        PropertyInt64 = 0x0080,
    }

    /// <see cref="ACE.Server.Network.GameEvent.Events.GameEventPlayerDescription.DescriptionVectorFlag"/>
    [Flags]
    private enum DescriptionVectorFlag
    {
        None = 0x0000,
        Attribute = 0x0001,
        Skill = 0x0002,
        Spell = 0x0100,
        Enchantment = 0x0200
    }

    private DescriptionPropertyFlag PropertyFlags;
    private BinaryReader Reader;

    /// <see cref="ACE.Server.Network.GameEvent.Events.GameEventPlayerDescription"/>
    public override void Handle(GameEvent gameEvent, Session session)
    {
        Reader = gameEvent.Reader;
        PropertyFlags = (DescriptionPropertyFlag)Reader.ReadUInt32();

        var weenieType = (WeenieType)Reader.ReadUInt32();
    }

    private void HandleDataDictionaries()
    {
        if ((PropertyFlags & DescriptionPropertyFlag.PropertyInt32) != 0) {
            ushort propertiesIntCount = Reader.ReadUInt16();
            ushort propertiesIntNumBuckets = Reader.ReadUInt16();

            Dictionary<PropertyInt, int> propertiesInt = new();
            for (int i = 0; i < propertiesIntCount; i++) {
                propertiesInt.Add(
                    (PropertyInt)Reader.ReadUInt32(),
                    Reader.ReadInt32()
                );
            }
        }

        if ((PropertyFlags & DescriptionPropertyFlag.PropertyInt32) != 0) {
            ushort propertiesInt64Count = Reader.ReadUInt16();
            ushort propertiesInt64NumBuckets = Reader.ReadUInt16();

            Dictionary<PropertyInt64, long> propertiesInt64 = new();
            for (int i = 0; i < propertiesInt64Count; i++) {
                propertiesInt64.Add(
                    (PropertyInt64)Reader.ReadUInt32(),
                    Reader.ReadInt64()
                );
            }
        }

        if ((PropertyFlags & DescriptionPropertyFlag.PropertyBool) != 0) {
            ushort propertiesBoolCount = Reader.ReadUInt16();
            ushort propertiesBoolNumBuckets = Reader.ReadUInt16();

            Dictionary<PropertyBool, bool> propertiesBool = new();
            for (int i = 0; i < propertiesBoolCount; i++) {
                propertiesBool.Add(
                    (PropertyBool)Reader.ReadUInt32(),
                    Convert.ToBoolean(Reader.ReadUInt32())
                );
            }
        }

        if ((PropertyFlags & DescriptionPropertyFlag.PropertyDouble) != 0) {
            ushort propertiesDoubleCount = Reader.ReadUInt16();
            ushort propertiesDoubleNumBuckets = Reader.ReadUInt16();

            Dictionary<PropertyFloat, double> propertiesDouble = new();
            for (int i = 0; i < propertiesDoubleCount; i++) {
                propertiesDouble.Add(
                    (PropertyFloat)Reader.ReadUInt32(),
                    Reader.ReadDouble()
                );
            }
        }

        if ((PropertyFlags & DescriptionPropertyFlag.PropertyString) != 0) {
            ushort propertiesStringCount = Reader.ReadUInt16();
            ushort propertiesStringNumBuckets = Reader.ReadUInt16();

            Dictionary<PropertyString, string> propertiesString = new();
            for (int i = 0; i < propertiesStringCount; i++) {
                propertiesString.Add(
                    (PropertyString)Reader.ReadUInt32(),
                    Reader.ReadString16L()
                );
            }
        }

        if ((PropertyFlags & DescriptionPropertyFlag.PropertyDid) != 0) {
            ushort propertiesDataIdCount = Reader.ReadUInt16();
            ushort propertiesDataIdNumBuckets = Reader.ReadUInt16();

            Dictionary<PropertyDataId, uint> propertiesDataId = new();
            for (int i = 0; i < propertiesDataIdCount; i++) {
                propertiesDataId.Add(
                    (PropertyDataId)Reader.ReadUInt32(),
                    Reader.ReadUInt32()
                );
            }
        }

        if ((PropertyFlags & DescriptionPropertyFlag.PropertyIid) != 0) {
            ushort propertiesInstanceIdCount = Reader.ReadUInt16();
            ushort propertiesInstanceIdNumBuckets = Reader.ReadUInt16();

            Dictionary<PropertyInstanceId, uint> propertiesInstanceId = new();
            for (int i = 0; i < propertiesInstanceIdCount; i++) {
                propertiesInstanceId.Add(
                    (PropertyInstanceId)Reader.ReadUInt32(),
                    Reader.ReadUInt32()
                );
            }
        }
    }
}
