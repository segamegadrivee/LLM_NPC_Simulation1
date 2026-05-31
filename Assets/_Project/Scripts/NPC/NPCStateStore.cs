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

    private static string NormalizeNpcId(string npcId)
    {
        return HasText(npcId) ? npcId.Trim().ToLowerInvariant() : "unknown";
    }

    private static bool HasText(string value)
    {
        return !string.IsNullOrEmpty(value) && value.Trim().Length > 0;
    }
}
