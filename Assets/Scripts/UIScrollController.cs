// ============================================================
// UIScrollController.cs
// Purpose: Automatically configures a ScrollRect and Layout Groups
//          at runtime to enable scrolling for any UI panel.
//
// Unity Setup:
//   - Attach this script to the "Scroll" or "Scroll Menu" GameObject.
//   - For Level Selection: Enable 'Use Grid Mode'.
//   - For Achievements/Lists: Enable 'Use Vertical List Mode'.
//   - Make sure a child named "Content" (or assign it manually) exists.
// ============================================================

using System.Collections;
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
    [Tooltip("If enabled, VerticalLayoutGroup controls (overrides) each row's height via LayoutElement. Disable this for pre-placed rows with fixed heights.")]
    public bool controlChildHeight = false;

    [Header("Scroll Settings")]
    [Tooltip("Mouse wheel / touch scroll sensitivity.")]
    public float scrollSensitivity = 30f;
    public float decelerationRate = 0.135f;

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
        StartCoroutine(DelayedRefresh());
    }

    // Wait one frame so Unity can calculate preferred sizes before we rebuild.
    private IEnumerator DelayedRefresh()
    {
        yield return null;
        RefreshLayout();
    }

    /// <summary>
    /// Finds or adds the necessary components to make the UI scrollable.
    /// </summary>
    [ContextMenu("Force Setup Components")]
    public void InitializeComponents()
    {
        // -------------------------------------------------------
        // 1. Ensure a RectMask2D clips content that scrolls out.
        //    Must be added BEFORE ScrollRect so events route correctly.
        // -------------------------------------------------------
        if (GetComponent<RectMask2D>() == null && GetComponent<Mask>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }

        // -------------------------------------------------------
        // 2. Ensure we have a transparent Image so the ScrollRect
        //    viewport can receive pointer / drag events.
        // -------------------------------------------------------
        Image bgImg = GetComponent<Image>();
        if (bgImg == null)
        {
            bgImg = gameObject.AddComponent<Image>();
        }
        bgImg.color = new Color(0f, 0f, 0f, 0f); // fully transparent
        bgImg.raycastTarget = true;               // MUST catch pointer events

        // -------------------------------------------------------
        // 3. ScrollRect on this object (acts as the viewport too)
        // -------------------------------------------------------
        _scrollRect = GetComponent<ScrollRect>();
        if (_scrollRect == null)
        {
            _scrollRect = gameObject.AddComponent<ScrollRect>();
        }

        // -------------------------------------------------------
        // 4. Resolve Content child
        // -------------------------------------------------------
        if (content == null)
        {
            Transform found = transform.Find("Content");
            if (found != null)
                content = found as RectTransform;
        }

        if (content == null)
        {
            Debug.LogWarning("[UIScrollController] No 'Content' child found. Assign it manually in the Inspector.", this);
            return;
        }

        // -------------------------------------------------------
        // 5. Content anchoring
        //    Vertical mode  → stretch full width, anchor to top.
        //    Grid mode      → keep a centered top anchor (horizontal scroll can vary).
        // -------------------------------------------------------
        if (useVerticalListMode && !useGridMode)
        {
            // Full horizontal stretch anchored to top.
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot     = new Vector2(0.5f, 1f);
            // Zero out the horizontal offset so it stretches exactly.
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);
        }
        else
        {
            // Grid / custom mode — top-center point anchor.
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot     = new Vector2(0.5f, 1f);
        }

        // Always start content at the top-left of the viewport.
        content.anchoredPosition = Vector2.zero;

        // -------------------------------------------------------
        // 6. Configure ScrollRect
        // -------------------------------------------------------
        _scrollRect.content          = content;
        _scrollRect.viewport         = GetComponent<RectTransform>(); // self is viewport
        _scrollRect.horizontal       = useGridMode;   // horizontal only for grids
        _scrollRect.vertical         = true;
        _scrollRect.movementType     = ScrollRect.MovementType.Elastic;
        _scrollRect.elasticity       = 0.1f;
        _scrollRect.inertia          = true;
        _scrollRect.decelerationRate = decelerationRate;
        _scrollRect.scrollSensitivity = scrollSensitivity;

        // -------------------------------------------------------
        // 7. ContentSizeFitter on Content — lets the layout group
        //    drive the height automatically.
        // -------------------------------------------------------
        _contentSizeFitter = content.GetComponent<ContentSizeFitter>();
        if (_contentSizeFitter == null)
            _contentSizeFitter = content.gameObject.AddComponent<ContentSizeFitter>();

        _contentSizeFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        _contentSizeFitter.horizontalFit = useGridMode
            ? ContentSizeFitter.FitMode.PreferredSize
            : ContentSizeFitter.FitMode.Unconstrained;

        // -------------------------------------------------------
        // 8. Layout group on Content
        // -------------------------------------------------------
        _gridLayoutGroup    = content.GetComponent<GridLayoutGroup>();
        _verticalLayoutGroup = content.GetComponent<VerticalLayoutGroup>();

        if (useGridMode)
        {
            if (_verticalLayoutGroup != null) DestroyImmediate(_verticalLayoutGroup);
            if (_gridLayoutGroup == null)
                _gridLayoutGroup = content.gameObject.AddComponent<GridLayoutGroup>();

            _gridLayoutGroup.constraint        = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayoutGroup.constraintCount   = gridColumns;
            _gridLayoutGroup.cellSize          = cellSize;
            _gridLayoutGroup.spacing           = spacing;
            _gridLayoutGroup.padding           = gridPadding;
            _gridLayoutGroup.childAlignment    = TextAnchor.UpperCenter;
            _gridLayoutGroup.startCorner       = GridLayoutGroup.Corner.UpperLeft;
            _gridLayoutGroup.startAxis         = GridLayoutGroup.Axis.Horizontal;
        }
        else if (useVerticalListMode)
        {
            if (_gridLayoutGroup != null) DestroyImmediate(_gridLayoutGroup);
            if (_verticalLayoutGroup == null)
                _verticalLayoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();

            _verticalLayoutGroup.spacing              = verticalSpacing;
            _verticalLayoutGroup.padding              = listPadding;
            _verticalLayoutGroup.childForceExpandWidth  = forceExpandWidth;
            _verticalLayoutGroup.childForceExpandHeight = false;
            _verticalLayoutGroup.childControlWidth      = forceExpandWidth;
            // controlChildHeight = false → respect each row's existing RectTransform height.
            // Set to true ONLY if rows have LayoutElement components with explicit preferredHeight.
            _verticalLayoutGroup.childControlHeight     = controlChildHeight;
            _verticalLayoutGroup.childAlignment         = TextAnchor.UpperCenter;
        }
    }

    public void RefreshLayout()
    {
        if (content == null) return;

        // Force layout to recalculate so ContentSizeFitter sets the correct height.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();

        if (_scrollRect != null)
        {
            _scrollRect.verticalNormalizedPosition = 1f; // scroll to top
        }
    }
}
