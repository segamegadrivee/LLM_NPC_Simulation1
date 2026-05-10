using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Attach this to the Player object so NPCs can react to what the player knows, has, or has done.
public class PlayerState : MonoBehaviour
{
    public string reputation = "neutral";
    public string currentRole = "traveler";
    public List<string> heldItems = new List<string>();
    public List<string> completedActions = new List<string>();
    public List<string> knownFacts = new List<string>();

    public string GetPlayerStateText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Player State");
        builder.AppendLine("Reputation: " + SafeText(reputation));
        builder.AppendLine("Current Role: " + SafeText(currentRole));
        AppendList(builder, "Held Items", heldItems);
        AppendList(builder, "Completed Actions", completedActions);
        AppendList(builder, "Known Facts", knownFacts);
        return builder.ToString();
    }

    public void AddKnownFact(string fact)
    {
        AddUnique(knownFacts, fact);
    }

    public void AddHeldItem(string item)
    {
        AddUnique(heldItems, item);
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (list == null || !HasText(value))
        {
            return;
        }

        if (!list.Contains(value))
        {
            list.Add(value);
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
