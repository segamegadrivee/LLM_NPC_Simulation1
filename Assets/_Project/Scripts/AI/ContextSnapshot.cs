using System.Collections.Generic;
using System.Text;

public class ContextSnapshot
{
    public NPCProfile npcProfile;
    public PlayerState playerState;
    public WorldState worldState;
    public NPCState npcState;
    public List<SceneContextObject> nearbyObjects = new List<SceneContextObject>();
    public List<WorldEvent> recentRelevantEvents = new List<WorldEvent>();
    public List<KnowledgeEntry> retrievedKnowledge = new List<KnowledgeEntry>();
    public List<DialogueMessage> recentDialogueHistory = new List<DialogueMessage>();
    public List<string> contextSourceReasons = new List<string>();
    public string playerMessage;

    // Context Availability Layer (provenance/visibility for every considered piece of context).
    // contextEntries holds everything; includedEntries/excludedEntries are convenience views.
    public List<ContextEntry> contextEntries = new List<ContextEntry>();
    public List<ContextEntry> includedEntries = new List<ContextEntry>();
    public List<ContextEntry> excludedEntries = new List<ContextEntry>();

    public string GetDebugText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("========== Context Snapshot ==========");

        builder.AppendLine();
        builder.AppendLine("---- NPC Profile ----");
        builder.AppendLine(npcProfile != null ? npcProfile.GetProfileContextText() : "None");

        builder.AppendLine();
        builder.AppendLine("---- Player State ----");
        builder.AppendLine(playerState != null ? playerState.GetPlayerStateText() : "None");

        builder.AppendLine();
        builder.AppendLine("---- World State ----");
        builder.AppendLine(worldState != null ? worldState.GetWorldStateText() : "None");

        builder.AppendLine();
        builder.AppendLine("---- NPC Personal State ----");
        builder.AppendLine(npcState != null ? npcState.GetStateText() : "None");

        builder.AppendLine();
        builder.AppendLine("---- Nearby Scene Objects ----");
        if (nearbyObjects == null || nearbyObjects.Count == 0)
        {
            builder.AppendLine("None");
        }
        else
        {
            for (int i = 0; i < nearbyObjects.Count; i++)
            {
                if (nearbyObjects[i] != null)
                {
                    builder.AppendLine(nearbyObjects[i].GetContextText());
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("---- Recent Relevant Events ----");
        if (recentRelevantEvents == null || recentRelevantEvents.Count == 0)
        {
            builder.AppendLine("None");
        }
        else
        {
            for (int i = 0; i < recentRelevantEvents.Count; i++)
            {
                if (recentRelevantEvents[i] != null)
                {
                    builder.AppendLine(recentRelevantEvents[i].GetShortText());
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("---- Retrieved Knowledge ----");
        if (retrievedKnowledge == null || retrievedKnowledge.Count == 0)
        {
            builder.AppendLine("None");
        }
        else
        {
            for (int i = 0; i < retrievedKnowledge.Count; i++)
            {
                if (retrievedKnowledge[i] != null)
                {
                    builder.AppendLine(retrievedKnowledge[i].GetKnowledgeText());
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("---- RECENT DIALOGUE HISTORY ----");
        if (recentDialogueHistory == null || recentDialogueHistory.Count == 0)
        {
            builder.AppendLine("None");
        }
        else
        {
            for (int i = 0; i < recentDialogueHistory.Count; i++)
            {
                DialogueMessage message = recentDialogueHistory[i];

                if (message != null)
                {
                    builder.AppendLine((string.IsNullOrEmpty(message.speaker) ? "Unknown" : message.speaker) + ": " + (string.IsNullOrEmpty(message.text) ? "..." : message.text));
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("---- Player Message ----");
        builder.AppendLine(string.IsNullOrEmpty(playerMessage) ? "None" : playerMessage);

        builder.AppendLine();
        builder.AppendLine("---- Context Source Reasons ----");
        if (contextSourceReasons == null || contextSourceReasons.Count == 0)
        {
            builder.AppendLine("None");
        }
        else
        {
            for (int i = 0; i < contextSourceReasons.Count; i++)
            {
                if (!string.IsNullOrEmpty(contextSourceReasons[i]))
                {
                    builder.AppendLine("- " + contextSourceReasons[i]);
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("---- Included Context Entries (allowed for this NPC) ----");
        AppendEntryLines(builder, includedEntries);

        builder.AppendLine();
        builder.AppendLine("---- Excluded Context Entries (not NPC-owned knowledge) ----");
        AppendEntryLines(builder, excludedEntries);

        return builder.ToString();
    }

    private static void AppendEntryLines(StringBuilder builder, List<ContextEntry> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            builder.AppendLine("None");
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
            {
                builder.AppendLine("- " + entries[i].GetDebugLine());
            }
        }
    }
}
