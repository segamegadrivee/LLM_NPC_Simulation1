using UnityEngine;

// Optional debug panel for demonstrating how world state changes affect generated prompts.
public class DemoWorldStateControls : MonoBehaviour
{
    private void OnGUI()
    {
        WorldState worldState = WorldState.Instance;
        Rect rect = new Rect(Screen.width - 280f, 20f, 260f, 190f);

        GUILayout.BeginArea(rect, GUI.skin.window);
        GUILayout.Label("World State Debug");

        if (worldState == null)
        {
            GUILayout.Label("No WorldState in scene.");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label("Mood: " + worldState.villageMood);
        GUILayout.Label("Bell Missing: " + worldState.churchBellMissing);

        if (GUILayout.Button("Set mood: calm"))
        {
            worldState.villageMood = "calm";
        }

        if (GUILayout.Button("Set mood: worried"))
        {
            worldState.villageMood = "worried";
        }

        if (GUILayout.Button("Toggle bell missing"))
        {
            worldState.churchBellMissing = !worldState.churchBellMissing;
            worldState.currentEvent = worldState.churchBellMissing ? "The old church bell is missing." : "The old church bell has been found.";
        }

        if (GUILayout.Button("Add fact: stranger was seen near tavern"))
        {
            worldState.AddGlobalFact("A stranger was seen near the tavern.");
        }

        if (GUILayout.Button("Add fact: bell could not be moved by one person"))
        {
            worldState.AddGlobalFact("The bell could not be moved by one person.");
        }

        GUILayout.EndArea();
    }
}
