using System.Collections.Generic;

public class Faction : Entity
{
    public List<Character> Members { get; private set; } = new List<Character>();

    public Faction(string name) : base(name, EntityType.ARTIFICIAL) { }
}
