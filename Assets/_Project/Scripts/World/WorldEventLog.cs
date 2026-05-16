using System.Collections.Generic;
using UnityEngine;

public class WorldEventLog : MonoBehaviour
{
    public static WorldEventLog Instance { get; private set; }

    public List<WorldEvent> events = new List<WorldEvent>();
    public bool debugLogs;

    private int nextSequenceNumber = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple WorldEventLog instances found. Using the first one.", this);
            return;
        }

        Instance = this;
        nextSequenceNumber = FindNextSequenceNumber();
    }

    public void AddEvent(WorldEvent worldEvent)
    {
        if (worldEvent == null)
        {
            return;
        }

        if (events == null)
        {
            events = new List<WorldEvent>();
        }

        worldEvent.sequenceNumber = nextSequenceNumber++;
        worldEvent.timestamp = Time.time;

        if (string.IsNullOrEmpty(worldEvent.eventId))
        {
            worldEvent.eventId = BuildEventId(worldEvent);
        }

        events.Add(worldEvent);

        if (debugLogs)
        {
            Debug.Log("WorldEventLog added event: " + worldEvent.GetShortText(), this);
        }
    }

    public List<WorldEvent> GetRecentEvents(int maxCount)
    {
        return TakeMostRecent(events, maxCount);
    }

    public List<WorldEvent> GetRelevantEventsForNpc(string npcId, int maxCount)
    {
        List<WorldEvent> result = new List<WorldEvent>();

        if (events == null)
        {
            return result;
        }

        for (int i = events.Count - 1; i >= 0; i--)
        {
            WorldEvent worldEvent = events[i];

            if (worldEvent != null && worldEvent.IsRelevantForNpc(npcId))
            {
                result.Add(worldEvent);
                if (ReachedMax(result, maxCount))
                {
                    break;
                }
            }
        }

        result.Reverse();
        return result;
    }

    public List<WorldEvent> GetRelevantEventsForLocation(string locationObjectId, int maxCount)
    {
        List<string> locationIds = new List<string>();

        if (!string.IsNullOrEmpty(locationObjectId))
        {
            locationIds.Add(locationObjectId);
        }

        return GetRelevantEventsForLocations(locationIds, maxCount);
    }

    public List<WorldEvent> GetRelevantEventsForLocations(List<string> locationObjectIds, int maxCount)
    {
        List<WorldEvent> result = new List<WorldEvent>();

        if (events == null)
        {
            return result;
        }

        for (int i = events.Count - 1; i >= 0; i--)
        {
            WorldEvent worldEvent = events[i];

            if (worldEvent != null && !worldEvent.IsTargeted() && worldEvent.IsRelevantForLocation(locationObjectIds))
            {
                result.Add(worldEvent);
                if (ReachedMax(result, maxCount))
                {
                    break;
                }
            }
        }

        result.Reverse();
        return result;
    }

    public List<WorldEvent> GetGlobalEvents(int maxCount)
    {
        List<WorldEvent> result = new List<WorldEvent>();

        if (events == null)
        {
            return result;
        }

        for (int i = events.Count - 1; i >= 0; i--)
        {
            WorldEvent worldEvent = events[i];

            if (worldEvent != null && worldEvent.isGlobal)
            {
                result.Add(worldEvent);
                if (ReachedMax(result, maxCount))
                {
                    break;
                }
            }
        }

        result.Reverse();
        return result;
    }

    public List<WorldEvent> GetRelevantEventsForContext(string npcId, List<string> locationObjectIds, int maxCount)
    {
        List<WorldEvent> result = new List<WorldEvent>();

        if (events == null)
        {
            return result;
        }

        for (int i = events.Count - 1; i >= 0; i--)
        {
            WorldEvent worldEvent = events[i];

            if (worldEvent == null)
            {
                continue;
            }

            bool relevantToNpc = worldEvent.IsRelevantForNpc(npcId);
            bool relevantToLocation = !worldEvent.IsTargeted() && worldEvent.IsRelevantForLocation(locationObjectIds);

            if (relevantToNpc || relevantToLocation)
            {
                result.Add(worldEvent);
                if (ReachedMax(result, maxCount))
                {
                    break;
                }
            }
        }

        result.Reverse();
        return result;
    }

    private int FindNextSequenceNumber()
    {
        int highest = 0;

        if (events != null)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] != null && events[i].sequenceNumber > highest)
                {
                    highest = events[i].sequenceNumber;
                }
            }
        }

        return highest + 1;
    }

    private static List<WorldEvent> TakeMostRecent(List<WorldEvent> source, int maxCount)
    {
        List<WorldEvent> result = new List<WorldEvent>();

        if (source == null)
        {
            return result;
        }

        for (int i = source.Count - 1; i >= 0; i--)
        {
            if (source[i] != null)
            {
                result.Add(source[i]);
                if (ReachedMax(result, maxCount))
                {
                    break;
                }
            }
        }

        result.Reverse();
        return result;
    }

    private static bool ReachedMax(List<WorldEvent> values, int maxCount)
    {
        return maxCount > 0 && values != null && values.Count >= maxCount;
    }

    private static string BuildEventId(WorldEvent worldEvent)
    {
        string type = string.IsNullOrEmpty(worldEvent.eventType) ? "event" : worldEvent.eventType.Trim().ToLowerInvariant().Replace(" ", "_");
        return type + "_" + worldEvent.sequenceNumber;
    }
}
