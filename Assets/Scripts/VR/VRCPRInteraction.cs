using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VR-only CPR objective.
/// – Shows a green two-hand placeholder on the patient's chest.
/// – When both XR hands are near the placeholder, enters CPR mode.
/// – Tracks vertical compressions; validates depth + vertical dominance + BPM.
/// – Displays a floating Canvas HUD (two white panels) matching the CPR-simulator
///   reference: compression count, BPM number, depth bar (vertical, coloured
///   segments + moving indicator), and BPM rate bar (horizontal, rainbow + dot).
/// – After 15 good compressions notifies ScenarioGameLoop.NotifyCPRDone().
/// – Desktop CPR (CPRMinigame / CPR.cs) is completely untouched.
/// </summary>
public class VRCPRInteraction : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Enums
    // -------------------------------------------------------------------------
    private enum SessionState { WaitingForHands, ActiveCPR, Complete }
    private enum StrokeState  { AtTop, Down, Up }
    private enum BpmQuality   { Idle, Good, Acceptable, Bad }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------
    [Header("Hand Tracking")]
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [Tooltip("Point the hands must be near to enter CPR mode. Defaults to this transform.")]
    [SerializeField] private Transform compressionAnchor;
    [SerializeField] private float handPlacementRadius = 0.28f;
    [SerializeField] private float maxHandSeparation   = 0.48f;

    [Header("Compression Detection")]
    [SerializeField] private int   requiredGoodCompressions = 15;
    [Tooltip("Minimum vertical depth (metres) for a stroke to count.")]
    [SerializeField] private float compressionDepthMeters   = 0.05f;
    [SerializeField] private float recoilToleranceMeters    = 0.015f;
    [Tooltip("Minimum ratio of vertical vs lateral hand movement (0-1).")]
    [SerializeField] private float verticalDominanceMin     = 0.60f;
    [Tooltip("Seconds of hands-off before falling back to WaitingForHands.")]
    [SerializeField] private float resetIfHandsLostSeconds  = 0.8f;

    [Header("BPM Thresholds")]
    [SerializeField] private float idealBpmLow      = 100f;
    [SerializeField] private float idealBpmHigh     = 120f;
    [SerializeField] private float acceptableBpmLow  = 80f;
    [SerializeField] private float acceptableBpmHigh = 140f;

    [Header("Auto-Build")]
    [SerializeField] private bool buildFeedbackUI      = true;
    [SerializeField] private bool buildHandPlaceholder = true;

    [Header("Optional Inspector Overrides")]
    [SerializeField] private GameObject handPlaceholder;
    [SerializeField] private GameObject snappedHandsVisual;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------
    private ScenarioGameLoop loop;

    private SessionState session = SessionState.WaitingForHands;
    private StrokeState  stroke  = StrokeState.AtTop;

    private float topHeight, bottomHeight, currentDepth;
    private Vector3 strokeStartPos;
    private float   handsLostAt = -1f;

    private int  goodCompressions;
    private bool notifiedStarted;

    private const int BpmSamples = 4;
    private readonly Queue<float> compressionTimes = new Queue<float>(BpmSamples + 1);
    private float currentBpm;

    // -------------------------------------------------------------------------
    // Auto-built scene objects
    // -------------------------------------------------------------------------
    private Transform feedbackRoot;    // follows CPR_Target, holds Canvas
    private Transform placeholderRoot; // follows CPR_Target, holds hand shapes

    // UI runtime references
    private TextMeshProUGUI countText;
    private TextMeshProUGUI bpmText;
    private TextMeshProUGUI hintText;
    private RectTransform   depthIndicator;      // the moving white bar on depth chart
    private Image           depthIndicatorImage; // its colour changes per quality
    private RectTransform   bpmDot;              // the blue dot on the BPM bar
    private Image           bpmDotImage;

    // Bar half-extents in Canvas units
    private const float DepthBarHalfH = 62f;
    private const float BpmBarHalfW   = 58f;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private Vector3 CompressionAxis => transform.up.normalized;
    private Vector3 AnchorPos       => compressionAnchor != null
                                        ? compressionAnchor.position
                                        : transform.position;

    // =========================================================================
    // Unity lifecycle
    // =========================================================================
    private void Awake()
    {
        loop = FindFirstObjectByType<ScenarioGameLoop>();
        if (compressionAnchor == null) compressionAnchor = transform;

        if (buildFeedbackUI)      CreateFeedbackCanvas();
        if (buildHandPlaceholder && handPlaceholder == null) CreatePlaceholder();
    }

    private void OnEnable()
    {
        ResetSession();
        ShowPlaceholder(true);
        ShowSnappedHands(false);
        RefreshUI();
    }

    private void OnDisable()
    {
        ShowSnappedHands(false);
    }

    private void OnDestroy()
    {
        if (feedbackRoot    != null) Destroy(feedbackRoot.gameObject);
        if (placeholderRoot != null) Destroy(placeholderRoot.gameObject);
    }

    private void LateUpdate()
    {
        // Keep Canvas above chest, always facing the player, correct side forward
        if (feedbackRoot != null)
        {
            Vector3 above = transform.position + Vector3.up * 0.45f;
            feedbackRoot.position = above;

            Camera cam = Camera.main;
            if (cam != null)
            {
                // Panel must face the camera: +Z away from camera so the front (-Z) faces toward it
                Vector3 toCam = cam.transform.position - above;
                Vector3 flat  = new Vector3(toCam.x, 0f, toCam.z);
                if (flat.sqrMagnitude > 0.01f)
                    feedbackRoot.rotation = Quaternion.LookRotation(-flat.normalized);
            }
        }

        // Keep placeholder at chest surface
        if (placeholderRoot != null)
        {
            placeholderRoot.position = AnchorPos + CompressionAxis * 0.055f;
            placeholderRoot.rotation = Quaternion.LookRotation(transform.forward, CompressionAxis);
        }
    }

    private void Update()
    {
        if (session == SessionState.Complete) return;

        TryResolveHands();

        // Depth smoothly returns to 0 when not in down-stroke
        if (stroke != StrokeState.Down)
            currentDepth = Mathf.Lerp(currentDepth, 0f, Time.deltaTime * 10f);

        if (leftHand == null || rightHand == null)
        {
            RefreshUI();
            UpdateDepthVisual();
            return;
        }

        bool placed = AreHandsPlacedCorrectly();

        if (session == SessionState.WaitingForHands)
        {
            if (placed) EnterCprMode();
            RefreshUI();
            UpdateDepthVisual();
            return;
        }

        // session == ActiveCPR
        if (!placed)
        {
            HandleHandsLost();
            RefreshUI();
            UpdateDepthVisual();
            return;
        }
        handsLostAt = -1f;

        TickCompressionDetection();
        UpdateBpm();

        RefreshUI();
        UpdateDepthVisual();
        UpdateBpmDotVisual();
    }

    // =========================================================================
    // Compression detection
    // =========================================================================
    private void TickCompressionDetection()
    {
        float h    = GetAverageHandHeight();
        Vector3 avg = GetAverageHandPos();

        switch (stroke)
        {
            case StrokeState.AtTop:
                if (h > topHeight) topHeight = h;
                if (topHeight - h > recoilToleranceMeters)
                {
                    stroke       = StrokeState.Down;
                    bottomHeight = h;
                    strokeStartPos = avg;
                }
                break;

            case StrokeState.Down:
                if (h < bottomHeight) bottomHeight = h;
                currentDepth = Mathf.Max(0f, topHeight - h);
                if (h - bottomHeight > recoilToleranceMeters)
                {
                    EvaluateStroke(avg);
                    stroke = StrokeState.Up;
                    currentDepth = 0f;
                }
                break;

            case StrokeState.Up:
                if (h >= topHeight - recoilToleranceMeters)
                {
                    stroke    = StrokeState.AtTop;
                    topHeight = h;
                }
                break;
        }
    }

    private void EvaluateStroke(Vector3 endPos)
    {
        float depth = topHeight - bottomHeight;
        if (depth < compressionDepthMeters) return;

        // Vertical-dominance check
        Vector3 delta   = endPos - strokeStartPos;
        float   vert    = Mathf.Abs(Vector3.Dot(delta, CompressionAxis));
        float   lateral = Vector3.ProjectOnPlane(delta, CompressionAxis).magnitude;
        float   total   = vert + lateral;
        if (total > 0.0001f && vert / total < verticalDominanceMin) return;

        compressionTimes.Enqueue(Time.time);
        while (compressionTimes.Count > BpmSamples) compressionTimes.Dequeue();
        UpdateBpm();

        // Only count if BPM is within acceptable window
        if (ClassifyBpm(currentBpm) == BpmQuality.Bad) return;

        goodCompressions++;
        if (goodCompressions >= requiredGoodCompressions) Complete();
    }

    private void UpdateBpm()
    {
        if (compressionTimes.Count < 2) { currentBpm = 0f; return; }

        float earliest = float.MaxValue, latest = float.MinValue;
        foreach (float t in compressionTimes)
        {
            if (t < earliest) earliest = t;
            if (t > latest)   latest   = t;
        }
        float span = latest - earliest;
        if (span < 0.0001f) { currentBpm = 0f; return; }
        currentBpm = 60f / (span / (compressionTimes.Count - 1));
    }

    private BpmQuality ClassifyBpm(float bpm)
    {
        if (bpm <= 0f)                                         return BpmQuality.Idle;
        if (bpm >= idealBpmLow     && bpm <= idealBpmHigh)    return BpmQuality.Good;
        if (bpm >= acceptableBpmLow && bpm <= acceptableBpmHigh) return BpmQuality.Acceptable;
        return BpmQuality.Bad;
    }

    // =========================================================================
    // State transitions
    // =========================================================================
    private void EnterCprMode()
    {
        session        = SessionState.ActiveCPR;
        stroke         = StrokeState.AtTop;
        topHeight      = GetAverageHandHeight();
        bottomHeight   = topHeight;
        strokeStartPos = GetAverageHandPos();
        handsLostAt    = -1f;
        currentDepth   = 0f;

        ShowPlaceholder(false);
        ShowSnappedHands(true);
        NotifyStarted();
    }

    private void HandleHandsLost()
    {
        if (handsLostAt < 0f) handsLostAt = Time.time;
        if (Time.time - handsLostAt < resetIfHandsLostSeconds) return;

        session      = SessionState.WaitingForHands;
        stroke       = StrokeState.AtTop;
        currentDepth = 0f;
        ShowPlaceholder(true);
        ShowSnappedHands(false);
    }

    private void Complete()
    {
        session = SessionState.Complete;
        ShowPlaceholder(false);
        ShowSnappedHands(false);

        if (loop != null) loop.NotifyCPRDone();

        RefreshUI();
        UpdateDepthVisual();
        UpdateBpmDotVisual();
        enabled = false; // stop Update/LateUpdate; OnDestroy cleans up roots
    }

    private void NotifyStarted()
    {
        if (notifiedStarted || loop == null) return;
        notifiedStarted = true;
        loop.NotifyCPRStarted();
    }

    private void ResetSession()
    {
        session = SessionState.WaitingForHands;
        stroke  = StrokeState.AtTop;
        topHeight = bottomHeight = currentDepth = 0f;
        handsLostAt    = -1f;
        goodCompressions = 0;
        notifiedStarted  = false;
        compressionTimes.Clear();
        currentBpm = 0f;
    }

    // =========================================================================
    // Hand utilities
    // =========================================================================
    private bool AreHandsPlacedCorrectly()
    {
        if (Vector3.Distance(leftHand.position, rightHand.position) > maxHandSeparation)
            return false;
        Vector3 anchor = AnchorPos;
        return Vector3.Distance(leftHand.position,  anchor) <= handPlacementRadius
            && Vector3.Distance(rightHand.position, anchor) <= handPlacementRadius;
    }

    private float   GetAverageHandHeight() =>
        Vector3.Dot((leftHand.position + rightHand.position) * 0.5f, CompressionAxis);

    private Vector3 GetAverageHandPos() =>
        (leftHand.position + rightHand.position) * 0.5f;

    private void TryResolveHands()
    {
        if (leftHand  == null) leftHand  = FindHand("Left Hand",  "LeftHand",  "Left Direct Interactor");
        if (rightHand == null) rightHand = FindHand("Right Hand", "RightHand", "Right Direct Interactor");
    }

    private static Transform FindHand(params string[] names)
    {
        foreach (string n in names)
        {
            GameObject go = GameObject.Find(n);
            if (go != null) return go.transform;
        }
        return null;
    }

    private void ShowPlaceholder(bool show)
    {
        if (handPlaceholder  != null && handPlaceholder.activeSelf  != show) handPlaceholder.SetActive(show);
        if (placeholderRoot  != null && placeholderRoot.gameObject.activeSelf != show) placeholderRoot.gameObject.SetActive(show);
    }

    private void ShowSnappedHands(bool show)
    {
        if (snappedHandsVisual != null && snappedHandsVisual.activeSelf != show)
            snappedHandsVisual.SetActive(show);
    }

    // =========================================================================
    // UI refresh (text)
    // =========================================================================
    private void RefreshUI()
    {
        if (countText != null)
        {
            countText.text = session == SessionState.Complete
                ? $"{requiredGoodCompressions}\nCompressions"
                : $"{goodCompressions}\nCompressions";
        }

        if (bpmText != null)
        {
            if (session == SessionState.Complete)
                bpmText.text = "DONE";
            else
                bpmText.text = currentBpm > 0f
                    ? $"{Mathf.RoundToInt(currentBpm)} BPM"
                    : "-- BPM";
        }

        if (hintText != null)
        {
            if (session == SessionState.WaitingForHands || leftHand == null || rightHand == null)
                hintText.text = "Place both hands on the chest";
            else if (session == SessionState.ActiveCPR)
                hintText.text = ClassifyBpm(currentBpm) == BpmQuality.Bad
                    ? "Adjust rhythm  (~110 / min)"
                    : "Compress down & release fully";
            else
                hintText.text = "CPR objective complete!";
        }
    }

    // =========================================================================
    // UI visual updates (bars)
    // =========================================================================
    private void UpdateDepthVisual()
    {
        if (depthIndicator == null) return;

        // Map currentDepth → bar position
        // 0 = bottom (red zone), compressionDepthMeters = ~67% up (green zone)
        float norm = currentDepth / (compressionDepthMeters * 1.5f); // 1.0 at 1.5× target
        float y    = Mathf.Lerp(-DepthBarHalfH, DepthBarHalfH, Mathf.Clamp01(norm));
        depthIndicator.anchoredPosition = new Vector2(0f, y);

        if (depthIndicatorImage != null)
        {
            float depthNorm = currentDepth / compressionDepthMeters;
            Color c = depthNorm >= 0.75f
                ? new Color(0.2f, 0.9f, 0.3f)   // green – good depth
                : depthNorm >= 0.35f
                    ? new Color(0.95f, 0.85f, 0.1f) // yellow – getting there
                    : new Color(0.9f, 0.25f, 0.25f); // red – too shallow
            depthIndicatorImage.color = c;
        }
    }

    private void UpdateBpmDotVisual()
    {
        if (bpmDot == null) return;

        if (currentBpm <= 0f)
        {
            if (bpmDotImage != null) bpmDotImage.color = Color.clear;
            return;
        }

        if (bpmDotImage != null) bpmDotImage.color = new Color(0.2f, 0.45f, 1f);

        float t = Mathf.Clamp01((currentBpm - acceptableBpmLow)
                                  / (acceptableBpmHigh - acceptableBpmLow));
        bpmDot.anchoredPosition = new Vector2(Mathf.Lerp(-BpmBarHalfW, BpmBarHalfW, t), 0f);
    }

    // =========================================================================
    // Canvas / UI construction
    // =========================================================================
    private void CreateFeedbackCanvas()
    {
        // Scene-level root – NOT parented to CPR_Target (avoids its non-uniform scale)
        feedbackRoot          = new GameObject("VR CPR Feedback Root").transform;
        feedbackRoot.position = transform.position + Vector3.up * 0.45f;
        feedbackRoot.rotation = Quaternion.identity;

        // World-space Canvas
        GameObject canvasGO = new GameObject("VR CPR Canvas");
        canvasGO.transform.SetParent(feedbackRoot, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform cr = canvasGO.GetComponent<RectTransform>();
        cr.sizeDelta    = new Vector2(420f, 260f);
        cr.localScale   = Vector3.one * 0.001f; // 420 × 0.001 = 0.42 m wide
        cr.localPosition = Vector3.zero;
        cr.localRotation = Quaternion.identity;

        // ── Panel 1: depth bar (left) + compression count ──────────────────
        RectTransform p1 = MakePanel(cr, "Panel1",
            pos: new Vector2(-105f, 15f), size: new Vector2(190f, 230f),
            bg: new Color(1f, 1f, 1f, 0.93f));

        BuildDepthBar(p1);

        countText = MakeTMP(p1, "Count",
            pos: new Vector2(45f, 0f), size: new Vector2(95f, 110f),
            text: "0\nCompressions", fontSize: 15f, color: Color.black);
        countText.fontStyle = FontStyles.Bold;

        // ── Panel 2: BPM number + rate bar (right) ──────────────────────────
        RectTransform p2 = MakePanel(cr, "Panel2",
            pos: new Vector2(112f, -48f), size: new Vector2(175f, 118f),
            bg: new Color(1f, 1f, 1f, 0.93f));

        bpmText = MakeTMP(p2, "BPM",
            pos: new Vector2(0f, 30f), size: new Vector2(160f, 52f),
            text: "-- BPM", fontSize: 20f, color: Color.black);
        bpmText.fontStyle = FontStyles.Bold;

        BuildBpmBar(p2);

        // ── Hint text below both panels ──────────────────────────────────────
        hintText = MakeTMP(cr, "Hint",
            pos: new Vector2(0f, -118f), size: new Vector2(390f, 32f),
            text: "Place both hands on the chest", fontSize: 9f,
            color: new Color(0.95f, 0.95f, 0.95f));
    }

    // ── Depth bar: vertical coloured segments + moving indicator ──────────────
    private void BuildDepthBar(RectTransform parent)
    {
        // Dark background container
        RectTransform bg = MakeImage(parent, "DepthBG",
            pos: new Vector2(-65f, 12f), size: new Vector2(36f, 138f),
            color: new Color(0.12f, 0.12f, 0.12f));

        // Stacked colour segments from bottom (red=shallow) to top (red=too deep)
        // Heights must sum to ≈ 138
        (Color col, float h)[] segs =
        {
            (new Color(0.85f, 0.15f, 0.15f), 18f), // bottom red
            (new Color(1.00f, 0.50f, 0.00f), 18f), // orange
            (new Color(0.95f, 0.88f, 0.10f), 22f), // yellow
            (new Color(0.20f, 0.85f, 0.25f), 32f), // GREEN – target zone
            (new Color(0.95f, 0.88f, 0.10f), 20f), // yellow
            (new Color(1.00f, 0.50f, 0.00f), 16f), // orange
            (new Color(0.85f, 0.15f, 0.15f), 12f), // top red
        };

        float y = -69f; // start at bottom of 138-tall rect
        foreach (var (col, h) in segs)
        {
            MakeImage(bg, "Seg", pos: new Vector2(0f, y + h * 0.5f),
                      size: new Vector2(36f, h), color: col);
            y += h;
        }

        // White indicator line that moves up/down
        RectTransform ind = MakeImage(bg, "DepthIndicator",
            pos: new Vector2(0f, -DepthBarHalfH),
            size: new Vector2(48f, 6f),
            color: Color.white);
        depthIndicator      = ind;
        depthIndicatorImage = ind.GetComponent<Image>();
    }

    // ── BPM bar: horizontal rainbow segments + sliding blue dot ───────────────
    private void BuildBpmBar(RectTransform parent)
    {
        RectTransform bg = MakeImage(parent, "BpmBarBG",
            pos: new Vector2(0f, -26f), size: new Vector2(148f, 30f),
            color: new Color(0.12f, 0.12f, 0.12f));

        Color[] cols =
        {
            new Color(0.85f, 0.15f, 0.15f), // red  (too slow)
            new Color(1.00f, 0.50f, 0.00f), // orange
            new Color(0.95f, 0.88f, 0.10f), // yellow
            new Color(0.20f, 0.85f, 0.25f), // green (ideal)
            new Color(0.95f, 0.88f, 0.10f), // yellow
            new Color(1.00f, 0.50f, 0.00f), // orange
            new Color(0.85f, 0.15f, 0.15f), // red  (too fast)
        };

        float segW = 148f / cols.Length;
        for (int i = 0; i < cols.Length; i++)
        {
            float x = -74f + segW * i + segW * 0.5f;
            MakeImage(bg, $"BpmSeg{i}",
                pos: new Vector2(x, 0f), size: new Vector2(segW, 28f),
                color: cols[i]);
        }

        // Sliding blue dot indicator
        RectTransform dot = MakeImage(bg, "BpmDot",
            pos: Vector2.zero, size: new Vector2(14f, 36f),
            color: new Color(0.2f, 0.45f, 1f));
        bpmDot      = dot;
        bpmDotImage = dot.GetComponent<Image>();
        if (bpmDotImage != null) bpmDotImage.color = Color.clear; // hidden until first BPM
    }

    // ── UGUI helpers ──────────────────────────────────────────────────────────
    private static RectTransform MakePanel(RectTransform parent, string name,
        Vector2 pos, Vector2 size, Color bg)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = bg;
        return SetRect(go, pos, size);
    }

    private static RectTransform MakeImage(RectTransform parent, string name,
        Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        return SetRect(go, pos, size);
    }

    private static TextMeshProUGUI MakeTMP(RectTransform parent, string name,
        Vector2 pos, Vector2 size, string text, float fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.fontSize         = fontSize;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.color            = color;
        tmp.enableWordWrapping = true;
        SetRect(go, pos, size);
        return tmp;
    }

    private static RectTransform SetRect(GameObject go, Vector2 pos, Vector2 size)
    {
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin      = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot          = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta      = size;
        return r;
    }

    // =========================================================================
    // CPR hand placeholder — real skeletal hand models in CPR grip pose
    // =========================================================================

    // Rotation offsets for CPR palm-down pose.
    // If hands appear sideways in your build, tweak these two fields in the Inspector.
    [Header("CPR Hand Placeholder Pose (tune if needed)")]
    [SerializeField] private Vector3 bottomHandEuler = new Vector3(270f, 0f, 180f);
    [SerializeField] private Vector3 topHandEuler    = new Vector3(270f, 0f, 180f);
    [SerializeField] private float guideHandScale = 0.36f;

    private void CreatePlaceholder()
    {
        placeholderRoot          = new GameObject("CPR Hand Placeholder Root").transform;
        placeholderRoot.position = AnchorPos + CompressionAxis * 0.055f;
        placeholderRoot.rotation = Quaternion.LookRotation(transform.forward, CompressionAxis);

        // Use the real skeletal hand models. Do not show primitive placeholders.
        GameObject leftPrefab  = ResolveGuideHandModel(true);
        GameObject rightPrefab = ResolveGuideHandModel(false);

        if (leftPrefab != null && rightPrefab != null)
        {
            SpawnRealHands(leftPrefab, rightPrefab);
        }
        else
        {
            Debug.LogError("[VRCPRInteraction] Hand model prefabs not found. Green CPR guide disabled instead of showing primitive fallback.");
        }
    }

    private static GameObject ResolveGuideHandModel(bool leftSide)
    {
        string resourceName = leftSide ? "Left Hand Model" : "Right Hand Model";
        GameObject prefab = Resources.Load<GameObject>(resourceName);
        if (prefab != null)
            return prefab;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t == null || t.name.Contains("CPR"))
                continue;

            if (t.name == resourceName || t.name.Contains(resourceName))
                return t.gameObject;
        }

        return null;
    }

    // Instantiates both hand models stacked in CPR grip (bottom = left, top = right).
    private void SpawnRealHands(GameObject leftPrefab, GameObject rightPrefab)
    {
        Color green = new Color(0.1f, 1f, 0.2f, 0.7f);
        Material greenMaterial = CreateGuideHandMaterial(green);
        float handScale = Mathf.Max(0.01f, guideHandScale);
        // Force the cloned glove models palm-down. This runtime value ignores stale
        // scene Inspector rotations that previously left both guide palms facing up.
        Vector3 palmDownEuler = new Vector3(270f, 0f, 180f);

        // ── Bottom hand (left) ──────────────────────────────────────────────
        GameObject bottom = Instantiate(leftPrefab, placeholderRoot);
        bottom.name = "CPR Bottom Hand";
        bottom.transform.localPosition = new Vector3(0f, 0.014f, 0f);
        bottom.transform.localEulerAngles = palmDownEuler;
        bottom.transform.localScale = Vector3.one * handScale;
        PrepareGuideHandClone(bottom);
        ApplyGreenMaterial(bottom, greenMaterial);
        SetHandGrip(bottom, 0f, 0f); // open palm resting on chest

        // ── Top hand (right) on top of left, interlocked ────────────────────
        GameObject top = Instantiate(rightPrefab, placeholderRoot);
        top.name = "CPR Top Hand";
        top.transform.localPosition    = new Vector3(0f, 0.04f, 0f);
        top.transform.localEulerAngles = palmDownEuler;
        top.transform.localScale = Vector3.one * handScale;
        PrepareGuideHandClone(top);
        ApplyGreenMaterial(top, greenMaterial);
        SetHandGrip(top, 1f, 1f); // fist/grip on top of lower hand
    }

    private static void PrepareGuideHandClone(GameObject hand)
    {
        foreach (MonoBehaviour behaviour in hand.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;

        foreach (Collider c in hand.GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        foreach (Rigidbody rb in hand.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);
    }

    private static Material CreateGuideHandMaterial(Color green)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
        {
            Debug.LogError("[VRCPRInteraction] Could not find a runtime shader for the green CPR hand guide.");
            return null;
        }

        Material material = new Material(shader);
        material.name = "CPR Guide Green Hand Material";

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", green);
        if (material.HasProperty("_Color")) material.SetColor("_Color", green);

        if (green.a < 0.99f)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        return material;
    }

    // Replace cloned glove materials completely so broken prefab shaders cannot render pink.
    private static void ApplyGreenMaterial(GameObject hand, Material greenMaterial)
    {
        if (greenMaterial == null)
            return;

        foreach (Renderer r in hand.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = greenMaterial;
            r.sharedMaterials = materials;
        }
    }

    // Tints every renderer on the hand green using MaterialPropertyBlock,
    // preserving the original material's normals, specular, and shading.
    private static void ApplyGreenMaterial(GameObject hand, Color green)
    {
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        foreach (Renderer r in hand.GetComponentsInChildren<Renderer>(true))
        {
            r.GetPropertyBlock(mpb);
            // _BaseColor = URP, _Color = Standard — try both
            mpb.SetColor("_BaseColor", green);
            mpb.SetColor("_Color",     green);
            r.SetPropertyBlock(mpb);
        }
    }

    // Drives the hand animator into open-palm pose (grip=0 = flat hand).
    private static void SetHandGrip(GameObject hand, float grip, float trigger)
    {
        foreach (Animator anim in hand.GetComponentsInChildren<Animator>(true))
        {
            if (anim == null) continue;
            anim.enabled = true;
            anim.SetFloat("Grip", grip);
            anim.SetFloat("Trigger", trigger);
            anim.Update(0f);
            anim.enabled = false;
        }
    }

    // Simple disc fallback used only if prefabs are missing from Resources.
    private void SpawnPrimitiveFallback()
    {
        Color green = new Color(0.15f, 1f, 0.35f, 0.80f);
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) return;
        Material mat = new Material(sh);
        mat.color = green;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", green);

        for (int i = 0; i < 2; i++)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            disc.name = i == 0 ? "FallbackHandL" : "FallbackHandR";
            Object.Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(placeholderRoot, false);
            disc.transform.localPosition = new Vector3(0f, i * 0.03f, 0f);
            disc.transform.localScale    = new Vector3(0.12f, 0.01f, 0.10f);
            disc.GetComponent<Renderer>().material = mat;
        }
    }
}
