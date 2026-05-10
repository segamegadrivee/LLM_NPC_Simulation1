using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// OpenAI-backed LLM client for NPC dialogue.
//
// Recommended local prototype setup:
// Option A: set the OPENAI_API_KEY environment variable.
// Option B: create Assets/_Project/LocalSecrets/openai_key.txt and paste only the API key inside.
//
// Security:
// Never commit the key. Do not paste API keys into chat logs or screenshots. This client never
// logs the Authorization header or API key.
//
// Model configuration:
// The default model comes from OpenAISettings and is gpt-5.4-mini for low-latency,
// cost-efficient, context-aware NPC dialogue. Change the ScriptableObject later to compare
// different models for diploma experiments.
public class OpenAIClient : MonoBehaviour, ILLMClient
{
    private const string ResponsesEndpoint = "https://api.openai.com/v1/responses";

    public OpenAISettings settings;
    public bool useMockOnFailure = true;
    public MockLLMClient fallbackMockClient;

    private void Awake()
    {
        EnsureFallbackMockClient();
    }

    public void SendPrompt(string prompt, Action<string> onResponse)
    {
        StartCoroutine(SendPromptCoroutine(prompt, onResponse));
    }

    private System.Collections.IEnumerator SendPromptCoroutine(string prompt, Action<string> onResponse)
    {
        if (settings == null)
        {
            CompleteWithFallbackOrError(prompt, onResponse, "[OpenAISettings is not assigned.]", true);
            yield break;
        }

        string apiKey = settings.ResolveApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            CompleteWithFallbackOrError(prompt, onResponse, "[OpenAI API key is missing. Configure OpenAISettings.]", true);
            yield break;
        }

        string requestPayload = BuildRequestPayload(prompt);

        if (settings.logRequestPayload)
        {
            Debug.Log("OpenAI request payload:\n" + requestPayload, this);
        }

        using (UnityWebRequest request = new UnityWebRequest(ResponsesEndpoint, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, settings.requestTimeoutSeconds);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

            if (settings.logRawResponse && !string.IsNullOrEmpty(responseBody))
            {
                Debug.Log("OpenAI raw response:\n" + responseBody, this);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                HandleFailedRequest(prompt, onResponse, request, responseBody);
                yield break;
            }

            Debug.Log("OpenAI response completed with HTTP status " + request.responseCode + ".", this);

            string parseError;
            string responseText = TryExtractResponseText(responseBody, out parseError);

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                if (onResponse != null)
                {
                    onResponse(responseText.Trim());
                }

                yield break;
            }

            string message = string.IsNullOrWhiteSpace(parseError)
                ? "[OpenAI returned an empty response.]"
                : "[OpenAI response parse error: " + parseError + "]";

