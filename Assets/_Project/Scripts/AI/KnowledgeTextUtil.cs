using System.Collections.Generic;
using System.Text;

// Shared, behavior-preserving text helpers used by the knowledge retrieval pipeline
// (access filter, appearance/world-state gates, scorer, debug builder, snapshot builder).
// These are pure functions extracted verbatim from the original ContextRetriever so that
// matching/normalization rules stay identical across every caller.
public static class KnowledgeTextUtil
{
    public static List<string> SplitSearchTerms(string value)
    {
        List<string> result = new List<string>();

        if (string.IsNullOrEmpty(value))
        {
            return result;
        }

        string normalized = value.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");

        if (normalized.Length > 0)
        {
            result.Add(normalized);
        }

        string[] words = normalized.Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].Trim();

            if (word.Length > 2 && !ContainsIgnoreCase(result, word))
            {
                result.Add(word);
            }
        }

        return result;
    }

    public static bool ContainsText(string text, string value)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value))
        {
            return false;
        }

        return text.ToLowerInvariant().Contains(value.Trim().ToLowerInvariant());
    }

    public static bool TextContainsSearchTerm(string text, string searchTerm)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchTerm))
        {
            return false;
        }

        string normalizedText = " " + NormalizeSearchText(text) + " ";
        string normalizedTerm = NormalizeSearchText(searchTerm);

        if (normalizedTerm.Length == 0 || normalizedTerm == "none" || normalizedTerm == "unknown")
        {
            return false;
        }

        if (normalizedText.Contains(" " + normalizedTerm + " "))
        {
            return true;
        }

        string[] words = normalizedTerm.Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].Trim();

            if (word.Length > 3 && normalizedText.Contains(" " + word + " "))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TextContainsExactSearchTerm(string text, string searchTerm)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchTerm))
        {
            return false;
        }

        string normalizedText = " " + NormalizeSearchText(text) + " ";
        string normalizedTerm = NormalizeSearchText(searchTerm);

        if (normalizedTerm.Length == 0 || normalizedTerm == "none" || normalizedTerm == "unknown")
        {
            return false;
        }

        return normalizedText.Contains(" " + normalizedTerm + " ");
    }

    public static string NormalizeSearchText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string lower = value.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");
        StringBuilder builder = new StringBuilder();
        bool lastWasSpace = false;

        for (int i = 0; i < lower.Length; i++)
        {
            char c = lower[i];

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    public static bool AnyTermAppearsInText(string text, List<string> terms)
    {
        if (string.IsNullOrEmpty(text) || terms == null)
        {
            return false;
        }

        for (int i = 0; i < terms.Count; i++)
        {
            if (TextContainsSearchTerm(text, terms[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasAnyTag(List<string> tags, string[] set)
    {
        if (tags == null || set == null)
        {
            return false;
        }

        for (int i = 0; i < tags.Count; i++)
        {
            for (int j = 0; j < set.Length; j++)
            {
                if (string.Equals(tags[i] != null ? tags[i].Trim() : null, set[j], System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static List<string> GetOverlap(List<string> first, List<string> second)
    {
        List<string> result = new List<string>();

        if (first == null || second == null)
        {
            return result;
        }

        for (int i = 0; i < first.Count; i++)
        {
            string value = first[i];

            if (!string.IsNullOrEmpty(value) && ContainsIgnoreCase(second, value) && !ContainsIgnoreCase(result, value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    public static bool ContainsIgnoreCase(List<string> values, string target)
    {
        if (values == null || string.IsNullOrEmpty(target))
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], target, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string FormatDebugList(List<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return "None";
        }

        return string.Join(", ", values.ToArray());
    }

    public static string SafeDebugText(string value)
    {
        return string.IsNullOrEmpty(value) ? "None" : value;
    }

    public static string SafeOrEmpty(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
    }
}
