using System.Collections.Generic;
using UnityEngine;

public class NPCStateStore : MonoBehaviour
{
    public static NPCStateStore Instance { get; private set; }

    public List<NPCState> states = new List<NPCState>();
    public bool debugLogs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple NPCStateStore instances found. Using the first one.", this);
            return;
        }

        Instance = this;
    }

    public NPCState GetOrCreateState(string npcId)
    {
        string key = NormalizeNpcId(npcId);

        if (states == null)
        {
            states = new List<NPCState>();
        }

        for (int i = 0; i < states.Count; i++)
        {
            NPCState state = states[i];

            if (state != null && string.Equals(NormalizeNpcId(state.npcId), key, System.StringComparison.OrdinalIgnoreCase))
            {
                return state;
            }
        }

        NPCState newState = new NPCState
        {
            npcId = key,
            mood = "neutral",
            trustToPlayer = "medium",
            personalEvents = new List<string>()
        };

        states.Add(newState);
        return newState;
    }

    public void SetMood(string npcId, string mood)
    {
        NPCState state = GetOrCreateState(npcId);
        state.mood = HasText(mood) ? mood.Trim() : "neutral";

        if (debugLogs)
        {
            Debug.Log("NPCStateStore set mood for " + state.npcId + ": " + state.mood, this);
        }
    }

    public void SetTrust(string npcId, string trust)
    {
        NPCState state = GetOrCreateState(npcId);
        state.trustToPlayer = HasText(trust) ? trust.Trim() : "medium";

        if (debugLogs)
        {
            Debug.Log("NPCStateStore set trust for " + state.npcId + ": " + state.trustToPlayer, this);
        }
    }

    public void AddPersonalEvent(string npcId, string eventDescription)
    {
        NPCState state = GetOrCreateState(npcId);
        state.AddPersonalEvent(eventDescription);

        if (debugLogs && HasText(eventDescription))
        {
            Debug.Log("NPCStateStore added personal event for " + state.npcId + ": " + eventDescription.Trim(), this);
        }
    }

    public void RegisterAggressionAgainstNpc(string npcId, string description)
    {
        NPCState state = GetOrCreateState(npcId);
        state.mood = "angry";
        state.trustToPlayer = "low";
        state.AddPersonalEvent(description);

        if (debugLogs)
        {
            Debug.Log("NPCStateStore registered aggression against " + state.npcId + ": " + description, this);
        }
    }

    private static string NormalizeNpcId(string npcId)
    {
        return HasText(npcId) ? npcId.Trim().ToLowerInvariant() : "unknown";
    }

    private static bool HasText(string value)
    {
        return !string.IsNullOrEmpty(value) && value.Trim().Length > 0;
    }
}
