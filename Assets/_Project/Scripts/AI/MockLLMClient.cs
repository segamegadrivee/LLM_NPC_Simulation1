using System;
using UnityEngine;

// Local LLM stand-in and fallback for offline/error cases.
public class MockLLMClient : MonoBehaviour, ILLMClient
{
    public bool debugLogs = true;

    public void SendPrompt(string prompt, Action<string> onResponse)
    {
        string npcName = ExtractLineValue(prompt, "Current NPC Name:");
        string npcRole = ExtractLineValue(prompt, "Current NPC Role:");
        string response = BuildMockResponse(prompt, npcName, npcRole);

        if (debugLogs)
        {
            Debug.Log("MockLLMClient generated a mock response for " + Safe(npcName, "unknown NPC") + ".", this);
        }

        if (onResponse != null)
        {
            onResponse(response);
        }
    }

    private static string BuildMockResponse(string prompt, string npcName, string npcRole)
    {
        string lowerPrompt = string.IsNullOrEmpty(prompt) ? string.Empty : prompt.ToLowerInvariant();
        bool bellMissing = !lowerPrompt.Contains("church bell missing: false");
        bool calmMood = lowerPrompt.Contains("village mood: calm");
        bool mentionsStranger = lowerPrompt.Contains("stranger");
        bool mentionsTools = lowerPrompt.Contains("tool") || lowerPrompt.Contains("metal") || lowerPrompt.Contains("cart");

        string name = Safe(npcName, "Villager");
        string role = Safe(npcRole, "villager");
        string moodLine = calmMood ? "The village is steadier now, but I would still choose my words carefully." : "People are worried, and worry spreads faster than truth.";

        if (NameMatches(npcName, "eldric"))
        {
            if (!bellMissing)
            {
                return "I am " + name + ", " + role + ". If the bell has been found, then order must be restored with the same care we used in the search.";
            }

            return "I am " + name + ", " + role + ". " + moodLine + " The bell is more than church property; without it, the village loses its warning voice. Ask carefully, and bring me facts before rumors.";
        }

        if (NameMatches(npcName, "mira"))
        {
            if (mentionsStranger)
            {
                return "Mira, tavern keeper. Yes, I noticed the nervous stranger. He drank little, watched the door, and asked too much about when the square goes quiet.";
            }

            return "Mira, tavern keeper. Folks talk when cups are full and courage is low. I have heard plenty about the missing bell, but I would start with the stranger and who saw him near the tavern.";
        }

        if (NameMatches(npcName, "borin"))
        {
            if (mentionsTools)
            {
                return "Borin, blacksmith. A bell that size does not walk away. Look for cart tracks, scraped stone, rope marks, or tool bites in the frame.";
            }

            return "Borin, blacksmith. Rumors are soft metal. Evidence holds shape. If the bell was taken, someone needed tools, help, or a cart.";
        }

        if (NameMatches(npcName, "anselm"))
        {
            return "I am " + name + ", " + role + ". The bell called this village together before many of us had names. If it is missing, the wound is not only metal but memory.";
        }

        return "I am " + name + ", " + role + ". I can speak from what I know here, but you may need to ask others for a fuller truth.";
    }

    private static bool NameMatches(string npcName, string expected)
    {
        return !string.IsNullOrEmpty(npcName) && npcName.ToLowerInvariant().Contains(expected);
    }

    private static string ExtractLineValue(string text, string prefix)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(prefix))
        {
            return string.Empty;
        }

        string[] lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring(prefix.Length).Trim();
            }
        }

        return string.Empty;
    }

    private static string Safe(string value, string fallback)
    {
        return !string.IsNullOrEmpty(value) ? value : fallback;
    }
}
