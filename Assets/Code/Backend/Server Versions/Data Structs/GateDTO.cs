using System;

[Serializable]
public class GateDTO
{
    public string gateName;
    public string connectedSystemId; // Store connected System identifier instead of reference
}