using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CPRMinigame : MonoBehaviour
{
    // Fired ONLY when the player actually starts compressions (first SPACE to begin)
    public event Action OnCPRStarted;
    public event Action<bool> OnCPRClosed; // completed? true/false


    [Header("Disable while CPR runs")]
    [SerializeField] private MonoBehaviour[] disableWhileRunning; // drag PlayerInteractor + movement here
    [SerializeField] private GameObject[] hideWhileRunning;       // drag your prompt UI object here (if you have one)
    [SerializeField] private AudioSource metronome;

    [Header("UI")]
    [SerializeField] private GameObject rootPanel;         // set to this panel object
    [SerializeField] private TextMeshProUGUI feedbackText; // instructions + feedback
    [SerializeField] private TextMeshProUGUI progressText; // progress
    [SerializeField] private Image beatMeterFill;          // optional, can be null

    [Header("Controls")]
    [SerializeField] private KeyCode pressKey = KeyCode.Space;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

    [Header("Timing")]
    [Tooltip("Target interval between compressions in seconds. 0.5s = 120/min")]
    [SerializeField] private float targetInterval = 0.5f;
    [Tooltip("How much deviation counts as a 'Good' press (seconds)")]
    [SerializeField] private float goodWindow = 0.12f;

    [Header("Win condition")]
    [SerializeField] private int requiredGoodPresses = 15;
    [SerializeField] private int maxPresses = 25;

    private bool running;
    private bool waitingToStart;
    private bool hasStartedCompressions; // becomes true only after the player begins

    private float lastPressTime;
    private int goodPresses;
    private int totalPresses;
    private Action onComplete;

    private void Awake()
    {
        if (!rootPanel) rootPanel = gameObject;
        rootPanel.SetActive(false);
    }

    public void StartCPR(Action onCompleted)
    {
        onComplete = onCompleted;
        running = true;

        goodPresses = 0;
        totalPresses = 0;

        waitingToStart = true;
        hasStartedCompressions = false;
        lastPressTime = 0f; // not used until we begin

        rootPanel.SetActive(true);
        SetRunningState(true);

        SetFeedback(
            "CPR Instructions:\n" +
            "- Press SPACE in steady rhythm\n" +
            "- Target ~120/min (every 0.5s)\n" +
            $"- Need {requiredGoodPresses} GOOD compressions\n\n" +
            "Press SPACE to begin.",
            true
        );

        UpdateProgress();

        // Keep meter idle while reading
        if (beatMeterFill)
        {
            beatMeterFill.fillAmount = 0f;
            beatMeterFill.color = Color.white;
        }
    }

    private void Update()
    {
        if (!running) return;

        // Waiting state: player reads instructions, nothing runs yet.
        if (waitingToStart)
        {
            if (Input.GetKeyDown(cancelKey))
            {
                StopCPR("CPR cancelled");
                return;
            }

            if (Input.GetKeyDown(pressKey))
            {
                waitingToStart = false;
                hasStartedCompressions = true;

                // ✅ CPR "started" is defined HERE (not when opening panel)
                OnCPRStarted?.Invoke();

                // Start the timing reference now
                lastPressTime = Time.time;

                SetFeedback("Start compressions — follow the beat.", true);
                UpdateProgress();
            }

            return;
        }

        // Metronome aligned to the same phase as the beat meter (important fix)
        if (metronome)
        {
            float phase = Mathf.Repeat(Time.time - lastPressTime, targetInterval);
            if (phase < 0.02f) metronome.Play();
        }

        if (Input.GetKeyDown(cancelKey))
        {
            StopCPR("CPR cancelled");
            return;
        }

        // Beat meter aligned with goodWindow (important fix)
        if (beatMeterFill)
        {
            // normalized phase 0..1 within interval
            float t = Mathf.Repeat(Time.time - lastPressTime, targetInterval) / targetInterval;

            // show countdown to "press moment" (0 means press now)
            beatMeterFill.fillAmount = 1f - t;

            // green zone corresponds exactly to +/- goodWindow around press moment
            float windowNorm = Mathf.Clamp01(goodWindow / targetInterval);

            // press moment is at end of interval (t=1) which wraps to t=0
            float distToPressMoment = Mathf.Min(Mathf.Abs(t - 1f), Mathf.Abs(t - 0f));

            beatMeterFill.color = (distToPressMoment <= windowNorm) ? Color.green : Color.white;
        }

        if (Input.GetKeyDown(pressKey))
        {
            HandlePress();
        }
    }

    private void HandlePress()
    {
        float now = Time.time;
        float dt = now - lastPressTime;

        totalPresses++;
        lastPressTime = now;

        float error = Mathf.Abs(dt - targetInterval);

        if (error <= goodWindow)
        {
            goodPresses++;
            SetFeedback("Good compression ✓", true);
        }
        else if (dt < targetInterval)
        {
            SetFeedback("Too fast — allow full recoil", false);
        }
        else
        {
            SetFeedback("Too slow — maintain 100–120/min", false);
        }

        UpdateProgress();

        if (goodPresses >= requiredGoodPresses)
        {
            Complete();
        }
        else if (totalPresses >= maxPresses)
        {
            StopCPR("Try again (not enough good compressions)");
        }
    }

    private void SetRunningState(bool isRunning)
    {
        if (disableWhileRunning != null)
        {
            foreach (var m in disableWhileRunning)
                if (m) m.enabled = !isRunning;
        }

        if (hideWhileRunning != null)
        {
            foreach (var go in hideWhileRunning)
                if (go) go.SetActive(!isRunning);
        }
    }

    private void UpdateProgress()
    {
        if (progressText)
            progressText.text =
                $"Good compressions: {goodPresses}/{requiredGoodPresses}\n" +
                $"Attempts used: {totalPresses}/{maxPresses}";
    }

    private void SetFeedback(string msg, bool positive)
    {
        if (!feedbackText) return;
        feedbackText.text = msg;
        // keep color simple for now
    }

    private void Complete()
    {
       running = false;
    SetRunningState(false);
    rootPanel.SetActive(false);

    OnCPRClosed?.Invoke(true);   
    onComplete?.Invoke();
    }

   private void StopCPR(string reason)
{
    running = false;
    SetRunningState(false);
    rootPanel.SetActive(false);
    Debug.Log(reason);

    OnCPRClosed?.Invoke(false); 
}
}
