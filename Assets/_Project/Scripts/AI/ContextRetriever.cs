using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Place one ContextRetriever on GameSystems and assign the KnowledgeBase asset.
public class ContextRetriever : MonoBehaviour
{
    public static ContextRetriever Instance { get; private set; }

    private const int KnowledgeRetrievalThreshold = 7;
    private const int MaxRetrievedKnowledgeEntriesCap = 5;

    public KnowledgeBase knowledgeBase;
    public NPCConversationMemoryStore conversationMemoryStore;
    public NPCStateStore npcStateStore;
    public WorldEventLog worldEventLog;
    public float sceneContextRadius = 20f;
    public int maxKnowledgeEntries = 5;
    public int maxHistoryMessagesForPrompt = 10;
    public int maxRelevantWorldEvents = 8;
    public bool debugLogs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple ContextRetriever instances found. Using the first one.", this);
            return;
        }

        Instance = this;
    }

    public ContextSnapshot BuildSnapshot(NPCProfile npc, Transform npcTransform, string playerMessage)
    {
        EnsureRuntimeStores();

        ContextSnapshot snapshot = new ContextSnapshot();
        snapshot.npcProfile = npc;
        snapshot.worldState = WorldState.Instance;
        snapshot.playerState = FindPlayerState();
        snapshot.npcState = npcStateStore != null && npc != null ? npcStateStore.GetOrCreateState(npc.npcId) : null;
        snapshot.playerMessage = playerMessage;
        snapshot.nearbyObjects = FindNearbySceneObjects(npcTransform);
        snapshot.recentRelevantEvents = GetRecentRelevantEvents(npc, snapshot.nearbyObjects);
        snapshot.retrievedKnowledge = RetrieveRelevantKnowledge(npc, snapshot.nearbyObjects, playerMessage, snapshot.playerState, snapshot.worldState, snapshot.npcState, snapshot.recentRelevantEvents);
        snapshot.recentDialogueHistory = GetRecentDialogueHistory(npc);
        snapshot.contextSourceReasons = BuildContextSourceReasons(snapshot);
        BuildContextAvailabilityEntries(snapshot);

        if (debugLogs)
        {
            Debug.Log(snapshot.GetDebugText(), this);
        }

        return snapshot;
    }

    public List<SceneContextObject> FindNearbySceneObjects(Transform npcTransform)
    {
        List<SceneContextObject> result = new List<SceneContextObject>();

        if (npcTransform == null)
        {
            return result;
        }

        SceneContextObject[] objects = FindObjectsByType<SceneContextObject>(FindObjectsSortMode.None);

        for (int i = 0; i < objects.Length; i++)
        {
            SceneContextObject contextObject = objects[i];

            if (contextObject == null)
            {
                continue;
            }

            float distance = Vector3.Distance(npcTransform.position, contextObject.transform.position);

            if (distance <= sceneContextRadius)
            {
                result.Add(contextObject);
            }
        }

        result.Sort(delegate(SceneContextObject a, SceneContextObject b)
        {
            float distanceA = Vector3.Distance(npcTransform.position, a.transform.position);
            float distanceB = Vector3.Distance(npcTransform.position, b.transform.position);
            return distanceA.CompareTo(distanceB);
        });

        return result;
    }

    public List<KnowledgeEntry> RetrieveRelevantKnowledge(NPCProfile npc, List<SceneContextObject> nearbyObjects, string playerMessage)
    {
        EnsureRuntimeStores();
        PlayerState playerState = FindPlayerState();
        WorldState worldState = WorldState.Instance;
        NPCState npcState = npcStateStore != null && npc != null ? npcStateStore.GetOrCreateState(npc.npcId) : null;
        List<WorldEvent> relevantEvents = GetRecentRelevantEvents(npc, nearbyObjects);
        return RetrieveRelevantKnowledge(npc, nearbyObjects, playerMessage, playerState, worldState, npcState, relevantEvents);
    }

    public List<KnowledgeEntry> RetrieveRelevantKnowledge(
        NPCProfile npc,
        List<SceneContextObject> nearbyObjects,
        string playerMessage,
        PlayerState playerState,
        WorldState worldState,
        NPCState npcState,
        List<WorldEvent> relevantEvents)
    {
        List<ScoredKnowledgeEntry> scoredEntries = new List<ScoredKnowledgeEntry>();

        if (knowledgeBase == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning("ContextRetriever has no KnowledgeBase assigned.", this);
            }

            return new List<KnowledgeEntry>();
        }

        List<KnowledgeEntry> entries = knowledgeBase.GetAllEntries();

        for (int i = 0; i < entries.Count; i++)
        {
            KnowledgeEntry entry = entries[i];
            KnowledgeRetrievalEvaluation evaluation = EvaluateKnowledgeEntry(entry, npc, nearbyObjects, playerMessage, playerState, worldState, npcState, relevantEvents);

            if (evaluation.IsEligibleForRetrieval)
            {
                scoredEntries.Add(new ScoredKnowledgeEntry(entry, evaluation.score));
            }
        }

        scoredEntries.Sort(delegate(ScoredKnowledgeEntry a, ScoredKnowledgeEntry b)
        {
            int scoreCompare = b.score.CompareTo(a.score);

            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            int importanceCompare = b.entry.importance.CompareTo(a.entry.importance);

            if (importanceCompare != 0)
            {
                return importanceCompare;
            }

            return string.Compare(a.entry.title, b.entry.title, System.StringComparison.OrdinalIgnoreCase);
        });

        List<KnowledgeEntry> result = new List<KnowledgeEntry>();
        int count = Mathf.Min(GetKnowledgeResultLimit(), scoredEntries.Count);

        for (int i = 0; i < count; i++)
        {
            result.Add(scoredEntries[i].entry);
        }

        if (debugLogs)
        {
            Debug.Log("Retrieved " + result.Count + " knowledge entries for " + (npc != null ? npc.npcName : "unknown NPC") + ".", this);
        }

        return result;
    }

    public List<DebugKnowledgeRetrievalEntry> DebugExplainKnowledgeRetrieval(NPCProfile npc, List<SceneContextObject> nearbyObjects, string playerMessage)
    {
        EnsureRuntimeStores();
        PlayerState playerState = FindPlayerState();
        WorldState worldState = WorldState.Instance;
        NPCState npcState = npcStateStore != null && npc != null ? npcStateStore.GetOrCreateState(npc.npcId) : null;
        List<WorldEvent> relevantEvents = GetRecentRelevantEvents(npc, nearbyObjects);
        return DebugExplainKnowledgeRetrieval(npc, nearbyObjects, playerMessage, playerState, worldState, npcState, relevantEvents);
    }

    public List<DebugKnowledgeRetrievalEntry> DebugExplainKnowledgeRetrieval(
        NPCProfile npc,
        List<SceneContextObject> nearbyObjects,
        string playerMessage,
        PlayerState playerState,
        WorldState worldState,
        NPCState npcState,
        List<WorldEvent> relevantEvents)
    {
        List<DebugKnowledgeRetrievalEntry> result = new List<DebugKnowledgeRetrievalEntry>();

        if (knowledgeBase == null)
        {
            return result;
        }

        List<KnowledgeEntry> entries = knowledgeBase.GetAllEntries();

        for (int i = 0; i < entries.Count; i++)
        {
            result.Add(BuildDebugKnowledgeRetrievalEntry(entries[i], npc, nearbyObjects, playerMessage, playerState, worldState, npcState, relevantEvents));
        }

        List<DebugKnowledgeRetrievalEntry> activatedEntries = new List<DebugKnowledgeRetrievalEntry>();

        for (int i = 0; i < result.Count; i++)
        {
            if (result[i] != null && result[i].allowedForNpc && result[i].hasStrongActivation && result[i].finalScore >= KnowledgeRetrievalThreshold)
            {
                activatedEntries.Add(result[i]);
            }
        }

        activatedEntries.Sort(delegate(DebugKnowledgeRetrievalEntry a, DebugKnowledgeRetrievalEntry b)
        {
            int scoreCompare = b.finalScore.CompareTo(a.finalScore);

            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            int importanceCompare = b.entry.importance.CompareTo(a.entry.importance);

            if (importanceCompare != 0)
            {
                return importanceCompare;
            }

            return string.Compare(a.entry.title, b.entry.title, System.StringComparison.OrdinalIgnoreCase);
        });

        int includedCount = Mathf.Min(GetKnowledgeResultLimit(), activatedEntries.Count);

        for (int i = 0; i < activatedEntries.Count; i++)
        {
            DebugKnowledgeRetrievalEntry debugEntry = activatedEntries[i];
            debugEntry.rank = i + 1;

            if (i < includedCount)
            {
                debugEntry.includedByRetriever = true;
                debugEntry.finalDecisionReason = "retrieved_top_ranked";
                debugEntry.retrievalReasons.Add("rank " + debugEntry.rank + " is within retrieval limit " + includedCount + ".");
            }
            else
            {
                debugEntry.includedByRetriever = false;
                debugEntry.finalDecisionReason = "retrieved_allowed_and_activated";
                debugEntry.skippedReasons.Add("Entry passed access, activation, and threshold, but rank " + debugEntry.rank + " is outside the retrieval limit " + includedCount + ".");
            }
        }

        return result;
    }

    private KnowledgeRetrievalEvaluation EvaluateKnowledgeEntry(
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

        evaluation.allowedForNpc = IsKnowledgeAllowedForNpc(entry, npc);

        if (!evaluation.allowedForNpc)
        {
            evaluation.finalDecisionReason = "skipped_not_allowed_for_npc";
            return evaluation;
        }

        evaluation.worldStateBlockReason = GetWorldStateBlockReason(entry, worldState, relevantEvents);

        if (!string.IsNullOrEmpty(evaluation.worldStateBlockReason))
        {
            evaluation.finalDecisionReason = "skipped_no_strong_activation";
            return evaluation;
        }

        evaluation.messageMatches = GetPlayerMessageEntryMatches(playerMessage, entry);
        evaluation.visibleStateMatches = GetPlayerVisibleStateMatches(entry, playerState);
        evaluation.npcStateMatches = GetNpcStateMatches(entry, npcState);
        evaluation.worldEventMatches = GetRelevantEventMatches(entry, relevantEvents);
        evaluation.worldStateMatches = GetWorldStateMatches(entry, worldState, playerMessage, nearbyObjects, evaluation.messageMatches.Count > 0, evaluation.worldEventMatches.Count > 0);
        evaluation.npcProfileTagMatches = GetOverlap(entry.tags, npc != null ? npc.knowledgeTags : null);

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
        else if (evaluation.score < KnowledgeRetrievalThreshold)
        {
            evaluation.finalDecisionReason = "skipped_below_threshold";
        }
        else
        {
            evaluation.finalDecisionReason = "retrieved_allowed_and_activated";
        }

        return evaluation;
    }

    private DebugKnowledgeRetrievalEntry BuildDebugKnowledgeRetrievalEntry(
        KnowledgeEntry entry,
        NPCProfile npc,
        List<SceneContextObject> nearbyObjects,
        string playerMessage,
        PlayerState playerState,
        WorldState worldState,
        NPCState npcState,
        List<WorldEvent> relevantEvents)
    {
        DebugKnowledgeRetrievalEntry debugEntry = new DebugKnowledgeRetrievalEntry();
        debugEntry.entry = entry;

        if (entry == null)
        {
            debugEntry.skippedReasons.Add("KnowledgeEntry is null.");
            debugEntry.finalDecisionReason = "skipped_below_threshold";
            return debugEntry;
        }

        string npcId = npc != null ? npc.npcId : null;
        KnowledgeRetrievalEvaluation evaluation = EvaluateKnowledgeEntry(entry, npc, nearbyObjects, playerMessage, playerState, worldState, npcState, relevantEvents);

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
            debugEntry.skippedReasons.Add("Access gate failed: knownByNpcIds is " + FormatDebugList(entry.knownByNpcIds) + " and current npcId is '" + SafeDebugText(npcId) + "'.");
            return debugEntry;
        }

        if (npc != null && ContainsIgnoreCase(entry.knownByNpcIds, npc.npcId))
        {
            debugEntry.retrievalReasons.Add("Access gate passed: knownByNpcIds contains current NPC id '" + SafeDebugText(npc.npcId) + "'.");
        }
        else if (IsPublicKnowledge(entry))
        {
            debugEntry.retrievalReasons.Add("Access gate passed: knownByNpcIds is empty/public, so this is public knowledge.");
        }

        if (!string.IsNullOrEmpty(evaluation.worldStateBlockReason))
        {
            debugEntry.skippedReasons.Add(evaluation.worldStateBlockReason);
        }

        if (evaluation.hasMessageActivation)
        {
            debugEntry.retrievalReasons.Add("message_activation: true - player message matched entry tags/title/related objects: " + FormatDebugList(evaluation.messageMatches) + " (+8).");
        }
        else
        {
            debugEntry.skippedReasons.Add("message_activation: false - player message did not match entry tags, significant title words, or relatedObjectIds.");
        }

        if (evaluation.hasVisibleStateActivation)
        {
            debugEntry.retrievalReasons.Add("visible_state_activation: true - visible player state matched entry tags/title: " + FormatDebugList(evaluation.visibleStateMatches) + " (+8).");
        }
        else
        {
            debugEntry.skippedReasons.Add("visible_state_activation: false - outfit, held item, reputation, and visible tags did not match this entry.");
        }

        if (evaluation.hasNpcStateActivation)
        {
            debugEntry.retrievalReasons.Add("npc_state_activation: true - NPC mood/trust/personal events matched entry tags/title: " + FormatDebugList(evaluation.npcStateMatches) + " (+7).");
        }
        else
        {
            debugEntry.skippedReasons.Add("npc_state_activation: false - NPC state did not match this entry.");
        }

        if (evaluation.hasWorldEventActivation)
        {
            debugEntry.retrievalReasons.Add("world_event_activation: true - relevant public/global/targeted event matched entry tags/title: " + FormatDebugList(evaluation.worldEventMatches) + " (+8).");
        }
        else
        {
            debugEntry.skippedReasons.Add("world_event_activation: false - recent relevant events did not match this entry.");
        }

        if (evaluation.hasWorldStateActivation)
        {
            debugEntry.retrievalReasons.Add("world_state_activation: true - WorldState directly matched and the message/event made that state relevant: " + FormatDebugList(evaluation.worldStateMatches) + " (+6).");
        }
        else
        {
            debugEntry.skippedReasons.Add("world_state_activation: false - WorldState alone did not directly activate this entry.");
        }

        if (evaluation.hasLocalActivation)
        {
            debugEntry.retrievalReasons.Add("local_activation: true - nearby SceneContextObject matched and the player referred to the place or another strong source activated the entry: " + FormatDebugList(evaluation.rawLocalMatches) + " (+3).");
        }
        else
        {
            debugEntry.skippedReasons.Add("local_activation: false - no local match, or location matched without a place reference/strong activation.");
        }

        if (evaluation.npcProfileTagMatches.Count > 0)
        {
            debugEntry.retrievalReasons.Add("NPCProfile.knowledgeTags overlap: " + FormatDebugList(evaluation.npcProfileTagMatches) + " (+2, not a strong activation source).");
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
        else if (evaluation.score < KnowledgeRetrievalThreshold)
        {
            debugEntry.skippedReasons.Add("Final gate failed: score " + evaluation.score + " is below threshold " + KnowledgeRetrievalThreshold + ".");
        }
        else
        {
            debugEntry.retrievalReasons.Add("Final gate passed: allowed, strongly activated, and score " + evaluation.score + " >= " + KnowledgeRetrievalThreshold + ".");
        }

        return debugEntry;
    }

    private void EnsureRuntimeStores()
    {
        if (conversationMemoryStore == null)
        {
            conversationMemoryStore = NPCConversationMemoryStore.Instance != null ? NPCConversationMemoryStore.Instance : FindFirstObjectByType<NPCConversationMemoryStore>();
        }

        if (npcStateStore == null)
        {
            npcStateStore = NPCStateStore.Instance != null ? NPCStateStore.Instance : FindFirstObjectByType<NPCStateStore>();
        }

        if (npcStateStore == null)
        {
            Debug.LogWarning("ContextRetriever: no NPCStateStore in the scene. Creating a runtime fallback. " +
                "Add a persistent NPCStateStore to GameSystems for the final scene.", this);
            GameObject storeObject = new GameObject("NPCStateStore (runtime fallback)");
            npcStateStore = storeObject.AddComponent<NPCStateStore>();
        }

        if (worldEventLog == null)
        {
            worldEventLog = WorldEventLog.Instance != null ? WorldEventLog.Instance : FindFirstObjectByType<WorldEventLog>();
        }

        if (worldEventLog == null)
        {
            Debug.LogWarning("ContextRetriever: no WorldEventLog in the scene. Creating a runtime fallback. " +
                "Add a persistent WorldEventLog to GameSystems for the final scene.", this);
            GameObject eventLogObject = new GameObject("WorldEventLog (runtime fallback)");
            worldEventLog = eventLogObject.AddComponent<WorldEventLog>();
        }
    }

    private List<WorldEvent> GetRecentRelevantEvents(NPCProfile npc, List<SceneContextObject> nearbyObjects)
    {
        EnsureRuntimeStores();

        if (worldEventLog == null)
        {
            return new List<WorldEvent>();
        }

        string npcId = npc != null ? npc.npcId : null;
        return worldEventLog.GetRelevantEventsForContext(npcId, GetNearbyObjectIds(nearbyObjects), maxRelevantWorldEvents);
    }

    private List<string> BuildContextSourceReasons(ContextSnapshot snapshot)
    {
        List<string> reasons = new List<string>();

        if (snapshot == null)
        {
            return reasons;
        }

        AddReason(reasons, "source: npc_profile - active NPC profile selected by DialogueManager.");
        AddReason(reasons, "source: player_state - PlayerState component attached to player.");
        AddReason(reasons, "source: visible_player_state - equippedOutfit, visibleHeldItem, visibleStatusTags, publicReputation are observable to all NPCs.");
        AddReason(reasons, "source: world_state - public/global WorldState is available to all NPCs.");

        if (snapshot.npcState != null)
        {
            AddReason(reasons, "source: npc_personal_memory - NPCState is loaded only for npcId '" + SafeDebugText(snapshot.npcState.npcId) + "'.");
        }

        if (snapshot.nearbyObjects != null)
        {
            for (int i = 0; i < snapshot.nearbyObjects.Count; i++)
            {
                SceneContextObject contextObject = snapshot.nearbyObjects[i];

                if (contextObject != null)
                {
                    AddReason(reasons, "source: local_environment - nearby SceneContextObject '" + SafeDebugText(contextObject.objectId) + "' is within sceneContextRadius.");
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
                    AddReason(reasons, "source: global_event - " + SafeDebugText(worldEvent.description));
                }
                else if (worldEvent.isPublic)
                {
                    AddReason(reasons, "source: public_event - " + SafeDebugText(worldEvent.description));
                }
                else if (snapshot.npcProfile != null && !string.IsNullOrEmpty(worldEvent.targetNpcId) &&
                    string.Equals(worldEvent.targetNpcId, snapshot.npcProfile.npcId, System.StringComparison.OrdinalIgnoreCase))
                {
                    AddReason(reasons, "source: targeted_event - event targets current NPC '" + SafeDebugText(worldEvent.targetNpcId) + "'.");
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
                    AddReason(reasons, "source: retrieved_knowledge - KnowledgeEntry '" + SafeDebugText(entry.id) + "' passed access, strong activation, threshold, and ranking rules.");
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
    private void BuildContextAvailabilityEntries(ContextSnapshot snapshot)
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

        AddIncludedEntry(entries, "profile_" + SafeDebugText(snapshot.npcProfile.npcId),
            "NPC profile: " + SafeDebugText(snapshot.npcProfile.npcName) + " (" + SafeDebugText(snapshot.npcProfile.role) + ")",
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
        AddVisibleStateEntry(entries, "visible_reputation", "Public reputation", playerState.publicReputation, "unknown");
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

        AddIncludedEntry(entries, "world_event_state", "Current village situation: " + SafeDebugText(worldState.currentEvent),
            ContextSourceType.WorldState, ContextVisibility.PublicWorldState, null);
        AddIncludedEntry(entries, "world_mood", "Village mood: " + SafeDebugText(worldState.villageMood),
            ContextSourceType.WorldState, ContextVisibility.PublicWorldState, null);
        AddTextEntries(entries, "world_fact", worldState.globalFacts,
            ContextSourceType.WorldState, ContextVisibility.PublicWorldState);
    }

    private void GatherWorldEventEntries(ContextSnapshot snapshot, List<ContextEntry> entries)
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
                SafeDebugText(worldEvent.eventId),
                SafeDebugText(worldEvent.description),
                ContextSourceType.WorldEventLog,
                visibility,
                true);
            entry.relatedObjectId = SafeOrEmpty(worldEvent.locationObjectId);
            entry.relatedNpcId = SafeOrEmpty(worldEvent.targetNpcId);
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

        AddIncludedEntry(entries, "npc_mood", "Own mood: " + SafeDebugText(npcState.mood),
            ContextSourceType.NPCState, ContextVisibility.NpcPersonalMemory, null);
        AddIncludedEntry(entries, "npc_trust", "Own trust toward player: " + SafeDebugText(npcState.trustToPlayer),
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
                SafeDebugText(contextObject.objectId),
                "Nearby: " + SafeDebugText(contextObject.displayName) + " (" + SafeDebugText(contextObject.objectType) + ")",
                ContextSourceType.SceneContextObject,
                ContextVisibility.NearbySceneContext,
                true);
            entry.relatedObjectId = SafeOrEmpty(contextObject.objectId);
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
                SafeDebugText(knowledge.id),
                SafeDebugText(knowledge.title),
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

    private static string SafeOrEmpty(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
    }

    private static void AddReason(List<string> reasons, string reason)
    {
        if (reasons == null || string.IsNullOrEmpty(reason))
        {
            return;
        }

        if (!ContainsIgnoreCase(reasons, reason))
        {
            reasons.Add(reason);
        }
    }

    private static List<string> GetNearbyObjectIds(List<SceneContextObject> nearbyObjects)
    {
        List<string> result = new List<string>();

        if (nearbyObjects == null)
        {
            return result;
        }

        for (int i = 0; i < nearbyObjects.Count; i++)
        {
            SceneContextObject contextObject = nearbyObjects[i];

            if (contextObject != null && !string.IsNullOrEmpty(contextObject.objectId) && !ContainsIgnoreCase(result, contextObject.objectId))
            {
                result.Add(contextObject.objectId);
            }
        }

        return result;
    }

    private static bool IsKnowledgeAllowedForNpc(KnowledgeEntry entry, NPCProfile npc)
    {
        if (entry == null)
        {
            return false;
        }

        if (IsPublicKnowledge(entry))
        {
            return true;
        }

        return npc != null && ContainsIgnoreCase(entry.knownByNpcIds, npc.npcId);
    }

    private static bool IsPublicKnowledge(KnowledgeEntry entry)
    {
        if (entry == null || entry.knownByNpcIds == null || entry.knownByNpcIds.Count == 0)
        {
            return true;
        }

        return ContainsIgnoreCase(entry.knownByNpcIds, "public") || ContainsIgnoreCase(entry.knownByNpcIds, "all");
    }

    private static string GetWorldStateBlockReason(KnowledgeEntry entry, WorldState worldState, List<WorldEvent> relevantEvents)
    {
        if (entry == null || worldState == null)
        {
            return string.Empty;
        }

        bool hasBellFoundEvent = HasBellFoundEvent(relevantEvents);

        if ((ContainsIgnoreCase(entry.tags, "bell_found") || ContainsIgnoreCase(entry.tags, "resolved")) &&
            worldState.churchBellMissing &&
            !hasBellFoundEvent)
        {
            return "WorldState gate: bell_found/resolution knowledge is inactive while churchBellMissing is true and no recent bell_found event is relevant.";
        }

        if (ContainsIgnoreCase(entry.tags, "missing_bell") &&
            (!worldState.churchBellMissing || hasBellFoundEvent))
        {
            return "WorldState gate: missing_bell tension knowledge is inactive because the bell has been found or a bell_found event is relevant.";
        }

        return string.Empty;
    }

    private static bool HasBellFoundEvent(List<WorldEvent> relevantEvents)
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

            if (TextContainsSearchTerm(worldEvent.eventType, "bell_found") ||
                TextContainsSearchTerm(worldEvent.description, "bell found") ||
                TextContainsSearchTerm(worldEvent.description, "bell has been found"))
            {
                return true;
            }
        }

        return false;
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

            if (!string.IsNullOrEmpty(contextObject.objectId) && ContainsIgnoreCase(entry.relatedObjectIds, contextObject.objectId) && !ContainsIgnoreCase(result, contextObject.objectId))
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

        if (TextContainsSearchTerm(npcState.mood, "angry") || TextContainsSearchTerm(npcState.mood, "hostile"))
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

            if (TextContainsSearchTerm(personalEvent, "throw") ||
                TextContainsSearchTerm(personalEvent, "threw") ||
                TextContainsSearchTerm(personalEvent, "hit") ||
                TextContainsSearchTerm(personalEvent, "attack") ||
                TextContainsSearchTerm(personalEvent, "aggression") ||
                TextContainsSearchTerm(personalEvent, "aggressive") ||
                TextContainsSearchTerm(personalEvent, "violence") ||
                TextContainsSearchTerm(personalEvent, "violent"))
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

            if (TextContainsSearchTerm(worldEvent.eventType, "bell_found") ||
                TextContainsSearchTerm(worldEvent.description, "bell found") ||
                TextContainsSearchTerm(worldEvent.description, "bell has been found"))
            {
                AddEntryMatchFromTerm(result, entry, "bell_found");
                AddEntryMatchFromTerm(result, entry, "found");
                AddEntryMatchFromTerm(result, entry, "calm");
            }

            if (TextContainsSearchTerm(worldEvent.eventType, "aggression") ||
                TextContainsSearchTerm(worldEvent.description, "aggression") ||
                TextContainsSearchTerm(worldEvent.description, "attack") ||
                TextContainsSearchTerm(worldEvent.description, "throw") ||
                TextContainsSearchTerm(worldEvent.description, "threw") ||
                TextContainsSearchTerm(worldEvent.description, "hit"))
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

        List<string> terms = SplitSearchTerms(term);

        for (int i = 0; i < terms.Count; i++)
        {
            string cleanTerm = terms[i];

            if (EntryMatchesTerm(entry, cleanTerm) && !ContainsIgnoreCase(result, cleanTerm))
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

        if (ContainsIgnoreCase(entry.tags, cleanTerm) || ContainsText(entry.title, cleanTerm) || ContainsText(entry.id, cleanTerm))
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

            if (!string.IsNullOrEmpty(term) && TextContainsExactSearchTerm(text, term) && !ContainsIgnoreCase(result, term))
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

        string[] words = NormalizeSearchText(title).Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].Trim();

            if (word.Length > 3 && TextContainsSearchTerm(text, word) && !ContainsIgnoreCase(result, word))
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

        if (TextContainsSearchTerm(playerMessage, "here") ||
            TextContainsSearchTerm(playerMessage, "this place") ||
            TextContainsSearchTerm(playerMessage, "around here") ||
            TextContainsSearchTerm(playerMessage, "near here") ||
            TextContainsSearchTerm(playerMessage, "nearby") ||
            TextContainsSearchTerm(playerMessage, "near") ||
            TextContainsSearchTerm(playerMessage, "where are we"))
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

            if (TextContainsSearchTerm(playerMessage, contextObject.objectId) ||
                TextContainsSearchTerm(playerMessage, contextObject.displayName) ||
                TextContainsSearchTerm(playerMessage, contextObject.objectType) ||
                AnyTermAppearsInText(playerMessage, contextObject.tags))
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
            (AnyTermAppearsInText(playerMessage, entry.tags) ||
            AnyTermAppearsInText(playerMessage, entry.relatedObjectIds) ||
            PlayerMessageContainsTitlePart(playerMessage, entry.title)))
        {
            return true;
        }

        if (TextContainsSearchTerm(playerMessage, worldState.currentEvent) ||
            TextContainsSearchTerm(playerMessage, worldState.villageMood) ||
            AnyTermAppearsInText(playerMessage, worldState.globalFacts))
        {
            return true;
        }

        return TextContainsSearchTerm(playerMessage, "what now") ||
            TextContainsSearchTerm(playerMessage, "now what") ||
            TextContainsSearchTerm(playerMessage, "what happens now") ||
            TextContainsSearchTerm(playerMessage, "problem") ||
            TextContainsSearchTerm(playerMessage, "problems") ||
            TextContainsSearchTerm(playerMessage, "trouble") ||
            TextContainsSearchTerm(playerMessage, "wrong") ||
            TextContainsSearchTerm(playerMessage, "happened") ||
            TextContainsSearchTerm(playerMessage, "news") ||
            TextContainsSearchTerm(playerMessage, "situation");
    }

    private static bool AnyTermAppearsInText(string text, List<string> terms)
    {
        if (string.IsNullOrEmpty(text) || terms == null)
        {
            return false;
        }

        for (int i = 0; i < terms.Count; i++)
        {
            if (TextContainsSearchTerm(text, terms[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> SplitSearchTerms(string value)
    {
        List<string> result = new List<string>();

        if (string.IsNullOrEmpty(value))
        {
            return result;
        }

        string normalized = value.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");

        if (normalized.Length > 0)
        {
            result.Add(normalized);
        }

        string[] words = normalized.Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].Trim();

            if (word.Length > 2 && !ContainsIgnoreCase(result, word))
            {
                result.Add(word);
            }
        }

        return result;
    }

    private static bool ContainsText(string text, string value)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value))
        {
            return false;
        }

        return text.ToLowerInvariant().Contains(value.Trim().ToLowerInvariant());
    }

    private static bool TextContainsSearchTerm(string text, string searchTerm)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchTerm))
        {
            return false;
        }

        string normalizedText = " " + NormalizeSearchText(text) + " ";
        string normalizedTerm = NormalizeSearchText(searchTerm);

        if (normalizedTerm.Length == 0 || normalizedTerm == "none" || normalizedTerm == "unknown")
        {
            return false;
        }

        if (normalizedText.Contains(" " + normalizedTerm + " "))
        {
            return true;
        }

        string[] words = normalizedTerm.Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].Trim();

            if (word.Length > 3 && normalizedText.Contains(" " + word + " "))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TextContainsExactSearchTerm(string text, string searchTerm)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchTerm))
        {
            return false;
        }

        string normalizedText = " " + NormalizeSearchText(text) + " ";
        string normalizedTerm = NormalizeSearchText(searchTerm);

        if (normalizedTerm.Length == 0 || normalizedTerm == "none" || normalizedTerm == "unknown")
        {
            return false;
        }

        return normalizedText.Contains(" " + normalizedTerm + " ");
    }

    private static string NormalizeSearchText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string lower = value.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");
        StringBuilder builder = new StringBuilder();
        bool lastWasSpace = false;

        for (int i = 0; i < lower.Length; i++)
        {
            char c = lower[i];

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private PlayerState FindPlayerState()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            PlayerState playerState = playerObject.GetComponent<PlayerState>();

            if (playerState != null)
            {
                return playerState;
            }
        }

        return FindFirstObjectByType<PlayerState>();
    }

    private List<DialogueMessage> GetRecentDialogueHistory(NPCProfile npc)
    {
        List<DialogueMessage> result = new List<DialogueMessage>();

        if (maxHistoryMessagesForPrompt <= 0)
        {
            return result;
        }

        if (conversationMemoryStore == null)
        {
            conversationMemoryStore = NPCConversationMemoryStore.Instance;
        }

        if (conversationMemoryStore == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning("ContextRetriever has no NPCConversationMemoryStore assigned.", this);
            }

            return result;
        }

        string npcId = npc != null ? npc.npcId : null;
        List<DialogueMessage> history = conversationMemoryStore.GetHistory(npcId);
        int count = Mathf.Min(maxHistoryMessagesForPrompt, history.Count);
        int startIndex = Mathf.Max(0, history.Count - count);

        for (int i = startIndex; i < history.Count; i++)
        {
            DialogueMessage message = history[i];

            if (message != null)
            {
                result.Add(new DialogueMessage
                {
                    speaker = message.speaker,
                    text = message.text
                });
            }
        }

        return result;
    }

    private static bool HasRelatedNearbyObject(KnowledgeEntry entry, List<SceneContextObject> nearbyObjects)
    {
        if (entry == null || entry.relatedObjectIds == null || nearbyObjects == null)
        {
            return false;
        }

        for (int i = 0; i < nearbyObjects.Count; i++)
        {
            SceneContextObject contextObject = nearbyObjects[i];

            if (contextObject != null && ContainsIgnoreCase(entry.relatedObjectIds, contextObject.objectId))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> GetRelatedNearbyObjectMatches(KnowledgeEntry entry, List<SceneContextObject> nearbyObjects)
    {
        List<string> result = new List<string>();

        if (entry == null || entry.relatedObjectIds == null || nearbyObjects == null)
        {
            return result;
        }

        for (int i = 0; i < nearbyObjects.Count; i++)
        {
            SceneContextObject contextObject = nearbyObjects[i];

            if (contextObject != null && ContainsIgnoreCase(entry.relatedObjectIds, contextObject.objectId) && !ContainsIgnoreCase(result, contextObject.objectId))
            {
                result.Add(contextObject.objectId);
            }
        }

        return result;
    }

    private static bool HasOverlap(List<string> first, List<string> second)
    {
        if (first == null || second == null)
        {
            return false;
        }

        for (int i = 0; i < first.Count; i++)
        {
            if (ContainsIgnoreCase(second, first[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> GetOverlap(List<string> first, List<string> second)
    {
        List<string> result = new List<string>();

        if (first == null || second == null)
        {
            return result;
        }

        for (int i = 0; i < first.Count; i++)
        {
            string value = first[i];

            if (!string.IsNullOrEmpty(value) && ContainsIgnoreCase(second, value) && !ContainsIgnoreCase(result, value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static bool PlayerMessageContainsAnyTag(string playerMessage, List<string> tags)
    {
        if (string.IsNullOrEmpty(playerMessage) || tags == null)
        {
            return false;
        }

        string lowerMessage = playerMessage.ToLowerInvariant();

        for (int i = 0; i < tags.Count; i++)
        {
            string tag = tags[i];

            if (!string.IsNullOrEmpty(tag) && lowerMessage.Contains(tag.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> GetPlayerMessageMatchingTags(string playerMessage, List<string> tags)
    {
        List<string> result = new List<string>();

        if (string.IsNullOrEmpty(playerMessage) || tags == null)
        {
            return result;
        }

        string lowerMessage = playerMessage.ToLowerInvariant();

        for (int i = 0; i < tags.Count; i++)
        {
            string tag = tags[i];

            if (!string.IsNullOrEmpty(tag) && lowerMessage.Contains(tag.ToLowerInvariant()) && !ContainsIgnoreCase(result, tag))
            {
                result.Add(tag);
            }
        }

        return result;
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

    private static List<string> GetPlayerMessageMatchingTitleWords(string playerMessage, string title)
    {
        List<string> result = new List<string>();

        if (string.IsNullOrEmpty(playerMessage) || string.IsNullOrEmpty(title))
        {
            return result;
        }

        string lowerMessage = playerMessage.ToLowerInvariant();
        string[] words = title.ToLowerInvariant().Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].Trim();

            if (word.Length > 3 && lowerMessage.Contains(word) && !ContainsIgnoreCase(result, word))
            {
                result.Add(word);
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

    private static string FormatDebugList(List<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return "None";
        }

        return string.Join(", ", values.ToArray());
    }

    private static string SafeDebugText(string value)
    {
        return string.IsNullOrEmpty(value) ? "None" : value;
    }

    private int GetKnowledgeResultLimit()
    {
        if (maxKnowledgeEntries <= 0)
        {
            return 0;
        }

        return Mathf.Min(maxKnowledgeEntries, MaxRetrievedKnowledgeEntriesCap);
    }

    public class DebugKnowledgeRetrievalEntry
    {
        public KnowledgeEntry entry;
        public int finalScore;
        public int rank = -1;
        public bool allowedForNpc;
        public bool includedByRetriever;
        public bool hasMessageActivation;
        public bool hasVisibleStateActivation;
        public bool hasNpcStateActivation;
        public bool hasWorldEventActivation;
        public bool hasWorldStateActivation;
        public bool hasLocalActivation;
        public bool hasStrongActivation;
        public string finalDecisionReason = string.Empty;
        public List<string> retrievalReasons = new List<string>();
        public List<string> skippedReasons = new List<string>();
    }

    private class KnowledgeRetrievalEvaluation
    {
        public bool allowedForNpc;
        public bool hasMessageActivation;
        public bool hasVisibleStateActivation;
        public bool hasNpcStateActivation;
        public bool hasWorldEventActivation;
        public bool hasWorldStateActivation;
        public bool hasLocalActivation;
        public int score;
        public int importanceScore;
        public string finalDecisionReason = string.Empty;
        public string worldStateBlockReason = string.Empty;
        public List<string> messageMatches = new List<string>();
        public List<string> visibleStateMatches = new List<string>();
        public List<string> npcStateMatches = new List<string>();
        public List<string> worldEventMatches = new List<string>();
        public List<string> worldStateMatches = new List<string>();
        public List<string> rawLocalMatches = new List<string>();
        public List<string> npcProfileTagMatches = new List<string>();

        public bool hasStrongActivation
        {
            get
            {
                return hasMessageActivation ||
                    hasVisibleStateActivation ||
                    hasNpcStateActivation ||
                    hasWorldEventActivation ||
                    hasWorldStateActivation;
            }
        }

        public bool IsEligibleForRetrieval
        {
            get
            {
                return allowedForNpc && hasStrongActivation && score >= KnowledgeRetrievalThreshold;
            }
        }
    }

    private class ScoredKnowledgeEntry
    {
        public KnowledgeEntry entry;
        public int score;

        public ScoredKnowledgeEntry(KnowledgeEntry entry, int score)
        {
            this.entry = entry;
            this.score = score;
        }
    }
}
