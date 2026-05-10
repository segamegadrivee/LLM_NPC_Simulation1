using System.Collections.Generic;
using UnityEngine;

// Place one ContextRetriever on GameSystems and assign the KnowledgeBase asset.
public class ContextRetriever : MonoBehaviour
{
    public static ContextRetriever Instance { get; private set; }

    public KnowledgeBase knowledgeBase;
    public NPCConversationMemoryStore conversationMemoryStore;
    public float sceneContextRadius = 20f;
    public int maxKnowledgeEntries = 5;
    public int maxHistoryMessagesForPrompt = 10;
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
        ContextSnapshot snapshot = new ContextSnapshot();
        snapshot.npcProfile = npc;
        snapshot.worldState = WorldState.Instance;
        snapshot.playerState = FindPlayerState();
        snapshot.playerMessage = playerMessage;
        snapshot.nearbyObjects = FindNearbySceneObjects(npcTransform);
        snapshot.retrievedKnowledge = RetrieveRelevantKnowledge(npc, snapshot.nearbyObjects, playerMessage);
        snapshot.recentDialogueHistory = GetRecentDialogueHistory(npc);

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
            int score = ScoreEntry(entry, npc, nearbyObjects, playerMessage);

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

    private int ScoreEntry(KnowledgeEntry entry, NPCProfile npc, List<SceneContextObject> nearbyObjects, string playerMessage)
    {
        if (entry == null)
        {
            return 0;
        }

        int score = 0;

        if (npc != null && ContainsIgnoreCase(entry.knownByNpcIds, npc.npcId))
        {
            score += 3;
        }

        if (npc != null && HasOverlap(entry.tags, npc.knowledgeTags))
        {
            score += 2;
        }

        if (HasRelatedNearbyObject(entry, nearbyObjects))
        {
            score += 2;
        }

        if (PlayerMessageContainsAnyTag(playerMessage, entry.tags))
        {
            score += 1;
        }

        if (PlayerMessageContainsTitlePart(playerMessage, entry.title))
        {
            score += 1;
        }

        score += Mathf.Max(0, entry.importance);
        return score;
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
