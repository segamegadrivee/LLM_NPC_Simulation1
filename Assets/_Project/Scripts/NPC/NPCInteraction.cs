using UnityEngine;

// Attach this to the root object of each NPC and assign the matching NPCProfile asset.
public class NPCInteraction : MonoBehaviour
{
    public NPCProfile profile;
    public float interactionDistance = 3.5f;
    public KeyCode interactKey = KeyCode.E;
    public string interactionHint = "Press E to talk";
    public bool debugLogs;

    private Transform player;
    private bool playerInRange;

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (player == null)
        {
            FindPlayer();
        }

        playerInRange = false;

        if (player == null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        playerInRange = distance <= interactionDistance;

        if (!playerInRange || !Input.GetKeyDown(interactKey))
        {
            return;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
        {
            return;
        }

        if (profile == null)
        {
            Debug.LogWarning(name + " has NPCInteraction but no NPCProfile assigned.", this);
            return;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("No DialogueManager found in the scene.", this);
            return;
        }

        if (debugLogs)
        {
            Debug.Log("Opening dialogue with " + profile.npcName, this);
        }

        DialogueManager.Instance.OpenDialogue(profile, transform);
    }

    private void OnGUI()
    {
        if (!playerInRange || string.IsNullOrEmpty(interactionHint))
        {
            return;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
        {
            return;
        }

        float width = 240f;
        Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 90f, width, 36f);
        GUI.Box(rect, interactionHint);
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
