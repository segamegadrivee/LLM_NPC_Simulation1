using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Place one DialogueManager on GameSystems and assign the explicit OpenAIClient or MockLLMClient fields.
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private ContextRetriever contextRetriever;
    [SerializeField] private NPCConversationMemoryStore conversationMemoryStore;
    [SerializeField, InspectorName("Use OpenAI")] private bool useOpenAI = true;
    [SerializeField, FormerlySerializedAs("llmClientBehaviour")] private OpenAIClient openAIClient;
    [FormerlySerializedAs("mockLLMClient")]
    [SerializeField] private MockLLMClient fallbackMockLLMClient;

    public NPCProfile currentNpc;
    public Transform currentNpcTransform;
    public List<DialogueMessage> messages = new List<DialogueMessage>();
    public bool IsOpen;
    public bool IsWaitingForResponse;
    public string LastGeneratedPrompt;
    public string CurrentLLMName { get; private set; } = "None";
    public ContextSnapshot LastContextSnapshot { get; private set; }
    public string LastPlayerMessage { get; private set; } = string.Empty;
    public string LastNpcResponse { get; private set; } = string.Empty;
    public string LastIntendedLLMProvider { get; private set; } = "None";
    public string LastActualLLMProvider { get; private set; } = "None";
    public bool LastLLMResponseReceived { get; private set; }
    public bool LastResponseStoredInMemory { get; private set; }

    private ILLMClient llmClient;

    public NPCProfile CurrentNpc
    {
        get { return currentNpc; }
    }

    public Transform CurrentNpcTransform
    {
        get { return currentNpcTransform; }
    }

    public ContextRetriever DebugContextRetriever
    {
        get { return contextRetriever; }
    }

    public NPCConversationMemoryStore DebugConversationMemoryStore
    {
        get { return conversationMemoryStore; }
    }

    public bool DebugUseOpenAI
    {
        get { return useOpenAI; }
    }

    public OpenAIClient DebugOpenAIClient
    {
        get { return openAIClient; }
    }

    public MockLLMClient DebugFallbackMockLLMClient
    {
        get { return fallbackMockLLMClient; }
    }

#if ENABLE_INPUT_SYSTEM
    private PlayerInput disabledPlayerInput;
