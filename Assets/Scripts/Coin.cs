// ============================================================
// 1. Script Name: Coin.cs
// 2. Purpose: Handles coin collection logic, updating player score, playing a sound, and destroying the coin object.
// 3. Unity Setup Instructions:
//    - Attach to: Coin GameObjects in the scene.
//    - Required Components: Collider2D (BoxCollider2D, CircleCollider2D, etc.) configured as "Is Trigger".
//    - Tags: The player GameObject must have the tag "Player".
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coin : MonoBehaviour
{
    [Header("Collection Settings")]
    [Tooltip("How close the player must be to collect this coin. Increase this if the player can't reach it while walking.")]
    public float collectionRadius = 0.8f;

    [Tooltip("The physics layers that contain the Player (e.g., Default or Player layer).")]
    public LayerMask playerLayerMask;
    
    // Safety flag to prevent double-collection during the same frame
    private bool _collected = false;

    private void Update()
    {
        if (_collected) return;

        // Collect all colliders within the radius regardless of physics layer
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, collectionRadius);
        
        foreach (Collider2D hit in hitColliders)
        {
            if (hit.CompareTag("Player"))
            {
                CollectCoin();
                break; // Stop checking once we collect it
            }
        }
    }

    /// <summary>
    /// Fallback direct collision support.
    /// Triggered when another collider enters this object's trigger collider.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_collected) return;

        if (collision.CompareTag("Player"))
        {
            CollectCoin();
        }
    }

    /// <summary>
    /// Core collection logic extracting score logic, audio, and saving.
    /// </summary>
    private void CollectCoin()
    {
        _collected = true; // Prevent multiple triggers from duplicate overlaps

        Debug.Log("COIN COLLECTED!");
        
        // Increment the static coin counter
        PlayerManager.numberOfCoin++;
        
        // Play the collection sound safely (prevents NullReferenceException if AudioManager is missing)
        if (AudioManager.instance != null)
        {
            AudioManager.instance.Play("Coin");
        }
        else
        {
            Debug.LogWarning("AudioManager.instance is null! Cannot play 'Coin' sound.");
        }
        
        // Save the progress
        PlayerPrefs.SetInt("numberOfCoin", PlayerManager.numberOfCoin);
        
        // Remove the coin from the scene
        Destroy(gameObject);
    }

    /// <summary>
    /// Visualize the collection radius in the Unity Editor Scene View 
    /// so it's easy to adjust the distance without guessing.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collectionRadius);
    }
}
