using TMPro;
using UnityEngine;

public class VRCPRInteraction : MonoBehaviour
{
    private enum CompressionState
    {
        WaitingForHands,
        ReadyAtTop,
        Downstroke,
        WaitingForRecoil,
        Complete
    }

    [Header("Hand Tracking")]
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private float handPlacementTolerance = 0.18f;
    [SerializeField] private float maxHandSeparation = 0.42f;

    [Header("Compression")]
    [SerializeField] private int requiredGoodCompressions = 15;
    [SerializeField] private float compressionDepthMeters = 0.05f;
    [SerializeField] private float fullRecoilToleranceMeters = 0.014f;
    [SerializeField] private float minimumDownVelocity = 0.12f;
    [SerializeField] private float resetIfHandsLostSeconds = 0.7f;

    [Header("Feedback")]
    [SerializeField] private TextMeshPro feedbackText;
    [SerializeField] private bool createWorldFeedback = true;

    private ScenarioGameLoop loop;
    private Collider chestCollider;
    private CompressionState state = CompressionState.WaitingForHands;

    private float topHandHeight;
    private float previousHandHeight;
    private float deepestDepth;
    private float handsLostAt = -1f;
    private int goodCompressions;
    private bool notifiedStarted;

    private Vector3 CompressionAxis => transform.up.normalized;

    private void Awake()
    {
        loop = FindFirstObjectByType<ScenarioGameLoop>();
        chestCollider = GetComponent<Collider>();

        if (createWorldFeedback && feedbackText == null)
            feedbackText = CreateFeedbackText();
    }

    private void OnEnable()
    {
        ResetSession();
    }

    private void Update()
    {
        if (state == CompressionState.Complete)
            return;

        TryResolveHands();

        if (leftHand == null || rightHand == null || chestCollider == null)
        {
            SetFeedback("VR CPR: place both hands on the patient's chest.");
            return;
        }

        bool handsPlaced = AreHandsPlacedCorrectly();
        if (!handsPlaced)
        {
            HandleHandsLost();
            return;
        }

        handsLostAt = -1f;

        float handHeight = GetAverageHandHeight();
        float velocity = (handHeight - previousHandHeight) / Mathf.Max(Time.deltaTime, 0.0001f);
        previousHandHeight = handHeight;

        if (state == CompressionState.WaitingForHands)
        {
            topHandHeight = handHeight;
            deepestDepth = 0f;
            state = CompressionState.ReadyAtTop;
            SetFeedback($"Both hands on chest. Compress down and fully release.\nGood compressions: {goodCompressions}/{requiredGoodCompressions}");
            NotifyStarted();
            return;
        }

        if (state == CompressionState.ReadyAtTop)
        {
            topHandHeight = Mathf.Max(topHandHeight, handHeight);
        }

        float depth = Mathf.Max(0f, topHandHeight - handHeight);
        deepestDepth = Mathf.Max(deepestDepth, depth);

        switch (state)
        {
            case CompressionState.ReadyAtTop:
                bool movingDown = velocity < -minimumDownVelocity || depth >= compressionDepthMeters * 0.35f;
                if (depth > fullRecoilToleranceMeters && movingDown)
                {
                    state = CompressionState.Downstroke;
                    SetFeedback($"Compressing...\nDepth: {depth * 100f:0} cm");
                }
                break;

            case CompressionState.Downstroke:
                SetFeedback($"Compressing...\nDepth: {depth * 100f:0} cm");
                if (depth >= compressionDepthMeters)
                    state = CompressionState.WaitingForRecoil;
                break;

            case CompressionState.WaitingForRecoil:
                if (depth <= fullRecoilToleranceMeters)
                {
                    RegisterCompression();
                    state = CompressionState.ReadyAtTop;
                    topHandHeight = handHeight;
                    deepestDepth = 0f;
                }
                else
                {
                    SetFeedback($"Release fully before the next compression.\nDepth: {depth * 100f:0} cm");
                }
                break;
        }
    }

    private void RegisterCompression()
    {
        if (deepestDepth < compressionDepthMeters)
        {
            SetFeedback("Too shallow. Push about 5 cm and let the chest recoil.");
            return;
        }

        goodCompressions++;
        SetFeedback($"Good compression.\nGood compressions: {goodCompressions}/{requiredGoodCompressions}");

        if (goodCompressions >= requiredGoodCompressions)
            Complete();
    }

    private void Complete()
    {
        state = CompressionState.Complete;
        SetFeedback("CPR objective complete.");

        if (loop != null)
            loop.NotifyCPRDone();

        CPR desktopInteractable = GetComponent<CPR>();
        if (desktopInteractable != null)
            desktopInteractable.enabled = false;
    }

    private void NotifyStarted()
    {
        if (notifiedStarted)
            return;

        notifiedStarted = true;

        if (loop != null)
            loop.NotifyCPRStarted();
    }

    private void HandleHandsLost()
    {
        if (handsLostAt < 0f)
            handsLostAt = Time.time;

        if (Time.time - handsLostAt >= resetIfHandsLostSeconds)
        {
            state = CompressionState.WaitingForHands;
            deepestDepth = 0f;
            SetFeedback($"Use both hands together over the chest.\nGood compressions: {goodCompressions}/{requiredGoodCompressions}");
        }
    }

    private bool AreHandsPlacedCorrectly()
    {
        if (Vector3.Distance(leftHand.position, rightHand.position) > maxHandSeparation)
            return false;

        return IsHandOverChest(leftHand.position) && IsHandOverChest(rightHand.position);
    }

    private bool IsHandOverChest(Vector3 handPosition)
    {
        Vector3 closest = chestCollider.ClosestPoint(handPosition);
        return Vector3.Distance(closest, handPosition) <= handPlacementTolerance;
    }

    private float GetAverageHandHeight()
    {
        Vector3 average = (leftHand.position + rightHand.position) * 0.5f;
        return Vector3.Dot(average, CompressionAxis);
    }

    private void ResetSession()
    {
        state = CompressionState.WaitingForHands;
        topHandHeight = 0f;
        previousHandHeight = 0f;
        deepestDepth = 0f;
        handsLostAt = -1f;
        goodCompressions = 0;
        notifiedStarted = false;
    }

    private void TryResolveHands()
    {
        if (leftHand == null)
            leftHand = FindHandTransform("Left Hand", "Left Direct Interactor", "XR Controller Left");

        if (rightHand == null)
            rightHand = FindHandTransform("Right Hand", "Right Direct Interactor", "XR Controller Right");
    }

    private static Transform FindHandTransform(params string[] names)
    {
        foreach (string name in names)
        {
            GameObject go = GameObject.Find(name);
            if (go != null)
                return go.transform;
        }

        return null;
    }

    private TextMeshPro CreateFeedbackText()
    {
        GameObject textObject = new GameObject("VR CPR Feedback");
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = new Vector3(0f, 0.42f, 0.05f);
        textObject.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
        textObject.transform.localScale = Vector3.one * 0.035f;

        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 2.6f;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.rectTransform.sizeDelta = new Vector2(8f, 3f);
        return text;
    }

    private void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }
}
