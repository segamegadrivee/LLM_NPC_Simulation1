using UnityEngine;

// Read-only runtime "World Status" panel for the demo.
//
// This was previously a dev cheat panel with "Set mood / Toggle bell / Add fact" buttons. Those
// mutating controls were removed: this panel now only DISPLAYS the current world state and the latest
// public event. It does not modify any state. Toggle visibility with F2.
//
// Class name is kept (DemoWorldStateControls) so the existing scene component keeps working without
// any re-attach or scene edit.
public class DemoWorldStateControls : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.F2;
    [SerializeField] private bool visible = true;

    private const float PanelWidth = 280f;
    private const float LabelColumnWidth = 120f;

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle fieldLabelStyle;
    private GUIStyle valueStyle;
    private GUIStyle hintStyle;
    private Texture2D panelBackground;

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

        EnsureStyles();

        float x = Screen.width - PanelWidth - 20f;
        GUILayout.BeginArea(new Rect(x, 20f, PanelWidth, Screen.height - 40f));
        GUILayout.BeginVertical(panelStyle, GUILayout.Width(PanelWidth));

        GUILayout.Label("World Status", titleStyle);
        GUILayout.Space(6f);

        WorldState worldState = WorldState.Instance != null ? WorldState.Instance : FindFirstObjectByType<WorldState>();

        if (worldState == null)
        {
            GUILayout.Label("No WorldState in the scene.", valueStyle);
        }
        else
        {
            DrawField("Village Mood", Safe(worldState.villageMood, "unknown"));
            DrawField("Bell Status", worldState.churchBellMissing ? "Missing" : "Found");
            DrawField("Current Event", Safe(worldState.currentEvent, "none"));
            DrawField("Latest Public Event", GetLatestPublicEvent());
            DrawField("Public Facts", GetFactsSummary(worldState));
        }

        GUILayout.Space(6f);
        GUILayout.Label("[" + toggleKey + "] hide / show", hintStyle);

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawField(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, fieldLabelStyle, GUILayout.Width(LabelColumnWidth));
        GUILayout.Label(value, valueStyle);
        GUILayout.EndHorizontal();
        GUILayout.Space(2f);
    }

    private static string GetLatestPublicEvent()
    {
        WorldEventLog log = WorldEventLog.Instance != null ? WorldEventLog.Instance : FindFirstObjectByType<WorldEventLog>();

        if (log == null || log.events == null)
        {
            return "none yet";
        }

        for (int i = log.events.Count - 1; i >= 0; i--)
        {
            WorldEvent worldEvent = log.events[i];

            if (worldEvent != null && (worldEvent.isPublic || worldEvent.isGlobal))
            {
                string text = Safe(worldEvent.description, string.Empty);
                return text.Length > 0 ? text : worldEvent.GetShortText();
            }
        }

        return "none yet";
    }

    private static string GetFactsSummary(WorldState worldState)
    {
        if (worldState.globalFacts == null || worldState.globalFacts.Count == 0)
        {
            return "0 known";
        }

        int count = worldState.globalFacts.Count;
        string latest = worldState.globalFacts[count - 1];
        latest = Safe(latest, string.Empty);

        if (latest.Length == 0)
        {
            return count + " known";
        }

        return count + " known (latest: " + latest + ")";
    }

    private void EnsureStyles()
    {
        if (panelBackground == null)
        {
            panelBackground = MakeColorTexture(new Color(0.08f, 0.09f, 0.12f, 0.82f));
        }

        if (panelStyle == null)
        {
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(14, 14, 12, 12)
            };
            panelStyle.normal.background = panelBackground;
        }

        Color textColor = new Color(0.92f, 0.92f, 0.94f);
        Color labelColor = new Color(0.66f, 0.74f, 0.86f);

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 15
            };
            titleStyle.normal.textColor = Color.white;
        }

        if (fieldLabelStyle == null)
        {
            fieldLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };
            fieldLabelStyle.normal.textColor = labelColor;
        }

        if (valueStyle == null)
        {
            valueStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true
            };
            valueStyle.normal.textColor = textColor;
        }

        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10
            };
            hintStyle.normal.textColor = new Color(0.6f, 0.6f, 0.65f);
        }
    }

    private static Texture2D MakeColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        return texture;
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrEmpty(value) || value.Trim().Length == 0 ? fallback : value.Trim();
    }
}
