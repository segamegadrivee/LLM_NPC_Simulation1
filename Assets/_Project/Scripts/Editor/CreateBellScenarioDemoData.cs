using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CreateBellScenarioDemoData
{
    private const string ProfileFolder = "Assets/_Project/Data/NPCProfiles";
    private const string KnowledgeFolder = "Assets/_Project/Data/Knowledge";

    [MenuItem("Tools/AI NPC/Create Bell Scenario Demo Data")]
    public static void CreateDemoData()
    {
        EnsureFolder("Assets", "_Project");
        EnsureFolder("Assets/_Project", "Data");
        EnsureFolder("Assets/_Project/Data", "NPCProfiles");
        EnsureFolder("Assets/_Project/Data", "Knowledge");

        CreateProfileIfMissing(
            "SO_NPC_Eldric_Profile",
            "eldric",
            "Eldric",
            "Village elder",
            "Calm, cautious, responsible, protective of the village.",
            "Eldric has led the village for many years. He knows its people, traditions, conflicts, and important decisions.",
            "Short, serious, grounded, slightly cautious.",
            new List<string> { "village", "order", "leadership", "bell", "people", "tradition" },
            new List<string>
            {
                "The old church bell was used to gather villagers and warn them of danger.",
                "The missing bell can damage trust and create fear.",
                "Mira often hears rumors before anyone else.",
                "Borin can inspect metal traces and tools.",
                "Anselm understands the symbolic meaning of the bell."
            },
            new List<string>
            {
                "Trusts Borin with practical inspections.",
                "Believes Mira knows the social mood of the village.",
                "Respects Anselm as a keeper of traditions."
            });

        CreateProfileIfMissing(
            "SO_NPC_Mira_Profile",
            "mira",
            "Mira",
            "Tavern keeper",
            "Observant, lively, practical, socially sharp, slightly sarcastic.",
            "Mira runs the tavern and hears most rumors from locals and travelers.",
            "Direct, conversational, slightly witty.",
            new List<string> { "tavern", "rumors", "travelers", "people", "stranger", "bell" },
            new List<string>
            {
                "A nervous stranger was seen near the tavern recently.",
                "People are worried but avoid speaking openly.",
                "The tavern is where rumors spread quickly.",
                "Eldric tries to keep panic away from the village."
            },
            new List<string>
            {
                "Often hears news before Eldric does.",
                "Thinks Borin is honest but too blunt.",
                "Knows Anselm avoids gossip but remembers old stories."
            });

        CreateProfileIfMissing(
            "SO_NPC_Borin_Profile",
            "borin",
            "Borin",
            "Blacksmith",
            "Direct, practical, hardworking, skeptical of rumors.",
            "Borin works with metal, tools, locks, bells, and repairs. He understands physical evidence better than gossip.",
            "Blunt, short, practical.",
            new List<string> { "blacksmith", "metal", "tools", "bell", "repair", "evidence" },
            new List<string>
            {
                "A church bell is heavy and difficult to move quietly.",
                "Moving the bell would require tools, a cart, or more than one person.",
                "Rumors are useless without evidence.",
                "Eldric often asks Borin to verify physical objects."
            },
            new List<string>
            {
                "Respects Eldric but dislikes panic.",
                "Does not fully trust tavern rumors.",
                "Thinks Mira talks too much but sometimes hears useful things."
            });

        CreateProfileIfMissing(
            "SO_NPC_Anselm_Profile",
            "anselm",
            "Anselm",
            "Church caretaker",
            "Quiet, thoughtful, patient, moral, careful with words.",
            "Anselm takes care of the old church and remembers many stories, traditions, and symbolic meanings connected to the village.",
            "Calm, reflective, symbolic, restrained.",
            new List<string> { "church", "history", "tradition", "bell", "memory", "village" },
            new List<string>
            {
                "The church bell is not only a tool but also a symbol of memory and unity.",
                "The bell called people together before many current villagers were born.",
                "People often forget that small symbols can hold a village together.",
                "Eldric carries the burden of responsibility for the village."
            },
            new List<string>
            {
                "Respects Eldric's responsibility.",
                "Does not like Mira's gossip, but knows it sometimes reveals truth.",
                "Trusts Borin when physical evidence is needed."
            });

        CreateKnowledgeBaseIfMissing();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Bell scenario demo data is ready under Assets/_Project/Data.");
    }

    private static void CreateProfileIfMissing(
        string assetName,
        string npcId,
        string npcName,
        string role,
        string personality,
        string backstory,
        string speakingStyle,
        List<string> knowledgeTags,
        List<string> knownFacts,
        List<string> relationships)
    {
        string path = ProfileFolder + "/" + assetName + ".asset";

        if (AssetDatabase.LoadAssetAtPath<NPCProfile>(path) != null)
        {
            return;
        }

        NPCProfile profile = ScriptableObject.CreateInstance<NPCProfile>();
        profile.npcId = npcId;
        profile.npcName = npcName;
        profile.role = role;
        profile.personality = personality;
        profile.backstory = backstory;
        profile.speakingStyle = speakingStyle;
        profile.knowledgeTags = knowledgeTags;
        profile.knownFacts = knownFacts;
        profile.relationships = relationships;

        AssetDatabase.CreateAsset(profile, path);
    }

    private static void CreateKnowledgeBaseIfMissing()
    {
        string path = KnowledgeFolder + "/SO_BellScenario_KnowledgeBase.asset";

        if (AssetDatabase.LoadAssetAtPath<KnowledgeBase>(path) != null)
        {
            return;
        }

        KnowledgeBase knowledgeBase = ScriptableObject.CreateInstance<KnowledgeBase>();
        knowledgeBase.entries = new List<KnowledgeEntry>
        {
            new KnowledgeEntry
            {
                id = "church_bell_history",
                title = "History of the church bell",
                text = "The old church bell was used for generations to gather villagers and warn them during danger. Many villagers see it as a symbol of protection and unity.",
                tags = new List<string> { "church", "bell", "history", "tradition" },
                relatedObjectIds = new List<string> { "church" },
                knownByNpcIds = new List<string> { "eldric", "anselm" },
                importance = 3
            },
            new KnowledgeEntry
            {
                id = "mira_saw_stranger",
                title = "Mira saw a stranger",
                text = "Mira noticed a nervous stranger in the tavern last night. He avoided long conversations and asked when the church square becomes empty.",
                tags = new List<string> { "tavern", "stranger", "rumors", "bell" },
                relatedObjectIds = new List<string> { "tavern", "church" },
                knownByNpcIds = new List<string> { "mira" },
                importance = 3
            },
            new KnowledgeEntry
            {
                id = "borin_bell_metal",
                title = "Borin understands the bell as metalwork",
                text = "Borin knows that a church bell is heavy and difficult to move quietly. Moving it would require tools, a cart, or several people.",
                tags = new List<string> { "blacksmith", "metal", "tools", "bell", "evidence" },
                relatedObjectIds = new List<string> { "blacksmith_area", "church" },
                knownByNpcIds = new List<string> { "borin" },
                importance = 3
            },
            new KnowledgeEntry
            {
                id = "eldric_public_order",
                title = "Eldric worries about public order",
                text = "Eldric worries that the missing bell may cause fear and distrust in the village because the bell was used as a public warning signal.",
                tags = new List<string> { "village", "order", "bell", "leadership" },
                relatedObjectIds = new List<string> { "village_square", "church" },
                knownByNpcIds = new List<string> { "eldric" },
                importance = 3
            },
            new KnowledgeEntry
            {
                id = "anselm_symbolic_meaning",
                title = "Anselm sees the bell as a symbol",
                text = "Anselm believes the bell is not just a tool but a symbol of memory, protection, and continuity for the village.",
                tags = new List<string> { "church", "bell", "tradition", "memory" },
                relatedObjectIds = new List<string> { "church" },
                knownByNpcIds = new List<string> { "anselm" },
                importance = 3
            },
            new KnowledgeEntry
            {
                id = "road_cart_possibility",
                title = "A cart may have been used",
                text = "If the bell was taken from the church, it was likely moved by cart or by several people through the village road.",
                tags = new List<string> { "road", "cart", "bell", "evidence" },
                relatedObjectIds = new List<string> { "village_road", "church" },
                knownByNpcIds = new List<string> { "borin", "eldric" },
                importance = 2
            }
        };

        AssetDatabase.CreateAsset(knowledgeBase, path);
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
