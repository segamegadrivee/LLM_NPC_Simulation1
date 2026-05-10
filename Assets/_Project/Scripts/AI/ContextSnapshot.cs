using System.Collections.Generic;
using System.Text;

public class ContextSnapshot
{
    public NPCProfile npcProfile;
    public PlayerState playerState;
    public WorldState worldState;
    public List<SceneContextObject> nearbyObjects = new List<SceneContextObject>();
    public List<KnowledgeEntry> retrievedKnowledge = new List<KnowledgeEntry>();
    public List<DialogueMessage> recentDialogueHistory = new List<DialogueMessage>();
    public string playerMessage;

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
        return builder.ToString();
    }
}
