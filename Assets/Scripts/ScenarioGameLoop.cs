using UnityEngine;
using TMPro;

public class ScenarioGameLoop : MonoBehaviour
{
    [Header("Objective Targets")]
    [SerializeField] private int conesRequired = 4;
    private bool pausedForCPR;
    [SerializeField] private int bandagesRequired = 3;
    [SerializeField] private ObjectiveUI objectiveUI;

    public int ConesRequired => conesRequired;
    public int BandagesRequired => bandagesRequired;

    public int ConesPlaced => conesPlaced;
    public int BandagesApplied => bandagesApplied;
    public bool CprDone => cprDone;

    [Header("Failure & Patient State")]
    [SerializeField] private GameObject failPanel;
    [SerializeField] private TMP_Text timerText;          // gameplay HUD: bleed timer
    [SerializeField] private CPRMinigame cprMinigame;      // to detect CPR start

    [Header("Bleed-out")]
    [SerializeField] private float bleedOutSeconds = 120f;
    [Tooltip("How many bandages stop bleeding? Usually bandagesRequired.")]
    [SerializeField] private int bandagesToStopBleeding = 3;

    [Header("CPR must start by")]
    [SerializeField] private float cprMustStartBySeconds = 45f;

    [Header("Unsafe scene penalty (cones)")]
    [Tooltip("At this time mark, if cones are not finished, apply a penalty once.")]
    [SerializeField] private float safetyCheckAtSeconds = 20f;
    [SerializeField] private float unsafePenaltySeconds = 20f;

    [Header("References")]
    [SerializeField] private ConePlacer conePlacer;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private MonoBehaviour[] disableOnWin; // optional rename later to disableOnEnd

    private bool finished;

    // objective state
    private int conesPlaced;
    private int bandagesApplied;
    private bool cprDone;

    // failure state
    private float bleedLeft;
    private float timeSinceStart;
    private bool bleedingStopped;
    private bool cprStarted;
    private bool safetyPenaltyApplied;
    [SerializeField] private TMP_Text failReasonText;


    private void Awake()
    {
        if (!objectiveUI) objectiveUI = FindFirstObjectByType<ObjectiveUI>();
        if (!conePlacer) conePlacer = FindFirstObjectByType<ConePlacer>();
        if (!cprMinigame) cprMinigame = FindFirstObjectByType<CPRMinigame>();

        if (bandagesToStopBleeding <= 0) bandagesToStopBleeding = bandagesRequired;

        bleedLeft = bleedOutSeconds;
        timeSinceStart = 0f;

        objectiveUI?.Refresh();
        UpdateTimerUI();
    }

   private void OnEnable()
{
    if (conePlacer != null)
        conePlacer.OnConePlaced += HandleConePlaced;

    if (cprMinigame != null)
        cprMinigame.OnCPRClosed += HandleCPRClosed;
    if (cprMinigame != null)
    cprMinigame.OnCPRStarted += HandleCPRStarted;

}

private void OnDisable()
{
    if (conePlacer != null)
        conePlacer.OnConePlaced -= HandleConePlaced;

    if (cprMinigame != null)
        cprMinigame.OnCPRClosed -= HandleCPRClosed;

    if (cprMinigame != null)
    cprMinigame.OnCPRStarted -= HandleCPRStarted;

}
public void PauseForCPR(bool pause)
{
    pausedForCPR = pause;
}

private void HandleCPRClosed(bool completed)
{
    pausedForCPR = false;
}


    private void Update()
    {
        if (finished) return;
         if (pausedForCPR)
    {
        UpdateTimerUI();
        return; 
    }

        timeSinceStart += Time.deltaTime;

        // (A) Hard fail if CPR not started in time
        if (!cprStarted && timeSinceStart >= cprMustStartBySeconds)
        {
            Fail("CPR was not initiated early enough. Begin CPR immediately after assessing unresponsiveness and no normal breathing.");
            return;
        }

        // (B) One-time safety penalty if cones not completed early enough
        if (!safetyPenaltyApplied && timeSinceStart >= safetyCheckAtSeconds)
        {
            safetyPenaltyApplied = true;

            if (conesPlaced < conesRequired)
            {
                bleedLeft = Mathf.Max(0f, bleedLeft - unsafePenaltySeconds);
                Debug.Log($"⚠️ Unsafe scene penalty: -{unsafePenaltySeconds}s (cones not placed)");
            }
        }

        // (C) Bleed-out countdown until bleeding controlled
        if (!bleedingStopped)
        {
            bleedLeft -= Time.deltaTime;

            if (bleedLeft <= 0f)
            {
                bleedLeft = 0f;
                Fail("Hemorrhage was not controlled in time. Apply bandages/pressure to stop bleeding before it becomes fatal.");
                return;
            }
        }

        UpdateTimerUI();
    }

