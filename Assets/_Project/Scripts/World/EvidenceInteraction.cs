using UnityEngine;

public class EvidenceInteraction : MonoBehaviour
{
    public EvidenceObject evidenceObject;
    public float interactionDistance = 3f;
    public KeyCode interactKey = KeyCode.E;
    public string interactionHint = "Press E to inspect";
    public bool debugLogs = true;

    private Transform playerTransform;
    private PlayerState playerState;
    private bool playerWasInRange;

    private void Awake()
    {
        if (evidenceObject == null)
        {
            evidenceObject = GetComponent<EvidenceObject>();
        }

        FindPlayer();
    }

    private void Update()
    {
        if (playerTransform == null || playerState == null)
        {
            FindPlayer();
        }

        if (playerTransform == null || playerState == null || evidenceObject == null)
        {
            return;
        }

        bool playerInRange = Vector3.Distance(transform.position, playerTransform.position) <= interactionDistance;

        if (debugLogs && playerInRange && !playerWasInRange)
        {
            Debug.Log(interactionHint + ": " + gameObject.name, this);
        }

        playerWasInRange = playerInRange;

        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            evidenceObject.Collect(playerState);
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

        if (debugLogs && playerState == null)
        {
            Debug.LogWarning("Player was found, but it does not have a PlayerState component.", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
