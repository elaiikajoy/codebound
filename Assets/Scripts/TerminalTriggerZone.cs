// ============================================================
// 1. Script Name: TerminalTriggerZone.cs
// 2. Purpose: Detects player proximity to a computer terminal. Shows a UI prompt and opens the coding IDE when interacting.
// 3. Unity Setup Instructions:
//    - Attach to: The Terminal/Computer GameObject that the player should approach.
//    - Required Components: Collider2D (BoxCollider2D, CircleCollider2D, etc.) with "Is Trigger" enabled.
//    - Inspector Links: Assign the 'TerminalLevelController' to the Inspector slot.
// ============================================================

using TMPro;
using UnityEngine;

public class TerminalTriggerZone : MonoBehaviour
{
    [SerializeField] private TerminalLevelController terminalController;
    [SerializeField] private bool requireInteractKey = true;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private TMP_Text interactPromptText;

    private bool playerInside;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[TerminalTriggerZone] Something touched the computer! It was: {other.gameObject.name} with Tag: {other.tag}");

        if (!other.CompareTag(playerTag)) 
        {
            Debug.Log($"[TerminalTriggerZone] Ignored {other.gameObject.name} because its tag is '{other.tag}' instead of '{playerTag}'.");
            return;
        }

        playerInside = true;

        if (interactPromptText != null)
        {
            interactPromptText.gameObject.SetActive(true);
            interactPromptText.text = requireInteractKey ? $"Press {interactKey} to open terminal" : "Opening terminal...";
        }

        if (!requireInteractKey)
        {
            OpenTerminal();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = false;

        if (interactPromptText != null)
        {
            interactPromptText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!playerInside || !requireInteractKey) return;

        if (Input.GetKeyDown(interactKey))
        {
            Debug.Log($"[TerminalTriggerZone] Player pressed {interactKey} while inside the zone! Calling OpenTerminal...");
            OpenTerminal();
        }
    }

    private void OpenTerminal()
    {
        if (terminalController == null)
        {
            Debug.LogWarning("[TerminalTriggerZone] Terminal controller is not assigned.");
            return;
        }

        terminalController.OpenCurrentLevel();
    }
}
