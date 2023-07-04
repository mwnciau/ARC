using ACE.Common.Extensions;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameEvent;
using ARC.Client.Network.GameMessages.Inbound;
using ARC.Client.Extensions;
using ARC.Client.Entity.WorldObject;

namespace ARC.Client.Network.GameMessages.GameEvents;
public class PlayerDescription : InboundGameEvent
{
    public static GameEventType GameEventType = GameEventType.PlayerDescription;

    public string Name { get; set; }

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

        ReadDataDictionaries();
        ReadPlayerData();
    }

    private void ReadDataDictionaries()
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

        if ((PropertyFlags & DescriptionPropertyFlag.PropertyInt64) != 0) {
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

            if (propertiesString.TryGetValue(PropertyString.Name, out string value)) {
                Name = value;
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

    private void ReadPlayerData()
    {
        if (PropertyFlags.HasFlag(DescriptionPropertyFlag.Position)) {
            ushort positionCount = Reader.ReadUInt16();
            ushort positionBuckets = Reader.ReadUInt16();

            // Should be Positionype.LastOutsideDeath
            var positionType = (PositionType)Reader.ReadUInt32();
            var lastOutsideDeath = new Position(Reader);
        }

        var vectorFlags = (DescriptionVectorFlag)Reader.ReadUInt32();

        uint currentHealth = Reader.ReadUInt32();

        if ((vectorFlags & DescriptionVectorFlag.Attribute) != 0) {
            ReadPlayerAttributes();
        }
        if ((vectorFlags & DescriptionVectorFlag.Skill) != 0) {
            ReadPlayerSkills();
        }
        if ((vectorFlags & DescriptionVectorFlag.Spell) != 0) {
            ReadPlayerKnownSpells();
        }
        if ((vectorFlags & DescriptionVectorFlag.Enchantment) != 0) {
            ReadPlayerEnchantments();
        }

        ReadCharacterOptions();
    }

    private void ReadPlayerAttributes()
    {
        var attributeFlags = (AttributeCache)Reader.ReadUInt32();

        if ((attributeFlags & AttributeCache.Strength) != 0) {
            uint strengthRanks = Reader.ReadUInt32();
            uint strengthBase = Reader.ReadUInt32();
            uint strengthExperience = Reader.ReadUInt32();
        }

        if ((attributeFlags & AttributeCache.Endurance) != 0) {
            uint enduranceRanks = Reader.ReadUInt32();
            uint enduranceBase = Reader.ReadUInt32();
            uint enduranceExperience = Reader.ReadUInt32();
        }

        if ((attributeFlags & AttributeCache.Quickness) != 0) {
            uint quicknessRanks = Reader.ReadUInt32();
            uint quicknessBase = Reader.ReadUInt32();
            uint quicknessExperience = Reader.ReadUInt32();
        }

        if ((attributeFlags & AttributeCache.Coordination) != 0) {
            uint coordinationRanks = Reader.ReadUInt32();
            uint coordinationBase = Reader.ReadUInt32();
            uint coordinationExperience = Reader.ReadUInt32();
        }

        if ((attributeFlags & AttributeCache.Focus) != 0) {
            uint focusRanks = Reader.ReadUInt32();
            uint focusBase = Reader.ReadUInt32();
            uint focusExperience = Reader.ReadUInt32();
        }

        if ((attributeFlags & AttributeCache.Self) != 0) {
            uint selfRanks = Reader.ReadUInt32();
            uint selfBase = Reader.ReadUInt32();
            uint selfExperience = Reader.ReadUInt32();
        }

        if ((attributeFlags & AttributeCache.Health) != 0) {
            uint healthRanks = Reader.ReadUInt32();
            uint healthBase = Reader.ReadUInt32();
            uint healthExperience = Reader.ReadUInt32();
            uint healthCurrent = Reader.ReadUInt32();
        }

        if ((attributeFlags & AttributeCache.Stamina) != 0) {
            uint staminaRanks = Reader.ReadUInt32();
            uint staminaBase = Reader.ReadUInt32();
            uint staminaExperience = Reader.ReadUInt32();
            uint staminaCurrent = Reader.ReadUInt32();
        }

        if ((attributeFlags & AttributeCache.Mana) != 0) {
            uint manaRanks = Reader.ReadUInt32();
            uint manaBase = Reader.ReadUInt32();
            uint manaExperience = Reader.ReadUInt32();
            uint manaCurrent = Reader.ReadUInt32();
        }
    }

    private void ReadPlayerSkills()
    {
        ushort skillCount = Reader.ReadUInt16();
        ushort skillBuckets = Reader.ReadUInt16();

        for (int i = 0; i < skillCount; i++) {
            var skillId = (Skill)Reader.ReadUInt32();
            ushort skillRanks = Reader.ReadUInt16();

            // Always set to 1u
            Reader.ReadUInt16();

            var advancementClass = (SkillAdvancementClass)Reader.ReadUInt32();
            uint skillExperience = Reader.ReadUInt32();
            uint skillBase = Reader.ReadUInt32();

            // "task difficulty, aka resistance_of_last_check". Always set to 0u.
            Reader.ReadUInt32();

            // "last_time_used". Always set to 0d.
            Reader.ReadDouble();
        }
    }

    private void ReadPlayerKnownSpells()
    {
        ushort spellCount = Reader.ReadUInt16();
        ushort spellBuckets = Reader.ReadUInt16();

        for (int i = 0; i < spellCount; i++) {
            int spellId = Reader.ReadInt32();
            // Use new spell configuration. Always set to 2.
            Reader.ReadSingle();
        }
    }

    /// <see cref="ACE.Server.Network.Structure.EnchantmentRegistryExtensions"/>
    private void ReadPlayerEnchantments()
    {
        var enchantmentMask = (EnchantmentMask)Reader.ReadUInt32();

        if (enchantmentMask.HasFlag(EnchantmentMask.Multiplicative)) {
            var multiplicativeEnchantments = ReadEnchantments();
        }
        if (enchantmentMask.HasFlag(EnchantmentMask.Additive)) {
            var additiveEnchantments = ReadEnchantments();
        }
        if (enchantmentMask.HasFlag(EnchantmentMask.Cooldown)) {
            var cooldownEnchantments = ReadEnchantments();
        }
        if (enchantmentMask.HasFlag(EnchantmentMask.Vitae)) {
            var vitaeEnchantments = ReadEnchantments();
        }
    }

    /// <see cref="ACE.Server.Network.Structure.EnchantmentExtensions"/>
    private List<Enchantment> ReadEnchantments()
    {
        int enchantmentCount = Reader.ReadInt32();
        var enchantments = new List<Enchantment>();

        for (int i = 0; i <  enchantmentCount; i++) {
            enchantments.Add(Reader.ReadEnchantment());
        }

        return enchantments;
    }

    private void ReadCharacterOptions()
    {
        var optionFlags = (CharacterOptionDataFlag)Reader.ReadUInt32();
        int characterOptions1 = Reader.ReadInt32();

        if (optionFlags.HasFlag(CharacterOptionDataFlag.Shortcut)) {
            int shortcutCount = Reader.ReadInt32();
            for (int i = 0; i < shortcutCount; i++) {
                uint index = Reader.ReadUInt32();
                uint objectId = Reader.ReadUInt32();
                ushort spellId = Reader.ReadUInt16();
                ushort spellLayer = Reader.ReadUInt16();
            }
        }

        if (optionFlags.HasFlag(CharacterOptionDataFlag.SpellLists8)) {
            var spellBars = new List<uint>[8];
            for (int spellBarIndex = 0; spellBarIndex < 8; spellBarIndex++) {
                spellBars[spellBarIndex] = new();
                int spellCount = Reader.ReadInt32();
                for (int i = 0; i < spellCount; i++) {
                    spellBars[spellBarIndex].Add(Reader.ReadUInt32());
                }
            }
        }

        if (optionFlags.HasFlag(CharacterOptionDataFlag.DesiredComps)) {
            ushort desiredCompsCount = Reader.ReadUInt16();
            ushort desiredCompsBuckets = Reader.ReadUInt16();

            for (int i = 0; i < desiredCompsCount; i++) {
                int spellCompId = Reader.ReadInt32();
                int rebuyAmount = Reader.ReadInt32();
            }
        }

        uint spellbookFilters = Reader.ReadUInt32();

        if (optionFlags.HasFlag(CharacterOptionDataFlag.CharacterOptions2)) {
            int characterOptions2 = Reader.ReadInt32();
        }

        if (optionFlags.HasFlag(CharacterOptionDataFlag.GameplayOptions)) {
            // 120 bytes is what the current ACClient/ACE uses
            byte[] gameplayOptions = Reader.ReadBytes(120);
        }

        uint inventoryCount = Reader.ReadUInt32();
        for (int i = 0; i <= inventoryCount; i++) {
            uint objectId = Reader.ReadUInt32();
            var containerType = (ContainerType)Reader.ReadUInt32();
        }

        uint equippedCount = Reader.ReadUInt32();
        for (int i = 0; i <= equippedCount; i++) {
            uint objectId = Reader.ReadUInt32();
            var wieldLocation = (EquipMask)Reader.ReadUInt32();
            var clothingPriority = (CoverageMask)Reader.ReadUInt32();
        }
    }
}