            CompleteWithFallbackOrError(prompt, onResponse, message, true);
        }
    }

    private string BuildRequestPayload(string prompt)
    {
        // Keep this builder small and explicit so future diploma work can add reasoning settings,
        // tool definitions, structured outputs, or action-generation schemas in one place.
        string model = settings != null && !string.IsNullOrWhiteSpace(settings.model)
            ? settings.model.Trim()
            : "gpt-5.4-mini";

        int maxOutputTokens = settings != null ? Mathf.Max(1, settings.maxOutputTokens) : 350;
        string input = prompt ?? string.Empty;

        if (settings != null && settings.includeTemperature)
        {
            OpenAIResponsesRequestWithTemperature request = new OpenAIResponsesRequestWithTemperature
            {
                model = model,
                input = input,
                max_output_tokens = maxOutputTokens,
                temperature = Mathf.Clamp(settings.temperature, 0f, 2f)
            };

            return JsonUtility.ToJson(request);
        }

        OpenAIResponsesRequest baseRequest = new OpenAIResponsesRequest
        {
            model = model,
            input = input,
            max_output_tokens = maxOutputTokens
        };

        return JsonUtility.ToJson(baseRequest);
    }

    private void HandleFailedRequest(string prompt, Action<string> onResponse, UnityWebRequest request, string responseBody)
    {
        long statusCode = request != null ? request.responseCode : 0;
        string requestError = request != null ? request.error : "Unknown UnityWebRequest error";
        string errorMessage = TryExtractErrorMessage(responseBody);

        Debug.LogError(
            "OpenAI request failed. HTTP status: " + statusCode +
            ". Unity error: " + SafeText(requestError, "none") +
            ". Error body: " + SafeText(responseBody, "empty"),
            this);

        if (LooksLikeUnsupportedOptionalParameter(errorMessage, responseBody))
        {
            Debug.LogError(
                "OpenAI rejected an optional request parameter. If this mentions temperature, disable " +
                "includeTemperature on OpenAISettings for this model, then retry.",
                this);
        }

        string userFacingMessage = !string.IsNullOrWhiteSpace(errorMessage)
            ? "[OpenAI request failed: " + errorMessage + "]"
            : "[OpenAI request failed. Check the Console for HTTP status and error details.]";

        CompleteWithFallbackOrError(prompt, onResponse, userFacingMessage, false);
    }

    private string TryExtractResponseText(string responseBody, out string parseError)
    {
        parseError = string.Empty;

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            parseError = "empty response body";
            return string.Empty;
        }

        OpenAIResponsesResponse response;

        try
        {
            response = JsonUtility.FromJson<OpenAIResponsesResponse>(responseBody);
        }
        catch (Exception exception)
        {
            parseError = exception.Message;
            return string.Empty;
        }

        if (response == null)
        {
            parseError = "JSON body did not match expected Responses shape";
            return string.Empty;
        }

        if (response.error != null && !string.IsNullOrWhiteSpace(response.error.message))
        {
            parseError = response.error.message;
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(response.output_text))
        {
            return response.output_text;
        }

        if (response.output == null)
        {
            parseError = "missing output array";
            return string.Empty;
        }

        StringBuilder textBuilder = new StringBuilder();

        for (int i = 0; i < response.output.Length; i++)
        {
            OpenAIOutputItem outputItem = response.output[i];

            if (outputItem == null || outputItem.content == null)
            {
                continue;
            }

            for (int j = 0; j < outputItem.content.Length; j++)
            {
                OpenAIContentItem contentItem = outputItem.content[j];

                if (contentItem != null && !string.IsNullOrWhiteSpace(contentItem.text))
                {
                    if (textBuilder.Length > 0)
                    {
                        textBuilder.AppendLine();
                    }

                    textBuilder.Append(contentItem.text.Trim());
                }
            }
        }

        if (textBuilder.Length > 0)
        {
            return textBuilder.ToString();
        }

        parseError = "no text content found in response output";
        return string.Empty;
    }

    private string TryExtractErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        try
        {
            OpenAIErrorWrapper wrapper = JsonUtility.FromJson<OpenAIErrorWrapper>(responseBody);

            if (wrapper != null && wrapper.error != null && !string.IsNullOrWhiteSpace(wrapper.error.message))
            {
                return wrapper.error.message;
            }
        }
        catch (Exception)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private bool LooksLikeUnsupportedOptionalParameter(string errorMessage, string responseBody)
    {
        string combined = ((errorMessage ?? string.Empty) + " " + (responseBody ?? string.Empty)).ToLowerInvariant();

        if (!combined.Contains("unsupported") && !combined.Contains("not support") && !combined.Contains("unknown parameter"))
        {
            return false;
        }

        return combined.Contains("temperature") || combined.Contains("parameter");
    }

    private void CompleteWithFallbackOrError(string prompt, Action<string> onResponse, string message, bool logError)
    {
        EnsureFallbackMockClient();

        if (useMockOnFailure && fallbackMockClient != null)
        {
            if (logError)
            {
                Debug.LogError(message + " Using MockLLMClient fallback.", this);
            }
            else
            {
                Debug.LogWarning(message + " Using MockLLMClient fallback.", this);
            }

            fallbackMockClient.SendPrompt(prompt, onResponse);
            return;
        }

        if (logError)
        {
            Debug.LogError(message, this);
        }
        else
        {
            Debug.LogWarning(message, this);
        }

        if (onResponse != null)
        {
            onResponse(message);
        }
    }

    private void EnsureFallbackMockClient()
    {
        if (fallbackMockClient == null)
        {
            fallbackMockClient = FindFirstObjectByType<MockLLMClient>();
        }
    }

    private string SafeText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}

[Serializable]
public class OpenAIResponsesRequest
{
    public string model;
    public string input;
    public int max_output_tokens;
}

[Serializable]
public class OpenAIResponsesRequestWithTemperature
{
    public string model;
    public string input;
    public int max_output_tokens;
    public float temperature;
}

[Serializable]
public class OpenAIResponsesResponse
{
    public string output_text;
    public OpenAIOutputItem[] output;
    public OpenAIError error;
}

[Serializable]
public class OpenAIOutputItem
{
    public string id;
    public string type;
    public string status;
    public string role;
    public OpenAIContentItem[] content;
}

[Serializable]
public class OpenAIContentItem
{
    public string type;
    public string text;
}

[Serializable]
public class OpenAIError
{
    public string message;
    public string type;
    public string param;
    public string code;
}

[Serializable]
public class OpenAIErrorWrapper
{
    public OpenAIError error;
}
