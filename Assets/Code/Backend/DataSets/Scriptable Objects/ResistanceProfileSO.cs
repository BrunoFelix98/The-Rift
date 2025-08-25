using UnityEngine;

[CreateAssetMenu(fileName = "NewResistanceProfile", menuName = "Game Data/Resistance Profile")]
public class ResistanceProfileSO : ScriptableObject
{
    public float em;
    public float heat;
    public float kinetic;
    public float explosive;

    public float GetResistance(DamageType type)
    {
        return type switch
        {
            DamageType.EM => em,
            DamageType.HEAT => heat,
            DamageType.KINETIC => kinetic,
            DamageType.EXPLOSIVE => explosive,
            _ => 0f
        };
    }
}
