using UnityEngine;

// Keeps runtime IMGUI menus usable after Unity loses/regains focus.
// StarterAssetsInputs relocks the cursor on focus, so debug/chat UI must explicitly win while open.
public class RuntimeCursorLockGuard : MonoBehaviour
{
    public static RuntimeCursorLockGuard Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        EnsureInstance();
    }

    public static RuntimeCursorLockGuard EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        RuntimeCursorLockGuard existing = FindFirstObjectByType<RuntimeCursorLockGuard>();

        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject guardObject = new GameObject("RuntimeCursorLockGuard");
        Instance = guardObject.AddComponent<RuntimeCursorLockGuard>();
        DontDestroyOnLoad(guardObject);
        return Instance;
    }

    public static bool ShouldReleaseCursorForRuntimeUi()
    {
        DialogueManager dialogueManager = DialogueManager.Instance;

        if (dialogueManager != null && dialogueManager.IsOpen)
        {
            return true;
        }

        ContextDebugOverlay debugOverlay = ContextDebugOverlay.Instance;
        return debugOverlay != null && debugOverlay.IsVisible;
    }

    public static void ReleaseCursorForRuntimeUi()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void RestoreGameplayCursorIfNoRuntimeUi()
    {
        if (ShouldReleaseCursorForRuntimeUi())
        {
            ReleaseCursorForRuntimeUi();
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        if (ShouldReleaseCursorForRuntimeUi())
        {
            ReleaseCursorForRuntimeUi();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && ShouldReleaseCursorForRuntimeUi())
        {
            ReleaseCursorForRuntimeUi();
        }
    }
}
