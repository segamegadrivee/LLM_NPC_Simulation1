using System.Collections.Generic;

// Result of evaluating a single KnowledgeEntry against the current context: access decision,
// gate block reasons, per-source activation flags/matches, the final score, and the final
// decision reason. Produced by KnowledgeScorer.Evaluate and consumed by the retriever (selection)
// and RetrievalDebugBuilder (explanations). Was a private nested type on ContextRetriever; the
// fields and threshold logic are unchanged.
public class KnowledgeRetrievalEvaluation
{
    public bool allowedForNpc;
    public bool hasMessageActivation;
    public bool hasVisibleStateActivation;
    public bool hasNpcStateActivation;
    public bool hasWorldEventActivation;
    public bool hasWorldStateActivation;
    public bool hasLocalActivation;
    public int score;
    public int importanceScore;
    public string finalDecisionReason = string.Empty;
    public string worldStateBlockReason = string.Empty;
    public string appearanceBlockReason = string.Empty;
    public List<string> messageMatches = new List<string>();
    public List<string> visibleStateMatches = new List<string>();
    public List<string> npcStateMatches = new List<string>();
    public List<string> worldEventMatches = new List<string>();
    public List<string> worldStateMatches = new List<string>();
    public List<string> rawLocalMatches = new List<string>();
    public List<string> npcProfileTagMatches = new List<string>();

    public bool hasStrongActivation
    {
        get
        {
            return hasMessageActivation ||
                hasVisibleStateActivation ||
                hasNpcStateActivation ||
                hasWorldEventActivation ||
                hasWorldStateActivation;
        }
    }

    public bool IsEligibleForRetrieval
    {
        get
        {
            return allowedForNpc && hasStrongActivation && score >= KnowledgeScorer.RetrievalThreshold;
        }
    }
}