#endif
    private Behaviour disabledStarterAssetsInputs;
    private Behaviour disabledFirstPersonController;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple DialogueManager instances found. Using the first one.", this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (contextRetriever == null)
        {
            contextRetriever = ContextRetriever.Instance != null ? ContextRetriever.Instance : FindFirstObjectByType<ContextRetriever>();
        }

        ResolveLLMClient();
        EnsureConversationMemoryStore();
    }

    public void OpenDialogue(NPCProfile profile, Transform npcTransform)
    {
        if (profile == null)
        {
            Debug.LogError("Cannot open dialogue because the NPC profile is null.", this);
            return;
        }

        currentNpc = profile;
        currentNpcTransform = npcTransform;
        IsOpen = true;
        EnsureMessagesList();
        EnsureConversationMemoryStore();
        messages.Clear();
        LastGeneratedPrompt = string.Empty;
        LastContextSnapshot = null;
        LastPlayerMessage = string.Empty;
        LastNpcResponse = string.Empty;
        LastIntendedLLMProvider = "None";
        LastActualLLMProvider = "None";
        LastLLMResponseReceived = false;
        LastResponseStoredInMemory = false;
        IsWaitingForResponse = false;
        LockDialogueInput();

        List<DialogueMessage> existingHistory = conversationMemoryStore != null ? conversationMemoryStore.GetHistory(currentNpc.npcId) : new List<DialogueMessage>();

        if (existingHistory.Count > 0)
        {
            for (int i = 0; i < existingHistory.Count; i++)
            {
                AddVisibleMessage(existingHistory[i]);
            }

            return;
        }

        DialogueMessage greeting = new DialogueMessage
        {
            speaker = currentNpc.npcName,
            text = "What do you need to know?"
        };

        AddVisibleMessage(greeting);
        AddMessageToMemory(currentNpc.npcId, greeting);
    }

    public void CloseDialogue()
    {
        IsOpen = false;
        IsWaitingForResponse = false;
        currentNpc = null;
        currentNpcTransform = null;
        UnlockDialogueInput();
    }

    public void SendPlayerMessage(string text)
    {
        if (!IsOpen)
        {
            Debug.LogWarning("Cannot send dialogue message because no NPC dialogue is open.", this);
            return;
        }

        if (currentNpc == null)
        {
            Debug.LogError("Cannot send dialogue message because the current NPC is null.", this);
            return;
        }

        if (IsWaitingForResponse)
        {
            return;
        }

        if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
        {
            return;
        }

        EnsureDependencies();
        EnsureMessagesList();

        DialogueMessage playerMessage = new DialogueMessage
        {
            speaker = "Player",
            text = text
        };

        AddVisibleMessage(playerMessage);

        IsWaitingForResponse = true;
        NPCProfile respondingNpc = currentNpc;
        LastContextSnapshot = null;
        LastGeneratedPrompt = string.Empty;
        LastPlayerMessage = text;
        LastNpcResponse = string.Empty;
        LastIntendedLLMProvider = CurrentLLMName;
        LastActualLLMProvider = "None";
        LastLLMResponseReceived = false;
        LastResponseStoredInMemory = false;

        if (contextRetriever == null)
        {
            AddNpcResponse(respondingNpc, "I cannot make sense of the village around me right now.");
            Debug.LogWarning("DialogueManager has no ContextRetriever assigned.", this);
            return;
        }

        if (llmClient == null)
        {
            AddNpcResponse(respondingNpc, "I have no answer ready yet.");
            Debug.LogError("DialogueManager has no ILLMClient assigned and no MockLLMClient fallback.", this);
            return;
        }

        ContextSnapshot snapshot = contextRetriever.BuildSnapshot(currentNpc, currentNpcTransform, text);
        LastContextSnapshot = snapshot;
        LastGeneratedPrompt = PromptBuilder.BuildPrompt(snapshot);
        AddMessageToMemory(currentNpc.npcId, playerMessage);
        LastActualLLMProvider = "Pending";
        Debug.Log("Generated NPC prompt:\n" + LastGeneratedPrompt, this);

        llmClient.SendPrompt(LastGeneratedPrompt, delegate(string response)
        {
            LastActualLLMProvider = ResolveActualLLMProviderName();
            LastLLMResponseReceived = true;
            AddNpcResponse(respondingNpc, response);
        });
    }

    private void AddNpcResponse(NPCProfile npc, string response)
    {
        EnsureMessagesList();

        DialogueMessage npcMessage = new DialogueMessage
        {
            speaker = npc != null ? npc.npcName : "NPC",
            text = string.IsNullOrEmpty(response) ? "..." : response
        };

        LastNpcResponse = npcMessage.text;

        if (IsOpen && IsSameNpc(npc, currentNpc))
        {
            AddVisibleMessage(npcMessage);
        }

        if (npc != null)
        {
            LastResponseStoredInMemory = AddMessageToMemory(npc.npcId, npcMessage);
        }
        else
        {
            LastResponseStoredInMemory = false;
        }

        IsWaitingForResponse = false;
    }

    private void EnsureDependencies()
    {
        if (contextRetriever == null)
        {
            contextRetriever = ContextRetriever.Instance != null ? ContextRetriever.Instance : FindFirstObjectByType<ContextRetriever>();
        }

        ResolveLLMClient();
        EnsureConversationMemoryStore();
    }

    public void ClearCurrentNpcMemory()
    {
        EnsureConversationMemoryStore();

        if (currentNpc == null)
        {
            Debug.LogWarning("Cannot clear dialogue memory because no current NPC is selected.", this);
            return;
        }

        if (conversationMemoryStore != null)
        {
            conversationMemoryStore.ClearHistory(currentNpc.npcId);
        }

        EnsureMessagesList();
        messages.Clear();
        LastGeneratedPrompt = string.Empty;
        LastContextSnapshot = null;
        LastPlayerMessage = string.Empty;
        LastNpcResponse = string.Empty;
        LastIntendedLLMProvider = "None";
        LastActualLLMProvider = "None";
        LastLLMResponseReceived = false;
        LastResponseStoredInMemory = false;
        IsWaitingForResponse = false;

        AddVisibleMessage(new DialogueMessage
        {
            speaker = currentNpc.npcName,
            text = "What do you need to know?"
        });
    }

    public void ClearAllDialogueMemory()
    {
        EnsureConversationMemoryStore();

        if (conversationMemoryStore != null)
        {
            conversationMemoryStore.ClearAll();
        }

        EnsureMessagesList();
        messages.Clear();
        LastGeneratedPrompt = string.Empty;
        LastContextSnapshot = null;
        LastPlayerMessage = string.Empty;
        LastNpcResponse = string.Empty;
        LastIntendedLLMProvider = "None";
        LastActualLLMProvider = "None";
        LastLLMResponseReceived = false;
        LastResponseStoredInMemory = false;
        IsWaitingForResponse = false;

        if (currentNpc != null)
        {
            AddVisibleMessage(new DialogueMessage
            {
                speaker = currentNpc.npcName,
                text = "What do you need to know?"
            });
        }
    }

    private void ResolveLLMClient()
    {
        // OpenAI is the only supported MVP runtime path. The Mock client is DEV-ONLY and becomes the
        // active client solely when no OpenAIClient is assigned, with a clearly distinct provider
        // label so the UI never presents scripted text as a real OpenAI answer.
        if (openAIClient != null)
        {
            if (!useOpenAI)
            {
                Debug.LogWarning("DialogueManager.useOpenAI is off, but OpenAI is the MVP runtime path. " +
                    "Using OpenAIClient anyway. Enable useOpenAI to remove this warning.", this);
            }

            llmClient = openAIClient;
            CurrentLLMName = "OpenAI";
            return;
        }

        if (fallbackMockLLMClient != null)
        {
            llmClient = fallbackMockLLMClient;
            CurrentLLMName = "Mock (DEV)";
            Debug.LogWarning("DialogueManager has no OpenAIClient assigned and is using the DEV MockLLMClient. " +
                "Assign an OpenAIClient for the diploma demo.", this);
            return;
        }

        llmClient = null;
        CurrentLLMName = "None";
        Debug.LogError("No LLM client assigned in DialogueManager.", this);
    }

    private string ResolveActualLLMProviderName()
    {
        if (llmClient == null)
        {
            return "None";
        }

        OpenAIClient selectedOpenAIClient = llmClient as OpenAIClient;
        if (selectedOpenAIClient != null)
        {
            return string.IsNullOrEmpty(selectedOpenAIClient.LastActualProvider)
                ? "OpenAI"
                : selectedOpenAIClient.LastActualProvider;
        }

        MockLLMClient selectedMockClient = llmClient as MockLLMClient;
        if (selectedMockClient != null)
        {
            return "Mock";
        }

        return llmClient.GetType().Name;
    }

    private void EnsureMessagesList()
    {
        if (messages == null)
        {
            messages = new List<DialogueMessage>();
        }
    }

    private void EnsureConversationMemoryStore()
    {
        if (conversationMemoryStore == null)
        {
            conversationMemoryStore = NPCConversationMemoryStore.Instance;
        }

        if (conversationMemoryStore != null)
        {
            AssignConversationMemoryStoreToContextRetriever();
            return;
        }

        Debug.LogWarning("DialogueManager: no NPCConversationMemoryStore in the scene. Creating a runtime " +
            "fallback. Add a persistent NPCConversationMemoryStore to GameSystems for the final scene.", this);
        GameObject memoryStoreObject = new GameObject("NPCConversationMemoryStore (runtime fallback)");
        conversationMemoryStore = memoryStoreObject.AddComponent<NPCConversationMemoryStore>();
        AssignConversationMemoryStoreToContextRetriever();
    }

    private void AssignConversationMemoryStoreToContextRetriever()
    {
        if (contextRetriever != null && contextRetriever.conversationMemoryStore == null)
        {
            contextRetriever.conversationMemoryStore = conversationMemoryStore;
        }
    }

    private void AddVisibleMessage(DialogueMessage message)
    {
        if (message == null)
        {
            return;
        }

        EnsureMessagesList();
        messages.Add(new DialogueMessage
        {
            speaker = message.speaker,
            text = message.text
        });
    }

    private bool AddMessageToMemory(string npcId, DialogueMessage message)
    {
        EnsureConversationMemoryStore();

        if (conversationMemoryStore != null)
        {
            conversationMemoryStore.AddMessage(npcId, message);
            return true;
        }

        return false;
    }

    private static bool IsSameNpc(NPCProfile first, NPCProfile second)
    {
        if (first == null || second == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(first.npcId) && !string.IsNullOrEmpty(second.npcId))
        {
            return string.Equals(first.npcId, second.npcId, System.StringComparison.OrdinalIgnoreCase);
        }

        return first == second;
    }

    private void LockDialogueInput()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameObject player = FindPlayer();

        if (player == null)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        PlayerInput playerInput = player.GetComponent<PlayerInput>();

        if (disabledPlayerInput == null && playerInput != null && playerInput.enabled)
        {
            playerInput.enabled = false;
            disabledPlayerInput = playerInput;
        }
