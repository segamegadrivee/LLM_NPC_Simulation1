using System;
using UnityEngine;

// Local LLM stand-in and fallback for offline/error cases.
public class MockLLMClient : MonoBehaviour, ILLMClient
{
    public bool debugLogs = true;
    public bool LastMockResponseGenerated { get; private set; }
    public string LastActualProvider { get; private set; } = "None";
    public string LastNpcName { get; private set; } = string.Empty;
    public string LastResponse { get; private set; } = string.Empty;

    public void SendPrompt(string prompt, Action<string> onResponse)
    {
        string npcName = ExtractLineValue(prompt, "Current NPC Name:");
        string npcRole = ExtractLineValue(prompt, "Current NPC Role:");
        string response = BuildMockResponse(prompt, npcName, npcRole);
        LastMockResponseGenerated = true;
        LastActualProvider = "Mock";
        LastNpcName = npcName;
        LastResponse = response;

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
        string name = Safe(npcName, "Villager");
        string role = Safe(npcRole, "villager");
        string npcStateSection = ExtractSection(prompt, "NPC STATE TOWARD PLAYER", "LOCAL ENVIRONMENT");
        string localSection = ExtractSection(prompt, "LOCAL ENVIRONMENT", "PUBLIC WORLD STATE");
        string worldSection = ExtractSection(prompt, "PUBLIC WORLD STATE", "RECENT RELEVANT EVENTS");
        string eventsSection = ExtractSection(prompt, "RECENT RELEVANT EVENTS", "RETRIEVED KNOWLEDGE");
        string visibleSection = ExtractSection(prompt, "VISIBLE PLAYER STATE", "PLAYER DISCOVERED FACTS");
        string retrievedKnowledgeSection = ExtractSection(prompt, "RETRIEVED KNOWLEDGE", "RECENT MEMORY");

        bool aggressionAgainstThisNpc = SectionContains(npcStateSection, "mood: angry") ||
            SectionContains(npcStateSection, "trust to player: low") ||
            SectionContains(npcStateSection, "player threw");

        bool bellMissing = !SectionContains(worldSection, "church bell missing: false");
        bool bellFound = SectionContains(worldSection, "church bell missing: false") ||
            SectionContains(worldSection, "bell has been found") ||
            SectionContains(eventsSection, "bell_found") ||
            SectionContains(eventsSection, "bell was found");

        bool armed = SectionContains(visibleSection, "visible held item: sword") ||
            SectionContains(visibleSection, "visible held item: hammer") ||
            SectionContains(visibleSection, "- armed");

        bool guardArmor = SectionContains(visibleSection, "equipped outfit: guard_armor") ||
            SectionContains(visibleSection, "- armored") ||
            SectionContains(visibleSection, "- authority_signal");

        bool nearForge = SectionContains(localSection, "id: forge") ||
            SectionContains(localSection, "id: blacksmith_area") ||
            SectionContains(localSection, "forge") ||
            SectionContains(localSection, "blacksmith");

        bool nearChurch = SectionContains(localSection, "id: church") || SectionContains(localSection, "church");
        bool nearTavern = SectionContains(localSection, "id: tavern") || SectionContains(localSection, "tavern");
        bool nearOldStorehouse = SectionContains(localSection, "old_storehouse") || SectionContains(localSection, "old storehouse");
        bool mentionsTools = SectionContains(retrievedKnowledgeSection, "tool") || SectionContains(retrievedKnowledgeSection, "metal") || SectionContains(retrievedKnowledgeSection, "cart");

        if (NameMatches(npcName, "eldric"))
        {
            if (aggressionAgainstThisNpc)
            {
                return "You caused trouble and now ask for cooperation. That is not how trust is built.";
            }

            if (bellFound)
            {
                return "The bell is found. Good. Now we need truth, not panic.";
            }

            if (armed)
            {
                return "Lower the weapon. A tense village does not need more fear.";
            }

            if (guardArmor)
            {
                return "If you wear guard armor, people will expect discipline from you. Do not use that authority carelessly.";
            }

            if (nearOldStorehouse && bellMissing)
            {
                return "Few people come near the old storehouse. That makes it a good place to hide something.";
            }

            return bellMissing
                ? "I am " + name + ", " + role + ". People are worried, and worry spreads faster than truth. Bring me facts before rumors."
                : "I am " + name + ", " + role + ". The village is steadier now, but I would still choose my words carefully.";
        }

        if (NameMatches(npcName, "mira"))
        {
            if (aggressionAgainstThisNpc)
            {
                return "If you came here to start trouble with me, do not expect warm answers.";
            }

            if (bellFound)
            {
                return "People will breathe easier now. Though I doubt the gossip will stop.";
            }

            if (armed)
            {
                return "Put that weapon away if you want people to speak honestly.";
            }

            if (guardArmor)
            {
                return "Guard armor in front of my tavern? That makes people choose their words carefully.";
            }

            if (nearTavern)
            {
                return "People talk more freely around the tavern, but lately every conversation returns to the bell.";
            }

            if (nearOldStorehouse && bellMissing)
            {
                return "Few people come near the old storehouse. That makes it a good place to hide something.";
            }

            return "Mira, tavern keeper. Folks talk when cups are full and courage is low. Say what you mean, and I will decide how much I believe you.";
        }

        if (NameMatches(npcName, "borin"))
        {
            if (aggressionAgainstThisNpc)
            {
                return "You throw things at me and then ask for help? Start with an apology.";
            }

            if (bellFound)
            {
                return "Found, then. That means it was moved, not destroyed. Someone planned this.";
            }

            if (armed)
            {
                return "A weapon in hand changes the tone of any conversation.";
            }

            if (guardArmor)
            {
                return "Armor is metal. Trust is not. What matters is what you do while wearing it.";
            }

            if (nearForge)
            {
                return "If it concerns metal, tools, or the bell, this is the right place to ask.";
            }

            if (nearOldStorehouse && bellMissing)
            {
                return "Few people come near the old storehouse. That makes it a good place to hide something.";
            }

            if (mentionsTools)
            {
                return "Borin, blacksmith. A bell that size does not walk away. Look for cart tracks, scraped stone, rope marks, or tool bites in the frame.";
            }

            return "Borin, blacksmith. Rumors are soft metal. Evidence holds shape. If the bell was taken, someone needed tools, help, or a cart.";
        }

        if (NameMatches(npcName, "anselm"))
        {
            if (aggressionAgainstThisNpc)
            {
                return "Anger leaves marks even when no blood is spilled. Speak carefully.";
            }

            if (bellFound)
            {
                return "If the bell can return to the tower, perhaps the village can return to itself.";
            }

            if (armed)
            {
                return "Please, not with a weapon here. This place has seen enough fear.";
            }

            if (guardArmor)
            {
                return "If you wear that armor near the church, I hope you came to protect what remains sacred.";
            }

            if (nearChurch)
            {
                return "Here near the church, every silence feels louder without the bell.";
            }

            if (nearOldStorehouse && bellMissing)
            {
                return "Few people come near the old storehouse. That makes it a good place to hide something.";
            }

            return "I am " + name + ", " + role + ". The bell called this village together before many of us had names. If it is missing, the wound is not only metal but memory.";
        }

        if (nearOldStorehouse && bellMissing)
        {
            return "Few people come near the old storehouse. That makes it a good place to hide something.";
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

    private static string ExtractSection(string text, string startHeader, string endHeader)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(startHeader))
        {
            return string.Empty;
        }

        string normalizedText = text.Replace("\r\n", "\n").Replace("\r", "\n");
        int startIndex = FindHeaderIndex(normalizedText, startHeader);

        if (startIndex < 0)
        {
            return string.Empty;
        }

        int contentStart = startIndex + startHeader.Length;

        if (contentStart < normalizedText.Length && normalizedText[contentStart] == '\n')
        {
            contentStart++;
        }

        int endIndex = string.IsNullOrEmpty(endHeader) ? -1 : FindHeaderIndex(normalizedText, endHeader);

        if (endIndex < 0 || endIndex <= contentStart)
        {
            return normalizedText.Substring(contentStart);
        }

        return normalizedText.Substring(contentStart, endIndex - contentStart);
    }

    private static int FindHeaderIndex(string text, string header)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(header))
        {
            return -1;
        }

        if (text.StartsWith(header + "\n", StringComparison.Ordinal) || text == header)
        {
            return 0;
        }

        int index = text.IndexOf("\n" + header + "\n", StringComparison.Ordinal);

        if (index >= 0)
        {
            return index + 1;
        }

        index = text.IndexOf("\n" + header, StringComparison.Ordinal);
        return index >= 0 ? index + 1 : -1;
    }

    private static bool SectionContains(string section, string value)
    {
        return !string.IsNullOrEmpty(section) &&
            !string.IsNullOrEmpty(value) &&
            section.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string Safe(string value, string fallback)
    {
        return !string.IsNullOrEmpty(value) ? value : fallback;
    }
}
