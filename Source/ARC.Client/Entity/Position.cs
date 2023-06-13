using ACE.Entity;

namespace ARC.Client.Entity;
public class Position
{
    public static Position Deserialize(BinaryReader reader)
    {
        Position position = new Position();

        // Todo: convert to LandblockId object
        uint landblockId = reader.ReadUInt32();

        float positionX = reader.ReadSingle();
        float positionY = reader.ReadSingle();
        float positionZ = reader.ReadSingle();

        float RotationW = reader.ReadSingle();
        float RotationX = reader.ReadSingle();
        float RotationY = reader.ReadSingle();
        float RotationZ = reader.ReadSingle();

        return position;
    }
}
