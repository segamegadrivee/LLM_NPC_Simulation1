using System;
using System.IO;
using UnityEngine;

// Settings for the OpenAI-backed NPC dialogue client.
//
// Recommended local setup:
// Option A: set the OPENAI_API_KEY environment variable.
// Option B: create Assets/_Project/LocalSecrets/openai_key.txt and paste only the API key inside.
//
// Security:
// Never commit API keys. Do not paste keys into chat logs or screenshots. Do not log the
// Authorization header. InspectorUnsafe is only for quick local experiments.
//
// Model configuration:
// The model name is configurable so different models can be compared without code changes.
[CreateAssetMenu(fileName = "SO_OpenAI_Settings", menuName = "AI NPC/OpenAI Settings")]
public class OpenAISettings : ScriptableObject
{
    public enum ApiKeySource
    {
        EnvironmentVariable,
        LocalTextFile,
        InspectorUnsafe
    }

    public string model = "gpt-5.4-mini";
    public int maxOutputTokens = 350;
    public float temperature = 0.7f;
    public int requestTimeoutSeconds = 60;
    public bool includeTemperature = true;
    public bool logRequestPayload;
    public bool logRawResponse;

    [Header("API Key")]
    public ApiKeySource apiKeySource = ApiKeySource.EnvironmentVariable;
    public string environmentVariableName = "OPENAI_API_KEY";
    public string localKeyFilePath = "Assets/_Project/LocalSecrets/openai_key.txt";

    [Tooltip("Unsafe: only use for quick local experiments. Never commit API keys.")]
    public string inspectorApiKeyUnsafe = string.Empty;

    public string ResolveApiKey()
    {
        switch (apiKeySource)
        {
            case ApiKeySource.EnvironmentVariable:
                return ResolveEnvironmentApiKey();

            case ApiKeySource.LocalTextFile:
                return ResolveLocalFileApiKey();

            case ApiKeySource.InspectorUnsafe:
                return string.IsNullOrWhiteSpace(inspectorApiKeyUnsafe) ? string.Empty : inspectorApiKeyUnsafe.Trim();

            default:
                return string.Empty;
        }
    }

    private string ResolveEnvironmentApiKey()
    {
        if (string.IsNullOrWhiteSpace(environmentVariableName))
        {
            return string.Empty;
        }

        string value = Environment.GetEnvironmentVariable(environmentVariableName.Trim());
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private string ResolveLocalFileApiKey()
    {
        if (string.IsNullOrWhiteSpace(localKeyFilePath))
        {
            return string.Empty;
        }

        string path = ResolveProjectRelativePath(localKeyFilePath.Trim());

        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            string value = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not read OpenAI API key file: " + exception.Message, this);
            return string.Empty;
        }
    }

    private string ResolveProjectRelativePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        string projectRoot = Directory.GetParent(Application.dataPath) != null
            ? Directory.GetParent(Application.dataPath).FullName
            : Directory.GetCurrentDirectory();

        return Path.Combine(projectRoot, path);
    }
}
