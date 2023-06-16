using ACE.Entity;

namespace ARC.Client.Extensions;
public class PositionExtension
{
    public static Position Deserialize(BinaryReader reader)
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
