using System.Collections.Generic;
using UnityEngine;

// Computes the relevance evaluation for a single KnowledgeEntry: runs the access filter and the
// world-state/appearance gates, collects per-source activation matches, applies the (unchanged)
// scoring weights, and sets the final decision reason. All weights, the strong-activation rule,
// and the retrieval threshold are preserved exactly as in the original ContextRetriever.
public static class KnowledgeScorer
{
    public const int RetrievalThreshold = 7;

    public static KnowledgeRetrievalEvaluation Evaluate(
        KnowledgeEntry entry,
        NPCProfile npc,
        List<SceneContextObject> nearbyObjects,
        string playerMessage,
        PlayerState playerState,
        WorldState worldState,
        NPCState npcState,
        List<WorldEvent> relevantEvents)
    {
        KnowledgeRetrievalEvaluation evaluation = new KnowledgeRetrievalEvaluation();

        if (entry == null)
        {
            evaluation.finalDecisionReason = "skipped_below_threshold";
            return evaluation;
        }

        evaluation.allowedForNpc = KnowledgeAccessFilter.IsKnowledgeAllowedForNpc(entry, npc);

        if (!evaluation.allowedForNpc)
        {
            evaluation.finalDecisionReason = "skipped_not_allowed_for_npc";
            return evaluation;
        }

        evaluation.worldStateBlockReason = WorldStateKnowledgeGate.GetWorldStateBlockReason(entry, worldState, relevantEvents);

        if (!string.IsNullOrEmpty(evaluation.worldStateBlockReason))
        {
            evaluation.finalDecisionReason = "skipped_no_strong_activation";
            return evaluation;
        }

        evaluation.appearanceBlockReason = AppearanceKnowledgeGate.GetAppearanceBlockReason(entry, playerState, playerMessage);

        if (!string.IsNullOrEmpty(evaluation.appearanceBlockReason))
        {
            evaluation.finalDecisionReason = "skipped_appearance_mismatch";
            return evaluation;
        }

        evaluation.messageMatches = GetPlayerMessageEntryMatches(playerMessage, entry);
        evaluation.visibleStateMatches = GetPlayerVisibleStateMatches(entry, playerState);
        evaluation.npcStateMatches = GetNpcStateMatches(entry, npcState);
        evaluation.worldEventMatches = GetRelevantEventMatches(entry, relevantEvents);
        evaluation.worldStateMatches = GetWorldStateMatches(entry, worldState, playerMessage, nearbyObjects, evaluation.messageMatches.Count > 0, evaluation.worldEventMatches.Count > 0);
        evaluation.npcProfileTagMatches = KnowledgeTextUtil.GetOverlap(entry.tags, npc != null ? npc.knowledgeTags : null);

        evaluation.hasMessageActivation = evaluation.messageMatches.Count > 0;
        evaluation.hasVisibleStateActivation = evaluation.visibleStateMatches.Count > 0;
        evaluation.hasNpcStateActivation = evaluation.npcStateMatches.Count > 0;
        evaluation.hasWorldEventActivation = evaluation.worldEventMatches.Count > 0;
        evaluation.hasWorldStateActivation = evaluation.worldStateMatches.Count > 0;

        evaluation.rawLocalMatches = GetLocalEnvironmentMatches(entry, nearbyObjects);
        bool hasStrongActivationWithoutLocal =
            evaluation.hasMessageActivation ||
            evaluation.hasVisibleStateActivation ||
            evaluation.hasNpcStateActivation ||
            evaluation.hasWorldEventActivation ||
            evaluation.hasWorldStateActivation;
        evaluation.hasLocalActivation = evaluation.rawLocalMatches.Count > 0 &&
            (hasStrongActivationWithoutLocal || PlayerMessageRefersToLocalEnvironment(playerMessage, nearbyObjects));

        if (evaluation.hasMessageActivation)
        {
            evaluation.score += 8;
        }

        if (evaluation.hasVisibleStateActivation)
        {
            evaluation.score += 8;
        }

        if (evaluation.hasWorldEventActivation)
        {
            evaluation.score += 8;
        }

        if (evaluation.hasNpcStateActivation)
        {
            evaluation.score += 7;
        }

        if (evaluation.hasWorldStateActivation)
        {
            evaluation.score += 6;
        }

        if (evaluation.hasLocalActivation)
        {
            evaluation.score += 3;
        }

        if (evaluation.npcProfileTagMatches.Count > 0)
        {
            evaluation.score += 2;
        }

        int importanceScore = Mathf.Clamp(entry.importance, 0, 1);
        evaluation.importanceScore = importanceScore;
        evaluation.score += importanceScore;

        if (!evaluation.hasStrongActivation)
        {
            evaluation.finalDecisionReason = "skipped_no_strong_activation";
        }
        else if (evaluation.score < RetrievalThreshold)
        {
            evaluation.finalDecisionReason = "skipped_below_threshold";
        }
        else
        {
            evaluation.finalDecisionReason = "retrieved_allowed_and_activated";
        }

        return evaluation;
    }

