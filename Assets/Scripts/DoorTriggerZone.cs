// ============================================================
// 1. Script Name: DoorTriggerZone.cs
// 2. Purpose: Controls the InnerDoor state using a box-based detection
//    (same pattern as Coin.cs) so the player never needs to jump to enter.
//    The black portal is hidden until the terminal challenge is completed.
//    Once unlocked, walking within the door box loads the Level Select scene.
// 3. Unity Setup Instructions:
//    - Attach to: The InnerDoor GameObject in the Level scene.
//    - Inspector Links:
//        - blackPortal         : drag the 'blackportal' child GameObject here.
//        - enterSize           : Width x Height of the detection box (default 2 x 3).
//        - playerTag           : must match the Player's tag (default "Player").
//        - levelSelectSceneName: name of the Level Select scene (default "LevelPanel").
//    - NO manual event wiring needed: DoorTriggerZone auto-connects to
//      TerminalLevelController.onLevelSolved at runtime via Awake().
// ============================================================

using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorTriggerZone : MonoBehaviour
{
    [Header("Door References")]
    [Tooltip("Drag the 'blackportal' child GameObject here.")]
    [SerializeField] private GameObject blackPortal;

    [Header("Detection Settings")]
    [Tooltip("Width and Height of the box detection area. Resize until the green box covers the door opening at ground level.")]
    [SerializeField] private Vector2 enterSize = new Vector2(2f, 3f);

    [Tooltip("Tag used to identify the Player GameObject.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Navigation")]
    [Tooltip("Exact name of the Level Select scene to load when the player enters the door.")]
    [SerializeField] private string levelSelectSceneName = "LevelPanel";

    // Tracks whether the terminal challenge has been completed.
    private bool isDoorUnlocked = false;

    // Safety flag so we only trigger the scene load once.
    private bool _transitioning = false;

    private void Awake()
    {
        // Auto-subscribe to TerminalLevelController so UnlockDoor() is always
        // called on success — no manual Inspector event wiring required.
        TerminalLevelController terminal = FindObjectOfType<TerminalLevelController>();
        if (terminal != null)
        {
            terminal.onLevelSolved.AddListener(UnlockDoor);
            Debug.Log("[DoorTriggerZone] Auto-subscribed to TerminalLevelController.onLevelSolved.");
        }
        else
        {
            Debug.LogWarning("[DoorTriggerZone] No TerminalLevelController found in scene! UnlockDoor will not be called automatically.");
        }
    }

    private void OnDestroy()
    {
        // Clean up the subscription when this object is destroyed.
        TerminalLevelController terminal = FindObjectOfType<TerminalLevelController>();
        if (terminal != null)
        {
            terminal.onLevelSolved.RemoveListener(UnlockDoor);
        }
    }

    private void Start()
    {
        // Door starts LOCKED — hide the black portal fill.
        SetPortalVisible(false);
        Debug.Log("[DoorTriggerZone] Door is LOCKED. Black portal hidden.");
    }

    private void Update()
    {
        // Only check for player when door is unlocked and not already transitioning.
        if (!isDoorUnlocked || _transitioning) return;

        // Box-based detection — works at ground level regardless of collider position.
        // The box size is set in the Inspector and visualised as a green wire cube in Scene view.
        Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, enterSize, 0f);
        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag(playerTag))
            {
                EnterDoor();
                break;
            }
        }
    }

    // -------------------------------------------------------
    // Called by TerminalLevelController's onLevelSolved event.
    // Wire this in the Inspector: OnLevelSolved -> UnlockDoor()
    // -------------------------------------------------------
    public void UnlockDoor()
    {
        isDoorUnlocked = true;
        SetPortalVisible(true);
        Debug.Log("[DoorTriggerZone] Door UNLOCKED. Black portal is now visible.");
    }

    // -------------------------------------------------------
    // Loads the Level Select scene when the player enters the door.
    // -------------------------------------------------------
    private void EnterDoor()
    {
        _transitioning = true;
        Debug.Log("[DoorTriggerZone] Player entered the unlocked door. Loading Level Select...");
        PlayerPrefs.Save();
        SceneManager.LoadSceneAsync(levelSelectSceneName);
    }

    // -------------------------------------------------------
    // Helper: show or hide the black portal child object.
    // -------------------------------------------------------
    private void SetPortalVisible(bool visible)
    {
        if (blackPortal != null)
        {
            blackPortal.SetActive(visible);
        }
        else
        {
            Debug.LogWarning("[DoorTriggerZone] 'blackPortal' is not assigned in the Inspector!");
        }
    }

    // -------------------------------------------------------
    // Visualize the detection box in the Scene view (green box).
    // Adjust 'enterSize' in the Inspector until the green box
    // covers the full door opening at ground level.
    // -------------------------------------------------------
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(enterSize.x, enterSize.y, 0f));
    }
}
