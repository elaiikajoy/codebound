using System.Text.RegularExpressions;
using UnityEngine;

// Ensures a consistent "no-fall" behavior for levels that opt-in via LevelData.preventFall.
// Attach this to the Player GameObject or a level manager object in the scene.
public class LevelFallController : MonoBehaviour
{
    [Tooltip("How far above the bottom of the camera the player will be clamped when preventFall is enabled.")]
    public float minAboveCameraBottom = 1.0f;

    private Camera mainCamera;
    private Rigidbody2D playerRb;
    private Transform playerTransform;
    private LevelData activeLevelData;
    private int sceneLevelNumber = 0;

    // Last known safe position (updated when the player is grounded).
    private Vector2 lastSafePosition;

    private void Awake()
    {
        playerTransform = transform; // assume attached to player
        playerRb = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;
        lastSafePosition = playerTransform.position;

        // Attempt to infer scene level number from the scene name (e.g., "Level3").
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        var match = Regex.Match(sceneName, "(\\d+)");
        if (match.Success && int.TryParse(match.Value, out int parsed))
        {
            sceneLevelNumber = parsed;
        }

        if (sceneLevelNumber > 0)
        {
            activeLevelData = LevelDataLoader.LoadLevel(sceneLevelNumber);
        }
    }

    private void Update()
    {
        if (activeLevelData == null || !activeLevelData.preventFall)
            return; // nothing to do for this level

        // Update last safe position when the player is roughly grounded.
        var movement = GetComponent<Movement>();
        if (movement != null && movement.Grounded)
        {
            lastSafePosition = playerTransform.position;
        }

        // Compute camera bottom world y coordinate
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 bottomLeft = mainCamera.ScreenToWorldPoint(Vector3.zero);
        float cameraBottom = bottomLeft.y;

        // If player is below the allowed bottom, move them back up to last safe position
        if (playerTransform.position.y < cameraBottom + minAboveCameraBottom)
        {
            // If we have a last safe position, move the player back there; otherwise clamp to camera bottom
            Vector2 target = lastSafePosition;
            if (target == Vector2.zero)
                target = new Vector2(playerTransform.position.x, cameraBottom + minAboveCameraBottom);

            playerTransform.position = new Vector3(target.x, Mathf.Max(target.y, cameraBottom + minAboveCameraBottom), playerTransform.position.z);

            if (playerRb != null)
            {
                playerRb.velocity = Vector2.zero;
            }
        }
    }
}
