using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CreateLadderEvidenceDemoObject
{
    private const string LadderObjectName = "Evidence_LadderNearChurch";

    [MenuItem("Tools/AI NPC/Create Ladder Evidence Demo Object")]
    public static void CreateDemoObject()
    {
        GameObject existing = GameObject.Find(LadderObjectName);

        if (existing != null)
        {
            Selection.activeGameObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("Ladder evidence demo object already exists. Selected " + LadderObjectName + ".");
            LogKnowledgeBaseSetupNote();
            return;
        }

        GameObject root = new GameObject(LadderObjectName);
        Undo.RegisterCreatedObjectUndo(root, "Create Ladder Evidence Demo Object");
        root.transform.position = Vector3.zero;

        CreateLadderPiece(root.transform, "Left Rail", new Vector3(-0.35f, 1.2f, 0f), new Vector3(0.08f, 2.4f, 0.08f));
        CreateLadderPiece(root.transform, "Right Rail", new Vector3(0.35f, 1.2f, 0f), new Vector3(0.08f, 2.4f, 0.08f));

        for (int i = 0; i < 5; i++)
        {
            float y = 0.35f + (i * 0.42f);
            CreateLadderPiece(root.transform, "Rung " + (i + 1), new Vector3(0f, y, 0f), new Vector3(0.8f, 0.07f, 0.08f));
        }

        EvidenceObject evidenceObject = root.AddComponent<EvidenceObject>();
        evidenceObject.evidenceId = "ladder_near_church";
        evidenceObject.displayName = "Ladder near the church";
        evidenceObject.description = "A wooden ladder leaning near the old church wall. It looks recently moved and could have been used to reach the bell tower or inspect the bell mechanism.";
        evidenceObject.factsToAddToPlayer = new List<string>
        {
            "found_ladder_near_church",
            "A wooden ladder was found leaning near the old church wall.",
            "The ladder looks recently moved.",
            "The ladder could have been used to reach the bell tower or inspect the bell mechanism."
        };
        evidenceObject.itemsToAddToPlayer = new List<string>
        {
            "observation: ladder near church"
        };
        evidenceObject.hideAfterCollect = false;

        EvidenceInteraction interaction = root.AddComponent<EvidenceInteraction>();
        interaction.evidenceObject = evidenceObject;

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        EditorSceneManager.MarkSceneDirty(root.scene);

        Debug.Log("Created ladder evidence demo object at the scene origin. Move " + LadderObjectName + " near the church.");
        LogKnowledgeBaseSetupNote();
    }

    private static void CreateLadderPiece(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        piece.transform.SetParent(parent);
        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = localScale;
    }

    private static void LogKnowledgeBaseSetupNote()
    {
        Debug.Log(
            "Optional KnowledgeBase entry can be added manually:\n" +
            "id: ladder_near_church_context\n" +
            "title: Ladder near the church\n" +
            "text: A ladder near the old church could indicate that someone tried to reach the bell tower, the rope, or the bell mechanism. By itself it does not prove who took the bell, but it changes the situation from a vague rumor into something that may involve preparation.\n" +
            "tags: church, ladder, bell, evidence, tower\n" +
            "relatedObjectIds: church\n" +
            "knownByNpcIds: eldric, borin, anselm\n" +
            "importance: 3");
    }
}
