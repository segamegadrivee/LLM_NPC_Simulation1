using UnityEngine;

// Updates the player's visible held item so NPCs can react to it. State-only; no dialogue.
public class VisibleItemInteractable : BaseInteractable
{
    public string itemId = "mug";
    public string displayName = "Mug";
    public bool addWorldEvent = true;

    private void Reset()
    {
        interactionText = "Pick up mug";
    }

    protected override void ApplyInteraction()
    {
        playerState.SetVisibleHeldItem(itemId);

        if (addWorldEvent)
        {
            WorldEventLog eventLog = ResolveWorldEventLog();

            if (eventLog != null)
            {
                eventLog.AddEvent(new WorldEvent
                {
                    eventType = "item_pickup",
                    actor = "player",
                    targetNpcId = string.Empty,
                    locationObjectId = FindNearestSceneContextObjectId(),
                    description = "Player picked up " + GetDisplayNameWithArticle() + ".",
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
        get { return Color.yellow; }
    }

    private string GetDisplayNameWithArticle()
    {
        string value = GetInteractionLabel();
        return StartsWithVowel(value) ? "an " + value : "a " + value;
    }

    private static bool StartsWithVowel(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        char first = char.ToLowerInvariant(value[0]);
        return first == 'a' || first == 'e' || first == 'i' || first == 'o' || first == 'u';
    }
}
