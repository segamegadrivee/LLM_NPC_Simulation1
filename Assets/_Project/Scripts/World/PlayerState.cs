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
    public bool debugLogs = true;

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
        AddUnique(ref knownFacts, fact, "known fact");
    }

    public void AddHeldItem(string item)
    {
        AddUnique(ref heldItems, item, "held item");
    }

    public void AddCompletedAction(string action)
    {
        AddUnique(ref completedActions, action, "completed action");
    }

    public bool HasKnownFact(string fact)
    {
        return ContainsValue(knownFacts, fact);
    }

    public bool HasHeldItem(string item)
    {
        return ContainsValue(heldItems, item);
    }

    public bool HasCompletedAction(string action)
    {
        return ContainsValue(completedActions, action);
    }

    private void AddUnique(ref List<string> list, string value, string label)
    {
        if (!HasText(value))
        {
            return;
        }

        if (list == null)
        {
            list = new List<string>();
        }

        string cleanValue = value.Trim();

        if (!ContainsValue(list, cleanValue))
        {
            list.Add(cleanValue);

            if (debugLogs)
            {
                Debug.Log("PlayerState added " + label + ": " + cleanValue, this);
            }
        }
    }

    private static bool ContainsValue(List<string> list, string value)
    {
        if (list == null || !HasText(value))
        {
            return false;
        }

        string cleanValue = value.Trim();

        for (int i = 0; i < list.Count; i++)
        {
            if (HasText(list[i]) && list[i].Trim() == cleanValue)
            {
                return true;
            }
        }

        return false;
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
