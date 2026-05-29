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
        knowledgeBase.name = "SO_FinalMvp_KnowledgeBase";
        knowledgeBase.entries = new List<KnowledgeEntry>
        {
            new KnowledgeEntry
            {
                id = "public_guard_armor_signal",
                title = "Guard armor as a social signal",
                text = "Guard armor makes villagers more careful around the player. Some see it as protection and responsibility; others see it as pressure, authority, or possible intimidation.",
                tags = new List<string> { "armor", "guard_armor", "armored", "authority_signal", "authority", "trust", "appearance" },
                relatedObjectIds = new List<string>(),
                knownByNpcIds = new List<string>(),
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "public_dark_cloak_signal",
                title = "Dark cloak as a suspicious signal",
                text = "A dark cloak makes the player look like someone trying not to be recognized. Villagers may become cautious, suspicious, or less willing to speak openly.",
                tags = new List<string> { "dark_cloak", "cloak", "suspicious", "hidden_identity", "secrecy", "appearance", "trust" },
                relatedObjectIds = new List<string>(),
                knownByNpcIds = new List<string>(),
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "public_visible_weapon_signal",
                title = "Visible weapon as a threat signal",
                text = "A visible weapon or dangerous object changes the tone of a conversation. Villagers may become cautious, defensive, or afraid when the player is visibly armed.",
                tags = new List<string> { "weapon", "armed", "sword", "hammer", "danger", "threat", "appearance" },
                relatedObjectIds = new List<string>(),
                knownByNpcIds = new List<string>(),
                importance = 4
            },
            new KnowledgeEntry
            {
                id = "public_aggressive_behavior_consequence",
                title = "Aggressive behavior reduces trust",
                text = "If the player acts aggressively toward someone, that target should become less trusting and more hostile. Other NPCs should not know about it unless the event is public or explicitly witnessed.",
                tags = new List<string> { "aggression", "trust", "reputation", "angry", "hostile", "personal_event" },
                relatedObjectIds = new List<string>(),
                knownByNpcIds = new List<string>(),
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "public_missing_bell_tension",
                title = "The missing bell creates village tension",
                text = "The old church bell is missing, and the village feels uneasy. The bell represented order, routine, and shared trust, so its absence makes people nervous.",
                tags = new List<string> { "bell", "missing_bell", "church", "village", "tension", "worried" },
                relatedObjectIds = new List<string> { "church" },
                knownByNpcIds = new List<string>(),
                importance = 4
            },
            new KnowledgeEntry
            {
                id = "public_bell_found_resolution",
                title = "Finding the bell changes the village mood",
                text = "If the missing bell is found, the village becomes calmer, but people still want to know why it was hidden and who moved it.",
                tags = new List<string> { "bell_found", "found", "resolved", "calm", "village", "church" },
                relatedObjectIds = new List<string> { "church", "old_storehouse" },
                knownByNpcIds = new List<string>(),
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "mira_reads_people_in_tavern",
                title = "Mira reads people in the tavern",
                text = "Mira pays close attention to how people dress, speak, and behave in the tavern. She notices uniforms, cloaks, nervousness, lies, and social tension quickly.",
                tags = new List<string> { "mira", "tavern", "people", "social", "appearance", "armor", "cloak", "suspicious", "trust" },
                relatedObjectIds = new List<string> { "tavern" },
                knownByNpcIds = new List<string> { "mira" },
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "borin_judges_actions_not_clothes",
                title = "Borin judges actions more than clothing",
                text = "Borin does not trust uniforms, cloaks, or appearances by themselves. He cares more about what the player does, especially around tools, weapons, or violence.",
                tags = new List<string> { "borin", "forge", "tools", "armor", "cloak", "weapon", "aggression", "trust", "actions" },
                relatedObjectIds = new List<string> { "forge" },
                knownByNpcIds = new List<string> { "borin" },
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "eldric_connects_armor_with_responsibility",
                title = "Eldric connects armor with responsibility",
                text = "Eldric sees guard armor as a sign of responsibility. If the player wears it, he expects discipline, restraint, and protection of the village.",
                tags = new List<string> { "eldric", "authority", "order", "guard_armor", "armor", "responsibility", "village" },
                relatedObjectIds = new List<string> { "village_square", "church" },
                knownByNpcIds = new List<string> { "eldric" },
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "anselm_cares_about_conduct_near_church",
                title = "Anselm cares about conduct near the church",
                text = "Anselm is sensitive to behavior near the church. Weapons, aggression, and suspicious conduct feel especially wrong near sacred or quiet places.",
                tags = new List<string> { "anselm", "church", "sacred", "conduct", "aggression", "weapon", "cloak", "bell" },
                relatedObjectIds = new List<string> { "church" },
                knownByNpcIds = new List<string> { "anselm" },
                importance = 5
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
