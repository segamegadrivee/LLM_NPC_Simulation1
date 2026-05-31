using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class ContextEvidenceDumper
{
    private const string ReportTitle = "CONTEXT EVIDENCE REPORT";

    public static void DumpCurrentContextEvidence(DialogueManager dialogueManager)
    {
        DumpData data = BuildDumpData(dialogueManager);
        string report = BuildReport(data);

        Debug.Log(report);
        SaveReport(report);
    }

    private static DumpData BuildDumpData(DialogueManager providedDialogueManager)
    {
        DumpData data = new DumpData();
        data.dialogueManager = providedDialogueManager != null ? providedDialogueManager : FindDialogueManager();
        data.contextRetriever = ResolveContextRetriever(data.dialogueManager);
        data.memoryStore = ResolveMemoryStore(data.dialogueManager);

        if (data.dialogueManager != null)
        {
            data.currentNpc = data.dialogueManager.currentNpc;
            data.currentNpcTransform = data.dialogueManager.currentNpcTransform;
            data.playerMessage = data.dialogueManager.LastPlayerMessage ?? string.Empty;
        }

        if (data.contextRetriever != null && data.currentNpc != null)
        {
            data.snapshot = data.contextRetriever.BuildSnapshot(data.currentNpc, data.currentNpcTransform, data.playerMessage);
            data.finalPrompt = PromptBuilder.BuildPrompt(data.snapshot);
            data.snapshotBuildNote = "Built at dump time by ContextRetriever.BuildSnapshot(currentNpc, currentNpcTransform, lastPlayerMessage).";
        }
        else
        {
            data.snapshotBuildNote = BuildSnapshotUnavailableReason(data.contextRetriever, data.currentNpc);
        }

        data.playerState = data.snapshot != null && data.snapshot.playerState != null ? data.snapshot.playerState : FindPlayerState();
        data.worldState = data.snapshot != null && data.snapshot.worldState != null ? data.snapshot.worldState : ResolveWorldState();

        if (data.contextRetriever != null && data.currentNpc != null)
        {
            List<SceneContextObject> nearbyObjects = data.snapshot != null && data.snapshot.nearbyObjects != null
                ? data.snapshot.nearbyObjects
                : data.contextRetriever.FindNearbySceneObjects(data.currentNpcTransform);

            if (data.snapshot != null)
            {
                data.knowledgeRetrievalEvidence = data.contextRetriever.DebugExplainKnowledgeRetrieval(
                    data.currentNpc,
                    nearbyObjects,
                    data.playerMessage,
                    data.snapshot.playerState,
                    data.snapshot.worldState,
                    data.snapshot.npcState,
                    data.snapshot.recentRelevantEvents);
            }
            else
            {
                data.knowledgeRetrievalEvidence = data.contextRetriever.DebugExplainKnowledgeRetrieval(data.currentNpc, nearbyObjects, data.playerMessage);
            }
        }

        data.nearbySceneObjects = BuildNearbySceneObjectEvidence(data.contextRetriever, data.currentNpcTransform, data.snapshot);
        data.evidenceObjects = UnityEngine.Object.FindObjectsByType<EvidenceObject>(FindObjectsSortMode.None);
        return data;
    }

    private static string BuildReport(DumpData data)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(ReportTitle);
        builder.AppendLine("Generated At: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine("Persistent Data Path: " + Application.persistentDataPath);
        builder.AppendLine("Snapshot Build: " + SafeText(data.snapshotBuildNote, "NOT AVAILABLE: no build status."));
        builder.AppendLine();

        AppendSelectedNpc(builder, data);
        AppendPlayerState(builder, data);
        AppendNpcState(builder, data);
        AppendWorldState(builder, data);
        AppendNearbySceneObjects(builder, data);
        AppendRecentRelevantEvents(builder, data);
        AppendKnowledgeRetrieval(builder, data);
        AppendContextSnapshot(builder, data);
        AppendFinalPrompt(builder, data);
        AppendLlmProvider(builder, data);
        AppendConclusion(builder, data);

        return builder.ToString();
    }

    private static void AppendSelectedNpc(StringBuilder builder, DumpData data)
    {
        builder.AppendLine("=== SELECTED NPC ===");

        if (data.dialogueManager == null)
        {
            builder.AppendLine("NOT AVAILABLE: DialogueManager was not found.");
            builder.AppendLine();
            return;
        }

        if (data.currentNpc == null)
        {
            builder.AppendLine("NOT AVAILABLE: DialogueManager.currentNpc is null. Open a dialogue first.");
            builder.AppendLine();
            return;
        }

        NPCProfile npc = data.currentNpc;
        builder.AppendLine("source: NPCProfile asset - " + DescribeAsset(npc));
        builder.AppendLine("selected by: DialogueManager.currentNpc");
        builder.AppendLine("npc transform: " + DescribeTransform(data.currentNpcTransform));
        builder.AppendLine("npcId: " + SafeText(npc.npcId, "None"));
        builder.AppendLine("displayName: " + SafeText(npc.npcName, "None") + " (NPCProfile.npcName)");
        builder.AppendLine("role: " + SafeText(npc.role, "None"));
        builder.AppendLine("personality: " + SafeText(npc.personality, "None"));
        builder.AppendLine("background: " + SafeText(npc.backstory, "None"));
        builder.AppendLine("speaking style: " + SafeText(npc.speakingStyle, "None"));
        AppendStringList(builder, "knowledgeTags", npc.knowledgeTags);
        AppendStringList(builder, "knownFacts", npc.knownFacts);
        AppendStringList(builder, "relationships", npc.relationships);
        builder.AppendLine();
    }

    private static void AppendPlayerState(StringBuilder builder, DumpData data)
    {
        builder.AppendLine("=== PLAYER STATE ===");

        if (data.playerState == null)
        {
            builder.AppendLine("NOT AVAILABLE: PlayerState component was not found on the Player object or in the scene.");
            builder.AppendLine();
            return;
        }

        PlayerState playerState = data.playerState;
        builder.AppendLine("source: PlayerState component/object - " + DescribeComponent(playerState));
        builder.AppendLine("currentRole: " + SafeText(playerState.currentRole, "None"));
        builder.AppendLine("equippedOutfit: " + SafeText(playerState.equippedOutfit, "None"));
        builder.AppendLine("visibleHeldItem: " + SafeText(playerState.visibleHeldItem, "None"));
        AppendStringList(builder, "visibleStatusTags", playerState.visibleStatusTags);
        AppendStringList(builder, "knownFacts", playerState.knownFacts);
        AppendStringList(builder, "heldItems", playerState.heldItems);
        AppendStringList(builder, "completedActions", playerState.completedActions);
        builder.AppendLine();
    }

    private static void AppendNpcState(StringBuilder builder, DumpData data)
    {
        builder.AppendLine("=== NPC STATE TOWARD PLAYER ===");

        if (data.snapshot == null || data.snapshot.npcState == null)
        {
            builder.AppendLine("source: npc_personal_memory");
            builder.AppendLine("mood: neutral or NOT AVAILABLE");
            builder.AppendLine("trustToPlayer: medium or NOT AVAILABLE");
            builder.AppendLine("personalEvents:");
            builder.AppendLine("- None");
            builder.AppendLine();
            return;
        }

        NPCState npcState = data.snapshot.npcState;
        builder.AppendLine("source: npc_personal_memory - NPCStateStore.GetOrCreateState(active npcId)");
        builder.AppendLine("npcId: " + SafeText(npcState.npcId, "None"));
        builder.AppendLine("mood: " + SafeText(npcState.mood, "None"));
        builder.AppendLine("trustToPlayer: " + SafeText(npcState.trustToPlayer, "None"));
        AppendStringList(builder, "personalEvents", npcState.personalEvents);
        builder.AppendLine();
    }

    private static void AppendWorldState(StringBuilder builder, DumpData data)
    {
        builder.AppendLine("=== WORLD STATE ===");

        if (data.worldState == null)
        {
            builder.AppendLine("NOT AVAILABLE: WorldState object was not found.");
            builder.AppendLine();
            return;
        }

        WorldState worldState = data.worldState;
        builder.AppendLine("source: WorldState object - " + DescribeComponent(worldState));
        builder.AppendLine("current event: " + SafeText(worldState.currentEvent, "None"));
        builder.AppendLine("mood: " + SafeText(worldState.villageMood, "None"));
        builder.AppendLine("global flags:");
        builder.AppendLine("- churchBellMissing: " + worldState.churchBellMissing);
        builder.AppendLine("- miraSawStranger: " + worldState.miraSawStranger);
        builder.AppendLine("- borinInspectedBellCase: " + worldState.borinInspectedBellCase);
        builder.AppendLine("- anselmReportedBellMissing: " + worldState.anselmReportedBellMissing);
        AppendStringList(builder, "global facts", worldState.globalFacts);
        builder.AppendLine();
    }

    private static void AppendNearbySceneObjects(StringBuilder builder, DumpData data)
    {
        builder.AppendLine("=== NEARBY SCENE OBJECTS ===");

        if (data.currentNpcTransform == null)
        {
            builder.AppendLine("NOT AVAILABLE: current NPC Transform is null, so distance cannot be calculated.");
            builder.AppendLine();
            return;
        }

        if (data.contextRetriever == null)
        {
            builder.AppendLine("NOT AVAILABLE: ContextRetriever was not found, so sceneContextRadius is unknown.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("source: SceneContextObject components found within ContextRetriever.sceneContextRadius = " + data.contextRetriever.sceneContextRadius);

        if (data.nearbySceneObjects == null || data.nearbySceneObjects.Count == 0)
        {
            builder.AppendLine("None");
            builder.AppendLine();
            return;
        }

        for (int i = 0; i < data.nearbySceneObjects.Count; i++)
        {
            SceneObjectEvidence evidence = data.nearbySceneObjects[i];

            if (evidence == null || evidence.contextObject == null)
            {
                continue;
            }

            SceneContextObject contextObject = evidence.contextObject;
            builder.AppendLine("SceneContextObject #" + (i + 1));
            builder.AppendLine("- object id: " + SafeText(contextObject.objectId, "None"));
            builder.AppendLine("- object name: " + SafeText(contextObject.displayName, contextObject.gameObject.name));
            builder.AppendLine("- description: " + SafeText(contextObject.description, "None"));
            AppendStringList(builder, "- tags", contextObject.tags);
            AppendStringList(builder, "- stateFacts", contextObject.stateFacts);
            builder.AppendLine("- distance: " + evidence.distance.ToString("0.00"));
            builder.AppendLine("- included in ContextSnapshot: " + evidence.includedInSnapshot);
        }

        builder.AppendLine();
    }

    private static void AppendRecentRelevantEvents(StringBuilder builder, DumpData data)
    {
        builder.AppendLine("=== RECENT RELEVANT EVENTS ===");

        if (data.snapshot == null)
        {
            builder.AppendLine("NOT AVAILABLE: no snapshot was built.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("source: WorldEventLog filtered by active npcId, global/public flags, and non-targeted nearby location events.");

        if (data.snapshot.recentRelevantEvents == null || data.snapshot.recentRelevantEvents.Count == 0)
        {
            builder.AppendLine("None");
            builder.AppendLine();
            return;
        }

        for (int i = 0; i < data.snapshot.recentRelevantEvents.Count; i++)
        {
            WorldEvent worldEvent = data.snapshot.recentRelevantEvents[i];

            if (worldEvent == null)
            {
                continue;
            }

            builder.AppendLine("WorldEvent #" + (i + 1));
            builder.AppendLine("- source: " + GetWorldEventSource(worldEvent, data.snapshot));
            builder.AppendLine("- eventType: " + SafeText(worldEvent.eventType, "None"));
            builder.AppendLine("- actor: " + SafeText(worldEvent.actor, "None"));
            builder.AppendLine("- targetNpcId: " + SafeText(worldEvent.targetNpcId, "None"));
            builder.AppendLine("- locationObjectId: " + SafeText(worldEvent.locationObjectId, "None"));
            builder.AppendLine("- description: " + SafeText(worldEvent.description, "None"));
            builder.AppendLine("- isPublic: " + worldEvent.isPublic);
            builder.AppendLine("- isGlobal: " + worldEvent.isGlobal);
        }

        builder.AppendLine();
    }

    private static void AppendKnowledgeRetrieval(StringBuilder builder, DumpData data)
    {
        builder.AppendLine("=== KNOWLEDGE RETRIEVAL ===");

        if (data.contextRetriever == null)
        {
            builder.AppendLine("NOT AVAILABLE: ContextRetriever was not found.");
            builder.AppendLine();
            return;
        }

        if (data.contextRetriever.knowledgeBase == null)
        {
            builder.AppendLine("NOT AVAILABLE: ContextRetriever.knowledgeBase is not assigned.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("source: KnowledgeBase asset - " + DescribeAsset(data.contextRetriever.knowledgeBase));
        builder.AppendLine("maxKnowledgeEntries: " + data.contextRetriever.maxKnowledgeEntries);
        builder.AppendLine("playerMessage used for retrieval: " + SafeText(data.playerMessage, "None"));

        if (data.knowledgeRetrievalEvidence == null || data.knowledgeRetrievalEvidence.Count == 0)
        {
            builder.AppendLine("No KnowledgeEntry records were available in the KnowledgeBase.");
            builder.AppendLine();
            return;
        }

        List<ContextRetriever.DebugKnowledgeRetrievalEntry> retrieved = GetRetrievedKnowledgeEvidence(data.knowledgeRetrievalEvidence);
        List<ContextRetriever.DebugKnowledgeRetrievalEntry> skipped = GetSkippedKnowledgeEvidence(data.knowledgeRetrievalEvidence);

        builder.AppendLine();
        builder.AppendLine("Retrieved KnowledgeEntries:");

        if (retrieved.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            for (int i = 0; i < retrieved.Count; i++)
            {
                AppendRetrievedKnowledgeEntry(builder, retrieved[i], data.snapshot);
            }
        }

        builder.AppendLine();
        builder.AppendLine("Skipped KnowledgeEntries:");

        if (skipped.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            for (int i = 0; i < skipped.Count; i++)
            {
                AppendSkippedKnowledgeEntry(builder, skipped[i]);
            }
        }

        builder.AppendLine();
    }

    private static void AppendRetrievedKnowledgeEntry(StringBuilder builder, ContextRetriever.DebugKnowledgeRetrievalEntry evidence, ContextSnapshot snapshot)
    {
        KnowledgeEntry entry = evidence != null ? evidence.entry : null;

        if (entry == null)
        {
            builder.AppendLine("RETRIEVED: NOT AVAILABLE: KnowledgeEntry reference is null.");
            return;
        }

        builder.AppendLine("RETRIEVED #" + evidence.rank + ": " + SafeText(entry.id, "no id") + " / " + SafeText(entry.title, "no title"));
        builder.AppendLine("- content/summary: " + SafeText(entry.text, "None"));
        AppendStringList(builder, "- tags", entry.tags);
        AppendStringList(builder, "- knownByNpcIds", entry.knownByNpcIds);
        AppendStringList(builder, "- relatedObjectIds", entry.relatedObjectIds);
        builder.AppendLine("- importance: " + entry.importance);
        builder.AppendLine("- allowed: " + BoolText(evidence.allowedForNpc));
        builder.AppendLine("- inContextSnapshot: " + BoolText(SnapshotContainsKnowledge(snapshot, entry)));
        builder.AppendLine("- final score: " + evidence.finalScore);
        AppendActivationFlags(builder, evidence);
        builder.AppendLine("- final reason: " + SafeText(evidence.finalDecisionReason, "No decision reason recorded."));
        AppendStringList(builder, "- scoring reasons", evidence.retrievalReasons);
    }

    private static void AppendSkippedKnowledgeEntry(StringBuilder builder, ContextRetriever.DebugKnowledgeRetrievalEntry evidence)
    {
        KnowledgeEntry entry = evidence != null ? evidence.entry : null;

        if (entry == null)
        {
            builder.AppendLine("SKIPPED: NOT AVAILABLE: KnowledgeEntry reference is null.");
            return;
        }

        builder.AppendLine("SKIPPED: " + SafeText(entry.id, "no id") + " / " + SafeText(entry.title, "no title"));
        builder.AppendLine("- allowed: " + (evidence != null ? BoolText(evidence.allowedForNpc) : "NOT AVAILABLE"));
        builder.AppendLine("- inContextSnapshot: false");
        builder.AppendLine("- final score: " + (evidence != null ? evidence.finalScore.ToString() : "NOT AVAILABLE"));
        if (evidence != null)
        {
            AppendActivationFlags(builder, evidence);
        }
        builder.AppendLine("- final reason: " + (evidence != null ? SafeText(evidence.finalDecisionReason, "No decision reason recorded.") : "NOT AVAILABLE: no debug evidence."));

        if (evidence != null)
        {
            AppendStringList(builder, "- skipped scoring checks", evidence.skippedReasons);

            if (evidence.retrievalReasons != null && evidence.retrievalReasons.Count > 0)
            {
                AppendStringList(builder, "- positive scoring evidence", evidence.retrievalReasons);
            }
        }
    }

    private static void AppendActivationFlags(StringBuilder builder, ContextRetriever.DebugKnowledgeRetrievalEntry evidence)
    {
        if (builder == null || evidence == null)
        {
            return;
        }

        builder.AppendLine("- activation sources:");
        builder.AppendLine("  - message_activation: " + BoolText(evidence.hasMessageActivation));
        builder.AppendLine("  - visible_state_activation: " + BoolText(evidence.hasVisibleStateActivation));
        builder.AppendLine("  - npc_state_activation: " + BoolText(evidence.hasNpcStateActivation));
        builder.AppendLine("  - world_event_activation: " + BoolText(evidence.hasWorldEventActivation));
        builder.AppendLine("  - world_state_activation: " + BoolText(evidence.hasWorldStateActivation));
        builder.AppendLine("  - local_activation: " + BoolText(evidence.hasLocalActivation));
    }

    private static void AppendContextSnapshot(StringBuilder builder, DumpData data)
    {
        builder.AppendLine("=== CONTEXT SNAPSHOT ===");

        if (data.snapshot == null)
        {
            builder.AppendLine("NOT AVAILABLE: " + SafeText(data.snapshotBuildNote, "ContextSnapshot could not be built."));
            builder.AppendLine();
            return;
        }

        ContextSnapshot snapshot = data.snapshot;
        builder.AppendLine("source: ContextRetriever.BuildSnapshot()");
        builder.AppendLine("npcProfile: " + (snapshot.npcProfile != null ? DescribeAsset(snapshot.npcProfile) : "None"));
        builder.AppendLine("playerState: " + (snapshot.playerState != null ? DescribeComponent(snapshot.playerState) : "None"));
        builder.AppendLine("worldState: " + (snapshot.worldState != null ? DescribeComponent(snapshot.worldState) : "None"));
        builder.AppendLine("npcState: " + (snapshot.npcState != null ? SafeText(snapshot.npcState.npcId, "None") : "None"));
        builder.AppendLine("playerMessage: " + SafeText(snapshot.playerMessage, "None"));
        builder.AppendLine();

        builder.AppendLine("npcProfile fields:");
        if (snapshot.npcProfile != null)
        {
            builder.Append(snapshot.npcProfile.GetProfileContextText());
        }
        else
        {
            builder.AppendLine("None");
        }

        builder.AppendLine("playerState fields:");
        if (snapshot.playerState != null)
        {
            builder.Append(snapshot.playerState.GetPlayerStateText());
        }
        else
        {
            builder.AppendLine("None");
        }

        builder.AppendLine("worldState fields:");
        if (snapshot.worldState != null)
        {
            builder.Append(snapshot.worldState.GetWorldStateText());
        }
        else
        {
            builder.AppendLine("None");
        }

        builder.AppendLine("npcState fields:");
        if (snapshot.npcState != null)
        {
            builder.Append(snapshot.npcState.GetStateText());
        }
        else
        {
            builder.AppendLine("None");
        }

        builder.AppendLine("nearbyObjects list:");
        AppendSceneContextObjectList(builder, snapshot.nearbyObjects);

        builder.AppendLine("recentRelevantEvents list:");
        AppendWorldEventList(builder, snapshot.recentRelevantEvents, snapshot);

        builder.AppendLine("retrievedKnowledge list:");
        AppendKnowledgeEntryList(builder, snapshot.retrievedKnowledge);

        builder.AppendLine("recentDialogueHistory memory entries in snapshot:");
        AppendDialogueMessages(builder, snapshot.recentDialogueHistory);

        builder.AppendLine("contextSourceReasons:");
        AppendStringList(builder, "source reasons", snapshot.contextSourceReasons);

        builder.AppendLine("full NPC memory store history for selected NPC:");
        AppendFullMemoryHistory(builder, data);
        builder.AppendLine();
    }

    private static void AppendFinalPrompt(StringBuilder builder, DumpData data)
    {
        builder.AppendLine("=== FINAL PROMPT ===");

        if (string.IsNullOrEmpty(data.finalPrompt))
        {
            builder.AppendLine("NOT AVAILABLE: final prompt could not be generated because no ContextSnapshot was available.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("source: PromptBuilder.BuildPrompt(snapshot)");
        builder.AppendLine(data.finalPrompt);
        builder.AppendLine();
    }

    private static void AppendLlmProvider(StringBuilder builder, DumpData data)
    {
        builder.AppendLine("=== LLM PROVIDER ===");

        if (data.dialogueManager == null)
        {
            builder.AppendLine("NOT AVAILABLE: DialogueManager was not found.");
            builder.AppendLine();
            return;
        }

        DialogueManager dialogueManager = data.dialogueManager;
        OpenAIClient openAIClient = dialogueManager.DebugOpenAIClient;
        MockLLMClient mockClient = dialogueManager.DebugFallbackMockLLMClient;

        builder.AppendLine("configured Use OpenAI: " + dialogueManager.DebugUseOpenAI);
        builder.AppendLine("intended provider now: " + DetermineConfiguredProvider(dialogueManager));
        builder.AppendLine("DialogueManager.CurrentLLMName: " + SafeText(dialogueManager.CurrentLLMName, "None"));
        builder.AppendLine("intended provider for last prompt: " + SafeText(dialogueManager.LastIntendedLLMProvider, "None"));
        builder.AppendLine("actual provider used for last response: " + SafeText(dialogueManager.LastActualLLMProvider, "None"));
        builder.AppendLine("OpenAIClient assigned: " + (openAIClient != null));
        builder.AppendLine("MockLLMClient fallback assigned: " + (mockClient != null));

        if (openAIClient == null)
        {
            builder.AppendLine("OpenAI request succeeded: NOT AVAILABLE: OpenAIClient is not assigned.");
            builder.AppendLine("MockLLMClient fallback was used: NOT AVAILABLE: OpenAIClient is not assigned.");
        }
        else
        {
            builder.AppendLine("OpenAI request succeeded: " + openAIClient.LastOpenAIRequestSucceeded);
            builder.AppendLine("MockLLMClient fallback was used: " + openAIClient.LastMockFallbackUsed);
            builder.AppendLine("OpenAIClient.LastActualProvider: " + SafeText(openAIClient.LastActualProvider, "None"));
            builder.AppendLine("OpenAIClient.LastModelUsed: " + SafeText(openAIClient.LastModelUsed, "None"));
            builder.AppendLine("OpenAIClient.LastHttpStatusCode: " + openAIClient.LastHttpStatusCode);
            builder.AppendLine("OpenAIClient.LastFailureMessage: " + SafeText(openAIClient.LastFailureMessage, "None"));
        }

        if (mockClient == null)
        {
            builder.AppendLine("MockLLMClient last response: NOT AVAILABLE: MockLLMClient is not assigned.");
        }
        else
        {
            builder.AppendLine("MockLLMClient.LastMockResponseGenerated: " + mockClient.LastMockResponseGenerated);
            builder.AppendLine("MockLLMClient.LastActualProvider: " + SafeText(mockClient.LastActualProvider, "None"));
            builder.AppendLine("MockLLMClient.LastNpcName: " + SafeText(mockClient.LastNpcName, "None"));
        }

        builder.AppendLine();
    }

    private static void AppendConclusion(StringBuilder builder, DumpData data)
    {
        builder.AppendLine("=== CONCLUSION ===");
        builder.AppendLine("Inspector values used in prompt:");
        AppendPromptUsageConclusion(builder, data);
        builder.AppendLine();

        builder.AppendLine("Inspector values used in retrieval:");
        AppendRetrievalUsageConclusion(builder, data);
        builder.AppendLine();

        builder.AppendLine("Dynamic values:");
        AppendDynamicValuesConclusion(builder, data);
        builder.AppendLine();

        builder.AppendLine("Hardcoded/static values:");
        builder.AppendLine("- PromptBuilder roleplay rules and safety/voice rules are hardcoded/static prompt text.");
        builder.AppendLine("- NPCProfile values are manually authored ScriptableObject inspector values.");
        builder.AppendLine("- KnowledgeBase entries are manually authored ScriptableObject inspector values.");
        builder.AppendLine("- SceneContextObject descriptions/tags/stateFacts are manually authored scene inspector values.");
        builder.AppendLine();

        builder.AppendLine("Missing / suspicious:");
        AppendSuspiciousConclusion(builder, data);
        builder.AppendLine();
    }

    private static void AppendPromptUsageConclusion(StringBuilder builder, DumpData data)
    {
        if (string.IsNullOrEmpty(data.finalPrompt) || data.snapshot == null)
        {
            builder.AppendLine("- NOT AVAILABLE: final prompt was not generated.");
            return;
        }

        ContextSnapshot snapshot = data.snapshot;

        if (snapshot.npcProfile != null && data.finalPrompt.Contains(snapshot.npcProfile.GetProfileContextText()))
        {
            builder.AppendLine("- NPCProfile: npcId, npcName/displayName, role, personality, backstory/background, speakingStyle, knowledgeTags, knownFacts, relationships.");
        }
        else
        {
            builder.AppendLine("- NPCProfile: NOT AVAILABLE or profile text was not found in final prompt.");
        }

        if (snapshot.playerState != null)
        {
            builder.AppendLine("- PlayerState: currentRole plus visible outfit, visible held item, visible status tags, knownFacts, heldItems, completedActions.");
        }
        else
        {
            builder.AppendLine("- PlayerState: NOT AVAILABLE in snapshot.");
        }

        if (snapshot.npcState != null)
        {
            builder.AppendLine("- NPCState: mood, trustToPlayer, and personalEvents for only the active NPC.");
        }
        else
        {
            builder.AppendLine("- NPCState: NOT AVAILABLE in snapshot.");
        }

        if (snapshot.worldState != null && data.finalPrompt.Contains(snapshot.worldState.GetWorldStateText()))
        {
            builder.AppendLine("- WorldState: villageMood, currentEvent, churchBellMissing, miraSawStranger, borinInspectedBellCase, anselmReportedBellMissing, globalFacts.");
        }
        else
        {
            builder.AppendLine("- WorldState: NOT AVAILABLE or world text was not found in final prompt.");
        }

        if (snapshot.nearbyObjects == null || snapshot.nearbyObjects.Count == 0)
        {
            builder.AppendLine("- SceneContextObject: none appeared because snapshot.nearbyObjects is empty.");
        }
        else
        {
            for (int i = 0; i < snapshot.nearbyObjects.Count; i++)
            {
                SceneContextObject contextObject = snapshot.nearbyObjects[i];

                if (contextObject != null)
                {
                    builder.AppendLine("- SceneContextObject '" + SafeText(contextObject.objectId, contextObject.name) + "': objectId, displayName, objectType, description, tags, stateFacts.");
                }
            }
        }

        if (snapshot.recentRelevantEvents == null || snapshot.recentRelevantEvents.Count == 0)
        {
            builder.AppendLine("- WorldEventLog: no recent relevant events appeared.");
        }
        else
        {
            builder.AppendLine("- WorldEventLog: filtered recent relevant events appeared in RECENT RELEVANT EVENTS.");
        }

        if (snapshot.retrievedKnowledge == null || snapshot.retrievedKnowledge.Count == 0)
        {
            builder.AppendLine("- KnowledgeEntry: none appeared because snapshot.retrievedKnowledge is empty.");
        }
        else
        {
            for (int i = 0; i < snapshot.retrievedKnowledge.Count; i++)
            {
                KnowledgeEntry entry = snapshot.retrievedKnowledge[i];

                if (entry != null)
                {
                    builder.AppendLine("- KnowledgeEntry '" + SafeText(entry.id, entry.title) + "': id, title, text, tags, relatedObjectIds, knownByNpcIds, importance.");
                }
            }
        }

        if (snapshot.recentDialogueHistory == null || snapshot.recentDialogueHistory.Count == 0)
        {
            builder.AppendLine("- Dialogue memory: none appeared because snapshot.recentDialogueHistory is empty.");
        }
        else
        {
            builder.AppendLine("- Dialogue memory: recentDialogueHistory speaker/text entries.");
        }

        builder.AppendLine("- Player message: snapshot.playerMessage.");
    }

    private static void AppendRetrievalUsageConclusion(StringBuilder builder, DumpData data)
    {
        if (data.contextRetriever == null)
        {
            builder.AppendLine("- NOT AVAILABLE: ContextRetriever was not found.");
            return;
        }

        if (data.contextRetriever.knowledgeBase == null)
        {
            builder.AppendLine("- NOT AVAILABLE: ContextRetriever.knowledgeBase is not assigned.");
            return;
        }

        builder.AppendLine("- ContextRetriever.sceneContextRadius affected which SceneContextObjects were nearby.");
        builder.AppendLine("- ContextRetriever.maxKnowledgeEntries affected how many positive-scoring KnowledgeEntries were included.");
        builder.AppendLine("- NPCProfile.npcId was checked against KnowledgeEntry.knownByNpcIds.");
        builder.AppendLine("- NPCProfile.knowledgeTags were checked against KnowledgeEntry.tags.");
        builder.AppendLine("- Nearby SceneContextObject.objectId values were checked against KnowledgeEntry.relatedObjectIds.");
        builder.AppendLine("- Nearby SceneContextObject tags/stateFacts can support matching KnowledgeEntry.tags.");
        builder.AppendLine("- Player visible state, WorldState, NPCState, and relevant WorldEvents can support deterministic scoring after access control passes.");
        builder.AppendLine("- Player message text was checked against KnowledgeEntry.tags and KnowledgeEntry.title words.");
        builder.AppendLine("- KnowledgeEntry.importance contributed directly to final score.");

        List<string> positiveEvidence = CollectPositiveRetrievalEvidence(data.knowledgeRetrievalEvidence);

        if (positiveEvidence.Count == 0)
        {
            builder.AppendLine("- Positive scoring evidence this dump: None.");
            return;
        }

        builder.AppendLine("- Positive scoring evidence this dump:");

        for (int i = 0; i < positiveEvidence.Count; i++)
        {
            builder.AppendLine("  - " + positiveEvidence[i]);
        }
    }

    private static void AppendDynamicValuesConclusion(StringBuilder builder, DumpData data)
    {
        if (data.playerState == null)
        {
            builder.AppendLine("- PlayerState facts/items/actions: NOT AVAILABLE.");
        }
        else
        {
            builder.AppendLine("- PlayerState.currentRole: " + SafeText(data.playerState.currentRole, "None"));
            builder.AppendLine("- PlayerState.equippedOutfit: " + SafeText(data.playerState.equippedOutfit, "None"));
            builder.AppendLine("- PlayerState.visibleHeldItem: " + SafeText(data.playerState.visibleHeldItem, "None"));
            AppendStringList(builder, "- PlayerState.visibleStatusTags", data.playerState.visibleStatusTags);
            AppendStringList(builder, "- PlayerState.knownFacts", data.playerState.knownFacts);
            AppendStringList(builder, "- PlayerState.heldItems", data.playerState.heldItems);
            AppendStringList(builder, "- PlayerState.completedActions", data.playerState.completedActions);
        }

        if (data.snapshot != null && data.snapshot.npcState != null)
        {
            builder.AppendLine("- NPCState for active NPC: mood=" + SafeText(data.snapshot.npcState.mood, "None") + ", trustToPlayer=" + SafeText(data.snapshot.npcState.trustToPlayer, "None"));
            AppendStringList(builder, "- NPCState.personalEvents", data.snapshot.npcState.personalEvents);
        }

        if (data.snapshot == null)
        {
            builder.AppendLine("- Memory: NOT AVAILABLE because no snapshot was built.");
        }
        else
        {
            builder.AppendLine("- Memory entries in ContextSnapshot.recentDialogueHistory:");
            AppendDialogueMessages(builder, data.snapshot.recentDialogueHistory);
        }

        if (data.worldState == null)
        {
            builder.AppendLine("- WorldState: NOT AVAILABLE.");
        }
        else
        {
            builder.AppendLine("- WorldState.currentEvent: " + SafeText(data.worldState.currentEvent, "None"));
            builder.AppendLine("- WorldState.villageMood: " + SafeText(data.worldState.villageMood, "None"));
            builder.AppendLine("- WorldState flags: churchBellMissing=" + data.worldState.churchBellMissing + ", miraSawStranger=" + data.worldState.miraSawStranger + ", borinInspectedBellCase=" + data.worldState.borinInspectedBellCase + ", anselmReportedBellMissing=" + data.worldState.anselmReportedBellMissing);
            AppendStringList(builder, "- WorldState.globalFacts", data.worldState.globalFacts);
        }

        AppendEvidenceObjectConclusion(builder, data);
    }

    private static void AppendEvidenceObjectConclusion(StringBuilder builder, DumpData data)
    {
        builder.AppendLine("- Evidence facts:");

        if (data.evidenceObjects == null || data.evidenceObjects.Length == 0)
        {
            builder.AppendLine("  - None: no EvidenceObject components found.");
            return;
        }

        for (int i = 0; i < data.evidenceObjects.Length; i++)
        {
            EvidenceObject evidenceObject = data.evidenceObjects[i];

            if (evidenceObject == null)
            {
                continue;
            }

            builder.AppendLine("  - EvidenceObject '" + SafeText(evidenceObject.evidenceId, evidenceObject.name) + "' collected=" + evidenceObject.collected + ", displayName=" + SafeText(evidenceObject.displayName, evidenceObject.name));
            AppendIndentedStringList(builder, "factsToAddToPlayer", evidenceObject.factsToAddToPlayer, "    ");
            AppendIndentedStringList(builder, "itemsToAddToPlayer", evidenceObject.itemsToAddToPlayer, "    ");
        }
    }

    private static void AppendSuspiciousConclusion(StringBuilder builder, DumpData data)
    {
        if (data.snapshot == null)
        {
            builder.AppendLine("- ContextSnapshot was not built, so prompt evidence is incomplete.");
        }

        if (data.currentNpc == null)
        {
            builder.AppendLine("- No selected NPC. NPCProfile inspector values cannot affect the prompt until DialogueManager.currentNpc is set.");
        }

        if (data.playerState == null)
        {
            builder.AppendLine("- PlayerState missing. Player role, facts, items, and actions cannot affect the prompt.");
        }

        if (data.worldState == null)
        {
            builder.AppendLine("- WorldState missing. Current event, mood, flags, and global facts cannot affect the prompt.");
        }

        if (data.contextRetriever == null)
        {
            builder.AppendLine("- ContextRetriever missing. KnowledgeBase retrieval and nearby scene context cannot be proven.");
            return;
        }

        if (data.contextRetriever.knowledgeBase == null)
        {
            builder.AppendLine("- KnowledgeBase missing. Retrieved knowledge cannot affect the prompt.");
        }

        builder.AppendLine("- KnowledgeEntry.text/content does not affect retrieval score; it only appears in the prompt after the entry is selected.");
        builder.AppendLine("- KnowledgeBase access control runs before scoring: private entries are skipped unless knownByNpcIds includes the active npcId.");
        builder.AppendLine("- KnowledgeBase retrieval now requires a strong activation source; SceneContextObject data, NPC profile tags, and importance can support scoring but do not activate by themselves.");
        builder.AppendLine("- SceneContextObject displayName, objectType, tags, and stateFacts can support local scoring; descriptions still mainly appear after local context is included.");
        builder.AppendLine("- EvidenceObject metadata is not read directly by ContextRetriever or PromptBuilder. Evidence affects context only after facts/items are copied into PlayerState.");
    }

    private static void AppendSceneContextObjectList(StringBuilder builder, List<SceneContextObject> objects)
    {
        if (objects == null || objects.Count == 0)
        {
            builder.AppendLine("None");
            return;
        }

        for (int i = 0; i < objects.Count; i++)
        {
            SceneContextObject contextObject = objects[i];

            if (contextObject == null)
            {
                builder.AppendLine("- Null SceneContextObject");
                continue;
            }

            builder.AppendLine("[" + i + "] " + DescribeComponent(contextObject));
            builder.Append(contextObject.GetContextText());
        }
    }

    private static void AppendKnowledgeEntryList(StringBuilder builder, List<KnowledgeEntry> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            builder.AppendLine("None");
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            KnowledgeEntry entry = entries[i];

            if (entry == null)
            {
                builder.AppendLine("- Null KnowledgeEntry");
                continue;
            }

            builder.AppendLine("[" + i + "]");
            builder.Append(entry.GetKnowledgeText());
        }
    }

    private static void AppendWorldEventList(StringBuilder builder, List<WorldEvent> events, ContextSnapshot snapshot)
    {
        if (events == null || events.Count == 0)
        {
            builder.AppendLine("None");
            return;
        }

        for (int i = 0; i < events.Count; i++)
        {
            WorldEvent worldEvent = events[i];

            if (worldEvent == null)
            {
                builder.AppendLine("- Null WorldEvent");
                continue;
            }

            builder.AppendLine("[" + i + "] source: " + GetWorldEventSource(worldEvent, snapshot));
            builder.Append(worldEvent.GetEventText());
        }
    }

    private static void AppendDialogueMessages(StringBuilder builder, List<DialogueMessage> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            builder.AppendLine("- None");
            return;
        }

        for (int i = 0; i < messages.Count; i++)
        {
            DialogueMessage message = messages[i];

            if (message == null)
            {
                builder.AppendLine("- Null message");
                continue;
            }

            builder.AppendLine("- [" + i + "] " + SafeText(message.speaker, "Unknown") + ": " + SafeText(message.text, "..."));
        }
    }

    private static void AppendFullMemoryHistory(StringBuilder builder, DumpData data)
    {
        if (data.memoryStore == null)
        {
            builder.AppendLine("- NOT AVAILABLE: NPCConversationMemoryStore was not found.");
            return;
        }

        string npcId = data.currentNpc != null ? data.currentNpc.npcId : null;
        List<DialogueMessage> history = data.memoryStore.GetHistory(npcId);
        AppendDialogueMessages(builder, history);
    }

    private static void AppendStringList(StringBuilder builder, string label, List<string> values)
    {
        builder.AppendLine(label + ":");

        if (values == null || values.Count == 0)
        {
            builder.AppendLine("- None");
            return;
        }

        bool wroteValue = false;

        for (int i = 0; i < values.Count; i++)
        {
            if (!string.IsNullOrEmpty(values[i]) && values[i].Trim().Length > 0)
            {
                builder.AppendLine("- " + values[i].Trim());
                wroteValue = true;
            }
        }

        if (!wroteValue)
        {
            builder.AppendLine("- None");
        }
    }

    private static void AppendIndentedStringList(StringBuilder builder, string label, List<string> values, string indent)
    {
        builder.AppendLine(indent + label + ":");

        if (values == null || values.Count == 0)
        {
            builder.AppendLine(indent + "- None");
            return;
        }

        bool wroteValue = false;

        for (int i = 0; i < values.Count; i++)
        {
            if (!string.IsNullOrEmpty(values[i]) && values[i].Trim().Length > 0)
            {
                builder.AppendLine(indent + "- " + values[i].Trim());
                wroteValue = true;
            }
        }

        if (!wroteValue)
        {
            builder.AppendLine(indent + "- None");
        }
    }

    private static List<SceneObjectEvidence> BuildNearbySceneObjectEvidence(ContextRetriever contextRetriever, Transform npcTransform, ContextSnapshot snapshot)
    {
        List<SceneObjectEvidence> result = new List<SceneObjectEvidence>();

        if (contextRetriever == null || npcTransform == null)
        {
            return result;
        }

        SceneContextObject[] objects = UnityEngine.Object.FindObjectsByType<SceneContextObject>(FindObjectsSortMode.None);

        for (int i = 0; i < objects.Length; i++)
        {
            SceneContextObject contextObject = objects[i];

            if (contextObject == null)
            {
                continue;
            }

            float distance = Vector3.Distance(npcTransform.position, contextObject.transform.position);

            if (distance > contextRetriever.sceneContextRadius)
            {
                continue;
            }

            result.Add(new SceneObjectEvidence
            {
                contextObject = contextObject,
                distance = distance,
                includedInSnapshot = SnapshotContainsSceneObject(snapshot, contextObject)
            });
        }

        result.Sort(delegate(SceneObjectEvidence a, SceneObjectEvidence b)
        {
            return a.distance.CompareTo(b.distance);
        });

        return result;
    }

    private static List<ContextRetriever.DebugKnowledgeRetrievalEntry> GetRetrievedKnowledgeEvidence(List<ContextRetriever.DebugKnowledgeRetrievalEntry> allEvidence)
    {
        List<ContextRetriever.DebugKnowledgeRetrievalEntry> result = new List<ContextRetriever.DebugKnowledgeRetrievalEntry>();

        if (allEvidence == null)
        {
            return result;
        }

        for (int i = 0; i < allEvidence.Count; i++)
        {
            ContextRetriever.DebugKnowledgeRetrievalEntry entry = allEvidence[i];

            if (entry != null && entry.includedByRetriever)
            {
                result.Add(entry);
            }
        }

        result.Sort(delegate(ContextRetriever.DebugKnowledgeRetrievalEntry a, ContextRetriever.DebugKnowledgeRetrievalEntry b)
        {
            return a.rank.CompareTo(b.rank);
        });

        return result;
    }

    private static List<ContextRetriever.DebugKnowledgeRetrievalEntry> GetSkippedKnowledgeEvidence(List<ContextRetriever.DebugKnowledgeRetrievalEntry> allEvidence)
    {
        List<ContextRetriever.DebugKnowledgeRetrievalEntry> result = new List<ContextRetriever.DebugKnowledgeRetrievalEntry>();

        if (allEvidence == null)
        {
            return result;
        }

        for (int i = 0; i < allEvidence.Count; i++)
        {
            ContextRetriever.DebugKnowledgeRetrievalEntry entry = allEvidence[i];

            if (entry != null && !entry.includedByRetriever)
            {
                result.Add(entry);
            }
        }

        result.Sort(delegate(ContextRetriever.DebugKnowledgeRetrievalEntry a, ContextRetriever.DebugKnowledgeRetrievalEntry b)
        {
            if (a.rank < 0 && b.rank >= 0)
            {
                return 1;
            }

            if (a.rank >= 0 && b.rank < 0)
            {
                return -1;
            }

            if (a.rank >= 0 && b.rank >= 0)
            {
                return a.rank.CompareTo(b.rank);
            }

            return string.Compare(GetKnowledgeSortName(a.entry), GetKnowledgeSortName(b.entry), StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    private static List<string> CollectPositiveRetrievalEvidence(List<ContextRetriever.DebugKnowledgeRetrievalEntry> allEvidence)
    {
        List<string> result = new List<string>();

        if (allEvidence == null)
        {
            return result;
        }

        for (int i = 0; i < allEvidence.Count; i++)
        {
            ContextRetriever.DebugKnowledgeRetrievalEntry evidence = allEvidence[i];

            if (evidence == null || evidence.retrievalReasons == null || evidence.retrievalReasons.Count == 0)
            {
                continue;
            }

            string entryName = evidence.entry != null ? SafeText(evidence.entry.id, evidence.entry.title) : "unknown entry";

            for (int j = 0; j < evidence.retrievalReasons.Count; j++)
            {
                string reason = evidence.retrievalReasons[j];

                if (!string.IsNullOrEmpty(reason))
                {
                    result.Add(entryName + ": " + reason);
                }
            }
        }

        return result;
    }

    private static bool SnapshotContainsSceneObject(ContextSnapshot snapshot, SceneContextObject contextObject)
    {
        if (snapshot == null || snapshot.nearbyObjects == null || contextObject == null)
        {
            return false;
        }

        for (int i = 0; i < snapshot.nearbyObjects.Count; i++)
        {
            if (snapshot.nearbyObjects[i] == contextObject)
            {
                return true;
            }
        }

        return false;
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
        }

        return false;
    }

    private static DialogueManager FindDialogueManager()
    {
        if (DialogueManager.Instance != null)
        {
            return DialogueManager.Instance;
        }

        return UnityEngine.Object.FindFirstObjectByType<DialogueManager>();
    }

    private static ContextRetriever ResolveContextRetriever(DialogueManager dialogueManager)
    {
        if (dialogueManager != null && dialogueManager.DebugContextRetriever != null)
        {
            return dialogueManager.DebugContextRetriever;
        }

        if (ContextRetriever.Instance != null)
        {
            return ContextRetriever.Instance;
        }

        return UnityEngine.Object.FindFirstObjectByType<ContextRetriever>();
    }

    private static NPCConversationMemoryStore ResolveMemoryStore(DialogueManager dialogueManager)
    {
        if (dialogueManager != null && dialogueManager.DebugConversationMemoryStore != null)
        {
            return dialogueManager.DebugConversationMemoryStore;
        }

        if (NPCConversationMemoryStore.Instance != null)
        {
            return NPCConversationMemoryStore.Instance;
        }

        return UnityEngine.Object.FindFirstObjectByType<NPCConversationMemoryStore>();
    }

    private static PlayerState FindPlayerState()
    {
        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                PlayerState playerState = playerObject.GetComponent<PlayerState>();

                if (playerState != null)
                {
                    return playerState;
                }
            }
        }
        catch (UnityException)
        {
        }

        return UnityEngine.Object.FindFirstObjectByType<PlayerState>();
    }

    private static WorldState ResolveWorldState()
    {
        if (WorldState.Instance != null)
        {
            return WorldState.Instance;
        }

        return UnityEngine.Object.FindFirstObjectByType<WorldState>();
    }

    private static string BuildSnapshotUnavailableReason(ContextRetriever contextRetriever, NPCProfile currentNpc)
    {
        if (contextRetriever == null)
        {
            return "NOT AVAILABLE: ContextRetriever was not found.";
        }

        if (currentNpc == null)
        {
            return "NOT AVAILABLE: no current NPC is selected.";
        }

        return "NOT AVAILABLE: unknown snapshot build failure.";
    }

    private static string DetermineConfiguredProvider(DialogueManager dialogueManager)
    {
        if (dialogueManager == null)
        {
            return "NOT AVAILABLE: DialogueManager was not found.";
        }

        if (dialogueManager.DebugUseOpenAI && dialogueManager.DebugOpenAIClient != null)
        {
            return "OpenAI";
        }

        if (dialogueManager.DebugFallbackMockLLMClient != null)
        {
            return "Mock";
        }

        return "None";
    }

    private static void SaveReport(string report)
    {
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            string fileName = "ContextEvidenceReport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
            string filePath = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllText(filePath, report, Encoding.UTF8);
            Debug.Log("CONTEXT EVIDENCE REPORT saved to: " + filePath);
        }
        catch (Exception exception)
        {
            Debug.LogError("Failed to save CONTEXT EVIDENCE REPORT: " + exception.Message);
        }
    }

    private static string DescribeAsset(UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return "NOT AVAILABLE: asset reference is null.";
        }

        string description = asset.GetType().Name + " '" + asset.name + "'";

#if UNITY_EDITOR
        string assetPath = AssetDatabase.GetAssetPath(asset);

        if (!string.IsNullOrEmpty(assetPath))
        {
            description += " at " + assetPath;
        }
#endif

        return description;
    }

    private static string DescribeComponent(Component component)
    {
        if (component == null)
        {
            return "NOT AVAILABLE: component reference is null.";
        }

        return component.GetType().Name + " on " + GetGameObjectPath(component.gameObject);
    }

    private static string DescribeTransform(Transform transform)
    {
        if (transform == null)
        {
            return "NOT AVAILABLE: Transform is null.";
        }

        return GetGameObjectPath(transform.gameObject);
    }

    private static string GetGameObjectPath(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return "NOT AVAILABLE: GameObject is null.";
        }

        string path = gameObject.name;
        Transform current = gameObject.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static string GetKnowledgeSortName(KnowledgeEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        return SafeText(entry.title, entry.id);
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

        if (snapshot != null && snapshot.npcProfile != null &&
            !string.IsNullOrEmpty(worldEvent.targetNpcId) &&
            string.Equals(worldEvent.targetNpcId, snapshot.npcProfile.npcId, StringComparison.OrdinalIgnoreCase))
        {
            return "targeted_event";
        }

        return "local_environment_event";
    }

    private static string SafeText(string value, string fallback)
    {
        return string.IsNullOrEmpty(value) || value.Trim().Length == 0 ? fallback : value;
    }

    private static string BoolText(bool value)
    {
        return value ? "true" : "false";
    }

    private class DumpData
    {
        public DialogueManager dialogueManager;
        public ContextRetriever contextRetriever;
        public NPCConversationMemoryStore memoryStore;
        public NPCProfile currentNpc;
        public Transform currentNpcTransform;
        public PlayerState playerState;
        public WorldState worldState;
        public ContextSnapshot snapshot;
        public string finalPrompt;
        public string playerMessage = string.Empty;
        public string snapshotBuildNote = string.Empty;
        public List<SceneObjectEvidence> nearbySceneObjects = new List<SceneObjectEvidence>();
        public List<ContextRetriever.DebugKnowledgeRetrievalEntry> knowledgeRetrievalEvidence = new List<ContextRetriever.DebugKnowledgeRetrievalEntry>();
        public EvidenceObject[] evidenceObjects = new EvidenceObject[0];
    }

    private class SceneObjectEvidence
    {
        public SceneContextObject contextObject;
        public float distance;
        public bool includedInSnapshot;
    }
}
