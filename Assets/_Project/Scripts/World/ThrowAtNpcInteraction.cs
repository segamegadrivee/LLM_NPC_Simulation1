using UnityEngine;

public class ThrowAtNpcInteraction : MonoBehaviour
{
    public string requiredHeldItem = "mug";
    public string targetNpcId;
    public float targetSearchRadius = 4f;
    public KeyCode interactKey = KeyCode.F;
    public string interactionText = "Throw held item";
    public bool allowAnyNonEmptyHeldItem;
    public bool debugLogs = true;

    private Transform playerTransform;
    private PlayerState playerState;

    private void Awake()
    {
        FindPlayer();
        UseAttachedNpcAsTargetIfPossible();
    }

    private void Update()
    {
        if (playerTransform == null || playerState == null)
        {
            FindPlayer();
        }

        if (Input.GetKeyDown(interactKey))
        {
            Activate();
        }
    }

    public void Activate()
    {
        if (playerState == null)
        {
            FindPlayer();
        }

        if (playerState == null)
        {
            Debug.LogWarning("ThrowAtNpcInteraction could not find PlayerState.", this);
            return;
        }

        string heldItem = string.IsNullOrEmpty(playerState.visibleHeldItem) ? "none" : playerState.visibleHeldItem;

        if (string.Equals(heldItem, "none", System.StringComparison.OrdinalIgnoreCase))
        {
            if (debugLogs)
            {
                Debug.Log("ThrowAtNpcInteraction ignored because player is not visibly holding an item.", this);
            }

            return;
        }

        if (!allowAnyNonEmptyHeldItem && !string.Equals(heldItem, requiredHeldItem, System.StringComparison.OrdinalIgnoreCase))
        {
            if (debugLogs)
            {
                Debug.Log("ThrowAtNpcInteraction requires held item '" + requiredHeldItem + "', but player holds '" + heldItem + "'.", this);
            }

            return;
        }

        NPCInteraction targetInteraction = ResolveTargetNpc();

        if (targetInteraction == null || targetInteraction.profile == null)
        {
            Debug.LogWarning("ThrowAtNpcInteraction could not find a target NPC.", this);
            return;
        }

        if (playerTransform != null && Vector3.Distance(playerTransform.position, targetInteraction.transform.position) > targetSearchRadius)
        {
            if (debugLogs)
            {
                Debug.Log("ThrowAtNpcInteraction ignored because target NPC is outside throw radius.", this);
            }

            return;
        }

        string resolvedTargetNpcId = string.IsNullOrEmpty(targetInteraction.profile.npcId)
            ? targetInteraction.profile.npcName
            : targetInteraction.profile.npcId;

        if (string.IsNullOrEmpty(resolvedTargetNpcId))
        {
            resolvedTargetNpcId = targetInteraction.gameObject.name;
        }

        string targetName = string.IsNullOrEmpty(targetInteraction.profile.npcName) ? resolvedTargetNpcId : targetInteraction.profile.npcName;
        string description = "Player threw a " + heldItem + " at " + targetName + ".";

        ResolveNpcStateStore().RegisterAggressionAgainstNpc(resolvedTargetNpcId, description);
        playerState.RegisterAggression();
        playerState.AddCompletedAction("player_threw_item_at_" + NormalizeToken(resolvedTargetNpcId));

        WorldEventLog eventLog = ResolveWorldEventLog();

        if (eventLog != null)
        {
            eventLog.AddEvent(new WorldEvent
            {
                eventType = "aggression",
                actor = "player",
                targetNpcId = resolvedTargetNpcId,
                locationObjectId = FindNearestSceneContextObjectId(),
                description = description,
                isPublic = false,
                isGlobal = false
            });
        }

        playerState.ClearVisibleHeldItem();

        if (debugLogs)
        {
            Debug.Log(description, this);
        }
    }

    private NPCInteraction ResolveTargetNpc()
    {
        NPCInteraction attachedNpc = GetComponent<NPCInteraction>();

        if (attachedNpc != null && attachedNpc.profile != null && TargetMatches(attachedNpc))
        {
            return attachedNpc;
        }

        NPCInteraction[] npcs = FindObjectsByType<NPCInteraction>(FindObjectsSortMode.None);
        NPCInteraction nearest = null;
        float nearestDistance = float.MaxValue;
        Vector3 origin = playerTransform != null ? playerTransform.position : transform.position;

        for (int i = 0; i < npcs.Length; i++)
        {
            NPCInteraction npc = npcs[i];

            if (npc == null || npc.profile == null || !TargetMatches(npc))
            {
                continue;
            }

            float distance = Vector3.Distance(origin, npc.transform.position);

            if (distance <= targetSearchRadius && distance < nearestDistance)
            {
                nearest = npc;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private bool TargetMatches(NPCInteraction npc)
    {
        if (npc == null || npc.profile == null || string.IsNullOrEmpty(targetNpcId))
        {
            return true;
        }

        return string.Equals(npc.profile.npcId, targetNpcId, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(npc.profile.npcName, targetNpcId, System.StringComparison.OrdinalIgnoreCase);
    }

    private void UseAttachedNpcAsTargetIfPossible()
    {
        if (!string.IsNullOrEmpty(targetNpcId))
        {
            return;
        }

        NPCInteraction attachedNpc = GetComponent<NPCInteraction>();

        if (attachedNpc != null && attachedNpc.profile != null)
        {
            targetNpcId = attachedNpc.profile.npcId;
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

    private NPCStateStore ResolveNpcStateStore()
    {
        if (NPCStateStore.Instance != null)
        {
            return NPCStateStore.Instance;
        }

        NPCStateStore store = FindFirstObjectByType<NPCStateStore>();

        if (store != null)
        {
            return store;
        }

        GameObject storeObject = new GameObject("NPCStateStore");
        return storeObject.AddComponent<NPCStateStore>();
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

    private void OnGUI()
    {
        if (playerTransform == null)
        {
            return;
        }

        NPCInteraction target = ResolveTargetNpc();

        if (target == null)
        {
            return;
        }

        if (Vector3.Distance(playerTransform.position, target.transform.position) > targetSearchRadius)
        {
            return;
        }

        float width = 260f;
        Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 130f, width, 32f);
        GUI.Box(rect, interactionText + " [" + interactKey + "]");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, targetSearchRadius);
    }
}
