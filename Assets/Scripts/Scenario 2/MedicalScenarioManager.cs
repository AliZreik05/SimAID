using System.Collections;
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
        InMedkitView,
        Success,
        Failure
    }

    [Header("Result Colors")]
    [SerializeField] private Color successTitleColor = new Color(0.2f, 0.85f, 0.35f);
    [SerializeField] private Color failureTitleColor = new Color(0.9f, 0.25f, 0.25f);
    [SerializeField] private Color resultBodyColor = Color.white;

    [Header("Scenario Data")]
    [SerializeField] private SubScenarioData[] subScenarios;

    [Header("Question Settings")]
    [SerializeField] private int maxQuestions = 3;

    [Header("Typing Settings")]
    [SerializeField] private float typingSpeed = 0.025f;
    [SerializeField] private float linePause = 0.35f;

    [Header("UI References")]
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text answerText;
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private GameObject answerPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultBodyText;
    [Header("Patient")]
    [SerializeField] private PatientConversationController conversationController;

    [Header("Optional UI")]
    [SerializeField] private GameObject medkitInstructionUI;

    [Header("Player Control")]
    [SerializeField] private InputManager inputManager;

    private SubScenarioData currentScenario;
    private ScenarioState currentState = ScenarioState.NotStarted;

    private int questionsAsked = 0;
    private readonly List<MedicalQuestionType> askedQuestions = new();

    private Coroutine typingCoroutine;
    private bool isTyping = false;
    private bool waitingForQuestionExit = false;

    public ScenarioState CurrentState => currentState;
    public bool CanOpenMedkit => currentState == ScenarioState.MedicationSelection;

    private void Start()
    {
        StartScenario();
    }

    private void Update()
    {
        if (waitingForQuestionExit && inputManager != null && inputManager.OnFoot.Interact.triggered)
        {
            waitingForQuestionExit = false;
            FinishQuestioning();
        }
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

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;
        waitingForQuestionExit = false;

        if (objectiveText != null)
            objectiveText.text = "Interact with the patient to begin assessment.";

        if (answerText != null)
            answerText.text = "";

        if (questionPanel != null) questionPanel.SetActive(false);
        if (answerPanel != null) answerPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (medkitInstructionUI != null) medkitInstructionUI.SetActive(false);

        SetGameplayLocked(false);
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

        if (questionPanel != null) questionPanel.SetActive(true);
        if (answerPanel != null) answerPanel.SetActive(false);
        if (medkitInstructionUI != null) medkitInstructionUI.SetActive(false);

        SetGameplayLocked(true);
        SetCursorState(true);

        Debug.Log("Patient interaction started. Question UI opened.");
    }

    public void AskQuestion(MedicalQuestionType questionType, string questionDisplayText)
    {
        if (currentState != ScenarioState.Questioning)
            return;

        if (isTyping || waitingForQuestionExit)
            return;

        if (questionsAsked >= maxQuestions)
        {
            if (objectiveText != null)
                objectiveText.text = "Assessment complete. Press E to continue.";
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

        if (answerPanel != null && !answerPanel.activeSelf)
            answerPanel.SetActive(true);

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        int remaining = maxQuestions - questionsAsked;
        bool isLastQuestion = remaining == 0;

        if (objectiveText != null)
        {
            objectiveText.text = isLastQuestion
                ? "Final question..."
                : $"Question {questionsAsked}/{maxQuestions}. You can ask {remaining} more.";
        }

        typingCoroutine = StartCoroutine(TypeDialogue(questionDisplayText, answer, isLastQuestion));

        Debug.Log($"Question asked: {questionType} | Answer: {answer}");
    }

    public void FinishQuestioning()
{
    currentState = ScenarioState.MedicationSelection;

    if (questionPanel != null) questionPanel.SetActive(false);
    if (answerPanel != null) answerPanel.SetActive(false);
    if (medkitInstructionUI != null) medkitInstructionUI.SetActive(false);

    if (objectiveText != null)
        objectiveText.text = "Assessment complete. Go to the medkit and choose the correct medication.";

    if (conversationController != null)
        conversationController.EndConversation();

    SetGameplayLocked(false);
    SetCursorState(false);

    Debug.Log("Questioning finished. Returned to normal player control.");
}

    public void EnterMedkitView()
    {
        if (currentState != ScenarioState.MedicationSelection)
            return;

        currentState = ScenarioState.InMedkitView;

        if (questionPanel != null) questionPanel.SetActive(false);
        if (answerPanel != null) answerPanel.SetActive(false);

        if (objectiveText != null)
            objectiveText.text = "";

        if (medkitInstructionUI != null)
            medkitInstructionUI.SetActive(true);

        SetGameplayLocked(true);
        SetCursorState(true);

        Debug.Log("Entered medkit view.");
    }

    public void ExitMedkitView()
    {
        if (currentState != ScenarioState.InMedkitView)
            return;

        currentState = ScenarioState.MedicationSelection;

        if (medkitInstructionUI != null)
            medkitInstructionUI.SetActive(false);

        if (objectiveText != null)
            objectiveText.text = "Choose the correct medication.";

        SetGameplayLocked(false);
        SetCursorState(false);

        Debug.Log("Exited medkit view.");
    }

    private IEnumerator TypeDialogue(string question, string answer, bool finishAfterTyping)
    {
        isTyping = true;

        if (answerText == null)
        {
            isTyping = false;
            yield break;
        }

        answerText.text = "";

        string youLine = $"You: {question}";
        string patientLine = $"Patient: {answer}";

        yield return StartCoroutine(TypeLine(youLine));
        yield return new WaitForSeconds(linePause);

        answerText.text += "\n\n";

        yield return StartCoroutine(TypeLine(patientLine));

        isTyping = false;
        typingCoroutine = null;

        if (finishAfterTyping)
        {
            waitingForQuestionExit = true;

            if (objectiveText != null)
                objectiveText.text = "Assessment complete. Press E to continue.";
        }
    }

    private IEnumerator TypeLine(string line)
    {
        for (int i = 0; i < line.Length; i++)
        {
            answerText.text += line[i];
            yield return new WaitForSeconds(typingSpeed);
        }
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
        if (currentState != ScenarioState.MedicationSelection &&
            currentState != ScenarioState.InMedkitView)
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
        ShowResult("SUCCESS", "Correct medication given.", successTitleColor);
    }

    private void FailScenario(string reason)
    {
        currentState = ScenarioState.Failure;
        ShowResult("FAILURE", reason, failureTitleColor);
    }

    private void ShowResult(string title, string body, Color titleColor)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;
        waitingForQuestionExit = false;

        if (questionPanel != null)
            questionPanel.SetActive(false);

        if (answerPanel != null)
            answerPanel.SetActive(false);

        if (resultPanel != null)
            resultPanel.SetActive(true);

        if (medkitInstructionUI != null)
            medkitInstructionUI.SetActive(false);

        if (resultTitleText != null)
        {
            resultTitleText.text = title;
            resultTitleText.color = titleColor;
            resultTitleText.fontSize = 44;
            resultTitleText.alignment = TextAlignmentOptions.Center;
        }

        if (resultBodyText != null)
        {
            resultBodyText.text = body;
            resultBodyText.color = resultBodyColor;
            resultBodyText.fontSize = 28;
            resultBodyText.alignment = TextAlignmentOptions.Center;
        }

        if (objectiveText != null)
            objectiveText.text = "";

        SetGameplayLocked(true);
        SetCursorState(true);
    }

    private void SetGameplayLocked(bool locked)
    {
        if (inputManager != null)
            inputManager.SetGameplayLocked(locked);
    }

    private void SetCursorState(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }
}