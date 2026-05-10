using UnityEditor;
using UnityEngine;

public static class CreateOpenAISettingsAsset
{
    private const string SettingsPath = "Assets/_Project/Data/AI/SO_OpenAI_Settings.asset";

    [MenuItem("Tools/AI NPC/Create OpenAI Settings")]
    public static void CreateSettingsAsset()
    {
        EnsureFolder("Assets", "_Project");
        EnsureFolder("Assets/_Project", "Data");
        EnsureFolder("Assets/_Project/Data", "AI");

        OpenAISettings settings = AssetDatabase.LoadAssetAtPath<OpenAISettings>(SettingsPath);

        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<OpenAISettings>();
            settings.model = "gpt-5.4-mini";
            settings.maxOutputTokens = 350;
            settings.temperature = 0.7f;
            settings.requestTimeoutSeconds = 60;
            settings.includeTemperature = true;
            settings.apiKeySource = OpenAISettings.ApiKeySource.EnvironmentVariable;
            settings.environmentVariableName = "OPENAI_API_KEY";
            settings.localKeyFilePath = "Assets/_Project/LocalSecrets/openai_key.txt";

            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Created OpenAI settings asset at " + SettingsPath + ".");
        }
        else
        {
            Debug.Log("OpenAI settings asset already exists at " + SettingsPath + ". It was not overwritten.");
        }

        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