    private static List<string> GetLocalEnvironmentMatches(KnowledgeEntry entry, List<SceneContextObject> nearbyObjects)
    {
        List<string> result = new List<string>();

        if (entry == null || nearbyObjects == null)
        {
            return result;
        }

        for (int i = 0; i < nearbyObjects.Count; i++)
        {
            SceneContextObject contextObject = nearbyObjects[i];

            if (contextObject == null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(contextObject.objectId) && KnowledgeTextUtil.ContainsIgnoreCase(entry.relatedObjectIds, contextObject.objectId) && !KnowledgeTextUtil.ContainsIgnoreCase(result, contextObject.objectId))
            {
                result.Add(contextObject.objectId);
            }

            AddEntryMatchesFromTerms(result, entry, contextObject.tags);
            AddEntryMatchesFromTerms(result, entry, contextObject.stateFacts);
            AddEntryMatchFromTerm(result, entry, contextObject.objectId);
            AddEntryMatchFromTerm(result, entry, contextObject.objectType);
            AddEntryMatchFromTerm(result, entry, contextObject.displayName);
        }

        return result;
    }

    private static List<string> GetPlayerVisibleStateMatches(KnowledgeEntry entry, PlayerState playerState)
    {
        List<string> result = new List<string>();

        if (entry == null || playerState == null)
        {
            return result;
        }

        AddEntryMatchFromTerm(result, entry, playerState.equippedOutfit);
        AddEntryMatchFromTerm(result, entry, playerState.visibleHeldItem);
        AddEntryMatchFromTerm(result, entry, playerState.publicReputation);
        AddEntryMatchesFromTerms(result, entry, playerState.visibleStatusTags);
        return result;
    }

    private static List<string> GetWorldStateMatches(
        KnowledgeEntry entry,
        WorldState worldState,
        string playerMessage,
        List<SceneContextObject> nearbyObjects,
        bool hasMessageActivation,
        bool hasWorldEventActivation)
    {
        List<string> rawMatches = new List<string>();

        if (entry == null || worldState == null)
        {
            return rawMatches;
        }

        AddEntryMatchFromTerm(rawMatches, entry, worldState.villageMood);
        AddEntryMatchFromTerm(rawMatches, entry, worldState.currentEvent);
        AddEntryMatchesFromTerms(rawMatches, entry, worldState.globalFacts);

        if (worldState.churchBellMissing)
        {
            AddEntryMatchFromTerm(rawMatches, entry, "missing_bell");
            AddEntryMatchFromTerm(rawMatches, entry, "bell_missing");
            AddEntryMatchFromTerm(rawMatches, entry, "missing bell");
        }
        else
        {
            AddEntryMatchFromTerm(rawMatches, entry, "bell_found");
            AddEntryMatchFromTerm(rawMatches, entry, "bell found");
            AddEntryMatchFromTerm(rawMatches, entry, "found");
            AddEntryMatchFromTerm(rawMatches, entry, "calm");
        }

        if (rawMatches.Count == 0)
        {
            return rawMatches;
        }

        if (hasMessageActivation || hasWorldEventActivation || PlayerMessageRefersToWorldState(playerMessage, worldState, entry) || PlayerMessageRefersToLocalEnvironment(playerMessage, nearbyObjects))
        {
            return rawMatches;
        }

        return new List<string>();
    }

