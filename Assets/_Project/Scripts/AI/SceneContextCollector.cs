using System.Collections.Generic;
using UnityEngine;

// Collects nearby SceneContextObjects within a radius of the NPC, sorted nearest-first, and
// exposes their distinct object ids for world-event relevance lookups. Behavior preserved verbatim
// from the original ContextRetriever (sceneContextRadius is passed in by the coordinator).
public static class SceneContextCollector
{
    public static List<SceneContextObject> FindNearby(Transform npcTransform, float sceneContextRadius)
    {
        List<SceneContextObject> result = new List<SceneContextObject>();

        if (npcTransform == null)
        {
            return result;
        }

        SceneContextObject[] objects = Object.FindObjectsByType<SceneContextObject>(FindObjectsSortMode.None);

        for (int i = 0; i < objects.Length; i++)
        {
            SceneContextObject contextObject = objects[i];

            if (contextObject == null)
            {
                continue;
            }

            float distance = Vector3.Distance(npcTransform.position, contextObject.transform.position);

            if (distance <= sceneContextRadius)
            {
                result.Add(contextObject);
            }
        }

        result.Sort(delegate(SceneContextObject a, SceneContextObject b)
        {
            float distanceA = Vector3.Distance(npcTransform.position, a.transform.position);
            float distanceB = Vector3.Distance(npcTransform.position, b.transform.position);
            return distanceA.CompareTo(distanceB);
        });

        return result;
    }

    public static List<string> GetObjectIds(List<SceneContextObject> nearbyObjects)
    {
        List<string> result = new List<string>();

        if (nearbyObjects == null)
        {
            return result;
        }

        for (int i = 0; i < nearbyObjects.Count; i++)
        {
            SceneContextObject contextObject = nearbyObjects[i];

            if (contextObject != null && !string.IsNullOrEmpty(contextObject.objectId) && !KnowledgeTextUtil.ContainsIgnoreCase(result, contextObject.objectId))
            {
                result.Add(contextObject.objectId);
            }
        }

        return result;
    }
}
