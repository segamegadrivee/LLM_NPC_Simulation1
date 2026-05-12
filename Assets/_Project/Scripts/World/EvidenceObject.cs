using System.Collections.Generic;
using UnityEngine;

public class EvidenceObject : MonoBehaviour
{
    public string evidenceId;
    public string displayName;

    [TextArea(3, 10)]
    public string description;

    public List<string> factsToAddToPlayer = new List<string>();
    public List<string> itemsToAddToPlayer = new List<string>();
    public bool collected = false;
    public bool hideAfterCollect = false;

    public void Collect(PlayerState playerState)
    {
        if (collected)
        {
            Debug.Log("Evidence already collected: " + GetDisplayName(), this);
            return;
        }

        if (playerState == null)
        {
            Debug.LogWarning("Evidence could not be collected because PlayerState was missing: " + GetDisplayName(), this);
            return;
        }

        AddFactsToPlayer(playerState);
        AddItemsToPlayer(playerState);

        collected = true;

        if (hideAfterCollect)
        {
            HideEvidenceObject();
        }

        Debug.Log("Evidence collected: " + GetDisplayName(), this);
    }

    private void AddFactsToPlayer(PlayerState playerState)
    {
        if (factsToAddToPlayer == null)
        {
            return;
        }

        for (int i = 0; i < factsToAddToPlayer.Count; i++)
        {
            playerState.AddKnownFact(factsToAddToPlayer[i]);
        }
    }

    private void AddItemsToPlayer(PlayerState playerState)
    {
        if (itemsToAddToPlayer == null)
        {
            return;
        }

        for (int i = 0; i < itemsToAddToPlayer.Count; i++)
        {
            playerState.AddHeldItem(itemsToAddToPlayer[i]);
        }
    }

    private void HideEvidenceObject()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private string GetDisplayName()
    {
        return string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
    }
}
