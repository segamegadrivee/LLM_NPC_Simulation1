using System.Collections.Generic;

// Gates appearance-sensitive knowledge (armor / cloak / weapon "signal" entries) so it is only
// retrieved when the player is currently SHOWING that appearance or explicitly ASKED about it.
// An entry whose appearance tags fall within EXACTLY ONE category is treated as a single
// "appearance signal" entry; entries spanning several categories are broad disposition knowledge
// and are NOT gated. Behavior preserved verbatim from the original ContextRetriever.
public static class AppearanceKnowledgeGate
{
    private static readonly string[] AppearanceArmorTags = { "guard_armor", "armor", "armored", "authority_signal" };
    private static readonly string[] AppearanceCloakTags = { "dark_cloak", "traveler_cloak", "cloak", "modest_clothing", "hidden_identity" };
    private static readonly string[] AppearanceWeaponTags = { "weapon", "armed", "sword", "hammer", "danger", "threat" };

    // Returns a non-empty reason when an appearance-signal entry is not supported by the player's
    // current visible state and was not explicitly asked about. This stops e.g. cloak/weapon
    // knowledge from being retrieved when the player only wears armor.
    public static string GetAppearanceBlockReason(KnowledgeEntry entry, PlayerState playerState, string playerMessage)
    {
        if (entry == null || entry.tags == null)
        {
            return string.Empty;
        }

        bool armor = KnowledgeTextUtil.HasAnyTag(entry.tags, AppearanceArmorTags);
        bool cloak = KnowledgeTextUtil.HasAnyTag(entry.tags, AppearanceCloakTags);
        bool weapon = KnowledgeTextUtil.HasAnyTag(entry.tags, AppearanceWeaponTags);

        int categories = (armor ? 1 : 0) + (cloak ? 1 : 0) + (weapon ? 1 : 0);

        // Not appearance-sensitive, or spans multiple categories (broad knowledge): do not gate.
        if (categories != 1)
        {
            return string.Empty;
        }

        string[] category;
        string label;

        if (armor) { category = AppearanceArmorTags; label = "armor"; }
        else if (cloak) { category = AppearanceCloakTags; label = "cloak"; }
        else { category = AppearanceWeaponTags; label = "weapon/threat"; }

        List<string> visibleTags = GetCurrentVisibleTags(playerState);

        if (KnowledgeTextUtil.HasAnyTag(visibleTags, category))
        {
            return string.Empty; // supported by what the player is currently showing
        }

        if (MessageMentionsAnyTag(playerMessage, category))
        {
            return string.Empty; // the player explicitly asked about this topic
        }

        return "appearance gate: entry is about " + label + ", but the player is not currently showing " +
            label + " and did not ask about it.";
    }

    // Current observable appearance vocabulary: equipped outfit + visible status tags + visible held item.
    private static List<string> GetCurrentVisibleTags(PlayerState playerState)
    {
        List<string> result = new List<string>();

        if (playerState == null)
        {
            return result;
        }

        AddVisibleToken(result, playerState.equippedOutfit);
        AddVisibleToken(result, playerState.visibleHeldItem);

        if (playerState.visibleStatusTags != null)
        {
            for (int i = 0; i < playerState.visibleStatusTags.Count; i++)
            {
                AddVisibleToken(result, playerState.visibleStatusTags[i]);
            }
        }

        return result;
    }

    private static void AddVisibleToken(List<string> result, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        string token = value.Trim().ToLowerInvariant();

        if (token.Length == 0 || token == "none" || token == "normal" || token == "unknown")
        {
            return;
        }

        if (!KnowledgeTextUtil.ContainsIgnoreCase(result, token))
        {
            result.Add(token);
        }
    }

    // Lenient substring match so plurals/phrases ("dark cloaks") still count as explicitly asking.
    private static bool MessageMentionsAnyTag(string playerMessage, string[] set)
    {
        if (string.IsNullOrEmpty(playerMessage) || set == null)
        {
            return false;
        }

        string normalized = " " + playerMessage.ToLowerInvariant().Replace("_", " ") + " ";

        for (int i = 0; i < set.Length; i++)
        {
            string term = set[i].Replace("_", " ");

            if (term.Length > 2 && normalized.Contains(term))
            {
                return true;
            }
        }

        return false;
    }
}
