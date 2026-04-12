using UnityEngine;
using TMPro;

public class AnaphylaxisScenarioManager : MonoBehaviour
{
    public enum ScenarioState
    {
        NotStarted,
        Running,
        Collapsing,
        Success,
        Failure
    }

    [Header("References")]
    public AnaphylaxisPatientController patient;
    public TMP_Text objectiveText;
    public GameObject successPanel;
    public GameObject failPanel;

    [Header("Timing")]
    public float neckHoldStartTime = 8f;
    public float collapseStartTime = 20f;
    public float failTime = 23f;

    [Header("Scenario Rules")]
    public bool requireAssessmentBeforeMedication = true;
    public string correctMedication = "Epinephrine";

    private ScenarioState currentState = ScenarioState.NotStarted;
    private float timer = 0f;

    private bool assessmentDone = false;
    private bool medicationGiven = false;
    private bool neckHoldActivated = false;
    private bool collapseTriggered = false;

    void Start()
    {
        if (successPanel != null)
            successPanel.SetActive(false);

        if (failPanel != null)
            failPanel.SetActive(false);

        StartScenario();
    }

    void Update()
    {
        if (currentState != ScenarioState.Running && currentState != ScenarioState.Collapsing)
            return;

        timer += Time.deltaTime;

        HandleSymptomProgression();
        HandleCollapse();
        HandleFailureAfterCollapse();
    }

    public void StartScenario()
    {
        currentState = ScenarioState.Running;
        timer = 0f;

        assessmentDone = false;
        medicationGiven = false;
        neckHoldActivated = false;
        collapseTriggered = false;

        if (patient != null)
        {
            patient.ResetSymptoms();
            patient.SetExhausted(true);
        }

        UpdateObjective("Assess the patient.");
        Debug.Log("Scenario started.");
    }

    private void HandleSymptomProgression()
{
    if (!neckHoldActivated && timer >= neckHoldStartTime)
    {
        neckHoldActivated = true;

        Debug.Log("NECK HOLD SHOULD START NOW");

        if (patient != null)
            patient.SetHoldingNeck(true);

        UpdateObjective("Airway symptoms are worsening. Give the correct medication.");
    }
}

    private void HandleCollapse()
    {
        if (!medicationGiven && !collapseTriggered && timer >= collapseStartTime)
        {
            collapseTriggered = true;
            currentState = ScenarioState.Collapsing;

            if (patient != null)
                patient.TriggerCollapse();

            UpdateObjective("Patient is collapsing.");
            Debug.Log("Collapse triggered.");
        }
    }

    private void HandleFailureAfterCollapse()
    {
        if (!medicationGiven && collapseTriggered && timer >= failTime)
        {
            FailScenario("Patient collapsed before treatment.");
        }
    }

    public void CompleteAssessment()
    {
        if (currentState != ScenarioState.Running)
            return;

        assessmentDone = true;
        UpdateObjective("Identify the condition and give the correct medication.");
        Debug.Log("Assessment completed.");
    }

    public void GiveMedication(string medicationName)
    {
        if (currentState != ScenarioState.Running)
            return;

        if (medicationGiven)
            return;

        if (requireAssessmentBeforeMedication && !assessmentDone)
        {
            UpdateObjective("Assess the patient first.");
            Debug.Log("Medication blocked: assessment not done.");
            return;
        }

        medicationGiven = true;

        Debug.Log("Medication given: " + medicationName);

        if (medicationName == correctMedication)
        {
            WinScenario();
        }
        else
        {
            if (patient != null)
                patient.TriggerCollapse();

            FailScenario("Wrong medication given.");
        }
    }

    private void WinScenario()
    {
        currentState = ScenarioState.Success;

        if (patient != null)
            patient.TriggerRecover();

        UpdateObjective("Success! Correct treatment given.");

        if (successPanel != null)
            successPanel.SetActive(true);

        Debug.Log("Scenario success.");
    }

    private void FailScenario(string reason)
    {
        if (currentState == ScenarioState.Failure)
            return;

        currentState = ScenarioState.Failure;

        UpdateObjective("Failure: " + reason);

        if (failPanel != null)
            failPanel.SetActive(true);

        Debug.Log("Scenario failed: " + reason);
    }

    private void UpdateObjective(string message)
    {
        if (objectiveText != null)
            objectiveText.text = message;
    }
}