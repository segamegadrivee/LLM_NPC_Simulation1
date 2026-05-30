using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Report-only validator for the final MVP scene. It never modifies the scene; it only checks that
// the core pipeline objects are present and configured for the diploma demo, and prints a summary.
//
// Run via: Tools > AI NPC > Validate MVP Scene.
public static class ValidateMvpScene
{
    [MenuItem("Tools/AI NPC/Validate MVP Scene")]
    public static void Validate()
    {
        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();
        List<string> ok = new List<string>();

        // Required core systems (singletons / scene services).
        DialogueManager dialogueManager = RequireSingle<DialogueManager>("DialogueManager", errors, ok);
        RequireSingle<RuntimeChatUI>("RuntimeChatUI", errors, ok);
        ContextRetriever contextRetriever = RequireSingle<ContextRetriever>("ContextRetriever", errors, ok);
        OpenAIClient openAIClient = RequireSingle<OpenAIClient>("OpenAIClient", errors, ok);
        RequireSingle<PlayerState>("PlayerState", errors, ok);
        RequireSingle<WorldState>("WorldState", errors, ok);

        // Core runtime stores: present is best (auto-created at runtime otherwise, which we warn about).
        PreferPresent<WorldEventLog>("WorldEventLog", warnings, ok);
        PreferPresent<NPCStateStore>("NPCStateStore", warnings, ok);
        PreferPresent<NPCConversationMemoryStore>("NPCConversationMemoryStore", warnings, ok);

        // Required content.
        RequireAtLeastOne<NPCInteraction>("NPCInteraction", errors, ok);
        RequireAtLeastOne<SceneContextObject>("SceneContextObject", errors, ok);

        // Optional demo content.
        ReportOptional<OutfitInteractable>("OutfitInteractable (visible appearance demo)", ok);
        ReportOptional<VisibleItemInteractable>("VisibleItemInteractable (visible item demo)", ok);
        ReportOptional<HiddenBellInteractable>("HiddenBellInteractable (public event demo)", ok);

        // Configuration checks.
        if (dialogueManager != null)
        {
            if (!dialogueManager.DebugUseOpenAI)
            {
                warnings.Add("DialogueManager.useOpenAI is OFF. OpenAI is the MVP runtime path; enable it.");
            }

            if (dialogueManager.DebugOpenAIClient == null)
            {
                errors.Add("DialogueManager.openAIClient is not assigned.");
            }
        }

        if (openAIClient != null)
        {
            if (openAIClient.settings == null)
            {
                errors.Add("OpenAIClient.settings (OpenAISettings asset) is not assigned.");
            }

            if (openAIClient.useMockOnFailure)
            {
                warnings.Add("OpenAIClient.useMockOnFailure is ON. For the diploma demo turn it OFF so OpenAI " +
                    "failures show a clear error instead of silently returning scripted Mock text.");
            }
        }

        if (contextRetriever != null && contextRetriever.knowledgeBase == null)
        {
            warnings.Add("ContextRetriever.knowledgeBase is not assigned. Retrieved knowledge will always be empty.");
        }

        if (GameObject.FindGameObjectWithTag("Player") == null)
        {
            warnings.Add("No GameObject is tagged 'Player'. PlayerState lookup and input locking rely on this tag.");
        }

        PrintReport(errors, warnings, ok);
    }

    private static T RequireSingle<T>(string label, List<string> errors, List<string> ok) where T : Object
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (found.Length == 0)
        {
            errors.Add("Missing required component: " + label + ".");
            return null;
        }

        if (found.Length > 1)
        {
            errors.Add("Found " + found.Length + " " + label + " components; expected exactly one.");
        }
        else
        {
            ok.Add(label);
        }

        return found[0];
    }

    private static void PreferPresent<T>(string label, List<string> warnings, List<string> ok) where T : Object
    {
        if (Object.FindFirstObjectByType<T>() == null)
        {
            warnings.Add(label + " is not in the scene. It will be auto-created at runtime, but a persistent " +
                "instance on GameSystems is recommended for the final scene.");
        }
        else
        {
            ok.Add(label);
        }
    }

    private static void RequireAtLeastOne<T>(string label, List<string> errors, List<string> ok) where T : Object
    {
        int count = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        if (count == 0)
        {
            errors.Add("Scene needs at least one " + label + ".");
        }
        else
        {
            ok.Add(label + " x" + count);
        }
    }

    private static void ReportOptional<T>(string label, List<string> ok) where T : Object
    {
        int count = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        if (count > 0)
        {
            ok.Add(label + " x" + count);
        }
    }

    private static void PrintReport(List<string> errors, List<string> warnings, List<string> ok)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("===== MVP Scene Validation =====");

        builder.AppendLine();
        builder.AppendLine("Present / OK (" + ok.Count + "):");
        AppendLines(builder, ok, "  + ");

        builder.AppendLine();
        builder.AppendLine("Warnings (" + warnings.Count + "):");
        AppendLines(builder, warnings, "  ! ");

        builder.AppendLine();
        builder.AppendLine("Errors (" + errors.Count + "):");
        AppendLines(builder, errors, "  X ");

        string report = builder.ToString();

        if (errors.Count > 0)
        {
            Debug.LogError(report);
        }
        else if (warnings.Count > 0)
        {
            Debug.LogWarning(report);
        }
        else
        {
            Debug.Log(report);
        }
    }

    private static void AppendLines(StringBuilder builder, List<string> lines, string prefix)
    {
        if (lines.Count == 0)
        {
            builder.AppendLine("  (none)");
            return;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            builder.AppendLine(prefix + lines[i]);
        }
    }
}
