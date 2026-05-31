using UnityEngine;

// Minimal PUBLIC "bell found" world event for the MVP demo.
//
// Reuses the existing systems (BaseInteractable for player/range/key + WorldEventLog/scene helpers,
// and the existing WorldState/WorldEvent/WorldEventLog). It does NOT create a parallel world-state
// system. Triggers once: flips the global WorldState to "found", logs a public/global WorldEvent that
// every NPC can see, optionally records a private player fact, and hides the bell prop.
//
// A single dedicated script keeps this demo clean: no bell_fragment held item, inspector-friendly
// fields, and visual disabling. This is the current/only bell-found mechanic for the MVP.
public class BellFoundInteractable : BaseInteractable
{
    public string eventId = "bell_found_publicly";
    public string displayName = "Missing Bell";
    public string bellLocationId = "church";
    public GameObject visualToDisable;
    public bool disableWholeObjectAfterFound = false;
    public bool addPlayerKnownFact = true;
    public string playerKnownFact = "player_found_missing_bell";

    private bool found;

    private void Reset()
    {
        interactionText = "Inspect the bell";
    }

    protected override Color GizmoColor
    {
        get { return new Color(0.9f, 0.8f, 0.2f, 0.45f); }
    }

    protected override string GetInteractionLabel()
    {
        return string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
    }

    protected override void ApplyInteraction()
    {
        if (found)
        {
            return;
        }

        found = true;

        UpdateWorldState();
        AddPublicEvent();
        RecordPlayerFact();

        Debug.Log("[BellFoundInteractable] Missing bell found. World state updated.", this);

        DisableBellVisual();

        // Stop this object from triggering again.
        enabled = false;
    }

    // 1) Global, authoritative world state: the bell is no longer missing. Reuses WorldState's public
    //    API only (no new world-state system, no WorldState.cs change).
    private void UpdateWorldState()
    {
        WorldState worldState = WorldState.Instance != null ? WorldState.Instance : FindFirstObjectByType<WorldState>();

        if (worldState == null)
        {
            Debug.LogWarning("[BellFoundInteractable] No WorldState in the scene; world state was not updated.", this);
            return;
        }

        worldState.churchBellMissing = false;
        worldState.villageMood = "relieved";
        worldState.currentEvent = "The missing church bell has been found.";
        worldState.AddGlobalFact("The missing church bell has been found.");
        worldState.AddGlobalFact("The village is calmer now that the bell has been found.");
    }

    // 2) Public + global event so EVERY NPC can reference it (relevant to all NPCs, not location-gated).
    private void AddPublicEvent()
    {
        WorldEventLog eventLog = ResolveWorldEventLog();

        if (eventLog == null)
        {
            return;
        }

        eventLog.AddEvent(new WorldEvent
        {
            eventId = string.IsNullOrEmpty(eventId) ? "bell_found_publicly" : eventId,
            eventType = "bell_found",
            actor = "player",
            targetNpcId = string.Empty,
            locationObjectId = bellLocationId,
            description = "The missing church bell has been found. The village can calm down.",
            isPublic = true,
            isGlobal = true
        });
    }

    // 3) Optional private record that the player found it. NPCs learn it from the PUBLIC event above,
    //    not from this private fact.
    private void RecordPlayerFact()
    {
        if (!addPlayerKnownFact || playerState == null || string.IsNullOrEmpty(playerKnownFact))
        {
            return;
        }

        playerState.AddKnownFact(playerKnownFact);
        playerState.AddCompletedAction("found_missing_bell");
    }

    private void DisableBellVisual()
    {
        if (disableWholeObjectAfterFound)
        {
            gameObject.SetActive(false);
            return;
        }

        if (visualToDisable != null)
        {
            visualToDisable.SetActive(false);
        }

        Collider selfCollider = GetComponent<Collider>();

        if (selfCollider != null)
        {
            selfCollider.enabled = false;
        }
    }
}
