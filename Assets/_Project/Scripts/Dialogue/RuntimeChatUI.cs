using UnityEngine;

// Simple OnGUI chat for the prototype. Add it to GameSystems; no Canvas setup is required yet.
public class RuntimeChatUI : MonoBehaviour
{
    private const string ChatInputControlName = "ChatInputField";
    private const float HeaderHeight = 32f;
    private const float ThinkingHeight = 24f;
    private const float InputHeight = 34f;
    private const float PromptButtonRowHeight = 30f;
    private const float EvidenceButtonRowHeight = 30f;
    private const float DebugButtonRowHeight = 30f;
    private const float PromptPreviewHeight = 120f;
    private const float WindowPadding = 30f;
    private const float MinMessageHistoryHeight = 70f;
    private const float MinPromptPreviewHeight = 60f;

    [SerializeField] private bool showDebugControls = false;

    private string inputText = string.Empty;
    private Vector2 scrollPosition;
    private Vector2 promptScrollPosition;
    private int lastMessageCount;
    private bool shouldScrollToBottom;
    private bool showPrompt;
    private float currentPromptPreviewHeight;
    private GUIStyle wrappedLabelStyle;

    private void OnGUI()
    {
        DialogueManager dialogueManager = DialogueManager.Instance;

        if (dialogueManager == null || !dialogueManager.IsOpen)
        {
            lastMessageCount = 0;
            return;
        }

        float windowWidth = Mathf.Min(520f, Screen.width - 40f);
        float windowHeight = Mathf.Min(460f, Screen.height - 40f);
        Rect rect = new Rect(20f, 20f, windowWidth, windowHeight);
        currentPromptPreviewHeight = GetPromptPreviewHeight(windowHeight);

        GUILayout.BeginArea(rect, GUI.skin.window);
        DrawHeader(dialogueManager);
        DrawMessages(dialogueManager, GetMessageHistoryHeight(windowHeight));
        DrawInput(dialogueManager);
        GUILayout.EndArea();

        Event currentEvent = Event.current;

        if (dialogueManager != null && dialogueManager.IsOpen && (currentEvent == null || currentEvent.type != EventType.ScrollWheel))
        {
            GUI.FocusControl(ChatInputControlName);
        }
    }

    private void DrawHeader(DialogueManager dialogueManager)
    {
        if (dialogueManager == null)
        {
            return;
        }

        string npcName = dialogueManager.currentNpc != null ? dialogueManager.currentNpc.npcName : "NPC";
        string npcRole = dialogueManager.currentNpc != null ? dialogueManager.currentNpc.role : "Unknown role";

        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label(npcName + " - " + npcRole, GUILayout.Height(22f));
        GUILayout.FlexibleSpace();
        GUILayout.Label("Provider: " + GetProviderStatus(dialogueManager));

        if (GUILayout.Button("[DEBUG CONTEXT]", GUILayout.Width(150f), GUILayout.Height(22f)))
        {
            ContextDebugOverlay.ToggleDebugMenu();
        }

        GUILayout.EndHorizontal();
    }

    private void DrawMessages(DialogueManager dialogueManager, float height)
    {
        if (dialogueManager == null || dialogueManager.messages == null)
        {
            return;
        }

        if (dialogueManager.messages.Count != lastMessageCount)
        {
            lastMessageCount = dialogueManager.messages.Count;
            shouldScrollToBottom = true;
        }

        if (shouldScrollToBottom)
        {
            scrollPosition.y = float.MaxValue;
        }

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(height), GUILayout.ExpandWidth(true));

        for (int i = 0; i < dialogueManager.messages.Count; i++)
        {
            DialogueMessage message = dialogueManager.messages[i];

            if (message == null)
            {
                continue;
            }

            GUILayout.Label(message.speaker + ": " + message.text, WrappedLabelStyle);
        }

        GUILayout.EndScrollView();

