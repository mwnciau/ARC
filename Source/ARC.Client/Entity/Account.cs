namespace ARC.Client.Entity;
public class Account
{
    public string Name { get; private set; }
    private List<Character> characters;
    public uint CharacterSlots { get; private set; }

    public Account(string name, List<Character> characters, uint characterSlots) {
        Name = name;
        this.characters = characters;
        CharacterSlots = characterSlots;
    }
}
