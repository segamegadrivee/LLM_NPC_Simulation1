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
            "Responsible, cautious, and diplomatic. He tries to keep order and stop fear from turning into panic, weighing the truth against what people are ready to hear.",
            "Eldric has guided the village for many years and knows its people, quarrels, and traditions. With the bell missing, his main concern is keeping the village calm and avoiding rushed blame.",
            "Measured, serious, and careful. He picks words that calm rather than alarm.",
            new List<string> { "village", "order", "leadership", "bell", "people", "tradition" },
            new List<string>
            {
                "The old bell gathered villagers and warned them of danger.",
                "A missing bell can shake trust and stir fear if people panic.",
                "He relies on Mira for the village mood and Borin for physical facts.",
                "His task is to keep order and stop rumors from becoming panic.",
                "He would rather people stay calm and wait for facts than rush to blame."
            },
            new List<string>
            {
                "Trusts Borin for practical inspections and honest judgment.",
                "Counts on Mira to know the social mood of the village.",
                "Respects Anselm as the keeper of the village's traditions."
            });

        CreateProfileIfMissing(
            "SO_NPC_Mira_Profile",
            "mira",
            "Mira",
            "Tavern keeper",
            "Observant, lively, and practical, with a sharp eye for people and a slightly sarcastic edge. She reads moods fast and rarely misses a nervous face or an out-of-place stranger.",
            "Mira has run the village tavern for years. Locals drink and complain there, travelers pass through with news, and almost every rumor reaches her counter first. Since the church bell went missing she has heard plenty of worried talk.",
            "Direct and conversational, often witty or teasing, never long-winded.",
            new List<string> { "tavern", "rumors", "travelers", "people", "gossip", "bell", "appearance" },
            new List<string>
            {
                "The tavern is where village rumors and traveler news gather first.",
                "Since the bell went missing, people have been more anxious and tight-lipped.",
                "A nervous stranger was seen near the tavern not long ago.",
                "She notices clothing, nervousness, and who might be hiding something.",
                "Quieter, more careful tavern talk usually means people are more worried."
            },
            new List<string>
            {
                "Hears news before Eldric does and passes on what matters.",
                "Likes Borin's honesty but finds him too blunt and deaf to gossip.",
                "Respects Anselm's quiet wisdom, even though he dislikes her gossip."
            });

        CreateProfileIfMissing(
            "SO_NPC_Borin_Profile",
            "borin",
            "Borin",
            "Blacksmith",
            "Blunt, practical, and reliable. He has no patience for gossip and trusts what he can see, test, and repair. He judges people by their work and actions, not their clothes.",
            "Borin runs the forge, making and mending tools, locks, hinges, and fittings, including past work on the church bell. He thinks in weight, metal, and evidence, which is why Eldric asks him to inspect anything physical.",
            "Short, blunt, and matter-of-fact. He says what he knows and stops.",
            new List<string> { "blacksmith", "forge", "metal", "tools", "repair", "evidence", "bell" },
            new List<string>
            {
                "A church bell is heavy and hard to move quietly without tools or a cart.",
                "Moving it would take more than one person, or the right equipment.",
                "Rumors mean little to him without physical evidence.",
                "Eldric often asks him to inspect tools, locks, and metal traces.",
                "He has mended the church's iron fittings before and knows how the bell was mounted."
            },
            new List<string>
            {
                "Respects Eldric but has no patience for panic.",
                "Does not trust tavern rumors, and thinks Mira talks too much.",
                "Values Anselm's steadiness, even if he says little."
            });

        CreateProfileIfMissing(
            "SO_NPC_Anselm_Profile",
            "anselm",
            "Anselm",
            "Church caretaker",
            "Quiet, reflective, and morally serious, but grounded rather than preachy. He chooses his words carefully and speaks only when he has something worth saying.",
            "Anselm has cared for the old church for many years and keeps its small traditions alive. To him the bell marked the rhythm of village life, calling people together and warning of danger, so its loss feels like more than a missing object.",
            "Calm, measured, and plain. Reflective without being dramatic.",
            new List<string> { "church", "bell", "tradition", "history", "memory", "conduct" },
            new List<string>
            {
                "The church bell set the village's daily rhythm and called people together.",
                "Its silence unsettles people more than they admit.",
                "He values quiet and respectful behavior near the church.",
                "Small shared symbols, like the bell, help hold a village together.",
                "The bell once rang for mornings, midday, gatherings, and warnings of danger."
            },
            new List<string>
            {
                "Respects Eldric's sense of responsibility for the village.",
                "Dislikes Mira's gossip, but admits it sometimes carries truth.",
                "Trusts Borin's judgment whenever something physical must be checked."
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
                title = "Guard armor as a sign of authority",
                text = "Guard armor marks the wearer as someone with authority. Villagers grow more careful and formal around them. Some feel protected, others feel pressured or watched.",
                tags = new List<string> { "armor", "guard_armor", "armored", "authority_signal", "authority" },
                relatedObjectIds = new List<string>(),
                knownByNpcIds = new List<string>(),
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "public_dark_cloak_signal",
                title = "A dark cloak as a sign of secrecy",
                text = "A dark, concealing cloak makes the wearer look like someone who does not want to be recognized. Villagers become more guarded and less willing to speak openly.",
                tags = new List<string> { "dark_cloak", "cloak", "hidden_identity", "secrecy", "suspicious" },
                relatedObjectIds = new List<string>(),
                knownByNpcIds = new List<string>(),
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "public_visible_weapon_signal",
                title = "A visible weapon as a sign of danger",
                text = "A weapon carried openly changes the mood of a conversation. People become wary or defensive when someone nearby is visibly armed.",
                tags = new List<string> { "weapon", "armed", "sword", "hammer", "danger", "threat" },
                relatedObjectIds = new List<string>(),
                knownByNpcIds = new List<string>(),
                importance = 3
            },
            new KnowledgeEntry
            {
                id = "public_missing_bell_tension",
                title = "The missing bell unsettles the village",
                text = "The old church bell is missing and the village feels uneasy. The bell once set the daily rhythm, called people together, and warned of danger, so its silence makes people anxious and quick to worry.",
                tags = new List<string> { "bell", "missing_bell", "church", "tension", "worried" },
                relatedObjectIds = new List<string> { "church" },
                knownByNpcIds = new List<string>(),
                importance = 4
            },
            new KnowledgeEntry
            {
                id = "public_bell_found_resolution",
                title = "Finding the bell calms the village",
                text = "When the missing bell is found, relief spreads and the village grows calmer, though people still wonder why it was gone and who moved it. Talk of it being missing becomes old news.",
                tags = new List<string> { "bell", "bell_found", "found", "resolved", "calm" },
                relatedObjectIds = new List<string> { "church" },
                knownByNpcIds = new List<string>(),
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "public_tavern_social_life",
                title = "The tavern is where village talk gathers",
                text = "The tavern is the heart of village talk. Rumors, traveler news, and the village mood surface there first, especially now that people are uneasy about the bell.",
                tags = new List<string> { "tavern", "rumors", "gossip", "news", "worried", "bell" },
                relatedObjectIds = new List<string> { "tavern" },
                knownByNpcIds = new List<string>(),
                importance = 4
            },
            new KnowledgeEntry
            {
                id = "mira_reads_people_in_tavern",
                title = "Mira reads the people she serves",
                text = "Mira watches how people dress, speak, and carry themselves. She quickly notices uniforms, cloaks, nervousness, and who is hiding something, and she hears most rumors before anyone else.",
                tags = new List<string> { "mira", "tavern", "people", "social", "rumors", "armor", "cloak" },
                relatedObjectIds = new List<string> { "tavern" },
                knownByNpcIds = new List<string> { "mira" },
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "borin_judges_workmanship",
                title = "Borin trusts work, not appearances",
                text = "Borin is not impressed by armor or cloaks. He judges people by what they do and how they handle tools and metal. He also knows the bell is heavy and would take real effort and equipment to move.",
                tags = new List<string> { "borin", "forge", "blacksmith", "tools", "metal", "bell", "armor", "cloak" },
                relatedObjectIds = new List<string> { "blacksmith_area" },
                knownByNpcIds = new List<string> { "borin" },
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "eldric_connects_armor_with_responsibility",
                title = "Eldric expects responsibility from armor",
                text = "Eldric reads guard armor as a claim of authority and duty. If the player wears it, he expects discipline and restraint, and quietly hopes such a person will help keep the village calm.",
                tags = new List<string> { "eldric", "guard_armor", "armor", "authority", "responsibility", "order" },
                relatedObjectIds = new List<string>(),
                knownByNpcIds = new List<string> { "eldric" },
                importance = 4
            },
            new KnowledgeEntry
            {
                id = "eldric_keeps_order_avoids_panic",
                title = "Eldric tries to keep the village calm",
                text = "Eldric's main worry is panic. He speaks carefully about the missing bell, balancing honesty with the need to keep people calm, and he discourages blame before there is proof.",
                tags = new List<string> { "eldric", "order", "leadership", "village", "bell", "worried", "panic" },
                relatedObjectIds = new List<string>(),
                knownByNpcIds = new List<string> { "eldric" },
                importance = 4
            },
            new KnowledgeEntry
            {
                id = "anselm_bell_meaning_and_conduct",
                title = "To Anselm the bell means shared rhythm",
                text = "For Anselm the church bell was never just metal. It marked the village's daily rhythm, called people together, and stood for shared trust, so its loss weighs on him. He also values quiet, respectful behavior near the church.",
                tags = new List<string> { "anselm", "church", "bell", "tradition", "memory", "conduct", "respect" },
                relatedObjectIds = new List<string> { "church" },
                knownByNpcIds = new List<string> { "anselm" },
                importance = 5
            },
            new KnowledgeEntry
            {
                id = "public_daily_village_rhythm",
                title = "The bell once set the village's daily rhythm",
                text = "For as long as most people remember, the bell rang for mornings, midday, and evening, and called everyone together for news or danger. Without it the days feel oddly shapeless and people lose their shared sense of time.",
                tags = new List<string> { "bell", "rhythm", "routine", "daily", "church", "gathering" },
                relatedObjectIds = new List<string> { "church" },
                knownByNpcIds = new List<string>(),
                importance = 4
            },
            new KnowledgeEntry
            {
                id = "public_church_symbolic_center",
                title = "The church is the village's symbolic heart",
                text = "The old stone church is where the village gathers and marks important moments. Even people who rarely pray treat it as the steady center of village life.",
                tags = new List<string> { "church", "tradition", "sacred", "gathering", "history" },
                relatedObjectIds = new List<string> { "church" },
                knownByNpcIds = new List<string>(),
                importance = 3
            },
            new KnowledgeEntry
            {
                id = "public_forge_practical_center",
                title = "The forge is where practical questions get answered",
                text = "Borin's forge makes and mends tools, locks, hinges, and metal fittings. When something needs weighing, checking, or proving, people look here rather than to rumor.",
                tags = new List<string> { "forge", "blacksmith", "metal", "tools", "repair", "evidence" },
                relatedObjectIds = new List<string> { "blacksmith_area" },
                knownByNpcIds = new List<string>(),
                importance = 3
            },
            new KnowledgeEntry
            {
                id = "mira_on_anselm",
                title = "Mira's view of Anselm",
                text = "Mira knows Anselm keeps clear of tavern gossip, but she respects that he remembers the things that actually matter and rarely speaks without reason.",
                tags = new List<string> { "mira", "anselm", "gossip", "memory", "tradition" },
                relatedObjectIds = new List<string>(),
                knownByNpcIds = new List<string> { "mira" },
                importance = 3
            },
            new KnowledgeEntry
            {
                id = "borin_on_rumors_vs_evidence",
                title = "Borin trusts evidence over rumor",
                text = "Borin puts more faith in what he can weigh, measure, and inspect than in Mira's tavern talk. He will believe the bell was moved when something physical shows it, not before.",
                tags = new List<string> { "borin", "evidence", "proof", "rumors", "mira", "bell" },
                relatedObjectIds = new List<string> { "blacksmith_area" },
                knownByNpcIds = new List<string> { "borin" },
                importance = 3
            },
            new KnowledgeEntry
            {
                id = "eldric_relies_on_mira_and_borin",
                title = "Eldric leans on Mira and Borin",
                text = "Eldric reads the village through others. He trusts Mira to tell him the public mood and Borin to give him plain physical facts, then weighs both before he says anything.",
                tags = new List<string> { "eldric", "mira", "borin", "mood", "facts", "order" },
                relatedObjectIds = new List<string>(),
                knownByNpcIds = new List<string> { "eldric" },
                importance = 3
            },
            new KnowledgeEntry
            {
                id = "anselm_on_eldric",
                title = "Anselm's view of Eldric",
                text = "Anselm respects Eldric's sense of duty and his effort to keep the village calm, but quietly worries that Eldric carries his own fear alone and hides his concern too well.",
                tags = new List<string> { "anselm", "eldric", "duty", "worry", "order" },
                relatedObjectIds = new List<string>(),
                knownByNpcIds = new List<string> { "anselm" },
                importance = 3
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
