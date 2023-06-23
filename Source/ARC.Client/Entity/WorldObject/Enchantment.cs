using ACE.Entity.Enum;

namespace ARC.Client.Entity.WorldObject;

public class Enchantment
{
    public ushort SpellId;
    public ushort Layer;
    public ushort SpellCategory;
    public uint PowerLevel;
    public double StartTime;
    public double Duration;
    public uint CasterGuid;
    public float DegradeModifier;
    public float DegradeLimit;
    public double LastTimeDegraded;
    public EnchantmentTypeFlags StatModType;
    public uint StatModKey;
    public float StatModValue;
    public uint? SpellSetId;
}
