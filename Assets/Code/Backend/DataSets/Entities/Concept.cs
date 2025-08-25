public class Concept : Entity
{
    public string ConceptName { get; set; }
    public Concept Allegiance { get; set; }     // For corporations: allegiance to alliance, for alliance: allegiance to leader
    public Living Leader { get; set; }          // CEO or leader if applicable

    public Concept(string name) : base(name, EntityType.CONCEPT) { }
}
