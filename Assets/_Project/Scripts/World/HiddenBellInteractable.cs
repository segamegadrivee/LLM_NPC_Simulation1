using UnityEngine;

public class HiddenBellInteractable : MonoBehaviour
{
    public string bellLocationId = "old_storehouse";
    public string interactionText = "Inspect hidden bell";
    public float interactionDistance = 3f;
    public KeyCode interactKey = KeyCode.E;
    public bool setBellFragmentAsVisibleHeldItem = true;
    public bool debugLogs = true;

    private Transform playerTransform;
    private PlayerState playerState;
    private bool playerWasInRange;
    private bool found;

    private void Awake()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (playerTransform == null || playerState == null)
        {
            FindPlayer();
        }

        if (playerTransform == null || playerState == null)
        {
            return;
        }

        bool playerInRange = Vector3.Distance(transform.position, playerTransform.position) <= interactionDistance;

        if (debugLogs && playerInRange && !playerWasInRange)
        {
            Debug.Log(interactionText + ": " + gameObject.name, this);
        }

        playerWasInRange = playerInRange;

        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            Interact();
        }
    }

    public void Interact()
    {
        if (found)
        {
            if (debugLogs)
            {
                Debug.Log("Hidden bell was already found.", this);
            }

            return;
        }

        if (playerState == null)
        {
            FindPlayer();
        }

        WorldState worldState = ResolveWorldState();

        if (worldState != null)
        {
            worldState.RegisterBellFound(bellLocationId);
        }

        if (playerState != null)
        {
            playerState.AddKnownFact("player_found_missing_bell");
            playerState.AddKnownFact("bell_found_in_" + NormalizeToken(bellLocationId));
            playerState.AddCompletedAction("found_hidden_bell");

            if (setBellFragmentAsVisibleHeldItem)
            {
                playerState.SetVisibleHeldItem("bell_fragment");
            }
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

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            return;
        }

        playerTransform = player.transform;
        playerState = player.GetComponent<PlayerState>();
    }

    private WorldState ResolveWorldState()
    {
        if (WorldState.Instance != null)
        {
            return WorldState.Instance;
        }

        return FindFirstObjectByType<WorldState>();
    }

    private WorldEventLog ResolveWorldEventLog()
    {
        if (WorldEventLog.Instance != null)
        {
            return WorldEventLog.Instance;
        }

        WorldEventLog eventLog = FindFirstObjectByType<WorldEventLog>();

        if (eventLog != null)
        {
            return eventLog;
        }

        GameObject eventLogObject = new GameObject("WorldEventLog");
        return eventLogObject.AddComponent<WorldEventLog>();
    }

    private static string NormalizeToken(string value)
    {
        return string.IsNullOrEmpty(value) ? "unknown" : value.Trim().ToLowerInvariant().Replace(" ", "_");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.8f, 0.2f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
