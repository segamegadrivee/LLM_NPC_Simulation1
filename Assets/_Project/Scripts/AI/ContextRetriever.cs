using System.Collections.Generic;
using UnityEngine;

// Place one ContextRetriever on GameSystems and assign the KnowledgeBase asset.
public class ContextRetriever : MonoBehaviour
{
    public static ContextRetriever Instance { get; private set; }

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
            int score = ScoreEntry(entry, npc, nearbyObjects, playerMessage, playerState, worldState, npcState, relevantEvents);

            if (score > 0)
            {
                scoredEntries.Add(new ScoredKnowledgeEntry(entry, score));
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
        int count = Mathf.Min(maxKnowledgeEntries, scoredEntries.Count);

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

        List<DebugKnowledgeRetrievalEntry> positiveEntries = new List<DebugKnowledgeRetrievalEntry>();

        for (int i = 0; i < result.Count; i++)
        {
            if (result[i] != null && result[i].finalScore > 0)
            {
                positiveEntries.Add(result[i]);
            }
        }

        positiveEntries.Sort(delegate(DebugKnowledgeRetrievalEntry a, DebugKnowledgeRetrievalEntry b)
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

        int includedCount = Mathf.Min(maxKnowledgeEntries, positiveEntries.Count);

        for (int i = 0; i < positiveEntries.Count; i++)
        {
            DebugKnowledgeRetrievalEntry debugEntry = positiveEntries[i];
            debugEntry.rank = i + 1;

            if (i < includedCount)
            {
                debugEntry.includedByRetriever = true;
                debugEntry.finalDecisionReason = "RETRIEVED: positive score and rank " + debugEntry.rank + " is within maxKnowledgeEntries (" + maxKnowledgeEntries + ").";
            }
            else
            {
                debugEntry.includedByRetriever = false;
                debugEntry.finalDecisionReason = "SKIPPED: scored " + debugEntry.finalScore + " but rank " + debugEntry.rank + " is outside maxKnowledgeEntries (" + maxKnowledgeEntries + ").";
            }
        }

        for (int i = 0; i < result.Count; i++)
        {
            DebugKnowledgeRetrievalEntry debugEntry = result[i];

            if (debugEntry != null && debugEntry.finalScore <= 0)
            {
                debugEntry.includedByRetriever = false;
                debugEntry.finalDecisionReason = "SKIPPED: final score is 0, so RetrieveRelevantKnowledge did not include it.";
            }
        }

        return result;
    }

    private int ScoreEntry(
        KnowledgeEntry entry,
        NPCProfile npc,
        List<SceneContextObject> nearbyObjects,
        string playerMessage,
        PlayerState playerState,
        WorldState worldState,
        NPCState npcState,
        List<WorldEvent> relevantEvents)
    {
        if (entry == null)
        {
            return 0;
        }

        if (!IsKnowledgeAllowedForNpc(entry, npc))
        {
            return 0;
        }

        int score = 0;

        if (npc != null && ContainsIgnoreCase(entry.knownByNpcIds, npc.npcId))
        {
            score += 3;
        }
        else if (IsPublicKnowledge(entry))
        {
            score += 1;
        }

        if (npc != null && HasOverlap(entry.tags, npc.knowledgeTags))
        {
            score += 2;
        }

        if (HasRelatedNearbyObject(entry, nearbyObjects))
        {
            score += 2;
        }

        if (HasNearbyObjectTagOverlap(entry, nearbyObjects))
        {
            score += 1;
        }

        if (PlayerMessageContainsAnyTag(playerMessage, entry.tags))
        {
            score += 1;
        }

        if (PlayerMessageContainsTitlePart(playerMessage, entry.title))
        {
            score += 1;
        }

        if (PlayerVisibleStateMatches(entry, playerState))
        {
            score += 2;
        }

        if (WorldStateMatches(entry, worldState))
        {
            score += 2;
        }

        if (NpcStateMatches(entry, npcState))
        {
            score += 1;
        }

        if (RelevantEventsMatch(entry, relevantEvents))
        {
            score += 2;
        }

        score += Mathf.Max(0, entry.importance);
        return score;
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
            debugEntry.finalDecisionReason = "SKIPPED: KnowledgeEntry is null.";
            return debugEntry;
        }

        int score = 0;
        string npcId = npc != null ? npc.npcId : null;

        if (!IsKnowledgeAllowedForNpc(entry, npc))
        {
            debugEntry.skippedReasons.Add("KnowledgeEntry is private to another NPC. Empty knownByNpcIds means public; otherwise current npcId must be listed.");
            debugEntry.finalScore = 0;
            debugEntry.finalDecisionReason = "SKIPPED: knowledge access control rejected this entry for current NPC '" + SafeDebugText(npcId) + "'.";
            return debugEntry;
        }

        if (npc != null && ContainsIgnoreCase(entry.knownByNpcIds, npc.npcId))
        {
            score += 3;
            debugEntry.retrievalReasons.Add("knownByNpcIds contains current NPC id '" + SafeDebugText(npc.npcId) + "' (+3).");
        }
        else if (IsPublicKnowledge(entry))
        {
            score += 1;
            debugEntry.retrievalReasons.Add("knownByNpcIds is empty or contains public, so this is public knowledge (+1).");
        }
        else if (npc == null)
        {
            debugEntry.skippedReasons.Add("knownByNpcIds could not be checked because current NPC is not available.");
        }
        else
        {
            debugEntry.skippedReasons.Add("knownByNpcIds does not contain current NPC id '" + SafeDebugText(npcId) + "'.");
        }

        List<string> tagOverlap = GetOverlap(entry.tags, npc != null ? npc.knowledgeTags : null);

        if (tagOverlap.Count > 0)
        {
            score += 2;
            debugEntry.retrievalReasons.Add("KnowledgeEntry.tags overlaps NPCProfile.knowledgeTags: " + FormatDebugList(tagOverlap) + " (+2).");
        }
        else if (npc == null)
        {
            debugEntry.skippedReasons.Add("NPCProfile.knowledgeTags could not be checked because current NPC is not available.");
        }
        else
        {
            debugEntry.skippedReasons.Add("KnowledgeEntry.tags has no overlap with NPCProfile.knowledgeTags.");
        }

        List<string> relatedObjectMatches = GetRelatedNearbyObjectMatches(entry, nearbyObjects);

        if (relatedObjectMatches.Count > 0)
        {
            score += 2;
            debugEntry.retrievalReasons.Add("relatedObjectIds matches nearby SceneContextObject.objectId: " + FormatDebugList(relatedObjectMatches) + " (+2).");
        }
        else
        {
            debugEntry.skippedReasons.Add("relatedObjectIds does not match any nearby SceneContextObject.objectId.");
        }

        List<string> nearbyTagOverlap = GetNearbyObjectTagOverlap(entry, nearbyObjects);

        if (nearbyTagOverlap.Count > 0)
        {
            score += 1;
            debugEntry.retrievalReasons.Add("KnowledgeEntry.tags overlaps nearby SceneContextObject tags/state facts: " + FormatDebugList(nearbyTagOverlap) + " (+1).");
        }
        else
        {
            debugEntry.skippedReasons.Add("KnowledgeEntry.tags does not overlap nearby SceneContextObject tags/state facts.");
        }

        List<string> messageTagMatches = GetPlayerMessageMatchingTags(playerMessage, entry.tags);

        if (messageTagMatches.Count > 0)
        {
            score += 1;
            debugEntry.retrievalReasons.Add("player message contains tag: " + FormatDebugList(messageTagMatches) + " (+1).");
        }
        else
        {
            debugEntry.skippedReasons.Add("player message does not contain any KnowledgeEntry.tags.");
        }

        List<string> titleWordMatches = GetPlayerMessageMatchingTitleWords(playerMessage, entry.title);

        if (titleWordMatches.Count > 0)
        {
            score += 1;
            debugEntry.retrievalReasons.Add("player message contains title word: " + FormatDebugList(titleWordMatches) + " (+1).");
        }
        else
        {
            debugEntry.skippedReasons.Add("player message does not contain a KnowledgeEntry.title word longer than 3 characters.");
        }

        List<string> visibleMatches = GetPlayerVisibleStateMatches(entry, playerState);

        if (visibleMatches.Count > 0)
        {
            score += 2;
            debugEntry.retrievalReasons.Add("visible player state matches KnowledgeEntry tags/title: " + FormatDebugList(visibleMatches) + " (+2).");
        }
        else
        {
            debugEntry.skippedReasons.Add("visible player state does not match KnowledgeEntry tags/title.");
        }

        List<string> worldMatches = GetWorldStateMatches(entry, worldState);

        if (worldMatches.Count > 0)
        {
            score += 2;
            debugEntry.retrievalReasons.Add("WorldState current/global facts match KnowledgeEntry tags/title: " + FormatDebugList(worldMatches) + " (+2).");
        }
        else
        {
            debugEntry.skippedReasons.Add("WorldState current/global facts do not match KnowledgeEntry tags/title.");
        }

        List<string> npcStateMatches = GetNpcStateMatches(entry, npcState);

        if (npcStateMatches.Count > 0)
        {
            score += 1;
            debugEntry.retrievalReasons.Add("NPCState mood/trust/personal events match KnowledgeEntry tags/title: " + FormatDebugList(npcStateMatches) + " (+1).");
        }
        else
        {
            debugEntry.skippedReasons.Add("NPCState did not match KnowledgeEntry tags/title.");
        }

        List<string> eventMatches = GetRelevantEventMatches(entry, relevantEvents);

        if (eventMatches.Count > 0)
        {
            score += 2;
            debugEntry.retrievalReasons.Add("Recent relevant events match KnowledgeEntry tags/title: " + FormatDebugList(eventMatches) + " (+2).");
        }
        else
        {
            debugEntry.skippedReasons.Add("Recent relevant events did not match KnowledgeEntry tags/title.");
        }

        int importanceScore = Mathf.Max(0, entry.importance);

        if (importanceScore > 0)
        {
            score += importanceScore;
            debugEntry.retrievalReasons.Add("importance contributes +" + importanceScore + ".");
        }
        else
        {
            debugEntry.skippedReasons.Add("importance is " + entry.importance + ", so it contributes +0.");
        }

        debugEntry.finalScore = score;
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
            GameObject storeObject = new GameObject("NPCStateStore");
            npcStateStore = storeObject.AddComponent<NPCStateStore>();
        }

