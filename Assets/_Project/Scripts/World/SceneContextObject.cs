using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Attach this to important scene objects such as the church, tavern, blacksmith, square, or road gate.
public class SceneContextObject : MonoBehaviour
{
    public string objectId;
    public string displayName;
    public string objectType;

    [TextArea(3, 8)]
    public string description;

    public List<string> tags = new List<string>();
    public List<string> stateFacts = new List<string>();

    public string GetContextText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Scene Object");
        AppendField(builder, "Id", objectId);
        AppendField(builder, "Name", displayName);
        AppendField(builder, "Type", objectType);
        AppendField(builder, "Description", description);
        AppendList(builder, "Tags", tags);
        AppendList(builder, "State Facts", stateFacts);
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
