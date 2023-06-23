using ACE.Common.Extensions;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Network.Structure;
using Enchantment = ARC.Client.Entity.WorldObject.Enchantment;

namespace ARC.Client.Extensions;

public static class BinaryReaderExtensions
{
    public static RestrictionDB ReadRestrictionDB(this BinaryReader reader)
    {
        var restrictionDB = new RestrictionDB();

        restrictionDB.Version = reader.ReadUInt32();
        restrictionDB.OpenStatus = Convert.ToBoolean(reader.ReadUInt32());
        restrictionDB.MonarchID = new ObjectGuid(reader.ReadUInt32());
        restrictionDB.Table = reader.ReadObjectGuidDictionary();

        return restrictionDB;
    }

    public static Dictionary<ObjectGuid,uint> ReadObjectGuidDictionary(this BinaryReader reader)
    {
        var dictionary = new Dictionary<ObjectGuid, uint>();

        int dictionaryLength = reader.ReadUInt16();

        // Contains the number of buckets, a currently hard-coded and unused variable in ACE
        reader.Skip(2);

        for (int i = 0; i < dictionaryLength; i++) {
            dictionary.Add(
                new ObjectGuid(reader.ReadUInt32()),
                reader.ReadUInt32()
            );
        }

        return dictionary;
    }

        public static uint ReadPackedDword(this BinaryReader reader)
    {
        uint dword = BitConverter.ToUInt16(reader.ReadBytes(2));

        if (dword > 32767) {
            // This was packed as 4 bytes
            uint secondPart = BitConverter.ToUInt16(reader.ReadBytes(2));

            dword = ((dword ^ 0x8000) << 16) | secondPart;
        }

        return dword;
    }

    public static uint ReadPackedDwordOfKnownType(this BinaryReader reader, uint type)
    {
        uint dword = reader.ReadPackedDword();

        return dword + type;
    }

    public static Enchantment ReadEnchantment(this BinaryReader reader)
    {
        var enchantment = new Enchantment();

        enchantment.SpellId = reader.ReadUInt16();
        enchantment.Layer = reader.ReadUInt16();
        enchantment.SpellCategory = reader.ReadUInt16();
        ushort hasSpellSetId = reader.ReadUInt16();
        enchantment.PowerLevel = reader.ReadUInt32();
        enchantment.StartTime = reader.ReadDouble();
        enchantment.Duration = reader.ReadDouble();
        enchantment.CasterGuid = reader.ReadUInt32();
        enchantment.DegradeModifier = reader.ReadSingle();
        enchantment.DegradeLimit = reader.ReadSingle();
        enchantment.LastTimeDegraded = reader.ReadDouble();
        enchantment.StatModType = (EnchantmentTypeFlags)reader.ReadUInt32();
        enchantment.StatModKey = reader.ReadUInt32();
        enchantment.StatModValue = reader.ReadSingle();
        if (hasSpellSetId != 0) {
            enchantment.SpellSetId = reader.ReadUInt32();
        }

        return enchantment;
    }

    public static Position ReadPosition(this BinaryReader reader)
    {
        uint landblockId = reader.ReadUInt32();

        float positionX = reader.ReadSingle();
        float positionY = reader.ReadSingle();
        float positionZ = reader.ReadSingle();

        float RotationW = reader.ReadSingle();
        float RotationX = reader.ReadSingle();
        float RotationY = reader.ReadSingle();
        float RotationZ = reader.ReadSingle();


        return new Position(
            landblockId,
            positionX,
            positionY,
            positionZ,
            RotationX,
            RotationY,
            RotationZ,
            RotationW
        );
    }
}
