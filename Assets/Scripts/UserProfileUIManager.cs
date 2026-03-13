using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles user profile panel flow using this hierarchy style:
/// UserProfilePanel -> UserProfilePanel / RenamePanel / ChangePassPanel
/// and wires Rename / Change Password / OK / Back buttons.
/// </summary>
public class UserProfileUIManager : MonoBehaviour
{
    [Header("Panel Roots")]
    public GameObject profileRootPanel;
    public GameObject mainProfilePanel;
    public GameObject renamePanel;
    public GameObject changePassPanel;
    public GameObject mainMenuPanel;
    public GameObject loginPanel;

    [Header("Inputs")]
    public TMP_InputField mainUsernameInput;
    public TMP_InputField renameInput;
    public TMP_InputField changePasswordInput;

    [Header("Main Panel Buttons")]
    public Button renameButton;
    public Button changePassButton;
    public Button deleteButton;
    public Button mainOkButton;

    [Header("Rename Panel Buttons")]
    public Button renameOkButton;
    public Button renameBackButton;

    [Header("Change Password Panel Buttons")]
    public Button changePassOkButton;
    public Button changePassBackButton;

    [Header("Behavior")]
    public bool autoFindByHierarchyNames = true;
    public bool autoLoadOnStart = true;

    [Header("Optional feedback")]
    public TMP_Text feedbackText;

    private bool _busy;

    private void Start()
    {
        if (autoFindByHierarchyNames)
            TryAutoFindReferences();

        BindButtons();
        OpenMainPanel();

        if (autoLoadOnStart)
            ClickRefreshProfile();
    }

    // Wrapper names to match existing button naming conventions.
    public void Rename() => OpenRenamePanel();
    public void ChangePassword() => OpenChangePassPanel();
    public void Delete() => ClickDeleteAccount();
    public void Ok() => ReturnToMainMenu();

    public void OpenMainPanel()
    {
        if (profileRootPanel != null) profileRootPanel.SetActive(true);
        if (mainProfilePanel != null) mainProfilePanel.SetActive(true);
        if (renamePanel != null) renamePanel.SetActive(false);
        if (changePassPanel != null) changePassPanel.SetActive(false);
    }

    public void ReturnToMainMenu()
    {
        if (_busy) return;

        if (profileRootPanel != null) profileRootPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (loginPanel != null) loginPanel.SetActive(false);
    }

    public void ReturnToLoginPanel()
    {
        if (profileRootPanel != null) profileRootPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (loginPanel != null) loginPanel.SetActive(true);
    }

    public void OpenRenamePanel()
    {
        if (_busy) return;
        if (mainProfilePanel != null) mainProfilePanel.SetActive(false);
        if (renamePanel != null) renamePanel.SetActive(true);
        if (changePassPanel != null) changePassPanel.SetActive(false);

        if (renameInput != null)
            renameInput.text = mainUsernameInput != null ? mainUsernameInput.text : string.Empty;
    }

    public void OpenChangePassPanel()
    {
        if (_busy) return;
        if (mainProfilePanel != null) mainProfilePanel.SetActive(false);
        if (renamePanel != null) renamePanel.SetActive(false);
        if (changePassPanel != null) changePassPanel.SetActive(true);
    }

    public void ClickRefreshProfile()
    {
        if (_busy) return;

        if (GameApiManager.Instance == null || !GameApiManager.Instance.IsLoggedIn)
        {
            ShowFeedback("Please log in first.");
            return;
        }

        SetBusy(true);
        ShowFeedback("Loading profile...");

        GameApiManager.Instance.GetProfile(
            onSuccess: user =>
            {
                string username = user.username ?? string.Empty;
                if (mainUsernameInput != null) mainUsernameInput.text = username;
                if (renameInput != null) renameInput.text = username;

                ShowFeedback("Profile loaded.");
                SetBusy(false);
            },
            onError: err =>
            {
                ShowFeedback(err);
                SetBusy(false);
            }
        );
    }

    public void ClickSubmitRename()
    {
        if (_busy) return;

        if (GameApiManager.Instance == null || !GameApiManager.Instance.IsLoggedIn)
        {
            ShowFeedback("Please log in first.");
            return;
        }

        string nextUsername = renameInput != null ? renameInput.text?.Trim() : string.Empty;
        if (string.IsNullOrEmpty(nextUsername))
        {
            ShowFeedback("Username is required.");
            return;
        }

        SetBusy(true);
        ShowFeedback("Updating username...");

        GameApiManager.Instance.UpdateUsername(
            nextUsername,
            onSuccess: user =>
            {
                string updated = user.username ?? string.Empty;
                if (mainUsernameInput != null) mainUsernameInput.text = updated;
                if (renameInput != null) renameInput.text = updated;

                ShowFeedback("Username updated.");
                SetBusy(false);
                OpenMainPanel();
            },
            onError: err =>
            {
                ShowFeedback(err);
                SetBusy(false);
            }
        );
    }

    public void ClickSubmitChangePassword()
    {
        if (_busy) return;

        if (GameApiManager.Instance == null || !GameApiManager.Instance.IsLoggedIn)
        {
            ShowFeedback("Please log in first.");
            return;
        }

        string nextPassword = changePasswordInput != null ? changePasswordInput.text : string.Empty;
        if (string.IsNullOrEmpty(nextPassword) || nextPassword.Length < 6)
        {
            ShowFeedback("New password must be at least 6 characters.");
            return;
        }

        SetBusy(true);
        ShowFeedback("Changing password...");

        GameApiManager.Instance.ChangePassword(
            string.Empty,
            nextPassword,
            onSuccess: () =>
            {
                if (changePasswordInput != null) changePasswordInput.text = string.Empty;

                ShowFeedback("Password updated.");
                SetBusy(false);
                OpenMainPanel();
            },
            onError: err =>
            {
                ShowFeedback(err);
                SetBusy(false);
            }
        );
    }

