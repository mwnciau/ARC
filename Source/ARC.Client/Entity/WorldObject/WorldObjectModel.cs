using ACE.Entity;

namespace ARC.Client.Entity.WorldObject;

public class WorldObjectModel
{
    public uint PaletteId;
    public int SubPaletteCount;
    public SubPalette[] SubPalettes;
    public int TextureMapChangeCount;
    public TextureMapChange[] TextureMapChanges;
    public int AnimationPartChangeCount;
    public AnimationPartChange[] AnimationPartChanges;
}
