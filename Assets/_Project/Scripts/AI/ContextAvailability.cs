using System.Collections.Generic;

// Context Availability Layer.
// Each context item records its source, visibility, and inclusion decision.
// Private player discoveries are not treated as NPC knowledge unless the player states them
// in dialogue or they become public world state/events.

public enum ContextSourceType
{
    PlayerState,
    WorldState,
    WorldEventLog,
    NPCState,
    DialogueMemory,
    SceneContextObject,
    KnowledgeBase,
    NPCProfile,
    Interaction,
    Debug
}

public enum ContextVisibility
{
    // Included visibilities (what the NPC is allowed to perceive or know):
    VisibleOnPlayer,     // Observable appearance of the player (outfit, held item, visible tags).
    NearbySceneContext,  // A SceneContextObject within range of the NPC.
    PublicWorldState,    // Public/global world facts available to everyone.
    PublicWorldEvent,    // A public or global event everyone can reference.
    TargetedEvent,       // An event that specifically targets this NPC.
    NpcPersonalMemory,   // This NPC's own NPCState (mood/trust/personal events).
    NpcProfileKnowledge, // Facts authored on the NPC profile.
    RetrievedKnowledge,  // KnowledgeBase entry retrieved for this NPC.
    PlayerClaim,         // Something the player explicitly said in dialogue.

    // Excluded visibilities (recorded for explanation, never sent as NPC-owned fact):
    PrivateToPlayer,     // A private player discovery the NPC has no way of knowing.
    Excluded             // Generic exclusion (failed access, gating, or relevance).
}

// One explainable unit of context. Lightweight, plain C# class (not serialized) so it can be built
// fresh per snapshot without touching Unity serialization.
public class ContextEntry
{
    public string id = string.Empty;
    public string text = string.Empty;
    public ContextSourceType sourceType;
    public ContextVisibility visibility;
    public List<string> tags = new List<string>();
    public string relatedObjectId = string.Empty;
    public string relatedNpcId = string.Empty;
    public float score;
    public bool includedInPrompt;
    public string exclusionReason = string.Empty;

    public ContextEntry()
    {
    }

    public ContextEntry(
        string id,
        string text,
        ContextSourceType sourceType,
        ContextVisibility visibility,
        bool includedInPrompt)
    {
        this.id = id ?? string.Empty;
        this.text = text ?? string.Empty;
        this.sourceType = sourceType;
        this.visibility = visibility;
        this.includedInPrompt = includedInPrompt;
    }

    public static bool IsExcludedVisibility(ContextVisibility visibility)
    {
        return visibility == ContextVisibility.PrivateToPlayer || visibility == ContextVisibility.Excluded;
    }

    public string GetDebugLine()
    {
        string status = includedInPrompt ? "INCLUDED" : "EXCLUDED";
        string detail = includedInPrompt
            ? string.Empty
            : (string.IsNullOrEmpty(exclusionReason) ? string.Empty : " (" + exclusionReason + ")");

        return "[" + status + "] " + sourceType + " / " + visibility + ": " +
            (string.IsNullOrEmpty(text) ? id : text) + detail;
    }
}
