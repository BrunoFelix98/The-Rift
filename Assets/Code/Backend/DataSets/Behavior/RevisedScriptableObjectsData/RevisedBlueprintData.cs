using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewRevisedBlueprint", menuName = "Game Data/Revised Blueprint", order = 4)]
public class RevisedBlueprintData : ScriptableObject
{
    [Header("Basic Information")]
    public string blueprintName;
    [TextArea] public string blueprintDescription;

    [Header("Usage Settings")]
    public bool isOriginal; // true = infinite uses
    public int numberOfUses = 1; // only applies to copies
    public int usesPerRun = 3; // how many runs can happen simultaneously (for queue limits)

    [Header("Manufacturing Requirements")]
    [Tooltip("List of base materials (resources) required to manufacture.")]
    public List<RevisedResourceRequirement> requiredResources;

    [Tooltip("Special or advanced materials (used for T2 or special variants). Optional.")]
    public List<RevisedResourceRequirement> advancedResources;

    [Header("Efficiency Settings")]
    [Range(0f, 100f)] public float materialEfficiency = 0f; // ME %
    [Range(0f, 100f)] public float timeEfficiency = 0f; // TE %

    [Header("Costs & Economy")]
    public float currencyCost; // ISK, credits, or however you call your currency

    [Header("Innovation (Upgrades to T2 or T3)")]
    [Range(0f, 100f)] public float innovationChance = 25f; // base chance to innovate
    [Range(0f, 100f)] public float innovationEfficiency = 0f; // ME/TE equivalent for innovation

    [Header("Derived Blueprints (T2, T3 Variants)")]
    [Tooltip("Blueprint produced if innovation is successful.")]
    public RevisedBlueprintData nextTierBlueprint; // e.g. T1 -> T2, T2 -> T3

    // Helper Methods
    public void IncreaseMaterialEfficiency(int numberOfRuns)
    {
        if (materialEfficiency < 50)
        {
            materialEfficiency += (numberOfRuns * 5); //Increase material efficiency up to 10 runs (5% per run), maximum of 50% ME
        }
        else
        {
            Debug.Log(blueprintName + " is already at maximum material efficiency!");
        }
    }

    public void IncreaseTimeEfficiency(int numberOfRuns)
    {
        if(timeEfficiency < 50)
        {
            timeEfficiency += (numberOfRuns * 5); //Increase time efficiency up to 10 runs (5% per run), maximum of 50% TE
        }
        else
        {
            Debug.Log(blueprintName + " is already at maximum time efficiency!");
        }
    }

    public void IncreaseInnovationEfficiency(int numberOfRuns)
    {
        if(innovationEfficiency < 75)
        {
            innovationEfficiency += (numberOfRuns * 10); //Increase innovation efficiency up to 5 runs (10% per run), maximum of 75% IE
        }
        else
        {
            Debug.Log(blueprintName + " is already at maximum innovation efficiency!");
        }
    }

    public bool IsDepleted()
    {
        if (isOriginal) return false;
        return numberOfUses <= 0;
    }

    public void ConsumeUse()
    {
        if (!isOriginal)
        {
            numberOfUses--;
            if (numberOfUses < 0) numberOfUses = 0;
        }
        else
        {
            Debug.Log(blueprintName + " is an original blueprint, and has infinite uses");
        }
    }

    public RevisedBlueprintData TryInnovate()
    {
        if (nextTierBlueprint == null)
            return null;

        float roll = Random.Range(0f, 100f);
        if (roll <= innovationChance)
        {
            // Success! Reduce efficiency for the next tier
            RevisedBlueprintData copy = Instantiate(nextTierBlueprint);
            copy.innovationEfficiency = Mathf.Max(0, innovationEfficiency - 25f); // lose 25% efficiency
            return copy;
        }

        return null; // Innovation failed
    }
}