    private static List<string> GetNpcStateMatches(KnowledgeEntry entry, NPCState npcState)
    {
        List<string> result = new List<string>();

        if (entry == null || npcState == null)
        {
            return result;
        }

        AddEntryMatchFromTerm(result, entry, npcState.mood);
        AddEntryMatchFromTerm(result, entry, npcState.trustToPlayer);
        AddEntryMatchesFromTerms(result, entry, npcState.personalEvents);

        if (!string.IsNullOrEmpty(npcState.trustToPlayer) &&
            !string.Equals(npcState.trustToPlayer, "medium", System.StringComparison.OrdinalIgnoreCase))
        {
            AddEntryMatchFromTerm(result, entry, "trust");
        }

        if (KnowledgeTextUtil.TextContainsSearchTerm(npcState.mood, "angry") || KnowledgeTextUtil.TextContainsSearchTerm(npcState.mood, "hostile"))
        {
            AddEntryMatchFromTerm(result, entry, "angry");
            AddEntryMatchFromTerm(result, entry, "hostile");
            AddEntryMatchFromTerm(result, entry, "aggression");
            AddEntryMatchFromTerm(result, entry, "trust");
        }

        if (npcState.personalEvents != null && npcState.personalEvents.Count > 0)
        {
            AddEntryMatchFromTerm(result, entry, "personal_event");
        }

        for (int i = 0; npcState.personalEvents != null && i < npcState.personalEvents.Count; i++)
        {
            string personalEvent = npcState.personalEvents[i];

            if (KnowledgeTextUtil.TextContainsSearchTerm(personalEvent, "throw") ||
                KnowledgeTextUtil.TextContainsSearchTerm(personalEvent, "threw") ||
                KnowledgeTextUtil.TextContainsSearchTerm(personalEvent, "hit") ||
                KnowledgeTextUtil.TextContainsSearchTerm(personalEvent, "attack") ||
                KnowledgeTextUtil.TextContainsSearchTerm(personalEvent, "aggression") ||
                KnowledgeTextUtil.TextContainsSearchTerm(personalEvent, "aggressive") ||
                KnowledgeTextUtil.TextContainsSearchTerm(personalEvent, "violence") ||
                KnowledgeTextUtil.TextContainsSearchTerm(personalEvent, "violent"))
            {
                AddEntryMatchFromTerm(result, entry, "aggression");
                AddEntryMatchFromTerm(result, entry, "angry");
                AddEntryMatchFromTerm(result, entry, "hostile");
                AddEntryMatchFromTerm(result, entry, "trust");
                AddEntryMatchFromTerm(result, entry, "personal_event");
            }
        }

        return result;
    }

    private static List<string> GetRelevantEventMatches(KnowledgeEntry entry, List<WorldEvent> relevantEvents)
    {
        List<string> result = new List<string>();

        if (entry == null || relevantEvents == null)
        {
            return result;
        }

        for (int i = 0; i < relevantEvents.Count; i++)
        {
            WorldEvent worldEvent = relevantEvents[i];

            if (worldEvent == null)
            {
                continue;
            }

            AddEntryMatchFromTerm(result, entry, worldEvent.eventType);
            AddEntryMatchFromTerm(result, entry, worldEvent.description);
            AddEntryMatchFromTerm(result, entry, worldEvent.locationObjectId);

            if (KnowledgeTextUtil.TextContainsSearchTerm(worldEvent.eventType, "bell_found") ||
                KnowledgeTextUtil.TextContainsSearchTerm(worldEvent.description, "bell found") ||
                KnowledgeTextUtil.TextContainsSearchTerm(worldEvent.description, "bell has been found"))
            {
                AddEntryMatchFromTerm(result, entry, "bell_found");
                AddEntryMatchFromTerm(result, entry, "found");
                AddEntryMatchFromTerm(result, entry, "calm");
            }

            if (KnowledgeTextUtil.TextContainsSearchTerm(worldEvent.eventType, "aggression") ||
                KnowledgeTextUtil.TextContainsSearchTerm(worldEvent.description, "aggression") ||
                KnowledgeTextUtil.TextContainsSearchTerm(worldEvent.description, "attack") ||
                KnowledgeTextUtil.TextContainsSearchTerm(worldEvent.description, "throw") ||
                KnowledgeTextUtil.TextContainsSearchTerm(worldEvent.description, "threw") ||
                KnowledgeTextUtil.TextContainsSearchTerm(worldEvent.description, "hit"))
            {
                AddEntryMatchFromTerm(result, entry, "aggression");
                AddEntryMatchFromTerm(result, entry, "angry");
                AddEntryMatchFromTerm(result, entry, "hostile");
                AddEntryMatchFromTerm(result, entry, "trust");
                AddEntryMatchFromTerm(result, entry, "personal_event");
            }

        }

        return result;
    }

    private static void AddEntryMatchesFromTerms(List<string> result, KnowledgeEntry entry, List<string> terms)
    {
        if (terms == null)
        {
            return;
        }

        for (int i = 0; i < terms.Count; i++)
        {
            AddEntryMatchFromTerm(result, entry, terms[i]);
        }
    }

    private static void AddEntryMatchFromTerm(List<string> result, KnowledgeEntry entry, string term)
    {
        if (result == null || entry == null || string.IsNullOrEmpty(term))
        {
            return;
        }

        List<string> terms = KnowledgeTextUtil.SplitSearchTerms(term);

        for (int i = 0; i < terms.Count; i++)
        {
            string cleanTerm = terms[i];

            if (EntryMatchesTerm(entry, cleanTerm) && !KnowledgeTextUtil.ContainsIgnoreCase(result, cleanTerm))
            {
                result.Add(cleanTerm);
            }
        }
    }

