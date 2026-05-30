using System.Collections.Generic;
using UnityEngine;

// Minimal runtime HUD. Shows only the player's current outfit, role, and visible tags.
// Toggle with the I key. Finds PlayerState automatically if it is not assigned in the inspector.
// Intentionally does NOT show reputation, opinion, aggression, helpfulness, mood, or trust.
public class PlayerStatusHUD : MonoBehaviour
{
    [SerializeField] private PlayerState playerState;
    [SerializeField] private KeyCode toggleKey = KeyCode.I;
    [SerializeField] private bool visible = true;

    private GUIStyle labelStyle;

    private void Awake()
    {
        if (playerState == null)
        {
            FindPlayerState();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            visible = !visible;
        }
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        if (playerState == null)
        {
            FindPlayerState();

            if (playerState == null)
            {
                return;
            }
        }

        const float width = 250f;
        const float height = 110f;
        Rect rect = new Rect(20f, Screen.height - height - 20f, width, height);

        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label("Player Status  [" + toggleKey + "]", LabelStyle);
        GUILayout.Label("Equipped Outfit: " + Safe(playerState.equippedOutfit, "normal"), LabelStyle);
        GUILayout.Label("Role: " + Safe(playerState.currentRole, "traveler"), LabelStyle);
        GUILayout.Label("Visible Tags: " + FormatTags(playerState.visibleStatusTags), LabelStyle);
        GUILayout.EndArea();
    }

    private void FindPlayerState()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            playerState = player.GetComponent<PlayerState>();
        }

        if (playerState == null)
        {
            playerState = FindFirstObjectByType<PlayerState>();
        }
    }

    private static string FormatTags(List<string> tags)
    {
        if (tags == null || tags.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", tags.ToArray());
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrEmpty(value) || value.Trim().Length == 0 ? fallback : value;
    }

    private GUIStyle LabelStyle
    {
        get
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            }

            return labelStyle;
        }
    }
}