        if (shouldScrollToBottom && Event.current.type == EventType.Repaint)
        {
            shouldScrollToBottom = false;
        }
    }

    private void DrawInput(DialogueManager dialogueManager)
    {
        if (dialogueManager.IsWaitingForResponse)
        {
            GUILayout.Label("NPC is thinking...", GUILayout.Height(ThinkingHeight));
        }
        else
        {
            GUILayout.Space(ThinkingHeight);
        }

        if (showPrompt && currentPromptPreviewHeight > 0f)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(currentPromptPreviewHeight));
            promptScrollPosition = GUILayout.BeginScrollView(promptScrollPosition);
            GUILayout.Label(string.IsNullOrEmpty(dialogueManager.LastGeneratedPrompt) ? "(No prompt generated yet.)" : dialogueManager.LastGeneratedPrompt, WrappedLabelStyle);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        GUILayout.BeginHorizontal();
        GUI.SetNextControlName(ChatInputControlName);
        Event currentEvent = Event.current;
        bool sendFromKeyboard = currentEvent != null && currentEvent.isKey && currentEvent.type == EventType.KeyDown &&
            (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter);

        inputText = GUILayout.TextField(inputText ?? string.Empty, GUILayout.Height(28f));

        if (sendFromKeyboard)
        {
            Send(dialogueManager);

            if (currentEvent != null)
            {
                currentEvent.Use();
            }
        }

        GUI.enabled = !dialogueManager.IsWaitingForResponse;

        if (GUILayout.Button("Send", GUILayout.Width(70f), GUILayout.Height(28f)))
        {
            Send(dialogueManager);
        }

        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Close", GUILayout.Width(90f)))
        {
            dialogueManager.CloseDialogue();
            inputText = string.Empty;
        }

        if (GUILayout.Button("Print Prompt", GUILayout.Width(120f)))
        {
            Debug.Log("Last generated prompt:\n" + dialogueManager.LastGeneratedPrompt, dialogueManager);
        }

        if (GUILayout.Button("Copy Prompt", GUILayout.Width(120f)))
        {
            GUIUtility.systemCopyBuffer = dialogueManager.LastGeneratedPrompt ?? string.Empty;
        }

        showPrompt = GUILayout.Toggle(showPrompt, "Show Prompt", GUILayout.Width(120f));

        GUILayout.EndHorizontal();

        // Heavy diagnostic tools are DEV-ONLY. They stay out of the normal MVP chat UI and only
        // appear when showDebugControls is enabled in the inspector.
        if (showDebugControls)
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("[DUMP CONTEXT EVIDENCE]", GUILayout.Height(24f)))
            {
                ContextEvidenceDumper.DumpCurrentContextEvidence(dialogueManager);
            }

            if (GUILayout.Button("[TRACE CONTEXT PIPELINE]", GUILayout.Height(24f)))
            {
                ContextPipelineTracer.TraceCurrentContextPipeline(dialogueManager, inputText);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Clear This NPC Memory", GUILayout.Width(170f)))
            {
                dialogueManager.ClearCurrentNpcMemory();
                lastMessageCount = 0;
                shouldScrollToBottom = true;
            }

            if (GUILayout.Button("Clear All Dialogue Memory", GUILayout.Width(180f)))
            {
                dialogueManager.ClearAllDialogueMemory();
                lastMessageCount = 0;
                shouldScrollToBottom = true;
            }

            GUILayout.EndHorizontal();
        }
    }

    private void Send(DialogueManager dialogueManager)
    {
        if (dialogueManager == null || dialogueManager.IsWaitingForResponse)
        {
            return;
        }

        string text = inputText != null ? inputText.Trim() : string.Empty;

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        inputText = string.Empty;
        dialogueManager.SendPlayerMessage(text);
    }

    private float GetMessageHistoryHeight(float windowHeight)
    {
        float height = windowHeight - HeaderHeight - ThinkingHeight - InputHeight - GetButtonRowsHeight() - currentPromptPreviewHeight - WindowPadding;

        return Mathf.Max(MinMessageHistoryHeight, height);
    }

    private float GetPromptPreviewHeight(float windowHeight)
    {
        if (!showPrompt)
        {
            return 0f;
        }

        float availableHeight = windowHeight - HeaderHeight - ThinkingHeight - InputHeight - GetButtonRowsHeight() - MinMessageHistoryHeight - WindowPadding;

        if (availableHeight < MinPromptPreviewHeight)
        {
            return 0f;
        }

        return Mathf.Min(availableHeight, PromptPreviewHeight);
    }

    private float GetButtonRowsHeight()
    {
        // Always: the Close/Print/Copy/Show-Prompt row. DEV-only: the dump/trace and clear-memory rows.
        return PromptButtonRowHeight + (showDebugControls ? EvidenceButtonRowHeight + DebugButtonRowHeight : 0f);
    }

    private static string GetProviderStatus(DialogueManager dialogueManager)
    {
        string intended = string.IsNullOrEmpty(dialogueManager.CurrentLLMName) ? "None" : dialogueManager.CurrentLLMName;

        // If a response actually came back from a different provider than intended (e.g. a DEV mock
        // fallback after an OpenAI failure), surface that so the demo never misreports its source.
        if (dialogueManager.LastLLMResponseReceived)
        {
            string actual = dialogueManager.LastActualLLMProvider;

            if (!string.IsNullOrEmpty(actual) &&
                !string.Equals(actual, intended, System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(actual, "Pending", System.StringComparison.OrdinalIgnoreCase))
            {
                return intended + " (actual: " + actual + ")";
            }
        }

        return intended;
    }

    private GUIStyle WrappedLabelStyle
    {
        get
        {
            if (wrappedLabelStyle == null)
            {
                wrappedLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true
                };
            }

            return wrappedLabelStyle;
        }
    }

}
