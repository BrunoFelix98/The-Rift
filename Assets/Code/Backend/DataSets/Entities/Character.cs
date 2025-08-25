using System.Collections.Generic;

public class Character : Entity
{
    public int Health { get; private set; }
    public List<Equipment> EquipmentSlots { get; private set; } = new List<Equipment>();

    public Character(string name, int health) : base(name, EntityType.LIVING)
    {
        Health = health;
    }
}

public class PlayerCharacter : Character
{
    public PlayerCharacter(string name, int health = 100) : base(name, health) { }
}

public class NPCCharacter : Character
{
    public NPCCharacter(string name, int health = 100) : base(name, health) { }
}
