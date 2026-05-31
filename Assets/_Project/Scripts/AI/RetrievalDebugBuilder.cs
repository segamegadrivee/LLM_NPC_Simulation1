using System.Collections.Generic;

// Builds the human-readable retrieval explanation for a single KnowledgeEntry: the access reason,
// each activation source's pass/skip reason and score contribution, gate block reasons (appearance,
// world-state), and the final gate decision. Produces a ContextRetriever.DebugKnowledgeRetrievalEntry
// so the debug overlay/dumper/tracer keep consuming the same type. Output preserved verbatim.
public static class RetrievalDebugBuilder
{
    public static ContextRetriever.DebugKnowledgeRetrievalEntry Build(
        KnowledgeEntry entry,
        NPCProfile npc,
        List<SceneContextObject> nearbyObjects,
        string playerMessage,
        PlayerState playerState,
        WorldState worldState,
        NPCState npcState,
        List<WorldEvent> relevantEvents)
    {
        ContextRetriever.DebugKnowledgeRetrievalEntry debugEntry = new ContextRetriever.DebugKnowledgeRetrievalEntry();
        debugEntry.entry = entry;

        if (entry == null)
        {
            debugEntry.skippedReasons.Add("KnowledgeEntry is null.");
            debugEntry.finalDecisionReason = "skipped_below_threshold";
            return debugEntry;
        }

        string npcId = npc != null ? npc.npcId : null;
        KnowledgeRetrievalEvaluation evaluation = KnowledgeScorer.Evaluate(entry, npc, nearbyObjects, playerMessage, playerState, worldState, npcState, relevantEvents);

        debugEntry.allowedForNpc = evaluation.allowedForNpc;
        debugEntry.hasMessageActivation = evaluation.hasMessageActivation;
        debugEntry.hasVisibleStateActivation = evaluation.hasVisibleStateActivation;
        debugEntry.hasNpcStateActivation = evaluation.hasNpcStateActivation;
        debugEntry.hasWorldEventActivation = evaluation.hasWorldEventActivation;
        debugEntry.hasWorldStateActivation = evaluation.hasWorldStateActivation;
        debugEntry.hasLocalActivation = evaluation.hasLocalActivation;
        debugEntry.hasStrongActivation = evaluation.hasStrongActivation;
        debugEntry.finalScore = evaluation.score;
        debugEntry.finalDecisionReason = evaluation.finalDecisionReason;

        if (!evaluation.allowedForNpc)
        {
            debugEntry.skippedReasons.Add("Access gate failed: knownByNpcIds is " + KnowledgeTextUtil.FormatDebugList(entry.knownByNpcIds) + " and current npcId is '" + KnowledgeTextUtil.SafeDebugText(npcId) + "'.");
            return debugEntry;
        }

        if (npc != null && KnowledgeTextUtil.ContainsIgnoreCase(entry.knownByNpcIds, npc.npcId))
        {
            debugEntry.retrievalReasons.Add("Access gate passed: knownByNpcIds contains current NPC id '" + KnowledgeTextUtil.SafeDebugText(npc.npcId) + "'.");
        }
        else if (KnowledgeAccessFilter.IsPublicKnowledge(entry))
        {
            debugEntry.retrievalReasons.Add("Access gate passed: knownByNpcIds is empty/public, so this is public knowledge.");
        }

        if (!string.IsNullOrEmpty(evaluation.appearanceBlockReason))
        {
            debugEntry.skippedReasons.Add(evaluation.appearanceBlockReason);
        }

        if (!string.IsNullOrEmpty(evaluation.worldStateBlockReason))
        {
            debugEntry.skippedReasons.Add(evaluation.worldStateBlockReason);
        }

        if (evaluation.hasMessageActivation)
        {
            debugEntry.retrievalReasons.Add("message_activation: true - player message matched entry tags/title/related objects: " + KnowledgeTextUtil.FormatDebugList(evaluation.messageMatches) + " (+8).");
        }
        else
        {
            debugEntry.skippedReasons.Add("message_activation: false - player message did not match entry tags, significant title words, or relatedObjectIds.");
        }

        if (evaluation.hasVisibleStateActivation)
        {
            debugEntry.retrievalReasons.Add("visible_state_activation: true - visible player state matched entry tags/title: " + KnowledgeTextUtil.FormatDebugList(evaluation.visibleStateMatches) + " (+8).");
        }
        else
        {
            debugEntry.skippedReasons.Add("visible_state_activation: false - outfit, held item, and visible tags did not match this entry.");
        }

        if (evaluation.hasNpcStateActivation)
        {
            debugEntry.retrievalReasons.Add("npc_state_activation: true - NPC mood/trust/personal events matched entry tags/title: " + KnowledgeTextUtil.FormatDebugList(evaluation.npcStateMatches) + " (+7).");
        }
        else
        {
            debugEntry.skippedReasons.Add("npc_state_activation: false - NPC state did not match this entry.");
        }

        if (evaluation.hasWorldEventActivation)
        {
            debugEntry.retrievalReasons.Add("world_event_activation: true - relevant public/global/targeted event matched entry tags/title: " + KnowledgeTextUtil.FormatDebugList(evaluation.worldEventMatches) + " (+8).");
        }
        else
        {
            debugEntry.skippedReasons.Add("world_event_activation: false - recent relevant events did not match this entry.");
        }

        if (evaluation.hasWorldStateActivation)
        {
            debugEntry.retrievalReasons.Add("world_state_activation: true - WorldState directly matched and the message/event made that state relevant: " + KnowledgeTextUtil.FormatDebugList(evaluation.worldStateMatches) + " (+6).");
        }
        else
        {
            debugEntry.skippedReasons.Add("world_state_activation: false - WorldState alone did not directly activate this entry.");
        }

        if (evaluation.hasLocalActivation)
        {
            debugEntry.retrievalReasons.Add("local_activation: true - nearby SceneContextObject matched and the player referred to the place or another strong source activated the entry: " + KnowledgeTextUtil.FormatDebugList(evaluation.rawLocalMatches) + " (+3).");
        }
        else
        {
            debugEntry.skippedReasons.Add("local_activation: false - no local match, or location matched without a place reference/strong activation.");
        }

        if (evaluation.npcProfileTagMatches.Count > 0)
        {
            debugEntry.retrievalReasons.Add("NPCProfile.knowledgeTags overlap: " + KnowledgeTextUtil.FormatDebugList(evaluation.npcProfileTagMatches) + " (+2, not a strong activation source).");
        }
        else
        {
            debugEntry.skippedReasons.Add("NPCProfile.knowledgeTags did not overlap this entry, or no NPC profile was available.");
        }

        if (evaluation.importanceScore > 0)
        {
            debugEntry.retrievalReasons.Add("importance contributes +" + evaluation.importanceScore + " (capped at +1, not a strong activation source).");
        }
        else
        {
            debugEntry.skippedReasons.Add("importance contributes +0.");
        }

        if (!evaluation.hasStrongActivation)
        {
            debugEntry.skippedReasons.Add("Final gate failed: no strong activation source. Access, NPC identity, local environment, and importance cannot retrieve by themselves.");
        }
        else if (evaluation.score < KnowledgeScorer.RetrievalThreshold)
        {
            debugEntry.skippedReasons.Add("Final gate failed: score " + evaluation.score + " is below threshold " + KnowledgeScorer.RetrievalThreshold + ".");
        }
        else
        {
            debugEntry.retrievalReasons.Add("Final gate passed: allowed, strongly activated, and score " + evaluation.score + " >= " + KnowledgeScorer.RetrievalThreshold + ".");
        }

        return debugEntry;
    }
}