    private void HandleConePlaced(int totalPlaced)
    {
        conesPlaced = totalPlaced;
        objectiveUI?.Refresh();
        Debug.Log($"Objective: Cones placed {conesPlaced}/{conesRequired}");
        CheckWin();
    }

    // Called from WoundInteractable when bandage application succeeds
    public void NotifyBandageApplied()
    {
        bandagesApplied++;
        Debug.Log($"Objective: Bandages {bandagesApplied}/{bandagesRequired}");

        if (!bleedingStopped && bandagesApplied >= bandagesToStopBleeding)
        {
            bleedingStopped = true;
            Debug.Log("🩹 Bleeding stopped!");
        }

        //objectiveUI?.Refresh();
        CheckWin();
    }

    // Called from CPR completion callback
    public void NotifyCPRDone()
    {
        cprDone = true;
        objectiveUI?.Refresh();
        Debug.Log("Objective: CPR done");
        CheckWin();
    }

    private void HandleCPRStarted()
    {
        cprStarted = true;
        Debug.Log("CPR started.");
    }

    private void UpdateTimerUI()
{
    if (!timerText) return;

    ApplyTimerTextStyle();

    int t = Mathf.CeilToInt(bleedLeft);
    int m = t / 60;
    int s = t % 60;

    int cprDeadline = Mathf.CeilToInt(cprMustStartBySeconds);
    int safetyAt = Mathf.CeilToInt(safetyCheckAtSeconds);

    string bleedState = bleedingStopped ? "Bleeding controlled" : "Bleeding!";

    string safetyState;
    if (!safetyPenaltyApplied && timeSinceStart < safetyCheckAtSeconds)
        safetyState = $"Safety check in: {Mathf.CeilToInt(safetyCheckAtSeconds - timeSinceStart)}s";
    else if (safetyPenaltyApplied && conesPlaced < conesRequired)
        safetyState = $"Safety: PENALTY APPLIED (-{unsafePenaltySeconds}s)";
    else
        safetyState = "Safety: OK";

    string cprState;
    if (cprStarted)
        cprState = "CPR: STARTED";
    else
        cprState = $"CPR deadline: {Mathf.CeilToInt(cprMustStartBySeconds - timeSinceStart)}s";

    // Clamp negative countdown display for CPR
    if (!cprStarted && timeSinceStart >= cprMustStartBySeconds)
        cprState = "CPR deadline: 0s";

    timerText.text =
        $"Bleed timer: {m:00}:{s:00}\n" +
        $"{bleedState}\n" +
        $"{safetyState}\n" +
        $"{cprState}";
}

private void ApplyTimerTextStyle()
{
    timerText.enableWordWrapping = true;
    timerText.overflowMode = TextOverflowModes.Overflow;
    timerText.enableAutoSizing = true;
    timerText.fontSizeMin = 14f;
    timerText.fontSizeMax = Mathf.Max(timerText.fontSize, 24f);
}


    private void CheckWin()
    {
        if (finished) return;

        bool conesOk = conesPlaced >= conesRequired;
        bool bandagesOk = bandagesApplied >= bandagesRequired;
        bool cprOk = cprDone;

        if (conesOk && bandagesOk && cprOk)
        {
            finished = true;
            Debug.Log("✅ SCENARIO COMPLETE");

            if (winPanel)
            {
                winPanel.SetActive(true);
                ScenarioEndMenuActions.AddReturnToMainMenuButton(winPanel);
            }

            if (disableOnWin != null)
                foreach (var m in disableOnWin)
                    if (m) m.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Fail(string reason)
{
    finished = true;
    Debug.Log($"SCENARIO FAILED: {reason}");

    if (failReasonText)
        failReasonText.text = $"Failure reason: {reason}";

    if (failPanel)
    {
        failPanel.SetActive(true);
        ScenarioEndMenuActions.AddReturnToMainMenuButton(failPanel);
    }

    if (disableOnWin != null)
        foreach (var m in disableOnWin)
            if (m) m.enabled = false;

    Cursor.lockState = CursorLockMode.None;
    Cursor.visible = true;
}

}
