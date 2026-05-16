using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Attach this to the Player object so NPCs can react to what the player knows, has, or has done.
public class PlayerState : MonoBehaviour
{
    public string reputation = "neutral";
    public string currentRole = "traveler";
    public string equippedOutfit = "normal";
    public string visibleHeldItem = "none";
    public List<string> visibleStatusTags = new List<string>();
    public string publicReputation = "unknown";
    public int aggressionScore;
    public int helpfulnessScore;
    public List<string> heldItems = new List<string>();
    public List<string> completedActions = new List<string>();
    public List<string> knownFacts = new List<string>();
    public List<string> reputationEvents = new List<string>();
    public bool debugLogs = true;

    public string GetPlayerStateText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Player State");
        builder.AppendLine("Reputation: " + SafeText(reputation));
        builder.AppendLine("Current Role: " + SafeText(currentRole));
        builder.AppendLine("Equipped Outfit: " + SafeText(equippedOutfit));
        builder.AppendLine("Visible Held Item: " + SafeText(visibleHeldItem));
        builder.AppendLine("Public Reputation: " + SafeText(publicReputation));
        builder.AppendLine("Aggression Score: " + aggressionScore);
        builder.AppendLine("Helpfulness Score: " + helpfulnessScore);
        AppendList(builder, "Visible Status Tags", visibleStatusTags);
        AppendList(builder, "Held Items", heldItems);
        AppendList(builder, "Completed Actions", completedActions);
        AppendList(builder, "Known Facts", knownFacts);
        AppendList(builder, "Reputation Events", reputationEvents);
        return builder.ToString();
    }

    public void SetOutfit(string outfit)
    {
        equippedOutfit = HasText(outfit) ? outfit.Trim() : "normal";
        RefreshVisibleStatusTags();

        if (string.Equals(equippedOutfit, "guard_armor", System.StringComparison.OrdinalIgnoreCase))
        {
            AddCompletedAction("player_equipped_guard_armor");
        }
        else if (string.Equals(equippedOutfit, "dark_cloak", System.StringComparison.OrdinalIgnoreCase))
        {
            AddCompletedAction("player_equipped_dark_cloak");
        }

        if (debugLogs)
        {
            Debug.Log("PlayerState outfit set to: " + equippedOutfit, this);
        }
    }

    public void SetVisibleHeldItem(string item)
    {
        visibleHeldItem = HasText(item) ? item.Trim() : "none";
        RefreshVisibleStatusTags();

        if (!string.Equals(visibleHeldItem, "none", System.StringComparison.OrdinalIgnoreCase))
        {
            AddHeldItem(visibleHeldItem);
            AddCompletedAction("player_picked_up_" + NormalizeToken(visibleHeldItem));
        }

        if (debugLogs)
        {
            Debug.Log("PlayerState visible held item set to: " + visibleHeldItem, this);
        }
    }

    public void ClearVisibleHeldItem()
    {
        visibleHeldItem = "none";
        RefreshVisibleStatusTags();

        if (debugLogs)
        {
            Debug.Log("PlayerState visible held item cleared.", this);
        }
    }

    public void AddReputationEvent(string eventDescription)
    {
        AddUnique(ref reputationEvents, eventDescription, "reputation event");
    }

    public void RegisterAggression()
    {
        aggressionScore++;
        publicReputation = aggressionScore >= 2 ? "dangerous" : "suspicious";
        reputation = publicReputation;
        AddCompletedAction("player_committed_aggression");
        AddReputationEvent("Player committed aggression.");
        RefreshVisibleStatusTags();

        if (debugLogs)
        {
            Debug.Log("PlayerState registered aggression. Score: " + aggressionScore + ", reputation: " + publicReputation, this);
        }
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

    private void RefreshVisibleStatusTags()
    {
        if (visibleStatusTags == null)
        {
            visibleStatusTags = new List<string>();
        }

        RemoveControlledVisibleTag("armored");
        RemoveControlledVisibleTag("authority_signal");
        RemoveControlledVisibleTag("armed");
        RemoveControlledVisibleTag("suspicious");
        RemoveControlledVisibleTag("carrying_sacred_object");

        if (string.Equals(equippedOutfit, "guard_armor", System.StringComparison.OrdinalIgnoreCase))
        {
            AddVisibleStatusTag("armored");
            AddVisibleStatusTag("authority_signal");
        }
        else if (string.Equals(equippedOutfit, "dark_cloak", System.StringComparison.OrdinalIgnoreCase))
        {
            AddVisibleStatusTag("suspicious");
        }

        if (string.Equals(visibleHeldItem, "sword", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(visibleHeldItem, "hammer", System.StringComparison.OrdinalIgnoreCase))
        {
            AddVisibleStatusTag("armed");
        }

        if (string.Equals(visibleHeldItem, "bell_fragment", System.StringComparison.OrdinalIgnoreCase))
        {
            AddVisibleStatusTag("carrying_sacred_object");
        }

        if (string.Equals(publicReputation, "suspicious", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(publicReputation, "dangerous", System.StringComparison.OrdinalIgnoreCase))
        {
            AddVisibleStatusTag("suspicious");
        }
    }

    private void AddVisibleStatusTag(string tag)
    {
        AddUnique(ref visibleStatusTags, tag, "visible status tag");
    }

    private void RemoveControlledVisibleTag(string tag)
    {
        if (visibleStatusTags == null || !HasText(tag))
        {
            return;
        }

        for (int i = visibleStatusTags.Count - 1; i >= 0; i--)
        {
            if (HasText(visibleStatusTags[i]) && string.Equals(visibleStatusTags[i].Trim(), tag.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                visibleStatusTags.RemoveAt(i);
            }
        }
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

    private static string NormalizeToken(string value)
    {
        return HasText(value) ? value.Trim().ToLowerInvariant().Replace(" ", "_") : "none";
    }
}
