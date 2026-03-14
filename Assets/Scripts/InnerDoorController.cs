// ============================================================
// 1. Script Name: InnerDoorController.cs
// 2. Purpose: Controls the InnerDoor state. The black portal is hidden
//    until the player successfully completes the terminal coding challenge.
//    Once unlocked, the player can walk through to load the Level Select scene.
// 3. Unity Setup Instructions:
//    - Attach to: The InnerDoor GameObject in the Level scene.
//    - Required Components: BoxCollider2D with "Is Trigger" enabled (already set).
//    - Inspector Links:
//        - blackPortal: drag the 'blackportal' child GameObject here.
//        - playerTag: must match the Player's tag (default "Player").
//        - levelSelectSceneName: name of the Level Select scene (default "LevelPanel").
//    - Wiring the event:
//        1. Select the GameObject that holds TerminalLevelController.
//        2. In the Inspector, find "On Level Solved ()" under Events.
//        3. Click [+], drag InnerDoor into the object slot.
//        4. Select: InnerDoorController -> UnlockDoor()
// ============================================================

using UnityEngine;
using UnityEngine.SceneManagement;

public class InnerDoorController : MonoBehaviour
{
    [Header("Door References")]
    [Tooltip("Drag the 'blackportal' child GameObject here.")]
    [SerializeField] private GameObject blackPortal;

    [Header("Settings")]
    [Tooltip("Tag used to identify the Player GameObject.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Exact name of the Level Select scene to load when the player exits.")]
    [SerializeField] private string levelSelectSceneName = "LevelPanel";

    // Tracks whether the terminal challenge has been completed.
    private bool isDoorUnlocked = false;

    private void Start()
    {
        // Door starts LOCKED: hide the black portal fill.
        SetPortalVisible(false);
        Debug.Log("[InnerDoorController] Door is LOCKED. Black portal hidden.");
    }

    // -------------------------------------------------------
    // Called by TerminalLevelController's onLevelSolved event.
    // Wire this in the Inspector: OnLevelSolved -> UnlockDoor()
    // -------------------------------------------------------
    public void UnlockDoor()
    {
        isDoorUnlocked = true;
        SetPortalVisible(true);
        Debug.Log("[InnerDoorController] Door UNLOCKED. Black portal is now visible.");
    }

    // -------------------------------------------------------
    // Triggered when the player walks into the InnerDoor collider.
    // Only loads LevelPanel if the door has been unlocked.
    // -------------------------------------------------------
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (!isDoorUnlocked)
        {
            Debug.Log("[InnerDoorController] Player touched the door but it is still LOCKED. Complete the terminal first.");
            return;
        }

        Debug.Log("[InnerDoorController] Player entered the unlocked door. Loading Level Select...");
        PlayerPrefs.Save(); // persist any progress before transitioning
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
            Debug.LogWarning("[InnerDoorController] 'blackPortal' is not assigned in the Inspector!");
        }
    }
}
