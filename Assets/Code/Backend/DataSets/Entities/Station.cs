using System;
using System.Collections.Generic;

public class Station : ArtificialEntity
{
    public string Allegiance { get; private set; }
    public float Shield { get; private set; }
    public ResistanceProfile ShieldResistances { get; private set; }
    public float Armor { get; private set; }
    public ResistanceProfile ArmorResistances { get; private set; }
    public bool IsAlive => Armor > 0;

    public event Action<Station> OnDestroyed;

    private Dictionary<SlotType, List<Module>> modulesBySlot = new Dictionary<SlotType, List<Module>>();
    public IReadOnlyDictionary<SlotType, List<Module>> ModulesBySlot => modulesBySlot;

    public Station(string name, string allegiance, string builtBy, float shield, ResistanceProfile shieldResist, float armor, ResistanceProfile armorResist, BlueprintSO blueprint = null) : base(name, builtBy, blueprint)
    {
        Allegiance = allegiance;
        Shield = shield;
        ShieldResistances = shieldResist ?? new ResistanceProfile();
        Armor = armor;
        ArmorResistances = armorResist ?? new ResistanceProfile();
        modulesBySlot[SlotType.Structure] = new List<Module>();
    }

    public void AddModuleToSlot(Module module, SlotType slot)
    {
        if (!modulesBySlot.ContainsKey(slot))
            throw new InvalidOperationException($"Slot type {slot} is not valid for this station.");
        if (!module.CompatibleSlots.Contains(slot))
            throw new InvalidOperationException($"Module {module.EntityName} cannot be placed in slot {slot}.");
        modulesBySlot[slot].Add(module);
    }

    public void TakeDamage(float damage, DamageType type)
    {
        float shieldResist = ShieldResistances.GetResistance(type) / 100f;
        float armorResist = ArmorResistances.GetResistance(type) / 100f;

        if (Shield > 0)
        {
            float effectiveDamage = damage * (1f - shieldResist);
            Shield -= effectiveDamage;
            if (Shield < 0)
            {
                damage = -Shield;
                Shield = 0;
                Armor -= damage * (1f - armorResist);
            }
        }
        else
        {
            Armor -= damage * (1f - armorResist);
        }

        if (!IsAlive)
        {
            OnDestroyed?.Invoke(this);
        }
    }
}
