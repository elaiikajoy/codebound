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
        if (!other.CompareTag(playerTag)) return;

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
