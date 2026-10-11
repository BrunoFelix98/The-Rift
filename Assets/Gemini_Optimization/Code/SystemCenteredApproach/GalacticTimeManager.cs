using Unity.Netcode;
using UnityEngine;

public class GalacticTimeManager : NetworkBehaviour
{
    public static GalacticTimeManager Instance { get; private set; }

    [SerializeField] private double timeMultiplier = 1.0;
    
    private NetworkVariable<double> serverGalacticTime = new NetworkVariable<double>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public double GalacticTime => serverGalacticTime.Value;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    private void Update()
    {
        if (IsServer)
        {
            serverGalacticTime.Value += Time.deltaTime * timeMultiplier;
        }
    }

    public void SetTimeMultiplier(double multiplier)
    {
        if (IsServer)
        {
            timeMultiplier = multiplier;
        }
    }
}
