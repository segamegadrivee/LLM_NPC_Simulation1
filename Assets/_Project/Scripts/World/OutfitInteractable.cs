using UnityEngine;

public class OutfitInteractable : MonoBehaviour
{
    public string outfitId = "guard_armor";
    public string displayName = "Guard Armor";
    public string interactionText = "Equip guard armor";
    public float interactionDistance = 3f;
    public KeyCode interactKey = KeyCode.E;
    public bool addWorldEvent = true;
    public bool debugLogs = true;

    private Transform playerTransform;
    private PlayerState playerState;
    private bool playerWasInRange;

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
            Debug.Log(interactionText + ": " + GetDisplayName(), this);
        }

        playerWasInRange = playerInRange;

        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            Interact();
        }
    }

    public void Interact()
    {
        if (playerState == null)
        {
            FindPlayer();
        }

        if (playerState == null)
        {
            Debug.LogWarning("OutfitInteractable could not find PlayerState.", this);
            return;
        }

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
                    description = "Player equipped " + GetDisplayName() + ".",
                    isPublic = false,
                    isGlobal = false
                });
            }
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

    private string FindNearestSceneContextObjectId()
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

    private string GetDisplayName()
    {
        return string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
