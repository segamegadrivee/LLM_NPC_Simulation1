using System.Collections.Generic;
using UnityEngine;

// Updates the player's visible outfit so NPCs can react to appearance. State-only; no dialogue.
// All outfit replacement/exclusivity lives in PlayerState; this only resolves and forwards the data.
//
// Safety: there is NO hardcoded "guard" default here. Role/tags come from the inspector, and when
// left empty are derived from outfitId. A cloak can never present as a guard or carry armor tags
// (it is corrected at runtime with a warning), so a mis-configured CloakStand is never misleading.
public class OutfitInteractable : BaseInteractable
{
    public string outfitId = "guard_armor";
    public string displayName = "Guard Armor";

    [Tooltip("Optional. Leave empty to derive a safe role from outfitId (cloak -> traveler, armor -> guard).")]
    public string currentRoleOverride = string.Empty;

    [Tooltip("Optional. Leave empty to derive safe tags from outfitId. Never hardcode armor tags on a cloak.")]
    public List<string> visibleStatusTags = new List<string>();

    public bool addWorldEvent = true;

    private void Reset()
    {
        interactionText = "Equip outfit";
    }

    protected override void ApplyInteraction()
    {
        string outfit = HasText(outfitId) ? outfitId.Trim() : "normal";
        string role = ResolveRole(outfit);
        List<string> tags = ResolveTags(outfit);

        Debug.Log("[OutfitInteractable] Equipping outfitId=" + outfit + ", role=" + role +
            ", tags=" + FormatTags(tags), this);

        playerState.EquipOutfit(outfit, role, tags);

        if (addWorldEvent)
        {
            WorldEventLog eventLog = ResolveWorldEventLog();

            if (eventLog != null)
            {
                eventLog.AddEvent(new WorldEvent
                {
                    eventType = "outfit_change",
                    actor = "player",
                    targetNpcId = string.Empty,
                    locationObjectId = FindNearestSceneContextObjectId(),
                    description = "Player equipped " + GetInteractionLabel() + ".",
                    isPublic = false,
                    isGlobal = false
                });
            }
        }
    }

    private string ResolveRole(string outfit)
    {
        string role = HasText(currentRoleOverride) ? currentRoleOverride.Trim() : DefaultRole(outfit);

        // A cloak must never present as a guard.
        if (IsCloak(outfit) && string.Equals(role, "guard", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[OutfitInteractable] outfitId '" + outfit + "' is a cloak but role was '" + role +
                "'. Correcting role to 'traveler'.", this);
            role = "traveler";
        }

        return role;
    }

    private List<string> ResolveTags(string outfit)
    {
        // Copy so the serialized inspector list is never mutated.
        List<string> tags = (visibleStatusTags != null && visibleStatusTags.Count > 0)
            ? new List<string>(visibleStatusTags)
            : DefaultTags(outfit);

        // A cloak must never carry armor tags.
        if (IsCloak(outfit))
        {
            int removed = tags.RemoveAll(IsArmorTag);

            if (removed > 0)
            {
                Debug.LogWarning("[OutfitInteractable] outfitId '" + outfit + "' is a cloak but had armor tags. " +
                    "Removed " + removed + " armor tag(s) at runtime.", this);
            }

            if (tags.Count == 0)
            {
                tags = DefaultTags(outfit);
            }
        }

        return tags;
    }

    // Minimal outfitId -> role mapping (no item database).
    private static string DefaultRole(string outfit)
    {
        if (IsCloak(outfit))
        {
            return "traveler";
        }

        return outfit.ToLowerInvariant().Contains("armor") ? "guard" : "traveler";
    }

    // Minimal outfitId -> tags mapping (no item database). The outfitId itself is included as a tag.
    private static List<string> DefaultTags(string outfit)
    {
        if (IsCloak(outfit))
        {
            return new List<string> { outfit, "cloak", "modest_clothing" };
        }

        if (outfit.ToLowerInvariant().Contains("armor"))
        {
            return new List<string> { outfit, "armored", "authority_signal" };
        }

        return new List<string>();
    }

    private static bool IsCloak(string outfit)
    {
        return !string.IsNullOrEmpty(outfit) && outfit.ToLowerInvariant().Contains("cloak");
    }

    private static bool IsArmorTag(string tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        string t = tag.Trim().ToLowerInvariant();
        return t == "guard_armor" || t == "armored" || t == "authority_signal";
    }

    private static string FormatTags(List<string> tags)
    {
        return (tags == null || tags.Count == 0) ? "(none)" : string.Join(", ", tags.ToArray());
    }

    protected override string GetInteractionLabel()
    {
        return string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
    }

    protected override Color GizmoColor
    {
        get { return new Color(0.3f, 0.7f, 1f, 0.35f); }
    }

    private static bool HasText(string value)
    {
        return !string.IsNullOrEmpty(value) && value.Trim().Length > 0;
    }
}
