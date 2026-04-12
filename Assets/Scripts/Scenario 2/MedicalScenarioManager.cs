using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MedicalScenarioManager : MonoBehaviour
{
    public enum ScenarioState
    {
        NotStarted,
        WaitingForPatientInteraction,
        Questioning,
        MedicationSelection,
        Success,
        Failure
    }

    [Header("Scenario Data")]
    [SerializeField] private SubScenarioData[] subScenarios;

    [Header("Question Settings")]
    [SerializeField] private int maxQuestions = 3;

    [Header("UI References")]
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text answerText;
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private GameObject answerPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultBodyText;

    [Header("Player Control")]
    [SerializeField] private Behaviour[] controlsToDisableDuringUI;

    private SubScenarioData currentScenario;
    private ScenarioState currentState = ScenarioState.NotStarted;

    private int questionsAsked = 0;
    private readonly List<MedicalQuestionType> askedQuestions = new();

    public ScenarioState CurrentState => currentState;

    public bool HasPatientBeenInteractedWith =>
        currentState == ScenarioState.Questioning ||
        currentState == ScenarioState.MedicationSelection ||
        currentState == ScenarioState.Success ||
        currentState == ScenarioState.Failure;

    public bool CanOpenMedkit =>
        currentState == ScenarioState.MedicationSelection;

    private void Start()
    {
        StartScenario();
    }

    public void StartScenario()
    {
        if (subScenarios == null || subScenarios.Length == 0)
        {
            Debug.LogError("MedicalScenarioManager: No sub-scenarios assigned.");
            return;
        }

        currentScenario = subScenarios[Random.Range(0, subScenarios.Length)];
        currentState = ScenarioState.WaitingForPatientInteraction;

        questionsAsked = 0;
        askedQuestions.Clear();

        if (objectiveText != null)
            objectiveText.text = "Interact with the patient to begin assessment.";

        if (answerText != null)
            answerText.text = "";

        if (questionPanel != null)
            questionPanel.SetActive(false);

        if (answerPanel != null)
            answerPanel.SetActive(false);

        if (resultPanel != null)
            resultPanel.SetActive(false);

        SetControlsEnabled(true);
        SetCursorState(false);

        Debug.Log($"Selected scenario: {currentScenario.scenarioName}");
    }

    public void BeginPatientAssessment()
    {
        if (currentState != ScenarioState.WaitingForPatientInteraction)
            return;

        currentState = ScenarioState.Questioning;

        if (objectiveText != null)
            objectiveText.text = $"Ask up to {maxQuestions} questions.";

        if (questionPanel != null)
            questionPanel.SetActive(true);

        if (answerPanel != null)
            answerPanel.SetActive(true);

        SetControlsEnabled(false);
        SetCursorState(true);

        Debug.Log("Patient interaction started. Question UI opened.");
    }

    public void AskQuestion(MedicalQuestionType questionType)
    {
        if (currentState != ScenarioState.Questioning)
            return;

        if (questionsAsked >= maxQuestions)
        {
            if (objectiveText != null)
                objectiveText.text = "Assessment complete. Go to the medkit.";
            return;
        }

        if (askedQuestions.Contains(questionType))
        {
            if (objectiveText != null)
                objectiveText.text = "You already asked that question.";
            return;
        }

        questionsAsked++;
        askedQuestions.Add(questionType);

        string answer = GetAnswerForQuestion(questionType);

        if (answerText != null)
            answerText.text = answer;

        int remaining = maxQuestions - questionsAsked;

        if (remaining > 0)
        {
            if (objectiveText != null)
                objectiveText.text = $"Question {questionsAsked}/{maxQuestions}. You can ask {remaining} more.";
        }
        else
        {
            FinishQuestioning();
        }

        Debug.Log($"Question asked: {questionType} | Answer: {answer}");
    }

    public void FinishQuestioning()
    {
        currentState = ScenarioState.MedicationSelection;

        if (questionPanel != null)
            questionPanel.SetActive(false);

        if (answerPanel != null)
            answerPanel.SetActive(false);

        if (objectiveText != null)
            objectiveText.text = "Assessment complete. Go to the medkit and choose the correct medication.";

        SetControlsEnabled(true);
        SetCursorState(false);

        Debug.Log("Questioning finished. Returned to normal player control.");
    }

    public void EnterMedkitView()
    {
        if (currentState != ScenarioState.MedicationSelection)
            return;

        if (questionPanel != null)
            questionPanel.SetActive(false);

        if (answerPanel != null)
            answerPanel.SetActive(false);

        if (objectiveText != null)
            objectiveText.text = "Choose the correct item from the medkit.";

        SetControlsEnabled(false);
        SetCursorState(true);

        Debug.Log("Entered medkit view.");
    }

    private string GetAnswerForQuestion(MedicalQuestionType questionType)
    {
        if (currentScenario == null || currentScenario.answers == null)
            return "No answer available.";

        foreach (QuestionAnswerPair pair in currentScenario.answers)
        {
            if (pair.questionType == questionType)
                return pair.answer;
        }

        return "No relevant answer.";
    }

    public void GiveMedication(string medicationName)
    {
        if (currentState != ScenarioState.Questioning &&
            currentState != ScenarioState.MedicationSelection)
            return;

        if (currentScenario == null)
            return;

        Debug.Log($"Medication given: {medicationName}");

        if (medicationName == currentScenario.correctMedication)
        {
            WinScenario();
        }
        else
        {
            FailScenario($"Wrong medication. Correct answer was {currentScenario.correctMedication}.");
        }
    }

    private void WinScenario()
    {
        currentState = ScenarioState.Success;
        ShowResult("Success", "Correct medication given.");
    }

    private void FailScenario(string reason)
    {
        currentState = ScenarioState.Failure;
        ShowResult("Failure", reason);
    }

    private void ShowResult(string title, string body)
    {
        if (questionPanel != null)
            questionPanel.SetActive(false);

        if (answerPanel != null)
            answerPanel.SetActive(false);

        if (resultPanel != null)
            resultPanel.SetActive(true);

        if (resultTitleText != null)
            resultTitleText.text = title;

        if (resultBodyText != null)
            resultBodyText.text = body;

        if (objectiveText != null)
            objectiveText.text = "";

        SetControlsEnabled(false);
        SetCursorState(true);
    }

    private void SetControlsEnabled(bool enabled)
    {
        if (controlsToDisableDuringUI == null)
            return;

        foreach (Behaviour control in controlsToDisableDuringUI)
        {
            if (control != null)
                control.enabled = enabled;
        }
    }

    private void SetCursorState(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }
}