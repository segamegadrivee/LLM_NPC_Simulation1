using System.Collections.Generic;
using System.Text;

[System.Serializable]
public class WorldEvent
{
    public string eventId;
    public string eventType;
    public string actor;
    public string targetNpcId;
    public string locationObjectId;
    public string description;
    public bool isPublic;
    public bool isGlobal;
    public int sequenceNumber;
    public float timestamp;

    public string GetEventText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("World Event");
        builder.AppendLine("Id: " + SafeText(eventId));
        builder.AppendLine("Type: " + SafeText(eventType));
        builder.AppendLine("Actor: " + SafeText(actor));
        builder.AppendLine("Target NPC Id: " + SafeText(targetNpcId));
        builder.AppendLine("Location Object Id: " + SafeText(locationObjectId));
        builder.AppendLine("Description: " + SafeText(description));
        builder.AppendLine("Is Public: " + isPublic);
        builder.AppendLine("Is Global: " + isGlobal);
        builder.AppendLine("Sequence Number: " + sequenceNumber);
        return builder.ToString();
    }

    public string GetShortText()
    {
        string source = isGlobal ? "global_event" : (isPublic ? "public_event" : (IsTargeted() ? "targeted_event" : "location_event"));
        return "[" + source + "] " + SafeText(description);
    }

    public bool IsTargeted()
    {
        return HasText(targetNpcId);
    }

    public bool IsRelevantForNpc(string npcId)
    {
        if (isGlobal || isPublic)
        {
            return true;
        }

        return HasText(npcId) && HasText(targetNpcId) &&
            string.Equals(targetNpcId.Trim(), npcId.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    public bool IsRelevantForLocation(List<string> locationObjectIds)
    {
        if (locationObjectIds == null || !HasText(locationObjectId))
        {
            return false;
        }

        for (int i = 0; i < locationObjectIds.Count; i++)
        {
            if (HasText(locationObjectIds[i]) && string.Equals(locationObjectIds[i].Trim(), locationObjectId.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasText(string value)
    {
        return !string.IsNullOrEmpty(value) && value.Trim().Length > 0;
    }

    private static string SafeText(string value)
    {
        return HasText(value) ? value.Trim() : "None";
    }
}
