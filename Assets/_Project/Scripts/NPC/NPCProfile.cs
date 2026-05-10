using System.Collections.Generic;
using System.Text;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_NPC_Profile", menuName = "AI NPC/NPC Profile")]
public class NPCProfile : ScriptableObject
{
    public string npcId;
    public string npcName;
    public string role;

    [TextArea(3, 8)]
    public string personality;

    [TextArea(3, 8)]
    public string backstory;

    [TextArea(2, 6)]
    public string speakingStyle;

    public List<string> knowledgeTags = new List<string>();
    public List<string> knownFacts = new List<string>();
    public List<string> relationships = new List<string>();

    public string GetProfileContextText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("NPC Profile");
        AppendField(builder, "Id", npcId);
        AppendField(builder, "Name", npcName);
        AppendField(builder, "Role", role);
        AppendField(builder, "Personality", personality);
        AppendField(builder, "Backstory", backstory);
        AppendField(builder, "Speaking Style", speakingStyle);
        AppendList(builder, "Knowledge Tags", knowledgeTags);
        AppendList(builder, "Known Facts", knownFacts);
        AppendList(builder, "Relationships", relationships);
        return builder.ToString();
    }

    private static void AppendField(StringBuilder builder, string label, string value)
    {
        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(string.IsNullOrEmpty(value) ? "None" : value);
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
            if (!string.IsNullOrEmpty(values[i]))
            {
                builder.AppendLine("- " + values[i]);
            }
        }
    }
}
