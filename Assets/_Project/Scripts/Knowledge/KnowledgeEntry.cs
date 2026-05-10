using System.Collections.Generic;
using System.Text;
using UnityEngine;

[System.Serializable]
public class KnowledgeEntry
{
    public string id;
    public string title;

    [TextArea(3, 10)]
    public string text;

    public List<string> tags = new List<string>();
    public List<string> relatedObjectIds = new List<string>();
    public List<string> knownByNpcIds = new List<string>();
    public int importance = 1;

    public string GetKnowledgeText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Knowledge Entry");
        AppendField(builder, "Id", id);
        AppendField(builder, "Title", title);
        AppendField(builder, "Text", text);
        AppendList(builder, "Tags", tags);
        AppendList(builder, "Related Object Ids", relatedObjectIds);
        AppendList(builder, "Known By NPC Ids", knownByNpcIds);
        builder.AppendLine("Importance: " + importance);
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
