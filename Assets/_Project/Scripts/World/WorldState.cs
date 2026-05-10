using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Place one WorldState component on a GameSystems object in the scene.
public class WorldState : MonoBehaviour
{
    public static WorldState Instance { get; private set; }

    public string villageMood = "worried";
    public string currentEvent = "The old church bell is missing.";
    public bool churchBellMissing = true;
    public bool miraSawStranger = true;
    public bool borinInspectedBellCase;
    public bool anselmReportedBellMissing = true;
    public List<string> globalFacts = new List<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple WorldState instances found. Using the first one.", this);
            return;
        }

        Instance = this;
    }

    public string GetWorldStateText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("World State");
        builder.AppendLine("Village Mood: " + SafeText(villageMood));
        builder.AppendLine("Current Event: " + SafeText(currentEvent));
        builder.AppendLine("Church Bell Missing: " + churchBellMissing);
        builder.AppendLine("Mira Saw Stranger: " + miraSawStranger);
        builder.AppendLine("Borin Inspected Bell Case: " + borinInspectedBellCase);
        builder.AppendLine("Anselm Reported Bell Missing: " + anselmReportedBellMissing);
        AppendList(builder, "Global Facts", globalFacts);
        return builder.ToString();
    }

    public void SetFact(string fact)
    {
        AddGlobalFact(fact);
    }

    public void AddGlobalFact(string fact)
    {
        if (!HasText(fact))
        {
            return;
        }

        if (!globalFacts.Contains(fact))
        {
            globalFacts.Add(fact);
        }
    }

    private static void AppendList(StringBuilder builder, string label, List<string> values)
    {
        builder.AppendLine(label + ":");

        if (values == null || values.Count == 0)
        {
            builder.AppendLine("- None");
            return;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (HasText(values[i]))
            {
                builder.AppendLine("- " + values[i]);
            }
        }
    }

    private static bool HasText(string value)
    {
        return !string.IsNullOrEmpty(value) && value.Trim().Length > 0;
    }

    private static string SafeText(string value)
    {
        return HasText(value) ? value : "None";
    }
}
