using ACE.Entity;

namespace ARC.Client.Entity.WorldObject;

public class WorldObjectModel
{
    public uint PaletteId;
    public List<SubPalette> SubPalettes;
    public List<TextureMapChange> TextureMapChanges;
    public List<AnimationPartChange> AnimationPartChanges;
}
