using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Runtime-only IMGUI overlay for diploma/debug explanation of the NPC context pipeline.
public class ContextDebugOverlay : MonoBehaviour
{
    private const string CompleteMarker = "[\u2713]";
    private const string IncompleteMarker = "[ ]";
    private const float Margin = 18f;
    private const float MinWindowWidth = 520f;
    private const float MaxWindowWidth = 820f;
    private const float PromptPreviewHeight = 260f;

    public static ContextDebugOverlay Instance { get; private set; }

    [SerializeField] private bool visible;
    [SerializeField] private KeyCode toggleKey = KeyCode.F2;

    private Vector2 mainScrollPosition;
    private Vector2 promptScrollPosition;
    private Texture2D panelBackground;
    private Texture2D sectionBackground;
    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle sectionHeaderStyle;
    private GUIStyle labelStyle;
    private GUIStyle smallLabelStyle;
    private GUIStyle boxStyle;
    private GUIStyle promptStyle;
    private GUIStyle warningStyle;

    private ContextSnapshot debugSnapshotOverride;
    private ContextSnapshot liveSnapshotAtDebugRefresh;
    private string debugPromptOverride = string.Empty;
    private string debugSnapshotNote = string.Empty;
    private bool usingDebugSnapshot;

    private ContextSnapshot cachedRetrievalSnapshot;
    private ContextRetriever cachedRetrievalRetriever;
    private string cachedRetrievalPlayerMessage = string.Empty;
    private List<ContextRetriever.DebugKnowledgeRetrievalEntry> cachedRetrievalEntries = new List<ContextRetriever.DebugKnowledgeRetrievalEntry>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        EnsureInstance();
    }

    public static ContextDebugOverlay EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ContextDebugOverlay existing = UnityEngine.Object.FindFirstObjectByType<ContextDebugOverlay>();

        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject overlayObject = new GameObject("ContextDebugOverlay");
        Instance = overlayObject.AddComponent<ContextDebugOverlay>();
        UnityEngine.Object.DontDestroyOnLoad(overlayObject);
        return Instance;
    }

    public static void ToggleDebugMenu()
    {
        EnsureInstance().ToggleVisible();
    }

    public void ToggleVisible()
    {
        visible = !visible;

        if (visible)
        {
            mainScrollPosition = Vector2.zero;
            RefreshRetrievalDebugCache(DialogueManager.Instance, GetDisplayedSnapshot(DialogueManager.Instance));
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (WasToggleKeyPressed())
        {
            ToggleVisible();
        }
    }

    private bool WasToggleKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return toggleKey == KeyCode.F2 && Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(toggleKey);
#endif
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        EnsureStyles();

        DialogueManager dialogueManager = ResolveDialogueManager();

        if (usingDebugSnapshot && dialogueManager != null && liveSnapshotAtDebugRefresh != dialogueManager.LastContextSnapshot)
        {
            ClearDebugSnapshotOverride();
        }

        ContextSnapshot snapshot = GetDisplayedSnapshot(dialogueManager);
        string prompt = GetDisplayedPrompt(dialogueManager);
        RefreshRetrievalDebugCache(dialogueManager, snapshot);

        float availableWidth = Mathf.Max(320f, Screen.width - Margin * 2f);
        float width = availableWidth < MinWindowWidth ? availableWidth : Mathf.Min(MaxWindowWidth, availableWidth);
        float height = Mathf.Max(320f, Screen.height - Margin * 2f);
        Rect windowRect = new Rect(Screen.width - width - Margin, Margin, width, height);

        int previousDepth = GUI.depth;
        GUI.depth = -1000;
        GUILayout.BeginArea(windowRect, panelStyle);
        DrawToolbar(dialogueManager, prompt);
        DrawBody(dialogueManager, snapshot, prompt);
        GUILayout.EndArea();
        GUI.depth = previousDepth;
    }

    private void DrawToolbar(DialogueManager dialogueManager, string prompt)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Debug Context", titleStyle);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Refresh Debug Snapshot", GUILayout.Width(170f), GUILayout.Height(24f)))
        {
            RefreshDebugSnapshot(dialogueManager);
        }

        GUI.enabled = usingDebugSnapshot;

        if (GUILayout.Button("Use Last Response Snapshot", GUILayout.Width(190f), GUILayout.Height(24f)))
        {
            ClearDebugSnapshotOverride();
        }

        GUI.enabled = true;

        if (GUILayout.Button("Copy Prompt To Console", GUILayout.Width(175f), GUILayout.Height(24f)))
        {
            Debug.Log("ContextDebugOverlay prompt preview:\n" + SafeMultiline(prompt), this);
        }

        if (GUILayout.Button("Close", GUILayout.Width(70f), GUILayout.Height(24f)))
        {
            visible = false;
        }

        GUILayout.EndHorizontal();
    }

    private void DrawBody(DialogueManager dialogueManager, ContextSnapshot snapshot, string prompt)
    {
        mainScrollPosition = GUILayout.BeginScrollView(mainScrollPosition);

        if (usingDebugSnapshot)
        {
            GUILayout.Label("DEBUG-ONLY SNAPSHOT: rebuilt for inspection only. It was not sent to the LLM and did not alter dialogue memory.", warningStyle);
        }

        if (!string.IsNullOrEmpty(debugSnapshotNote))
        {
            GUILayout.Label(debugSnapshotNote, smallLabelStyle);
        }

        DrawPipelineFlow(dialogueManager, snapshot, prompt);
        DrawCurrentNpc(dialogueManager, snapshot);
        DrawNpcState(snapshot);
        DrawPlayerMessage(dialogueManager, snapshot);
        DrawLastNpcResponse(dialogueManager);
        DrawContextSources(snapshot);
        DrawContextSourceReasons(snapshot);
        DrawRetrievedKnowledge(snapshot);
        DrawNearbySceneContext(dialogueManager, snapshot);
        DrawRelevantWorldEvents(snapshot);
        DrawPlayerState(snapshot);
        DrawWorldState(snapshot);
        DrawProviderStatus(dialogueManager);
        DrawPromptPreview(prompt);

        GUILayout.EndScrollView();
    }

    private void DrawPipelineFlow(DialogueManager dialogueManager, ContextSnapshot snapshot, string prompt)
    {
        DrawSectionHeader("Pipeline Flow");
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("UI Input -> DialogueManager -> ContextRetriever -> ContextSnapshot -> PromptBuilder -> LLM -> Response -> Memory", labelStyle);
        DrawStatusLine("UI Input", dialogueManager != null && HasText(dialogueManager.LastPlayerMessage));
        DrawStatusLine("ContextSnapshot built", snapshot != null);
        DrawStatusLine("Prompt built", HasText(prompt));
        DrawStatusLine("LLM response received", dialogueManager != null && dialogueManager.LastLLMResponseReceived);
        DrawStatusLine("Memory updated", dialogueManager != null && dialogueManager.LastResponseStoredInMemory);
        GUILayout.EndVertical();
    }

    private void DrawCurrentNpc(DialogueManager dialogueManager, ContextSnapshot snapshot)
    {
        DrawSectionHeader("Current NPC");
        GUILayout.BeginVertical(boxStyle);

        NPCProfile npc = ResolveNpc(dialogueManager, snapshot);
        DrawField("npcId", npc != null ? npc.npcId : null);
        DrawField("npcName/displayName", npc != null ? npc.npcName : null);
        DrawField("role", npc != null ? npc.role : null);
        DrawField("speakingStyle", npc != null ? npc.speakingStyle : null);
        DrawField("personality summary", npc != null ? npc.personality : null);

        GUILayout.EndVertical();
    }

    private void DrawNpcState(ContextSnapshot snapshot)
    {
        DrawSectionHeader("NPC State Toward Player");
        GUILayout.BeginVertical(boxStyle);

        NPCState npcState = snapshot != null ? snapshot.npcState : null;

        if (npcState == null)
        {
            DrawField("mood", "neutral or N/A");
            DrawField("trustToPlayer", "medium or N/A");
            DrawField("personalEvents", "None");
            GUILayout.EndVertical();
            return;
        }

        DrawField("source", "npc_personal_memory");
        DrawField("npcId", npcState.npcId);
        DrawField("mood", npcState.mood);
        DrawField("trustToPlayer", npcState.trustToPlayer);
        DrawField("personalEvents", InlineList(npcState.personalEvents));

        GUILayout.EndVertical();
    }

    private void DrawPlayerMessage(DialogueManager dialogueManager, ContextSnapshot snapshot)
    {
        DrawSectionHeader("Player Message");
        GUILayout.BeginVertical(boxStyle);
        string message = snapshot != null ? snapshot.playerMessage : (dialogueManager != null ? dialogueManager.LastPlayerMessage : null);
        DrawMultilineValue(message);
        GUILayout.EndVertical();
    }

    private void DrawLastNpcResponse(DialogueManager dialogueManager)
    {
        DrawSectionHeader("Last NPC Response");
        GUILayout.BeginVertical(boxStyle);
        DrawMultilineValue(dialogueManager != null ? dialogueManager.LastNpcResponse : null);
        GUILayout.EndVertical();
    }

    private void DrawContextSources(ContextSnapshot snapshot)
    {
        DrawSectionHeader("Context Sources");
        GUILayout.BeginVertical(boxStyle);

        DrawSourceLine("NPCProfile", snapshot != null && snapshot.npcProfile != null, snapshot != null && snapshot.npcProfile != null ? "present" : "N/A");
        DrawSourceLine("PlayerState", snapshot != null && snapshot.playerState != null, snapshot != null && snapshot.playerState != null ? "present" : "N/A");
        DrawSourceLine("Visible Player State", snapshot != null && snapshot.playerState != null, snapshot != null && snapshot.playerState != null ? "source: visible_player_state" : "N/A");
        DrawSourceLine("NPCState", snapshot != null && snapshot.npcState != null, snapshot != null && snapshot.npcState != null ? "source: npc_personal_memory" : "N/A");
        DrawSourceLine("WorldState", snapshot != null && snapshot.worldState != null, snapshot != null && snapshot.worldState != null ? "present" : "N/A");
        DrawSourceLine("Nearby SceneContextObjects", snapshot != null && snapshot.nearbyObjects != null, SourceCountText(snapshot != null ? snapshot.nearbyObjects : null));
        DrawSourceLine("Recent Relevant WorldEvents", snapshot != null && snapshot.recentRelevantEvents != null, SourceCountText(snapshot != null ? snapshot.recentRelevantEvents : null));
        DrawSourceLine("Retrieved KnowledgeEntries", snapshot != null && snapshot.retrievedKnowledge != null, SourceCountText(snapshot != null ? snapshot.retrievedKnowledge : null));
        DrawSourceLine("Recent Dialogue Memory", snapshot != null && snapshot.recentDialogueHistory != null, SourceCountText(snapshot != null ? snapshot.recentDialogueHistory : null));
        DrawSourceLine("Current Player Message", snapshot != null && HasText(snapshot.playerMessage), snapshot != null && HasText(snapshot.playerMessage) ? "present" : "N/A");

        GUILayout.EndVertical();
    }

    private void DrawContextSourceReasons(ContextSnapshot snapshot)
    {
        DrawSectionHeader("Context Source Reasons");
        GUILayout.BeginVertical(boxStyle);

        if (snapshot == null || snapshot.contextSourceReasons == null || snapshot.contextSourceReasons.Count == 0)
        {
            GUILayout.Label(snapshot == null ? "N/A" : "None", labelStyle);
            GUILayout.EndVertical();
            return;
        }

        for (int i = 0; i < snapshot.contextSourceReasons.Count; i++)
        {
            if (HasText(snapshot.contextSourceReasons[i]))
            {
                GUILayout.Label("- " + snapshot.contextSourceReasons[i], labelStyle);
            }
        }

        GUILayout.EndVertical();
    }

    private void DrawRetrievedKnowledge(ContextSnapshot snapshot)
    {
        DrawSectionHeader("Retrieved Knowledge");
        GUILayout.BeginVertical(boxStyle);

        if (cachedRetrievalEntries != null && cachedRetrievalEntries.Count > 0)
        {
            List<ContextRetriever.DebugKnowledgeRetrievalEntry> entries = new List<ContextRetriever.DebugKnowledgeRetrievalEntry>(cachedRetrievalEntries);
            entries.Sort(CompareKnowledgeDebugEntries);

            for (int i = 0; i < entries.Count; i++)
            {
                DrawKnowledgeDebugEntry(entries[i], snapshot);
            }
        }
        else if (snapshot != null && snapshot.retrievedKnowledge != null && snapshot.retrievedKnowledge.Count > 0)
        {
            for (int i = 0; i < snapshot.retrievedKnowledge.Count; i++)
            {
                DrawKnowledgeEntryWithoutDebug(snapshot.retrievedKnowledge[i]);
            }
        }
        else
        {
            GUILayout.Label(snapshot != null ? "None" : "N/A", labelStyle);
        }

        GUILayout.EndVertical();
    }

    private void DrawKnowledgeDebugEntry(ContextRetriever.DebugKnowledgeRetrievalEntry debugEntry, ContextSnapshot snapshot)
    {
        KnowledgeEntry entry = debugEntry != null ? debugEntry.entry : null;

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label((debugEntry != null && debugEntry.includedByRetriever ? "RETRIEVED" : "SKIPPED") + ": " + KnowledgeTitle(entry), labelStyle);
        DrawField("allowed", debugEntry != null ? BoolText(debugEntry.allowedForNpc) : null);
        DrawField("inContextSnapshot", entry != null && snapshot != null ? BoolText(SnapshotContainsKnowledge(snapshot, entry)) : null);
        DrawField("score", debugEntry != null ? debugEntry.finalScore.ToString() : null);
        DrawField("message_activation", debugEntry != null ? BoolText(debugEntry.hasMessageActivation) : null);
        DrawField("visible_state_activation", debugEntry != null ? BoolText(debugEntry.hasVisibleStateActivation) : null);
        DrawField("npc_state_activation", debugEntry != null ? BoolText(debugEntry.hasNpcStateActivation) : null);
        DrawField("world_event_activation", debugEntry != null ? BoolText(debugEntry.hasWorldEventActivation) : null);
        DrawField("world_state_activation", debugEntry != null ? BoolText(debugEntry.hasWorldStateActivation) : null);
        DrawField("local_activation", debugEntry != null ? BoolText(debugEntry.hasLocalActivation) : null);
        DrawField("final reason", debugEntry != null ? debugEntry.finalDecisionReason : null);
        DrawField("knownByNpcIds", entry != null ? InlineList(entry.knownByNpcIds) : null);
        DrawField("relatedObjectIds", entry != null ? InlineList(entry.relatedObjectIds) : null);
        GUILayout.EndVertical();
    }

    private void DrawKnowledgeEntryWithoutDebug(KnowledgeEntry entry)
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("RETRIEVED: " + KnowledgeTitle(entry), labelStyle);
        DrawField("allowed", null);
        DrawField("inContextSnapshot", null);
        DrawField("score", null);
        DrawField("message_activation", null);
        DrawField("visible_state_activation", null);
        DrawField("npc_state_activation", null);
        DrawField("world_event_activation", null);
        DrawField("world_state_activation", null);
        DrawField("local_activation", null);
        DrawField("final reason", null);
        DrawField("knownByNpcIds", entry != null ? InlineList(entry.knownByNpcIds) : null);
        DrawField("relatedObjectIds", entry != null ? InlineList(entry.relatedObjectIds) : null);
        GUILayout.EndVertical();
    }

    private void DrawNearbySceneContext(DialogueManager dialogueManager, ContextSnapshot snapshot)
    {
        DrawSectionHeader("Nearby Scene Context");
        GUILayout.BeginVertical(boxStyle);

        if (snapshot == null || snapshot.nearbyObjects == null || snapshot.nearbyObjects.Count == 0)
        {
            GUILayout.Label(snapshot == null ? "N/A" : "None", labelStyle);
            GUILayout.EndVertical();
            return;
        }

        Transform npcTransform = ResolveNpcTransform(dialogueManager);

        for (int i = 0; i < snapshot.nearbyObjects.Count; i++)
        {
            SceneContextObject contextObject = snapshot.nearbyObjects[i];

            GUILayout.BeginVertical(GUI.skin.box);
            DrawField("objectId", contextObject != null ? contextObject.objectId : null);
            DrawField("displayName", contextObject != null ? SafeText(contextObject.displayName, contextObject.gameObject.name) : null);
            DrawField("distance", GetDistanceText(npcTransform, contextObject));
            DrawField("tags", contextObject != null ? InlineList(contextObject.tags) : null);
            DrawField("stateFacts", contextObject != null ? InlineList(contextObject.stateFacts) : null);
            GUILayout.EndVertical();
        }

        GUILayout.EndVertical();
    }

    private void DrawRelevantWorldEvents(ContextSnapshot snapshot)
    {
        DrawSectionHeader("Recent Relevant Events");
        GUILayout.BeginVertical(boxStyle);

        if (snapshot == null || snapshot.recentRelevantEvents == null || snapshot.recentRelevantEvents.Count == 0)
        {
            GUILayout.Label(snapshot == null ? "N/A" : "None", labelStyle);
            GUILayout.EndVertical();
            return;
        }

        for (int i = 0; i < snapshot.recentRelevantEvents.Count; i++)
        {
            WorldEvent worldEvent = snapshot.recentRelevantEvents[i];

            GUILayout.BeginVertical(GUI.skin.box);
            DrawField("source", GetWorldEventSource(worldEvent, snapshot));
            DrawField("eventType", worldEvent != null ? worldEvent.eventType : null);
            DrawField("targetNpcId", worldEvent != null ? worldEvent.targetNpcId : null);
            DrawField("locationObjectId", worldEvent != null ? worldEvent.locationObjectId : null);
            DrawField("isPublic/isGlobal", worldEvent != null ? worldEvent.isPublic + " / " + worldEvent.isGlobal : null);
            DrawField("description", worldEvent != null ? worldEvent.description : null);
            GUILayout.EndVertical();
        }

        GUILayout.EndVertical();
    }

    private void DrawPlayerState(ContextSnapshot snapshot)
    {
        DrawSectionHeader("PlayerState");
        GUILayout.BeginVertical(boxStyle);

        PlayerState playerState = snapshot != null ? snapshot.playerState : null;

        if (playerState == null)
        {
            GUILayout.Label("N/A", labelStyle);
            GUILayout.EndVertical();
            return;
        }

        DrawField("currentRole", playerState.currentRole);
        DrawField("equippedOutfit", playerState.equippedOutfit);
        DrawField("visibleHeldItem", playerState.visibleHeldItem);
        DrawField("visibleStatusTags", InlineList(playerState.visibleStatusTags));
        DrawField("knownFacts", InlineList(playerState.knownFacts));
        DrawField("heldItems", InlineList(playerState.heldItems));
        DrawField("completedActions", InlineList(playerState.completedActions));

        GUILayout.EndVertical();
    }

    private void DrawWorldState(ContextSnapshot snapshot)
    {
        DrawSectionHeader("WorldState");
        GUILayout.BeginVertical(boxStyle);

        WorldState worldState = snapshot != null ? snapshot.worldState : null;

        if (worldState == null)
        {
            GUILayout.Label("N/A", labelStyle);
            GUILayout.EndVertical();
            return;
        }

        DrawField("currentEvent", worldState.currentEvent);
        DrawField("mood", worldState.villageMood);
        DrawField("global flags", "churchBellMissing=" + worldState.churchBellMissing +
            ", miraSawStranger=" + worldState.miraSawStranger +
            ", borinInspectedBellCase=" + worldState.borinInspectedBellCase +
            ", anselmReportedBellMissing=" + worldState.anselmReportedBellMissing);
        DrawField("global facts", InlineList(worldState.globalFacts));

        GUILayout.EndVertical();
    }

    private void DrawProviderStatus(DialogueManager dialogueManager)
    {
        DrawSectionHeader("LLM Provider Status");
        GUILayout.BeginVertical(boxStyle);

        OpenAIClient openAIClient = dialogueManager != null ? dialogueManager.DebugOpenAIClient : null;
        string intendedProvider = dialogueManager != null ? NormalizeStatusText(dialogueManager.LastIntendedLLMProvider) : null;
        string actualProvider = dialogueManager != null ? NormalizeStatusText(dialogueManager.LastActualLLMProvider) : null;
        bool openAIWasIntended = string.Equals(intendedProvider, "OpenAI", System.StringComparison.OrdinalIgnoreCase);
        bool mockWasIntended = string.Equals(intendedProvider, "Mock", System.StringComparison.OrdinalIgnoreCase);

        DrawField("intended provider", intendedProvider);
        DrawField("actual provider used for last response", actualProvider);
        DrawField("OpenAI success", openAIClient != null && openAIWasIntended ? openAIClient.LastOpenAIRequestSucceeded.ToString() : null);
        DrawField("HTTP status", openAIClient != null && openAIClient.LastHttpStatusCode > 0 ? openAIClient.LastHttpStatusCode.ToString() : null);
        DrawField("mock fallback used", openAIClient != null && openAIWasIntended ? openAIClient.LastMockFallbackUsed.ToString() : (mockWasIntended ? "false" : null));
        DrawField("model name", openAIClient != null && openAIWasIntended ? openAIClient.LastModelUsed : null);

        GUILayout.EndVertical();
    }

    private void DrawPromptPreview(string prompt)
    {
        DrawSectionHeader("Prompt Preview");
        GUILayout.BeginVertical(boxStyle, GUILayout.Height(PromptPreviewHeight));
        promptScrollPosition = GUILayout.BeginScrollView(promptScrollPosition);
        GUILayout.TextArea(SafeMultiline(prompt), promptStyle, GUILayout.ExpandHeight(true));
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void RefreshDebugSnapshot(DialogueManager dialogueManager)
    {
        ContextRetriever retriever = ResolveContextRetriever(dialogueManager);
        NPCProfile npc = ResolveNpc(dialogueManager, null);
        Transform npcTransform = ResolveNpcTransform(dialogueManager);
        string playerMessage = dialogueManager != null ? dialogueManager.LastPlayerMessage : string.Empty;

        if (retriever == null || npc == null || !HasText(playerMessage))
        {
            debugSnapshotNote = "Refresh Debug Snapshot unavailable: ContextRetriever, current NPC, or last player message is N/A.";
            return;
        }

        debugSnapshotOverride = retriever.BuildSnapshot(npc, npcTransform, playerMessage);
        debugPromptOverride = PromptBuilder.BuildPrompt(debugSnapshotOverride);
        liveSnapshotAtDebugRefresh = dialogueManager != null ? dialogueManager.LastContextSnapshot : null;
        usingDebugSnapshot = true;
        debugSnapshotNote = "Refresh Debug Snapshot used ContextRetriever.BuildSnapshot(currentNpc, currentNpcTransform, lastPlayerMessage) for inspection only.";
        RefreshRetrievalDebugCache(dialogueManager, debugSnapshotOverride);
    }

    private void ClearDebugSnapshotOverride()
    {
        debugSnapshotOverride = null;
        liveSnapshotAtDebugRefresh = null;
        debugPromptOverride = string.Empty;
        debugSnapshotNote = string.Empty;
        usingDebugSnapshot = false;
        cachedRetrievalSnapshot = null;
    }

    private void RefreshRetrievalDebugCache(DialogueManager dialogueManager, ContextSnapshot snapshot)
    {
        ContextRetriever retriever = ResolveContextRetriever(dialogueManager);
        string playerMessage = snapshot != null ? snapshot.playerMessage : (dialogueManager != null ? dialogueManager.LastPlayerMessage : string.Empty);

        if (cachedRetrievalSnapshot == snapshot && cachedRetrievalRetriever == retriever && cachedRetrievalPlayerMessage == playerMessage)
        {
            return;
        }

        cachedRetrievalSnapshot = snapshot;
        cachedRetrievalRetriever = retriever;
        cachedRetrievalPlayerMessage = playerMessage;
        cachedRetrievalEntries = new List<ContextRetriever.DebugKnowledgeRetrievalEntry>();

        NPCProfile npc = ResolveNpc(dialogueManager, snapshot);
        List<SceneContextObject> nearbyObjects = snapshot != null ? snapshot.nearbyObjects : null;

        if (retriever != null && npc != null)
        {
            if (snapshot != null)
            {
                cachedRetrievalEntries = retriever.DebugExplainKnowledgeRetrieval(
                    npc,
                    nearbyObjects,
                    playerMessage,
                    snapshot.playerState,
                    snapshot.worldState,
                    snapshot.npcState,
                    snapshot.recentRelevantEvents);
            }
            else
            {
                cachedRetrievalEntries = retriever.DebugExplainKnowledgeRetrieval(npc, nearbyObjects, playerMessage);
            }
        }
    }

    private ContextSnapshot GetDisplayedSnapshot(DialogueManager dialogueManager)
    {
        if (usingDebugSnapshot && debugSnapshotOverride != null)
        {
            return debugSnapshotOverride;
        }

        return dialogueManager != null ? dialogueManager.LastContextSnapshot : null;
    }

    private string GetDisplayedPrompt(DialogueManager dialogueManager)
    {
        if (usingDebugSnapshot)
        {
            return debugPromptOverride;
        }

        return dialogueManager != null ? dialogueManager.LastGeneratedPrompt : string.Empty;
    }

    private static DialogueManager ResolveDialogueManager()
    {
        return DialogueManager.Instance != null ? DialogueManager.Instance : UnityEngine.Object.FindFirstObjectByType<DialogueManager>();
    }

    private static ContextRetriever ResolveContextRetriever(DialogueManager dialogueManager)
    {
        if (dialogueManager != null && dialogueManager.DebugContextRetriever != null)
        {
            return dialogueManager.DebugContextRetriever;
        }

        return ContextRetriever.Instance != null ? ContextRetriever.Instance : UnityEngine.Object.FindFirstObjectByType<ContextRetriever>();
    }

    private static NPCProfile ResolveNpc(DialogueManager dialogueManager, ContextSnapshot snapshot)
    {
        if (dialogueManager != null && dialogueManager.CurrentNpc != null)
        {
            return dialogueManager.CurrentNpc;
        }

        return snapshot != null ? snapshot.npcProfile : null;
    }

    private static Transform ResolveNpcTransform(DialogueManager dialogueManager)
    {
        return dialogueManager != null ? dialogueManager.CurrentNpcTransform : null;
    }

    private void DrawSectionHeader(string text)
    {
        GUILayout.Space(8f);
        GUILayout.Label(text, sectionHeaderStyle);
    }

    private void DrawStatusLine(string label, bool complete)
    {
        GUILayout.Label((complete ? CompleteMarker : IncompleteMarker) + " " + label, labelStyle);
    }

    private void DrawSourceLine(string label, bool included, string detail)
    {
        GUILayout.Label((included ? CompleteMarker : IncompleteMarker) + " " + label + ": " + SafeText(detail, "N/A"), labelStyle);
    }

    private void DrawField(string label, string value)
    {
        GUILayout.Label(label + ": " + SafeText(value, "N/A"), labelStyle);
    }

    private void DrawMultilineValue(string value)
    {
        GUILayout.Label(SafeText(value, "N/A"), labelStyle);
    }

    private static int CompareKnowledgeDebugEntries(ContextRetriever.DebugKnowledgeRetrievalEntry a, ContextRetriever.DebugKnowledgeRetrievalEntry b)
    {
        if (a == null && b == null)
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        int includedCompare = b.includedByRetriever.CompareTo(a.includedByRetriever);

        if (includedCompare != 0)
        {
            return includedCompare;
        }

        int scoreCompare = b.finalScore.CompareTo(a.finalScore);

        if (scoreCompare != 0)
        {
            return scoreCompare;
        }

        KnowledgeEntry first = a.entry;
        KnowledgeEntry second = b.entry;
        return string.Compare(first != null ? first.title : string.Empty, second != null ? second.title : string.Empty, System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool SnapshotContainsKnowledge(ContextSnapshot snapshot, KnowledgeEntry entry)
    {
        if (snapshot == null || snapshot.retrievedKnowledge == null || entry == null)
        {
            return false;
        }

        for (int i = 0; i < snapshot.retrievedKnowledge.Count; i++)
        {
            if (snapshot.retrievedKnowledge[i] == entry)
            {
                return true;
            }

            if (snapshot.retrievedKnowledge[i] != null && !string.IsNullOrEmpty(entry.id) &&
                string.Equals(snapshot.retrievedKnowledge[i].id, entry.id, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string KnowledgeTitle(KnowledgeEntry entry)
    {
        if (entry == null)
        {
            return "N/A";
        }

        return SafeText(entry.id, "no id") + " / " + SafeText(entry.title, "no title");
    }

    private static string GetWorldEventSource(WorldEvent worldEvent, ContextSnapshot snapshot)
    {
        if (worldEvent == null)
        {
            return "N/A";
        }

        if (worldEvent.isGlobal)
        {
            return "global_event";
        }

        if (worldEvent.isPublic)
        {
            return "public_event";
        }

        if (snapshot != null && snapshot.npcProfile != null && HasText(worldEvent.targetNpcId) &&
            string.Equals(worldEvent.targetNpcId, snapshot.npcProfile.npcId, System.StringComparison.OrdinalIgnoreCase))
        {
            return "targeted_event";
        }

        return "local_environment_event";
    }

    private static string GetDistanceText(Transform npcTransform, SceneContextObject contextObject)
    {
        if (npcTransform == null || contextObject == null)
        {
            return null;
        }

        return Vector3.Distance(npcTransform.position, contextObject.transform.position).ToString("0.00");
    }

    private static string SourceCountText<T>(List<T> list)
    {
        return list != null ? list.Count + " included" : "N/A";
    }

    private static string InlineList(List<string> values)
    {
        if (values == null)
        {
            return "N/A";
        }

        List<string> cleanValues = new List<string>();

        for (int i = 0; i < values.Count; i++)
        {
            if (HasText(values[i]))
            {
                cleanValues.Add(values[i].Trim());
            }
        }

        return cleanValues.Count > 0 ? string.Join(", ", cleanValues.ToArray()) : "None";
    }

    private static string SafeText(string value, string fallback)
    {
        return HasText(value) ? value.Trim() : fallback;
    }

    private static string BoolText(bool value)
    {
        return value ? "true" : "false";
    }

    private static string NormalizeStatusText(string value)
    {
        if (!HasText(value) || string.Equals(value.Trim(), "None", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Trim();
    }

    private static string SafeMultiline(string value)
    {
        return HasText(value) ? value : "N/A";
    }

    private static bool HasText(string value)
    {
        return !string.IsNullOrEmpty(value) && value.Trim().Length > 0;
    }

    private void EnsureStyles()
    {
        if (panelStyle != null)
        {
            return;
        }

        panelBackground = MakeTexture(new Color(0.02f, 0.02f, 0.025f, 0.92f));
        sectionBackground = MakeTexture(new Color(0.12f, 0.12f, 0.14f, 0.82f));

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 10, 12),
            normal = { background = panelBackground }
        };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        sectionHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.9f, 0.9f, 0.92f) }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true,
            normal = { textColor = new Color(0.88f, 0.88f, 0.9f) }
        };

        smallLabelStyle = new GUIStyle(labelStyle)
        {
            fontSize = 11,
            normal = { textColor = new Color(0.72f, 0.72f, 0.76f) }
        };

        warningStyle = new GUIStyle(labelStyle)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.82f, 0.48f) }
        };

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 6, 6),
            normal = { background = sectionBackground }
        };

        promptStyle = new GUIStyle(GUI.skin.textArea)
        {
            wordWrap = true,
            normal = { textColor = Color.white },
            focused = { textColor = Color.white },
            hover = { textColor = Color.white },
            active = { textColor = Color.white }
        };
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (panelBackground != null)
        {
            Destroy(panelBackground);
        }

        if (sectionBackground != null)
        {
            Destroy(sectionBackground);
        }
    }
}
