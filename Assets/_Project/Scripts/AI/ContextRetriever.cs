using System.Collections.Generic;
using UnityEngine;

// Place one ContextRetriever on GameSystems and assign the KnowledgeBase asset.
//
// ContextRetriever is the scene-facing coordinator/facade for the context pipeline. It collects the
// current world/player/NPC state and then delegates the heavy lifting to focused helper classes:
//   - SceneContextCollector   : nearby SceneContextObjects + their ids
//   - KnowledgeScorer          : per-entry evaluation (access + gates + activation + scoring)
//       - KnowledgeAccessFilter    : knownByNpcIds / public access gate
//       - WorldStateKnowledgeGate  : missing-bell vs found-bell world-state gate
//       - AppearanceKnowledgeGate  : armor/cloak/weapon visible-appearance gate
//       - KnowledgeTextUtil        : shared text matching/normalization
//   - RetrievalDebugBuilder    : per-entry retrieved/skipped explanation entries
//   - ContextSnapshotBuilder   : context source reasons + Context Availability Layer
// This class keeps the public API (BuildSnapshot / RetrieveRelevantKnowledge /
// DebugExplainKnowledgeRetrieval / FindNearbySceneObjects) and owns selection + ranking.
public class ContextRetriever : MonoBehaviour
{
    public static ContextRetriever Instance { get; private set; }

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
        snapshot.contextSourceReasons = ContextSnapshotBuilder.BuildContextSourceReasons(snapshot);
        ContextSnapshotBuilder.BuildAvailabilityEntries(snapshot);

        if (debugLogs)
        {
            Debug.Log(snapshot.GetDebugText(), this);
        }

        return snapshot;
    }

    public List<SceneContextObject> FindNearbySceneObjects(Transform npcTransform)
    {
        return SceneContextCollector.FindNearby(npcTransform, sceneContextRadius);
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
            KnowledgeRetrievalEvaluation evaluation = KnowledgeScorer.Evaluate(entry, npc, nearbyObjects, playerMessage, playerState, worldState, npcState, relevantEvents);

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
            result.Add(RetrievalDebugBuilder.Build(entries[i], npc, nearbyObjects, playerMessage, playerState, worldState, npcState, relevantEvents));
        }

        List<DebugKnowledgeRetrievalEntry> activatedEntries = new List<DebugKnowledgeRetrievalEntry>();

        for (int i = 0; i < result.Count; i++)
        {
            if (result[i] != null && result[i].allowedForNpc && result[i].hasStrongActivation && result[i].finalScore >= KnowledgeScorer.RetrievalThreshold)
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
        return worldEventLog.GetRelevantEventsForContext(npcId, SceneContextCollector.GetObjectIds(nearbyObjects), maxRelevantWorldEvents);
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
