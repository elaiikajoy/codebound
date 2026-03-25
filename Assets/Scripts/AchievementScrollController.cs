using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the setup and logic for a vertical scrolling list in the UI.
/// Specifically tailored for the Achievement list.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
[RequireComponent(typeof(Image))]
public class AchievementScrollController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The Content GameObject holding the Achievement Row prefabs.")]
    public RectTransform content;

    [Header("Scroll List Settings")]
    [Tooltip("Distance between each achievement row.")]
    public float spaceBetweenAchievements = 10f;
    [Tooltip("Inward padding across the entire list.")]
    public RectOffset padding;
    [Tooltip("How fast the scroll responds to mouse/touch drag.")]
    public float scrollSensitivity = 15f;

    private ScrollRect _scrollRect;

    private void Awake()
    {
        if (padding == null) padding = new RectOffset(10, 10, 10, 10);
        InitializeScrolling();
    }

    private void OnEnable()
    {
        RefreshLayout();
    }

    /// <summary>
    /// Forces a refresh of the layout group so the scroll boundaries update perfectly,
    /// especially important when rows are added dynamically or the panel is toggled.
    /// </summary>
    public void RefreshLayout()
    {
        if (content != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 1f; // Always start from Top
            }
        }
    }

    [ContextMenu("Setup Achievement Scrolling")]
    public void InitializeScrolling()
    {
        // 1. Handle background image for raycast (needed to detect dragging)
        Image bgImg = GetComponent<Image>();
        if (bgImg != null)
        {
            bgImg.color = new Color(1f, 1f, 1f, 0f); // Invisible, but catches clicks
            bgImg.raycastTarget = true;
        }

        // 2. Configure ScrollRect properties
        _scrollRect = GetComponent<ScrollRect>();
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Elastic;
        _scrollRect.scrollSensitivity = scrollSensitivity;
        _scrollRect.viewport = GetComponent<RectTransform>();

        // 3. Apply Masking to hide items outside the view area
        if (GetComponent<RectMask2D>() == null && GetComponent<Mask>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }

        SetupContent();
    }

    private void SetupContent()
    {
        // Safely find Content if unassigned
        if (content == null)
        {
            Transform foundContent = transform.Find("Content") ?? transform.Find("Contentt");
            if (foundContent != null)
            {
                content = foundContent as RectTransform;
            }
            else
            {
                Debug.LogError("[AchievementScrollController] Validation Error: Missing 'Content' object. Please assign it in the Inspector.");
                return;
            }
        }

        // Some Achievement scenes use a wrapper Content object that contains a single
        // row container (for example, AchievementRowPanle). ScrollRect needs the
        // transform that actually expands with the rows, not the wrapper shell.
        if (content.childCount == 1)
        {
            RectTransform nestedContent = content.GetChild(0) as RectTransform;
            if (nestedContent != null && nestedContent.childCount > 0)
            {
                content = nestedContent;
            }
        }

        _scrollRect.content = content;

        // Set anchors for a vertical top-down list
        content.anchorMin = new Vector2(0.5f, 1f);
        content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);

        RectTransform viewport = _scrollRect.viewport != null ? _scrollRect.viewport : GetComponent<RectTransform>();
        if (viewport != null)
        {
            Vector2 sizeDelta = content.sizeDelta;
            sizeDelta.x = viewport.rect.width;
            content.sizeDelta = sizeDelta;
        }

        content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);

        // Configure Layout Group for automated dynamic spacing
        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();

        vlg.spacing = spaceBetweenAchievements;
        vlg.padding = padding;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandHeight = false; // Prevent rows from stretching to fill empty space
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = false;     // Use the row prefab's explicit RectTransform height
        vlg.childControlWidth = true;

        // Configure Size Fitter so Content stretches downwards as rows are added
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
}
