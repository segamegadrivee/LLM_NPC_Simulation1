using UnityEngine;

// Public bell-found event trigger. Updates WorldState + WorldEventLog (and the player's visible
// held item) so NPCs can reference the public event. State-only; no dialogue.
public class HiddenBellInteractable : BaseInteractable
{
    public string bellLocationId = "old_storehouse";
    public bool setBellFragmentAsVisibleHeldItem = true;

    private bool found;

    private void Reset()
    {
        interactionText = "Inspect hidden bell";
    }

    protected override Color GizmoColor
    {
        get { return new Color(0.9f, 0.8f, 0.2f, 0.45f); }
    }

    protected override void ApplyInteraction()
    {
        if (found)
        {
            if (debugLogs)
            {
                Debug.Log("Hidden bell was already found.", this);
            }

            return;
        }

        WorldState worldState = ResolveWorldState();

        if (worldState != null)
        {
            worldState.RegisterBellFound(bellLocationId);
        }

        playerState.AddKnownFact("player_found_missing_bell");
        playerState.AddKnownFact("bell_found_in_" + NormalizeToken(bellLocationId));
        playerState.AddCompletedAction("found_hidden_bell");

        if (setBellFragmentAsVisibleHeldItem)
        {
            playerState.SetVisibleHeldItem("bell_fragment");
        }

        WorldEventLog eventLog = ResolveWorldEventLog();

        if (eventLog != null)
        {
            eventLog.AddEvent(new WorldEvent
            {
                eventType = "bell_found",
                actor = "player",
                targetNpcId = string.Empty,
                locationObjectId = bellLocationId,
                description = "The missing church bell was found in the old storehouse.",
                isPublic = true,
                isGlobal = true
            });
        }

        found = true;

        if (debugLogs)
        {
            Debug.Log("Hidden bell found at " + bellLocationId + ".", this);
        }
    }

    private WorldState ResolveWorldState()
    {
        if (WorldState.Instance != null)
        {
            return WorldState.Instance;
        }

        return FindFirstObjectByType<WorldState>();
    }

    private static string NormalizeToken(string value)
    {
        return string.IsNullOrEmpty(value) ? "unknown" : value.Trim().ToLowerInvariant().Replace(" ", "_");
    }
}
