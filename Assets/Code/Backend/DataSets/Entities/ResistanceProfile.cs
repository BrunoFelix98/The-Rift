public class ResistanceProfile
{
    public float EM { get; set; }
    public float Heat { get; set; }
    public float Kinetic { get; set; }
    public float Explosive { get; set; }

    public ResistanceProfile(float em = 0, float heat = 0, float kinetic = 0, float explosive = 0)
    {
        EM = em;
        Heat = heat;
        Kinetic = kinetic;
        Explosive = explosive;
    }

    // Looks up resistance by type
    public float GetResistance(DamageType type) => type switch
    {
        DamageType.EM => EM,
        DamageType.HEAT => Heat,
        DamageType.KINETIC => Kinetic,
        DamageType.EXPLOSIVE => Explosive,
        _ => 0
    };
}