    private static bool EntryMatchesTerm(KnowledgeEntry entry, string term)
    {
        if (entry == null || string.IsNullOrEmpty(term))
        {
            return false;
        }

        string cleanTerm = term.Trim();

        if (cleanTerm.Length == 0 || cleanTerm == "none" || cleanTerm == "unknown")
        {
            return false;
        }

        if (KnowledgeTextUtil.ContainsIgnoreCase(entry.tags, cleanTerm) || KnowledgeTextUtil.ContainsText(entry.title, cleanTerm) || KnowledgeTextUtil.ContainsText(entry.id, cleanTerm))
        {
            return true;
        }

        return EntryTagsAppearInText(entry, cleanTerm);
    }

    private static bool EntryTagsAppearInText(KnowledgeEntry entry, string text)
    {
        if (entry == null || entry.tags == null || string.IsNullOrEmpty(text))
        {
            return false;
        }

        string lowerText = text.ToLowerInvariant();

        for (int i = 0; i < entry.tags.Count; i++)
        {
            string tag = entry.tags[i];

            if (!string.IsNullOrEmpty(tag) && tag.Trim().Length > 2 && lowerText.Contains(tag.Trim().ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> GetPlayerMessageEntryMatches(string playerMessage, KnowledgeEntry entry)
    {
        List<string> result = new List<string>();

        if (entry == null || string.IsNullOrEmpty(playerMessage))
        {
            return result;
        }

        AddTermsAppearingInText(result, playerMessage, entry.tags);
        AddTermsAppearingInText(result, playerMessage, entry.relatedObjectIds);
        AddSignificantTitleWordsAppearingInText(result, playerMessage, entry.title);
        return result;
    }

    private static void AddTermsAppearingInText(List<string> result, string text, List<string> terms)
    {
        if (result == null || string.IsNullOrEmpty(text) || terms == null)
        {
            return;
        }

        for (int i = 0; i < terms.Count; i++)
        {
            string term = terms[i];

            if (!string.IsNullOrEmpty(term) && KnowledgeTextUtil.TextContainsExactSearchTerm(text, term) && !KnowledgeTextUtil.ContainsIgnoreCase(result, term))
            {
                result.Add(term);
            }
        }
    }

    private static void AddSignificantTitleWordsAppearingInText(List<string> result, string text, string title)
    {
        if (result == null || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(title))
        {
            return;
        }

        string[] words = KnowledgeTextUtil.NormalizeSearchText(title).Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].Trim();

            if (word.Length > 3 && KnowledgeTextUtil.TextContainsSearchTerm(text, word) && !KnowledgeTextUtil.ContainsIgnoreCase(result, word))
            {
                result.Add(word);
            }
        }
    }

    private static bool PlayerMessageRefersToLocalEnvironment(string playerMessage, List<SceneContextObject> nearbyObjects)
    {
        if (string.IsNullOrEmpty(playerMessage))
        {
            return false;
        }

        if (KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "here") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "this place") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "around here") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "near here") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "nearby") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "near") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "where are we"))
        {
            return true;
        }

        if (nearbyObjects == null)
        {
            return false;
        }

        for (int i = 0; i < nearbyObjects.Count; i++)
        {
            SceneContextObject contextObject = nearbyObjects[i];

            if (contextObject == null)
            {
                continue;
            }

            if (KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, contextObject.objectId) ||
                KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, contextObject.displayName) ||
                KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, contextObject.objectType) ||
                KnowledgeTextUtil.AnyTermAppearsInText(playerMessage, contextObject.tags))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PlayerMessageRefersToWorldState(string playerMessage, WorldState worldState, KnowledgeEntry entry)
    {
        if (string.IsNullOrEmpty(playerMessage) || worldState == null)
        {
            return false;
        }

        if (entry != null &&
            (KnowledgeTextUtil.AnyTermAppearsInText(playerMessage, entry.tags) ||
            KnowledgeTextUtil.AnyTermAppearsInText(playerMessage, entry.relatedObjectIds) ||
            PlayerMessageContainsTitlePart(playerMessage, entry.title)))
        {
            return true;
        }

        if (KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, worldState.currentEvent) ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, worldState.villageMood) ||
            KnowledgeTextUtil.AnyTermAppearsInText(playerMessage, worldState.globalFacts))
        {
            return true;
        }

        return KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "what now") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "now what") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "what happens now") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "problem") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "problems") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "trouble") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "wrong") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "happened") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "news") ||
            KnowledgeTextUtil.TextContainsSearchTerm(playerMessage, "situation");
    }

    private static bool PlayerMessageContainsTitlePart(string playerMessage, string title)
    {
        if (string.IsNullOrEmpty(playerMessage) || string.IsNullOrEmpty(title))
        {
            return false;
        }

        string lowerMessage = playerMessage.ToLowerInvariant();
        string[] words = title.ToLowerInvariant().Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].Trim();

            if (word.Length > 3 && lowerMessage.Contains(word))
            {
                return true;
            }
        }

        return false;
    }
}
