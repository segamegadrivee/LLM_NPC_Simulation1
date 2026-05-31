using System.Collections.Generic;
using System.Text;

[System.Serializable]
public class NPCState
{
    public string npcId;
    public string mood = "neutral";
    public string trustToPlayer = "medium";
    public List<string> personalEvents = new List<string>();

    public string GetStateText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("NPC State");
        builder.AppendLine("NPC Id: " + SafeText(npcId));
        builder.AppendLine("Mood: " + SafeText(mood));
        builder.AppendLine("Trust To Player: " + SafeText(trustToPlayer));
        AppendList(builder, "Personal Events", personalEvents);
        return builder.ToString();
    }

    private static void AppendList(StringBuilder builder, string label, List<string> values)
    {
        builder.AppendLine(label + ":");

        if (values == null || values.Count == 0)
        {
            builder.AppendLine("- None");
            return;
        }

        bool wroteValue = false;

        for (int i = 0; i < values.Count; i++)
        {
            if (HasText(values[i]))
            {
                builder.AppendLine("- " + values[i].Trim());
                wroteValue = true;
            }
        }

        if (!wroteValue)
        {
            builder.AppendLine("- None");
        }
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
