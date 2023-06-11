namespace ARC.Client.Entity;
public class Character
{
    public uint Id { get; private set; }
    public string Name { get; private set; }
    public uint DeleteTime { get; private set; }

    public Character(uint id, string name, uint deleteTime) {
        Id = id;
        Name = name;
        DeleteTime = deleteTime;
    }
}
