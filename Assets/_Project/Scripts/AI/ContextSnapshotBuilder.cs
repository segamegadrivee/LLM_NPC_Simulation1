using System.Collections.Generic;

// Assembles the explainable side of a ContextSnapshot: the flat "why was each source available"
// reason list, and the Context Availability Layer (every considered ContextEntry with its source,
// visibility, and inclusion decision). This does NOT change what the PromptBuilder receives; it
// records WHY each piece is allowed or excluded for the active NPC. Behavior preserved verbatim
// from the original ContextRetriever.
public static class ContextSnapshotBuilder
{
    public static List<string> BuildContextSourceReasons(ContextSnapshot snapshot)
    {
        List<string> reasons = new List<string>();

        if (snapshot == null)
        {
            return reasons;
        }

        AddReason(reasons, "source: npc_profile - active NPC profile selected by DialogueManager.");
        AddReason(reasons, "source: player_state - PlayerState component attached to player.");
        AddReason(reasons, "source: visible_player_state - equippedOutfit, visibleHeldItem, visibleStatusTags are observable to all NPCs.");
        AddReason(reasons, "source: world_state - public/global WorldState is available to all NPCs.");

        if (snapshot.npcState != null)
        {
            AddReason(reasons, "source: npc_personal_memory - NPCState is loaded only for npcId '" + KnowledgeTextUtil.SafeDebugText(snapshot.npcState.npcId) + "'.");
        }

        if (snapshot.nearbyObjects != null)
        {
            for (int i = 0; i < snapshot.nearbyObjects.Count; i++)
            {
                SceneContextObject contextObject = snapshot.nearbyObjects[i];

                if (contextObject != null)
                {
                    AddReason(reasons, "source: local_environment - nearby SceneContextObject '" + KnowledgeTextUtil.SafeDebugText(contextObject.objectId) + "' is within sceneContextRadius.");
                }
            }
        }

        if (snapshot.recentRelevantEvents != null)
        {
            for (int i = 0; i < snapshot.recentRelevantEvents.Count; i++)
            {
                WorldEvent worldEvent = snapshot.recentRelevantEvents[i];

                if (worldEvent == null)
                {
                    continue;
                }

                if (worldEvent.isGlobal)
                {
                    AddReason(reasons, "source: global_event - " + KnowledgeTextUtil.SafeDebugText(worldEvent.description));
                }
                else if (worldEvent.isPublic)
                {
                    AddReason(reasons, "source: public_event - " + KnowledgeTextUtil.SafeDebugText(worldEvent.description));
                }
                else if (snapshot.npcProfile != null && !string.IsNullOrEmpty(worldEvent.targetNpcId) &&
                    string.Equals(worldEvent.targetNpcId, snapshot.npcProfile.npcId, System.StringComparison.OrdinalIgnoreCase))
                {
                    AddReason(reasons, "source: targeted_event - event targets current NPC '" + KnowledgeTextUtil.SafeDebugText(worldEvent.targetNpcId) + "'.");
                }
                else
                {
                    AddReason(reasons, "source: local_event - event matches current nearby SceneContextObject.");
                }
            }
        }

        if (snapshot.retrievedKnowledge != null)
        {
            for (int i = 0; i < snapshot.retrievedKnowledge.Count; i++)
            {
                KnowledgeEntry entry = snapshot.retrievedKnowledge[i];

                if (entry != null)
                {
                    AddReason(reasons, "source: retrieved_knowledge - KnowledgeEntry '" + KnowledgeTextUtil.SafeDebugText(entry.id) + "' passed access, strong activation, threshold, and ranking rules.");
                }
            }
        }

        if (snapshot.recentDialogueHistory != null && snapshot.recentDialogueHistory.Count > 0)
        {
            AddReason(reasons, "source: dialogue_memory - only history for the active npcId was retrieved.");
        }

        return reasons;
    }

    // Builds the Context Availability Layer: a flat, explainable list of every considered piece of
    // context with its source, visibility, and inclusion decision. This does not change what the
    // PromptBuilder receives (it still reads the typed snapshot fields); it records WHY each piece
    // is allowed or excluded for the active NPC so the debug overlay and diploma can explain it.
    public static void BuildAvailabilityEntries(ContextSnapshot snapshot)
    {
        List<ContextEntry> entries = new List<ContextEntry>();

        if (snapshot == null)
        {
            return;
        }

        GatherNpcProfileEntries(snapshot, entries);
        GatherPlayerVisibleEntries(snapshot, entries);
        GatherWorldStateEntries(snapshot, entries);
        GatherWorldEventEntries(snapshot, entries);
        GatherNpcStateEntries(snapshot, entries);
        GatherNearbySceneEntries(snapshot, entries);
        GatherKnowledgeEntries(snapshot, entries);
        GatherPlayerClaimEntries(snapshot, entries);
        GatherPrivatePlayerEntries(snapshot, entries);

        snapshot.contextEntries = entries;
        snapshot.includedEntries = new List<ContextEntry>();
        snapshot.excludedEntries = new List<ContextEntry>();

        for (int i = 0; i < entries.Count; i++)
        {
            ContextEntry entry = entries[i];

            if (entry == null)
            {
                continue;
            }

            if (entry.includedInPrompt)
            {
                snapshot.includedEntries.Add(entry);
            }
            else
            {
                snapshot.excludedEntries.Add(entry);
            }
        }
    }

