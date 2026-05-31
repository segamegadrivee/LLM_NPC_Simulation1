// Decides whether a KnowledgeEntry is allowed to be considered for the current NPC.
// Handles knownByNpcIds / public ("public"/"all"/empty) access logic. Entries that fail
// this gate are reported as "skipped_not_allowed_for_npc" by the retriever/debug builder.
// Behavior preserved verbatim from the original ContextRetriever.
public static class KnowledgeAccessFilter
{
    public static bool IsKnowledgeAllowedForNpc(KnowledgeEntry entry, NPCProfile npc)
    {
        if (entry == null)
        {
            return false;
        }

        if (IsPublicKnowledge(entry))
        {
            return true;
        }

        return npc != null && KnowledgeTextUtil.ContainsIgnoreCase(entry.knownByNpcIds, npc.npcId);
    }

    public static bool IsPublicKnowledge(KnowledgeEntry entry)
    {
        if (entry == null || entry.knownByNpcIds == null || entry.knownByNpcIds.Count == 0)
        {
            return true;
        }

        return KnowledgeTextUtil.ContainsIgnoreCase(entry.knownByNpcIds, "public") || KnowledgeTextUtil.ContainsIgnoreCase(entry.knownByNpcIds, "all");
    }
}