        if (worldEventLog == null)
        {
            worldEventLog = WorldEventLog.Instance != null ? WorldEventLog.Instance : FindFirstObjectByType<WorldEventLog>();
        }

        if (worldEventLog == null)
        {
            GameObject eventLogObject = new GameObject("WorldEventLog");
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
                    AddReason(reasons, "source: npc_allowed_knowledge - KnowledgeEntry '" + SafeDebugText(entry.id) + "' passed knownByNpcIds access rules.");
                }
            }
        }

        if (snapshot.recentDialogueHistory != null && snapshot.recentDialogueHistory.Count > 0)
        {
            AddReason(reasons, "source: dialogue_memory - only history for the active npcId was retrieved.");
        }

        return reasons;
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

    private static bool HasNearbyObjectTagOverlap(KnowledgeEntry entry, List<SceneContextObject> nearbyObjects)
    {
        return GetNearbyObjectTagOverlap(entry, nearbyObjects).Count > 0;
    }

    private static List<string> GetNearbyObjectTagOverlap(KnowledgeEntry entry, List<SceneContextObject> nearbyObjects)
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

            AddEntryMatchesFromTerms(result, entry, contextObject.tags);
            AddEntryMatchesFromTerms(result, entry, contextObject.stateFacts);
            AddEntryMatchFromTerm(result, entry, contextObject.objectType);
            AddEntryMatchFromTerm(result, entry, contextObject.displayName);
        }

        return result;
    }

    private static bool PlayerVisibleStateMatches(KnowledgeEntry entry, PlayerState playerState)
    {
        return GetPlayerVisibleStateMatches(entry, playerState).Count > 0;
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

    private static bool WorldStateMatches(KnowledgeEntry entry, WorldState worldState)
    {
        return GetWorldStateMatches(entry, worldState).Count > 0;
    }

    private static List<string> GetWorldStateMatches(KnowledgeEntry entry, WorldState worldState)
    {
        List<string> result = new List<string>();

        if (entry == null || worldState == null)
        {
            return result;
        }

        AddEntryMatchFromTerm(result, entry, worldState.villageMood);
        AddEntryMatchFromTerm(result, entry, worldState.currentEvent);
        AddEntryMatchesFromTerms(result, entry, worldState.globalFacts);

        if (worldState.churchBellMissing)
        {
            AddEntryMatchFromTerm(result, entry, "bell_missing");
            AddEntryMatchFromTerm(result, entry, "missing bell");
        }
        else
        {
            AddEntryMatchFromTerm(result, entry, "bell_found");
            AddEntryMatchFromTerm(result, entry, "bell found");
        }

        return result;
    }

    private static bool NpcStateMatches(KnowledgeEntry entry, NPCState npcState)
    {
        return GetNpcStateMatches(entry, npcState).Count > 0;
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
        return result;
    }

    private static bool RelevantEventsMatch(KnowledgeEntry entry, List<WorldEvent> relevantEvents)
    {
        return GetRelevantEventMatches(entry, relevantEvents).Count > 0;
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

    public class DebugKnowledgeRetrievalEntry
    {
        public KnowledgeEntry entry;
        public int finalScore;
        public int rank = -1;
        public bool includedByRetriever;
        public string finalDecisionReason = string.Empty;
        public List<string> retrievalReasons = new List<string>();
        public List<string> skippedReasons = new List<string>();
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