    private static void GatherNpcProfileEntries(ContextSnapshot snapshot, List<ContextEntry> entries)
    {
        if (snapshot.npcProfile == null)
        {
            return;
        }

        AddIncludedEntry(entries, "profile_" + KnowledgeTextUtil.SafeDebugText(snapshot.npcProfile.npcId),
            "NPC profile: " + KnowledgeTextUtil.SafeDebugText(snapshot.npcProfile.npcName) + " (" + KnowledgeTextUtil.SafeDebugText(snapshot.npcProfile.role) + ")",
            ContextSourceType.NPCProfile, ContextVisibility.NpcProfileKnowledge, snapshot.npcProfile.knowledgeTags);

        AddTextEntries(entries, "profile_fact", snapshot.npcProfile.knownFacts,
            ContextSourceType.NPCProfile, ContextVisibility.NpcProfileKnowledge);
    }

    private static void GatherPlayerVisibleEntries(ContextSnapshot snapshot, List<ContextEntry> entries)
    {
        PlayerState playerState = snapshot.playerState;

        if (playerState == null)
        {
            return;
        }

        AddVisibleStateEntry(entries, "visible_outfit", "Outfit", playerState.equippedOutfit, "normal");
        AddVisibleStateEntry(entries, "visible_held_item", "Visible held item", playerState.visibleHeldItem, "none");
        AddTextEntries(entries, "visible_tag", playerState.visibleStatusTags,
            ContextSourceType.PlayerState, ContextVisibility.VisibleOnPlayer);
    }

    private static void GatherWorldStateEntries(ContextSnapshot snapshot, List<ContextEntry> entries)
    {
        WorldState worldState = snapshot.worldState;

        if (worldState == null)
        {
            return;
        }

        AddIncludedEntry(entries, "world_event_state", "Current village situation: " + KnowledgeTextUtil.SafeDebugText(worldState.currentEvent),
            ContextSourceType.WorldState, ContextVisibility.PublicWorldState, null);
        AddIncludedEntry(entries, "world_mood", "Village mood: " + KnowledgeTextUtil.SafeDebugText(worldState.villageMood),
            ContextSourceType.WorldState, ContextVisibility.PublicWorldState, null);
        AddTextEntries(entries, "world_fact", worldState.globalFacts,
            ContextSourceType.WorldState, ContextVisibility.PublicWorldState);
    }

    private static void GatherWorldEventEntries(ContextSnapshot snapshot, List<ContextEntry> entries)
    {
        if (snapshot.recentRelevantEvents == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.recentRelevantEvents.Count; i++)
        {
            WorldEvent worldEvent = snapshot.recentRelevantEvents[i];

            if (worldEvent == null)
            {
                continue;
            }

            ContextVisibility visibility = worldEvent.isGlobal || worldEvent.isPublic
                ? ContextVisibility.PublicWorldEvent
                : ContextVisibility.TargetedEvent;

            ContextEntry entry = new ContextEntry(
                KnowledgeTextUtil.SafeDebugText(worldEvent.eventId),
                KnowledgeTextUtil.SafeDebugText(worldEvent.description),
                ContextSourceType.WorldEventLog,
                visibility,
                true);
            entry.relatedObjectId = KnowledgeTextUtil.SafeOrEmpty(worldEvent.locationObjectId);
            entry.relatedNpcId = KnowledgeTextUtil.SafeOrEmpty(worldEvent.targetNpcId);
            entries.Add(entry);
        }
    }

    private static void GatherNpcStateEntries(ContextSnapshot snapshot, List<ContextEntry> entries)
    {
        NPCState npcState = snapshot.npcState;

        if (npcState == null)
        {
            return;
        }

        AddIncludedEntry(entries, "npc_mood", "Own mood: " + KnowledgeTextUtil.SafeDebugText(npcState.mood),
            ContextSourceType.NPCState, ContextVisibility.NpcPersonalMemory, null);
        AddIncludedEntry(entries, "npc_trust", "Own trust toward player: " + KnowledgeTextUtil.SafeDebugText(npcState.trustToPlayer),
            ContextSourceType.NPCState, ContextVisibility.NpcPersonalMemory, null);
        AddTextEntries(entries, "npc_personal_event", npcState.personalEvents,
            ContextSourceType.NPCState, ContextVisibility.NpcPersonalMemory);
    }

