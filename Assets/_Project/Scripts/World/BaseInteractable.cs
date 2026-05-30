using UnityEngine;

// Shared base for proximity "press key" world interactables.
//
// It centralises the player lookup, range check, interaction key handling, and the small
// scene/event helpers that were previously copy-pasted across every interactable. Concrete
// interactables only implement ApplyInteraction(), which should update structured state/context
// (PlayerState, WorldState, WorldEventLog) and must NOT generate NPC dialogue directly.
//
// Serialized field names (interactionDistance, interactKey, interactionText, debugLogs) are kept
// identical to the original per-interactable fields so existing scene/serialized values still bind.
public abstract class BaseInteractable : MonoBehaviour
{
    public float interactionDistance = 3f;
    public KeyCode interactKey = KeyCode.E;
    public string interactionText = "Interact";
    public bool debugLogs = true;

    protected Transform playerTransform;
    protected PlayerState playerState;
    private bool playerWasInRange;

    protected virtual void Awake()
    {
        FindPlayer();
    }

    protected virtual void Update()
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
            Debug.Log(interactionText + ": " + GetInteractionLabel(), this);
        }

        playerWasInRange = playerInRange;

        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            TryInteract();
        }
    }

    // Public entry point so an interaction can also be triggered from code if needed.
    public void TryInteract()
    {
        if (playerState == null)
        {
            FindPlayer();
        }

        if (playerState == null)
        {
            Debug.LogWarning(GetType().Name + " could not find PlayerState.", this);
            return;
        }

        ApplyInteraction();
    }

    // Concrete interactables update structured state/context here. No NPC dialogue.
    protected abstract void ApplyInteraction();

    protected virtual string GetInteractionLabel()
    {
        return gameObject.name;
    }

    protected virtual Color GizmoColor
    {
        get { return new Color(0.3f, 0.7f, 1f, 0.35f); }
    }

    protected void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            return;
        }

        playerTransform = player.transform;
        playerState = player.GetComponent<PlayerState>();
    }

    protected string FindNearestSceneContextObjectId()
    {
        SceneContextObject[] objects = FindObjectsByType<SceneContextObject>(FindObjectsSortMode.None);
        SceneContextObject nearest = null;
        float nearestDistance = float.MaxValue;
        Vector3 origin = playerTransform != null ? playerTransform.position : transform.position;

        for (int i = 0; i < objects.Length; i++)
        {
            SceneContextObject contextObject = objects[i];

            if (contextObject == null || string.IsNullOrEmpty(contextObject.objectId))
            {
                continue;
            }

            float distance = Vector3.Distance(origin, contextObject.transform.position);

            if (distance < nearestDistance)
            {
                nearest = contextObject;
                nearestDistance = distance;
            }
        }

        return nearest != null ? nearest.objectId : string.Empty;
    }

    protected WorldEventLog ResolveWorldEventLog()
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

        Debug.LogWarning(GetType().Name + ": no WorldEventLog in the scene. Creating a runtime fallback. " +
            "Add a persistent WorldEventLog to GameSystems for the final scene.", this);
        GameObject eventLogObject = new GameObject("WorldEventLog (runtime fallback)");
        return eventLogObject.AddComponent<WorldEventLog>();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = GizmoColor;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
