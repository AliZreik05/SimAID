using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MedicalScenarioManager : MonoBehaviour
{
    public enum ScenarioState
    {
        NotStarted,
        Intro,
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

    [Header("Dialogue Audio")]
    [SerializeField] private AudioSource dialogueAudioSource;

    [Header("Narrator Audio")]
    [SerializeField] private AudioSource narratorAudioSource;

    [Header("Opening Dialogue")]
    [SerializeField] private string openingPlayerLine = "Hello, I noticed you seem distressed. I'm an emergency medical responder. Do you need any help?";
    [SerializeField] private string openingPatientLine = "Yes, please... I'm not feeling well.";
    [SerializeField] private AudioClip openingPlayerVoiceClip;
    [SerializeField] private AudioClip openingPatientVoiceClip;

    [Header("Intro UI")]
    [SerializeField] private GameObject introFadePanel;
    [SerializeField] private TMP_Text introText;

    [Header("Intro Camera")]
    [SerializeField] private Transform introLookTarget;
    [SerializeField] private Transform playerCameraTransform;
    [SerializeField] private float lookRotateSpeed = 2.5f;
    [SerializeField] private float fadeDuration = 1f;

    [Header("UI References")]
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text answerText;
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private GameObject answerPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultBodyText;

    [Header("Optional UI")]
    [SerializeField] private GameObject medkitInstructionUI;

    [Header("Player Control")]
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private InputManager inputManager;

    [Header("Patient")]
    [SerializeField] private PatientConversationController conversationController;

    private SubScenarioData currentScenario;
    private ScenarioState currentState = ScenarioState.NotStarted;

    private int questionsAsked = 0;
    private readonly List<MedicalQuestionType> askedQuestions = new();

    private Coroutine typingCoroutine;
    private bool isTyping = false;
    private bool waitingForQuestionExit = false;
    private bool introFinished = false;
    private bool openingDialoguePlayed = false;

    public ScenarioState CurrentState => currentState;
    public bool CanOpenMedkit => currentState == ScenarioState.MedicationSelection;

    private void Start()
    {
        StartScenario();
        StartCoroutine(IntroSequence());
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
        currentState = ScenarioState.Intro;

        questionsAsked = 0;
        askedQuestions.Clear();

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();

        if (narratorAudioSource != null)
            narratorAudioSource.Stop();

        isTyping = false;
        waitingForQuestionExit = false;
        introFinished = false;
        openingDialoguePlayed = false;

        if (objectiveText != null)
            objectiveText.text = "";

        if (answerText != null)
            answerText.text = "";

        if (questionPanel != null) questionPanel.SetActive(false);
        if (answerPanel != null) answerPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (medkitInstructionUI != null) medkitInstructionUI.SetActive(false);

        if (introFadePanel != null) introFadePanel.SetActive(true);
        if (introText != null) introText.text = "";

        SetGameplayLocked(true);
        SetCursorState(false);

        Debug.Log($"Selected scenario: {currentScenario.scenarioName}");
    }

    private IEnumerator IntroSequence()
{
    SetGameplayLocked(true);
    SetCursorState(false);

    if (playerLook != null && introLookTarget != null)
        playerLook.BeginForcedLook(introLookTarget, lookRotateSpeed);

    if (questionPanel != null) questionPanel.SetActive(false);
    if (answerPanel != null) answerPanel.SetActive(false);
    if (resultPanel != null) resultPanel.SetActive(false);
    if (medkitInstructionUI != null) medkitInstructionUI.SetActive(false);

    if (introFadePanel != null)
        introFadePanel.SetActive(true);

    if (introText != null)
        introText.text = "";

    yield return new WaitForSeconds(0.5f);
    yield return StartCoroutine(FadeIntroPanel(1f, 0.3f));

    PlayNarratorClip(currentScenario.introSceneClip);
    yield return StartCoroutine(TypeIntroSequenceLine(
        "You arrive at a restaurant and notice a distressed individual.", 0.4f));

    if (narratorAudioSource != null && narratorAudioSource.isPlaying)
        yield return new WaitUntil(() => !narratorAudioSource.isPlaying);

    if (introText != null)
        introText.text = "";
    yield return new WaitForSeconds(0.35f);

    PlayNarratorClip(currentScenario.assessmentObjectiveClip);
    yield return StartCoroutine(TypeIntroSequenceLine(
        "Approach the patient and begin assessment.", 0.4f));

    if (narratorAudioSource != null && narratorAudioSource.isPlaying)
        yield return new WaitUntil(() => !narratorAudioSource.isPlaying);

    currentState = ScenarioState.WaitingForPatientInteraction;
    introFinished = true;

    if (introText != null)
        introText.text = "";

    if (introFadePanel != null)
        introFadePanel.SetActive(false);

    if (objectiveText != null)
        objectiveText.text = "Approach the patient and begin assessment.";

    SetGameplayLocked(false);
    SetCursorState(false);

    if (playerLook != null)
        playerLook.EndForcedLook();
}
    private IEnumerator FadeIntroPanel(float fromAlpha, float toAlpha)
    {
        if (introFadePanel == null)
            yield break;

        Image img = introFadePanel.GetComponent<Image>();
        if (img == null)
            yield break;

        float elapsed = 0f;
        Color c = img.color;
        c.a = fromAlpha;
        img.color = c;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            c.a = Mathf.Lerp(fromAlpha, toAlpha, t);
            img.color = c;

            yield return null;
        }

        c.a = toAlpha;
        img.color = c;
    }

    private IEnumerator TypeIntroLine(string line)
    {
        if (introText == null)
            yield break;

        introText.text = "";

        for (int i = 0; i < line.Length; i++)
        {
            introText.text += line[i];
            yield return new WaitForSeconds(typingSpeed);
        }
    }

    private IEnumerator TypeIntroSequenceLine(string line, float holdTime)
    {
        yield return StartCoroutine(TypeIntroLine(line));
        yield return new WaitForSeconds(holdTime);
    }

    public void BeginPatientAssessment()
    {
        if (currentState != ScenarioState.WaitingForPatientInteraction || !introFinished)
            return;

        currentState = ScenarioState.Questioning;
        openingDialoguePlayed = false;

        if (questionPanel != null) questionPanel.SetActive(false);
        if (answerPanel != null) answerPanel.SetActive(true);
        if (medkitInstructionUI != null) medkitInstructionUI.SetActive(false);

        if (objectiveText != null)
            objectiveText.text = "Listen and assess the patient.";

        if (conversationController != null)
            conversationController.StartConversation();

        SetGameplayLocked(true);
        SetCursorState(true);

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();

        typingCoroutine = StartCoroutine(PlayOpeningDialogue());

        Debug.Log("Patient interaction started. Opening dialogue playing.");
    }

    private IEnumerator PlayOpeningDialogue()
    {
        isTyping = true;

        if (answerText == null)
        {
            isTyping = false;
            yield break;
        }

        answerText.text = "";

        string youLine = $"You: {openingPlayerLine}";
        string patientLine = $"Patient: {openingPatientLine}";

        if (dialogueAudioSource != null && openingPlayerVoiceClip != null)
        {
            dialogueAudioSource.Stop();
            dialogueAudioSource.clip = openingPlayerVoiceClip;
            dialogueAudioSource.Play();
        }

        yield return StartCoroutine(TypeLine(youLine));

        if (dialogueAudioSource != null && dialogueAudioSource.isPlaying)
            yield return new WaitUntil(() => !dialogueAudioSource.isPlaying);

        yield return new WaitForSeconds(linePause);

        answerText.text += "\n\n";

        if (dialogueAudioSource != null && openingPatientVoiceClip != null)
        {
            dialogueAudioSource.Stop();
            dialogueAudioSource.clip = openingPatientVoiceClip;
            dialogueAudioSource.Play();
        }

        yield return StartCoroutine(TypeLine(patientLine));

        if (dialogueAudioSource != null && dialogueAudioSource.isPlaying)
            yield return new WaitUntil(() => !dialogueAudioSource.isPlaying);

        openingDialoguePlayed = true;
        isTyping = false;
        typingCoroutine = null;

        if (questionPanel != null)
            questionPanel.SetActive(true);

        if (objectiveText != null)
    objectiveText.text = $"Ask up to {maxQuestions} questions.";

yield return new WaitForSeconds(0.5f);

if (currentScenario.questioningObjectiveClip != null)
    PlayNarratorClip(currentScenario.questioningObjectiveClip);

Debug.Log("Opening dialogue finished. Question UI enabled.");
    }

    public void AskQuestion(MedicalQuestionType questionType, string questionDisplayText)
    {
        if (currentState != ScenarioState.Questioning)
            return;

        if (!openingDialoguePlayed)
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

        QuestionAnswerPair pair = GetQuestionAnswerPair(questionType);
        if (pair == null)
        {
            Debug.LogWarning($"No Q/A pair found for {questionType}");
            return;
        }

        questionsAsked++;
        askedQuestions.Add(questionType);

        if (answerPanel != null && !answerPanel.activeSelf)
            answerPanel.SetActive(true);

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();

        int remaining = maxQuestions - questionsAsked;
        bool isLastQuestion = remaining == 0;

        if (objectiveText != null)
        {
            objectiveText.text = isLastQuestion
                ? "Final question..."
                : $"Question {questionsAsked}/{maxQuestions}. You can ask {remaining} more.";
        }

        typingCoroutine = StartCoroutine(TypeDialogueWithAudio(pair, questionDisplayText, isLastQuestion));

        Debug.Log($"Question asked: {questionType} | Answer: {pair.answer}");
    }

    public void FinishQuestioning()
    {
        currentState = ScenarioState.MedicationSelection;

        if (questionPanel != null) questionPanel.SetActive(false);
        if (answerPanel != null) answerPanel.SetActive(false);
        if (medkitInstructionUI != null) medkitInstructionUI.SetActive(false);

        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();

        if (objectiveText != null)
            objectiveText.text = "Assessment complete. Go to the medkit and choose the correct medication.";

        PlayNarratorClip(currentScenario.medkitObjectiveClip);

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

        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();

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

    private IEnumerator TypeDialogueWithAudio(QuestionAnswerPair pair, string question, bool finishAfterTyping)
    {
        isTyping = true;

        if (answerText == null)
        {
            isTyping = false;
            yield break;
        }

        answerText.text = "";

        string youLine = $"You: {question}";
        string patientLine = $"Patient: {pair.answer}";

        if (dialogueAudioSource != null && pair.userVoiceClip != null)
        {
            dialogueAudioSource.Stop();
            dialogueAudioSource.clip = pair.userVoiceClip;
            dialogueAudioSource.Play();
        }

        yield return StartCoroutine(TypeLine(youLine));

        if (dialogueAudioSource != null && dialogueAudioSource.isPlaying)
            yield return new WaitUntil(() => !dialogueAudioSource.isPlaying);

        yield return new WaitForSeconds(linePause);

        answerText.text += "\n\n";

        if (dialogueAudioSource != null && pair.patientVoiceClip != null)
        {
            dialogueAudioSource.Stop();
            dialogueAudioSource.clip = pair.patientVoiceClip;
            dialogueAudioSource.Play();
        }

        yield return StartCoroutine(TypeLine(patientLine));

        if (dialogueAudioSource != null && dialogueAudioSource.isPlaying)
            yield return new WaitUntil(() => !dialogueAudioSource.isPlaying);

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

    private QuestionAnswerPair GetQuestionAnswerPair(MedicalQuestionType questionType)
    {
        if (currentScenario == null || currentScenario.answers == null)
            return null;

        foreach (QuestionAnswerPair pair in currentScenario.answers)
        {
            if (pair.questionType == questionType)
                return pair;
        }

        return null;
    }

    public void GiveMedication(string medicationName)
    {
        if (currentState != ScenarioState.MedicationSelection &&
            currentState != ScenarioState.InMedkitView)
            return;

        if (currentScenario == null)
            return;

        Debug.Log($"Medication given: {medicationName}");

        if (string.Equals(
                medicationName.Trim(),
                currentScenario.correctMedication.Trim(),
                System.StringComparison.OrdinalIgnoreCase))
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
        PlayNarratorClip(currentScenario.successClip);
        ShowResult("SUCCESS", "Correct medication given.", successTitleColor);
    }

    private void FailScenario(string reason)
    {
        currentState = ScenarioState.Failure;
        PlayNarratorClip(currentScenario.failureClip);
        ShowResult("FAILURE", reason, failureTitleColor);
    }

    private void ShowResult(string title, string body, Color titleColor)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();

        isTyping = false;
        waitingForQuestionExit = false;

        if (questionPanel != null) questionPanel.SetActive(false);
        if (answerPanel != null) answerPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(true);
        if (medkitInstructionUI != null) medkitInstructionUI.SetActive(false);

        if (conversationController != null)
            conversationController.EndConversation();

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

    private void PlayNarratorClip(AudioClip clip)
    {
        if (narratorAudioSource == null || clip == null)
            return;

        narratorAudioSource.Stop();
        narratorAudioSource.clip = clip;
        narratorAudioSource.Play();
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