using System.Collections.Generic;

// Handles world-state dependent knowledge. Authoritative WorldState (churchBellMissing) plus any
// recent "bell_found" event decide whether missing-bell tension knowledge or bell-found/resolution
// knowledge is currently active. Behavior preserved verbatim from the original ContextRetriever.
public static class WorldStateKnowledgeGate
{
    public static string GetWorldStateBlockReason(KnowledgeEntry entry, WorldState worldState, List<WorldEvent> relevantEvents)
    {
        if (entry == null || worldState == null)
        {
            return string.Empty;
        }

        bool hasBellFoundEvent = HasBellFoundEvent(relevantEvents);

        if ((KnowledgeTextUtil.ContainsIgnoreCase(entry.tags, "bell_found") || KnowledgeTextUtil.ContainsIgnoreCase(entry.tags, "resolved")) &&
            worldState.churchBellMissing &&
            !hasBellFoundEvent)
        {
            return "WorldState gate: bell_found/resolution knowledge is inactive while churchBellMissing is true and no recent bell_found event is relevant.";
        }

        if (KnowledgeTextUtil.ContainsIgnoreCase(entry.tags, "missing_bell") &&
            (!worldState.churchBellMissing || hasBellFoundEvent))
        {
            return "WorldState gate: missing_bell tension knowledge is inactive because the bell has been found or a bell_found event is relevant.";
        }

        return string.Empty;
    }

    public static bool HasBellFoundEvent(List<WorldEvent> relevantEvents)
    {
        if (relevantEvents == null)
        {
            return false;
        }

        for (int i = 0; i < relevantEvents.Count; i++)
        {
            WorldEvent worldEvent = relevantEvents[i];

            if (worldEvent == null)
            {
                continue;
            }

            if (KnowledgeTextUtil.TextContainsSearchTerm(worldEvent.eventType, "bell_found") ||
                KnowledgeTextUtil.TextContainsSearchTerm(worldEvent.description, "bell found") ||
                KnowledgeTextUtil.TextContainsSearchTerm(worldEvent.description, "bell has been found"))
            {
                return true;
            }
        }

        return false;
    }
}