    public void ClickDeleteAccount()
    {
        if (_busy) return;

        if (GameApiManager.Instance == null || !GameApiManager.Instance.IsLoggedIn)
        {
            ShowFeedback("Please log in first.");
            return;
        }

        SetBusy(true);
        ShowFeedback("Deleting account...");

        GameApiManager.Instance.DeleteAccount(
            onSuccess: () =>
            {
                ShowFeedback("Account deleted.");
                SetBusy(false);
                ReturnToLoginPanel();
            },
            onError: err =>
            {
                ShowFeedback(err);
                SetBusy(false);
            }
        );
    }

    private void BindButtons()
    {
        if (renameButton != null)
        {
            renameButton.onClick.RemoveListener(OpenRenamePanel);
            renameButton.onClick.AddListener(OpenRenamePanel);
        }

        if (changePassButton != null)
        {
            changePassButton.onClick.RemoveListener(OpenChangePassPanel);
            changePassButton.onClick.AddListener(OpenChangePassPanel);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(ClickDeleteAccount);
            deleteButton.onClick.AddListener(ClickDeleteAccount);
        }

        if (mainOkButton != null)
        {
            mainOkButton.onClick.RemoveListener(ReturnToMainMenu);
            mainOkButton.onClick.AddListener(ReturnToMainMenu);
        }

        if (renameOkButton != null)
        {
            renameOkButton.onClick.RemoveListener(ClickSubmitRename);
            renameOkButton.onClick.AddListener(ClickSubmitRename);
        }

        if (renameBackButton != null)
        {
            renameBackButton.onClick.RemoveListener(OpenMainPanel);
            renameBackButton.onClick.AddListener(OpenMainPanel);
        }

        if (changePassOkButton != null)
        {
            changePassOkButton.onClick.RemoveListener(ClickSubmitChangePassword);
            changePassOkButton.onClick.AddListener(ClickSubmitChangePassword);
        }

        if (changePassBackButton != null)
        {
            changePassBackButton.onClick.RemoveListener(OpenMainPanel);
            changePassBackButton.onClick.AddListener(OpenMainPanel);
        }
    }

    private void TryAutoFindReferences()
    {
        if (profileRootPanel == null) profileRootPanel = gameObject;
        if (mainProfilePanel == null) mainProfilePanel = FindChildGameObject("UserProfilePanel");
        if (renamePanel == null) renamePanel = FindChildGameObject("RenamePanel");
        if (changePassPanel == null) changePassPanel = FindChildGameObject("ChangePassPanel");
        if (mainMenuPanel == null) mainMenuPanel = FindAnywhere("Main Menu");
        if (loginPanel == null) loginPanel = FindAnywhere("LoginPanel");

        if (mainUsernameInput == null && mainProfilePanel != null)
            mainUsernameInput = mainProfilePanel.GetComponentInChildren<TMP_InputField>(true);

        if (renameInput == null && renamePanel != null)
            renameInput = renamePanel.GetComponentInChildren<TMP_InputField>(true);

        if (changePasswordInput == null && changePassPanel != null)
            changePasswordInput = changePassPanel.GetComponentInChildren<TMP_InputField>(true);

        if (renameButton == null && mainProfilePanel != null) renameButton = FindButtonByName(mainProfilePanel.transform, "RenameButton");
        if (changePassButton == null && mainProfilePanel != null) changePassButton = FindButtonByName(mainProfilePanel.transform, "ChangePassButton");
        if (deleteButton == null && mainProfilePanel != null) deleteButton = FindButtonByName(mainProfilePanel.transform, "DeleteButton");
        if (mainOkButton == null && mainProfilePanel != null) mainOkButton = FindButtonByName(mainProfilePanel.transform, "OK");

        if (renameOkButton == null && renamePanel != null) renameOkButton = FindButtonByName(renamePanel.transform, "OK");
        if (renameBackButton == null && renamePanel != null) renameBackButton = FindButtonByName(renamePanel.transform, "BackButton");

        if (changePassOkButton == null && changePassPanel != null) changePassOkButton = FindButtonByName(changePassPanel.transform, "OK");
        if (changePassBackButton == null && changePassPanel != null) changePassBackButton = FindButtonByName(changePassPanel.transform, "BackButton");
    }

    private GameObject FindChildGameObject(string name)
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            if (t.name == name) return t.gameObject;
        }
        return null;
    }

    private GameObject FindAnywhere(string name)
    {
        foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj.name == name && obj.scene.IsValid())
                return obj;
        }

        return null;
    }

    private Button FindButtonByName(Transform root, string name)
    {
        if (root == null) return null;
        Transform target = root.Find(name);
        if (target == null)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    target = t;
                    break;
                }
            }
        }

        return target != null ? target.GetComponent<Button>() : null;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;

        if (renameButton != null) renameButton.interactable = !busy;
        if (changePassButton != null) changePassButton.interactable = !busy;
        if (deleteButton != null) deleteButton.interactable = !busy;
        if (mainOkButton != null) mainOkButton.interactable = !busy;
        if (renameOkButton != null) renameOkButton.interactable = !busy;
        if (renameBackButton != null) renameBackButton.interactable = !busy;
        if (changePassOkButton != null) changePassOkButton.interactable = !busy;
        if (changePassBackButton != null) changePassBackButton.interactable = !busy;
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;

        Debug.Log("[Profile UI] " + message);
    }
}
