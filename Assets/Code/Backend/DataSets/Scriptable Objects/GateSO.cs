using UnityEngine;

[CreateAssetMenu(fileName = "NewGate", menuName = "Game Data/Gate")]
public class GateSO : ScriptableObject
{
    public string gateName;

    // Reference to the connected system ScriptableObject
    public SystemSO connectedSystem;
}
