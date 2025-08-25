using System;
using System.Collections.Generic;

public class Ship : ArtificialEntity
{
    // Ownership and Builder info
    public string CurrentOwner { get; private set; }

    // Base stats
    public float ShieldHP { get; private set; }
    public float ArmorHP { get; private set; }
    public float Speed { get; private set; }
    public float Tonnage { get; private set; }
    public float CargoCapacity { get; private set; }
    public float LongAxisLength { get; private set; }
    public ShipWeightClass WeightClass { get; private set; }

    public ResistanceProfile ShieldResistances { get; private set; }
    public ResistanceProfile ArmorResistances { get; private set; }
    public bool IsAlive => ArmorHP > 0;

    // Modules categorized by slot type
    private Dictionary<SlotType, List<Module>> modulesBySlot = new Dictionary<SlotType, List<Module>>();
    public IReadOnlyDictionary<SlotType, List<Module>> ModulesBySlot => modulesBySlot;

    public event Action<Ship> OnDestroyed;

    // Constructor
    public Ship(string name, string builtBy, string currentOwner, ShipWeightClass weightClass, float shieldHP, ResistanceProfile shieldResist, float armorHP, ResistanceProfile armorResist, float speed, float tonnage, float cargoCapacity, float longAxisLength, BlueprintSO blueprint = null) : base(name, builtBy, blueprint)
    {
        CurrentOwner = currentOwner;
        WeightClass = weightClass;

        ShieldHP = shieldHP;
        ShieldResistances = shieldResist ?? new ResistanceProfile();
        ArmorHP = armorHP;
        ArmorResistances = armorResist ?? new ResistanceProfile();

        Speed = speed;
        Tonnage = tonnage;
        CargoCapacity = cargoCapacity;
        LongAxisLength = longAxisLength;

        // Initialize module slots
        InitializeModuleSlots();
    }

    private void InitializeModuleSlots()
    {
        modulesBySlot[SlotType.High] = new List<Module>();
        modulesBySlot[SlotType.Medium] = new List<Module>();
        modulesBySlot[SlotType.Low] = new List<Module>();
        modulesBySlot[SlotType.Rig] = new List<Module>();

        if (WeightClass == ShipWeightClass.Capital)
        {
            modulesBySlot[SlotType.Capital] = new List<Module>();
        }
    }

    // Methods to Add modules to specific slots
    public void AddModuleToSlot(Module module, SlotType slot)
    {
        if (!modulesBySlot.ContainsKey(slot))
            throw new InvalidOperationException($"Slot type {slot} is not available for this ship.");

        modulesBySlot[slot].Add(module);
    }

    public void ChangeOwner(string newOwner)
    {
        CurrentOwner = newOwner;
        // You might want to trigger an event here for ownership changes
    }

    public void TakeDamage(float damage, DamageType type)
    {
        float shieldResist = ShieldResistances.GetResistance(type) / 100f;
        float armorResist = ArmorResistances.GetResistance(type) / 100f;

        if (ShieldHP > 0)
        {
            float effectiveDamage = damage * (1f - shieldResist);
            ShieldHP -= effectiveDamage;
            if (ShieldHP < 0)
            {
                damage = -ShieldHP;
                ShieldHP = 0;
                ArmorHP -= damage * (1f - armorResist);
            }
        }
        else
        {
            ArmorHP -= damage * (1f - armorResist);
        }

        if (!IsAlive)
        {
            OnDestroyed?.Invoke(this);
        }
    }
}
