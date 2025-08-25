using System.Collections.Generic;

public abstract class Module : Entity
{
    public List<SlotType> CompatibleSlots { get; private set; }
    public BlueprintSO Blueprint { get; private set; }

    protected Module(string name, List<SlotType> compatibleSlots, BlueprintSO blueprint = null)
        : base(name, EntityType.EQUIPMENT)
    {
        CompatibleSlots = compatibleSlots;
        Blueprint = blueprint;
    }
}