    private static void GatherNearbySceneEntries(ContextSnapshot snapshot, List<ContextEntry> entries)
    {
        if (snapshot.nearbyObjects == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.nearbyObjects.Count; i++)
        {
            SceneContextObject contextObject = snapshot.nearbyObjects[i];

            if (contextObject == null)
            {
                continue;
            }

            ContextEntry entry = new ContextEntry(
                KnowledgeTextUtil.SafeDebugText(contextObject.objectId),
                "Nearby: " + KnowledgeTextUtil.SafeDebugText(contextObject.displayName) + " (" + KnowledgeTextUtil.SafeDebugText(contextObject.objectType) + ")",
                ContextSourceType.SceneContextObject,
                ContextVisibility.NearbySceneContext,
                true);
            entry.relatedObjectId = KnowledgeTextUtil.SafeOrEmpty(contextObject.objectId);
            entry.tags = contextObject.tags;
            entries.Add(entry);
        }
    }

    private static void GatherKnowledgeEntries(ContextSnapshot snapshot, List<ContextEntry> entries)
    {
        if (snapshot.retrievedKnowledge == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.retrievedKnowledge.Count; i++)
        {
            KnowledgeEntry knowledge = snapshot.retrievedKnowledge[i];

            if (knowledge == null)
            {
                continue;
            }

            ContextEntry entry = new ContextEntry(
                KnowledgeTextUtil.SafeDebugText(knowledge.id),
                KnowledgeTextUtil.SafeDebugText(knowledge.title),
                ContextSourceType.KnowledgeBase,
                ContextVisibility.RetrievedKnowledge,
                true);
            entry.tags = knowledge.tags;
            entry.score = knowledge.importance;
            entries.Add(entry);
        }
    }

    private static void GatherPlayerClaimEntries(ContextSnapshot snapshot, List<ContextEntry> entries)
    {
        if (string.IsNullOrEmpty(snapshot.playerMessage))
        {
            return;
        }

        // What the player says in dialogue is a PlayerClaim: the NPC may react to it, but must not
        // treat it as a fact it personally witnessed unless another included source supports it.
        AddIncludedEntry(entries, "player_claim", "Player states: " + snapshot.playerMessage.Trim(),
            ContextSourceType.DialogueMemory, ContextVisibility.PlayerClaim, null);
    }

    private static void GatherPrivatePlayerEntries(ContextSnapshot snapshot, List<ContextEntry> entries)
    {
        PlayerState playerState = snapshot.playerState;

        if (playerState == null)
        {
            return;
        }

        // PRIVACY RULE: private player discoveries never become NPC-owned knowledge automatically.
        const string privateReason = "Private player discovery; the NPC has no way to know this unless the player says it (PlayerClaim) or it becomes a public event.";

        AddExcludedEntries(entries, "private_known_fact", playerState.knownFacts,
            ContextSourceType.PlayerState, ContextVisibility.PrivateToPlayer, privateReason);
        AddExcludedEntries(entries, "private_completed_action", playerState.completedActions,
            ContextSourceType.PlayerState, ContextVisibility.PrivateToPlayer, privateReason);

        // heldItems is the player's private inventory/history; only visibleHeldItem is observable.
        AddExcludedEntries(entries, "private_held_item", playerState.heldItems,
            ContextSourceType.PlayerState, ContextVisibility.PrivateToPlayer,
            "Carried in the player's pack; not visibly observable unless equipped as the visible held item.");
    }

    private static void AddVisibleStateEntry(List<ContextEntry> entries, string id, string label, string value, string emptyValue)
    {
        if (string.IsNullOrEmpty(value) || string.Equals(value.Trim(), emptyValue, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AddIncludedEntry(entries, id, label + ": " + value.Trim(),
            ContextSourceType.PlayerState, ContextVisibility.VisibleOnPlayer, null);
    }

    private static void AddIncludedEntry(List<ContextEntry> entries, string id, string text,
        ContextSourceType sourceType, ContextVisibility visibility, List<string> tags)
    {
        ContextEntry entry = new ContextEntry(id, text, sourceType, visibility, true);

        if (tags != null)
        {
            entry.tags = tags;
        }

        entries.Add(entry);
    }

    private static void AddTextEntries(List<ContextEntry> entries, string idPrefix, List<string> values,
        ContextSourceType sourceType, ContextVisibility visibility)
    {
        if (values == null)
        {
            return;
        }

        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i];

            if (string.IsNullOrEmpty(value) || value.Trim().Length == 0)
            {
                continue;
            }

            entries.Add(new ContextEntry(idPrefix + "_" + i, value.Trim(), sourceType, visibility, true));
        }
    }

    private static void AddExcludedEntries(List<ContextEntry> entries, string idPrefix, List<string> values,
        ContextSourceType sourceType, ContextVisibility visibility, string reason)
    {
        if (values == null)
        {
            return;
        }

        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i];

            if (string.IsNullOrEmpty(value) || value.Trim().Length == 0)
            {
                continue;
            }

            ContextEntry entry = new ContextEntry(idPrefix + "_" + i, value.Trim(), sourceType, visibility, false);
            entry.exclusionReason = reason;
            entries.Add(entry);
        }
    }

    private static void AddReason(List<string> reasons, string reason)
    {
        if (reasons == null || string.IsNullOrEmpty(reason))
        {
            return;
        }

        if (!KnowledgeTextUtil.ContainsIgnoreCase(reasons, reason))
        {
            reasons.Add(reason);
        }
    }
}