#endif

        if (disabledStarterAssetsInputs == null)
        {
            disabledStarterAssetsInputs = DisableIfEnabled(player, "StarterAssets.StarterAssetsInputs");
        }

        if (disabledFirstPersonController == null)
        {
            disabledFirstPersonController = DisableIfEnabled(player, "StarterAssets.FirstPersonController");
        }
    }

    private void UnlockDialogueInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (disabledPlayerInput != null)
        {
            disabledPlayerInput.enabled = true;
            disabledPlayerInput = null;
        }
#endif

        EnableIfDisabledByDialogue(disabledStarterAssetsInputs);
        disabledStarterAssetsInputs = null;

        EnableIfDisabledByDialogue(disabledFirstPersonController);
        disabledFirstPersonController = null;

        RuntimeCursorLockGuard.RestoreGameplayCursorIfNoRuntimeUi();
    }

    private GameObject FindPlayer()
    {
        try
        {
            return GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException exception)
        {
            Debug.LogWarning("DialogueManager could not find a Player tag: " + exception.Message, this);
            return null;
        }
    }

    private Behaviour DisableIfEnabled(GameObject owner, string componentTypeName)
    {
        if (owner == null || string.IsNullOrEmpty(componentTypeName))
        {
            return null;
        }

        MonoBehaviour[] behaviours = owner.GetComponents<MonoBehaviour>();

        if (behaviours == null)
        {
            return null;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null || behaviour.GetType().FullName != componentTypeName || !behaviour.enabled)
            {
                continue;
            }

            behaviour.enabled = false;
            return behaviour;
        }

        return null;
    }

    private void EnableIfDisabledByDialogue(Behaviour behaviour)
    {
        if (behaviour != null)
        {
            behaviour.enabled = true;
        }
    }
}
