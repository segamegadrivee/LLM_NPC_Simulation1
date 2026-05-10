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

    private ILLMClient llmClient;

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
        AddMessageToMemory(currentNpc.npcId, playerMessage);

        IsWaitingForResponse = true;
        NPCProfile respondingNpc = currentNpc;

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
        LastGeneratedPrompt = PromptBuilder.BuildPrompt(snapshot);
        Debug.Log("Generated NPC prompt:\n" + LastGeneratedPrompt, this);

        llmClient.SendPrompt(LastGeneratedPrompt, delegate(string response)
        {
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

        if (IsOpen && IsSameNpc(npc, currentNpc))
        {
            AddVisibleMessage(npcMessage);
        }

        if (npc != null)
        {
            AddMessageToMemory(npc.npcId, npcMessage);
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
        if (useOpenAI && openAIClient != null)
        {
            llmClient = openAIClient;
            CurrentLLMName = "OpenAI";
            return;
        }

        if (fallbackMockLLMClient != null)
        {
            llmClient = fallbackMockLLMClient;
            CurrentLLMName = "Mock";
            return;
        }

        llmClient = null;
        CurrentLLMName = "None";
        Debug.LogError("No LLM client assigned in DialogueManager.", this);
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

        GameObject memoryStoreObject = new GameObject("NPCConversationMemoryStore");
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

    private void AddMessageToMemory(string npcId, DialogueMessage message)
    {
        EnsureConversationMemoryStore();

        if (conversationMemoryStore != null)
        {
            conversationMemoryStore.AddMessage(npcId, message);
        }
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

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
