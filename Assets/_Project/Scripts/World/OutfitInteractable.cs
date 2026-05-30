using UnityEngine;

// Updates the player's visible outfit so NPCs can react to appearance. State-only; no dialogue.
public class OutfitInteractable : BaseInteractable
{
    public string outfitId = "guard_armor";
    public string displayName = "Guard Armor";
    public bool addWorldEvent = true;

    private void Reset()
    {
        interactionText = "Equip guard armor";
    }

    protected override void ApplyInteraction()
    {
        playerState.SetOutfit(outfitId);

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

    protected override string GetInteractionLabel()
    {
        return string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
    }

    protected override Color GizmoColor
    {
        get { return new Color(0.3f, 0.7f, 1f, 0.35f); }
    }
}
