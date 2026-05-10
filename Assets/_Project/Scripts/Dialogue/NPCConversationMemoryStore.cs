using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Short-term per-NPC dialogue memory for the current play session.
public class NPCConversationMemoryStore : MonoBehaviour
{
    public static NPCConversationMemoryStore Instance { get; private set; }

    public int maxMessagesPerNpc = 12;

    private readonly Dictionary<string, List<DialogueMessage>> npcHistories = new Dictionary<string, List<DialogueMessage>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple NPCConversationMemoryStore instances found. Using the first one.", this);
            return;
        }

        Instance = this;
    }

    public List<DialogueMessage> GetHistory(string npcId)
    {
        string key = NormalizeNpcId(npcId);

        if (!npcHistories.TryGetValue(key, out List<DialogueMessage> history))
        {
            return new List<DialogueMessage>();
        }

        List<DialogueMessage> copy = new List<DialogueMessage>();

        for (int i = 0; i < history.Count; i++)
        {
            copy.Add(CopyMessage(history[i]));
        }

        return copy;
    }

    public void AddMessage(string npcId, DialogueMessage message)
    {
        if (message == null)
        {
            return;
        }

        string key = NormalizeNpcId(npcId);

        if (!npcHistories.TryGetValue(key, out List<DialogueMessage> history))
        {
            history = new List<DialogueMessage>();
            npcHistories.Add(key, history);
        }

        history.Add(CopyMessage(message));
        TrimHistory(history);
    }

    public void ClearHistory(string npcId)
    {
        npcHistories.Remove(NormalizeNpcId(npcId));
    }

    public void ClearAll()
    {
        npcHistories.Clear();
    }

    public string GetHistoryText(string npcId, int maxMessages)
    {
        List<DialogueMessage> history = GetHistory(npcId);

        if (history.Count == 0)
        {
            return "None";
        }

        int count = maxMessages > 0 ? Mathf.Min(maxMessages, history.Count) : history.Count;
        int startIndex = Mathf.Max(0, history.Count - count);
        StringBuilder builder = new StringBuilder();

        for (int i = startIndex; i < history.Count; i++)
        {
            DialogueMessage message = history[i];

            if (message == null)
            {
                continue;
            }

            builder.AppendLine(SafeText(message.speaker, "Unknown") + ": " + SafeText(message.text, "..."));
        }

        return builder.Length > 0 ? builder.ToString() : "None";
    }

    private void TrimHistory(List<DialogueMessage> history)
    {
        int maxMessages = Mathf.Max(1, maxMessagesPerNpc);

        while (history.Count > maxMessages)
        {
            history.RemoveAt(0);
        }
    }

    private static DialogueMessage CopyMessage(DialogueMessage message)
    {
        if (message == null)
        {
            return null;
        }

        return new DialogueMessage
        {
            speaker = message.speaker,
            text = message.text
        };
    }

    private static string NormalizeNpcId(string npcId)
    {
        return string.IsNullOrEmpty(npcId) ? "unknown" : npcId.Trim();
    }

    private static string SafeText(string value, string fallback)
    {
        return string.IsNullOrEmpty(value) ? fallback : value;
    }
}
