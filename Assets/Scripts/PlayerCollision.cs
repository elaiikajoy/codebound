// ============================================================
// PlayerCollision.cs
// Purpose: Triggers Game Over when the player touches this hazard/GameObject.
//
// Handles BOTH:
//   - Trigger colliders (Is Trigger = ON)  -> OnTriggerEnter2D
//   - Solid colliders   (Is Trigger = OFF) -> OnCollisionEnter2D
//
// Unity Setup:
//   - Attach to the hazard/enemy GameObject shown in the Inspector.
//   - Tag the Player GameObject as "Player".
//   - The hazard must have a Collider2D.
//   - If using a solid collider, make sure either the player or hazard has a Rigidbody2D.
// ============================================================

using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Tag used to identify the player object.")]
    [SerializeField] private string playerTag = "Player";

    // Trigger-based collision (Is Trigger = ON on this hazard)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(playerTag))
            TriggerGameOver(collision.gameObject);
    }

    // Physical collision (Is Trigger = OFF on this hazard)
    // This handles hazards like spike walls, fences, etc. whose
    // BoxCollider2D does NOT have "Is Trigger" checked.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
            TriggerGameOver(collision.gameObject);
    }

    // Shared game-over logic
    private void TriggerGameOver(GameObject playerObject)
    {
        // Prevent double-triggering if already game over.
        if (PlayerManager.isGameOver) return;

        PlayerManager.isGameOver = true;

        // Play game over sound if AudioManager is present.
        if (AudioManager.instance != null)
            AudioManager.instance.Play("GameOver");

        // Hide the player after the hit.
        if (playerObject != null)
            playerObject.SetActive(false);
    }
}
