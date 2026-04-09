// ============================================================
// 1. Script Name: Coin.cs
// 2. Purpose: Handles coin collection logic — updates the player's
//             unified token total via TokenManager, plays a sound,
//             and destroys the coin object.
// 3. Unity Setup Instructions:
//    - Attach to: Coin GameObjects in the scene.
//    - Required Components: Collider2D (BoxCollider2D, CircleCollider2D, etc.)
//      configured as "Is Trigger".
//    - Tags: The player GameObject must have the tag "Player".
// ============================================================

using UnityEngine;

public class Coin : MonoBehaviour
{
    [Header("Collection Settings")]
    [Tooltip("How close the player must be to collect this coin.")]
    public float collectionRadius = 0.8f;

    [Tooltip("Token value of this coin (default 1).")]
    public int tokenValue = 1;

    [Tooltip("Physics layers that contain the Player.")]
    public LayerMask playerLayerMask;

    // Safety flag — prevents double-collection in the same frame
    private bool _collected = false;

    private void Update()
    {
        if (_collected) return;

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, collectionRadius);
        foreach (Collider2D hit in hitColliders)
        {
            if (hit.CompareTag("Player"))
            {
                CollectCoin();
                break;
            }
        }
    }

    /// <summary>Fallback trigger-based collection.</summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_collected) return;
        if (collision.CompareTag("Player"))
            CollectCoin();
    }

    /// <summary>
    /// Core collection logic: adds token(s) via TokenManager, plays sound, destroys coin.
    /// </summary>
    private void CollectCoin()
    {
        _collected = true;

        // ── Update unified token count ─────────────────────────────────
        // TokenManager.AddTokens also increments PendingTokensToSync so
        // the batch can be flushed to the backend immediately when logged in.
        TokenManager.AddTokens(tokenValue);
        TokenManager.RequestPendingSync();

        Debug.Log($"[Coin] Collected! +{tokenValue} token(s). Total: {TokenManager.GetTokens()} | Pending: {TokenManager.GetPending()}");

        // ── Play sound ─────────────────────────────────────────────────
        if (AudioManager.instance != null)
            AudioManager.instance.Play("Coin");
        else
            Debug.LogWarning("[Coin] AudioManager.instance is null — cannot play 'Coin' sound.");

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collectionRadius);
    }
}
