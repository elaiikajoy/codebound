// ============================================================
// UIScrollController.cs
// Purpose: Automatically configures a ScrollRect and Layout Groups
//          at runtime to enable scrolling for any UI panel.
//
// Unity Setup:
//   - Attach this script to the "Scroll" or "Scroll Menu" GameObject.
//   - For Level Selection: Enable 'Use Grid Mode'.
//   - For Achievements/Lists: Enable 'Use Vertical List Mode'.
// ============================================================

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class UIScrollController : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("The container holding all UI elements. If left empty, it tries to find a child named 'Content'.")]
    public RectTransform content;

    [Header("Grid Layout (e.g. Levels)")]
    [Tooltip("If enabled, forces a grid layout on the content.")]
    public bool useGridMode = false;
    public int gridColumns = 10;
    public Vector2 cellSize = new Vector2(50, 50);
    public Vector2 spacing = new Vector2(47, 49);
    public RectOffset gridPadding;

    [Header("Vertical List Layout (e.g. Achievements)")]
    [Tooltip("If enabled, forces a vertical list layout on the content.")]
    public bool useVerticalListMode = false;
    public float verticalSpacing = 10f;
    public RectOffset listPadding;
    public bool forceExpandWidth = true;

    private ScrollRect _scrollRect;
    private ContentSizeFitter _contentSizeFitter;
    private GridLayoutGroup _gridLayoutGroup;
    private VerticalLayoutGroup _verticalLayoutGroup;

    private void Awake()
    {
        if (gridPadding == null) gridPadding = new RectOffset(20, 20, 20, 20);
        if (listPadding == null) listPadding = new RectOffset(10, 10, 10, 10);
        InitializeComponents();
    }

    private void OnEnable()
    {
        RefreshLayout();
    }

    /// <summary>
    /// Finds or adds the necessary components to make the UI scrollable.
    /// </summary>
    [ContextMenu("Force Setup Components")]
    public void InitializeComponents()
    {
        // 1. Resolve target graphic for dragging
        // ScrollRect REQUIRES a graphic (Image) that receives raycasts to detect drags.
        Image bgImg = GetComponent<Image>();
        if (bgImg == null)
        {
            bgImg = gameObject.AddComponent<Image>();
            // Make it completely transparent so we don't block the background, 
            // but it STILL catches raycasts for scrolling.
            bgImg.color = new Color(1f, 1f, 1f, 0f);
        }
        bgImg.raycastTarget = true;

        // 2. Resolve ScrollRect on this object (the viewport)
        _scrollRect = GetComponent<ScrollRect>();
        if (_scrollRect == null)
        {
            _scrollRect = gameObject.AddComponent<ScrollRect>();
        }

        // 3. Resolve Content Transform
        if (content == null)
        {
            Transform found = transform.Find("Content");
            if (found != null)
            {
                content = found as RectTransform;
            }
        }

        if (content == null) return;

        // Ensure Content anchors and pivot are set correctly for top-down vertical scrolling
        content.anchorMin = new Vector2(0.5f, 1f);
        content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);

        // 4. Configure ScrollRect
        _scrollRect.content = content;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.viewport = GetComponent<RectTransform>();
        _scrollRect.movementType = ScrollRect.MovementType.Elastic;
        _scrollRect.inertia = true;
        _scrollRect.decelerationRate = 0.135f;
        _scrollRect.scrollSensitivity = 2.5f; // Adjusted sensitivity to 2.5f

        // 5. Resolve/Add ContentSizeFitter to the Content object
        _contentSizeFitter = content.GetComponent<ContentSizeFitter>();
        if (_contentSizeFitter == null)
        {
            _contentSizeFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        }
        _contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _contentSizeFitter.horizontalFit = useGridMode ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;

        // 6. Layout Configuration
        _gridLayoutGroup = content.GetComponent<GridLayoutGroup>();
        _verticalLayoutGroup = content.GetComponent<VerticalLayoutGroup>();

        if (useGridMode)
        {
            if (_verticalLayoutGroup != null) Destroy(_verticalLayoutGroup);
            if (_gridLayoutGroup == null) _gridLayoutGroup = content.gameObject.AddComponent<GridLayoutGroup>();

            _gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayoutGroup.constraintCount = gridColumns;
            _gridLayoutGroup.cellSize = cellSize;
            _gridLayoutGroup.spacing = spacing;
            _gridLayoutGroup.padding = gridPadding;
            _gridLayoutGroup.childAlignment = TextAnchor.UpperCenter;
            _gridLayoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
            _gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
        }
        else if (useVerticalListMode)
        {
            if (_gridLayoutGroup != null) Destroy(_gridLayoutGroup);
            if (_verticalLayoutGroup == null) _verticalLayoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();

            _verticalLayoutGroup.spacing = verticalSpacing;
            _verticalLayoutGroup.padding = listPadding;
            _verticalLayoutGroup.childForceExpandWidth = forceExpandWidth;
            _verticalLayoutGroup.childForceExpandHeight = false;
            _verticalLayoutGroup.childControlWidth = forceExpandWidth;
            _verticalLayoutGroup.childControlHeight = true;
            _verticalLayoutGroup.childAlignment = TextAnchor.UpperCenter;
        }

        // Add a RectMask2D to this object to clip elements that scroll out of view
        if (GetComponent<RectMask2D>() == null && GetComponent<Mask>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }
    }

    public void RefreshLayout()
    {
        if (content == null) return;

        // Force fully rebuild layouts this frame so position calculations are flawless
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        if (_scrollRect != null)
        {
            _scrollRect.verticalNormalizedPosition = 1f; // Always start scrolled to the very top
        }
    }
}
