using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_KnowledgeBase", menuName = "AI NPC/Knowledge Base")]
public class KnowledgeBase : ScriptableObject
{
    public List<KnowledgeEntry> entries = new List<KnowledgeEntry>();

    public List<KnowledgeEntry> GetAllEntries()
    {
        List<KnowledgeEntry> result = new List<KnowledgeEntry>();

        if (entries == null)
        {
            return result;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
            {
                result.Add(entries[i]);
            }
        }

        return result;
    }

    public List<KnowledgeEntry> GetEntriesKnownBy(string npcId)
    {
        List<KnowledgeEntry> result = new List<KnowledgeEntry>();

        if (entries == null || string.IsNullOrEmpty(npcId))
        {
            return result;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            KnowledgeEntry entry = entries[i];

            if (entry != null && ContainsIgnoreCase(entry.knownByNpcIds, npcId))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    public List<KnowledgeEntry> GetEntriesByTag(string tag)
    {
        List<KnowledgeEntry> result = new List<KnowledgeEntry>();

        if (entries == null || string.IsNullOrEmpty(tag))
        {
            return result;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            KnowledgeEntry entry = entries[i];

            if (entry != null && ContainsIgnoreCase(entry.tags, tag))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private static bool ContainsIgnoreCase(List<string> values, string target)
    {
        if (values == null || string.IsNullOrEmpty(target))
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], target, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
