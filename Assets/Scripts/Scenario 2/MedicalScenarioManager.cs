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
        InspectionView,
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

    [Header("Result Text Styling")]
[SerializeField] private float resultTitleFontSize = 34f;
[SerializeField] private float resultBodyFontSize = 25f;

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

    private bool inspectedChest = false;
    private bool inspectedHand = false;
    private ScenarioState stateBeforeInspection;

    private int questionsAsked = 0;
    private readonly List<MedicalQuestionType> askedQuestions = new();

    private Coroutine typingCoroutine;
    private bool isTyping = false;
    private bool waitingForQuestionExit = false;
    private bool introFinished = false;
    private bool openingDialoguePlayed = false;

    public ScenarioState CurrentState => currentState;
    public bool CanOpenMedkit => currentState == ScenarioState.MedicationSelection;

    public bool CanInspect =>
    currentState == ScenarioState.MedicationSelection &&
    Time.time >= inspectionUnlockTime;

    public bool InspectedChest => inspectedChest;
    public bool InspectedHand => inspectedHand;
    private float inspectionUnlockTime = 0f;

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

        inspectionUnlockTime = 0f;

        questionsAsked = 0;
        askedQuestions.Clear();
        inspectedChest = false;
        inspectedHand = false;

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

    public string GetInspectionClue(InspectionPartType partType)
    {
        if (currentScenario == null)
            return "";

        return partType == InspectionPartType.Chest
            ? currentScenario.chestInspectionClue
            : currentScenario.handInspectionClue;
    }

    public void EnterInspectionView(InspectionPartType partType)
    {
        if (!CanInspect)
            return;

        stateBeforeInspection = currentState;
        currentState = ScenarioState.InspectionView;

        if (questionPanel != null)
            questionPanel.SetActive(false);

        if (answerPanel != null)
            answerPanel.SetActive(false);

        if (medkitInstructionUI != null)
            medkitInstructionUI.SetActive(false);

        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();

        if (narratorAudioSource != null)
            narratorAudioSource.Stop();

        SetGameplayLocked(true);
        SetCursorState(true);

        if (partType == InspectionPartType.Chest)
            inspectedChest = true;
        else
            inspectedHand = true;

        Debug.Log($"Entered inspection view: {partType}");
    }

    public void ExitInspectionView(InspectionPartType partType)
    {
        if (currentState != ScenarioState.InspectionView)
            return;

        currentState = stateBeforeInspection;

        if (currentState == ScenarioState.Questioning)
        {
            if (questionPanel != null && openingDialoguePlayed && !isTyping && !waitingForQuestionExit)
                questionPanel.SetActive(true);

            if (answerPanel != null)
                answerPanel.SetActive(true);

            if (objectiveText != null)
                objectiveText.text = waitingForQuestionExit
                    ? "Assessment complete. Press E to continue."
                    : $"Ask up to {maxQuestions} questions.";
        }
        else if (currentState == ScenarioState.MedicationSelection)
{
    if (objectiveText != null)
    {
        if (inspectedChest && inspectedHand)
        {
            objectiveText.text = "Physical inspection complete. Proceed to the medkit and choose the correct medication.";
        }
        else if (inspectedChest)
        {
            objectiveText.text = "Chest inspection complete. You may inspect the hand or proceed to the medkit.";
        }
        else if (inspectedHand)
        {
            objectiveText.text = "Hand inspection complete. You may inspect the chest or proceed to the medkit.";
        }
        else
        {
            objectiveText.text = "Inspect the chest or hand for more clues, or go to the medkit and choose the correct medication.";
        }
    }
}

        SetGameplayLocked(currentState == ScenarioState.Questioning);
        SetCursorState(currentState == ScenarioState.Questioning);

        Debug.Log($"Exited inspection view: {partType}");
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

    // Lock gameplay while in question mode
    SetGameplayLocked(true);

    // Keep look script enabled unless your InputManager handles it differently
    if (playerLook != null)
        playerLook.enabled = true;

    Cursor.visible = true;
    Cursor.lockState = CursorLockMode.None;

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

        if (conversationController != null)
            conversationController.StopPatientTalking();

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

        if (conversationController != null)
            conversationController.StartPatientTalking();

        yield return new WaitForSeconds(0.1f);

        if (dialogueAudioSource != null && openingPatientVoiceClip != null)
        {
            dialogueAudioSource.Stop();
            dialogueAudioSource.clip = openingPatientVoiceClip;
            dialogueAudioSource.Play();
        }

        yield return StartCoroutine(TypeLine(patientLine));

        if (dialogueAudioSource != null && dialogueAudioSource.isPlaying)
            yield return new WaitUntil(() => !dialogueAudioSource.isPlaying);

        if (conversationController != null)
            conversationController.StopPatientTalking();

        openingDialoguePlayed = true;
        isTyping = false;
        typingCoroutine = null;

        if (questionPanel != null)
            questionPanel.SetActive(false);

        if (objectiveText != null)
            objectiveText.text = $"Ask up to {maxQuestions} questions.";

        yield return new WaitForSeconds(0.5f);

        if (currentScenario.questioningObjectiveClip != null)
        {
            PlayNarratorClip(currentScenario.questioningObjectiveClip);

            if (narratorAudioSource != null && narratorAudioSource.isPlaying)
                yield return new WaitUntil(() => !narratorAudioSource.isPlaying);
        }

        if (questionPanel != null)
            questionPanel.SetActive(true);

        Debug.Log("Opening dialogue finished. Question UI enabled.");
    }

    public void AskQuestion(MedicalQuestionType questionType, string questionDisplayText)
    {
        if (currentState != ScenarioState.Questioning)
            return;

        if (!openingDialoguePlayed)
            return;

        if (isTyping || waitingForQuestionExit || IsNarratorBusy())
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
    inspectionUnlockTime = Time.time + 0.35f;

    if (questionPanel != null) questionPanel.SetActive(false);
    if (answerPanel != null) answerPanel.SetActive(false);
    if (medkitInstructionUI != null) medkitInstructionUI.SetActive(false);

    if (dialogueAudioSource != null)
        dialogueAudioSource.Stop();

    if (objectiveText != null)
        objectiveText.text = "Assessment complete. Inspect the chest or hand for more clues, or go to the medkit and choose the correct medication.";

    if (conversationController != null)
        conversationController.EndConversation();

    // Explicitly restore player look
    if (playerLook != null)
        playerLook.enabled = true;

    // Explicitly unlock gameplay
    SetGameplayLocked(false);

    // Restore normal in-game cursor state
    Cursor.visible = false;
    Cursor.lockState = CursorLockMode.Locked;

    Debug.Log("Questioning finished. Returned to normal player control.");
}

    private bool IsNarratorBusy()
    {
        return narratorAudioSource != null && narratorAudioSource.isPlaying;
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
            objectiveText.text = "Inspect the chest or hand for more clues, or go to the medkit and choose the correct medication.";

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

        if (conversationController != null)
            conversationController.StopPatientTalking();

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

        if (conversationController != null)
            conversationController.StartPatientTalking();

        yield return new WaitForSeconds(0.1f);

        if (dialogueAudioSource != null && pair.patientVoiceClip != null)
        {
            dialogueAudioSource.Stop();
            dialogueAudioSource.clip = pair.patientVoiceClip;
            dialogueAudioSource.Play();
        }

        yield return StartCoroutine(TypeLine(patientLine));

        if (dialogueAudioSource != null && dialogueAudioSource.isPlaying)
            yield return new WaitUntil(() => !dialogueAudioSource.isPlaying);

        if (conversationController != null)
            conversationController.StopPatientTalking();

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

    string debrief = GenerateDebrief(
        success: true,
        reason: "Correct medication given."
    );

    ShowResult("SUCCESS", debrief, successTitleColor);
}

private void FailScenario(string reason)
{
    currentState = ScenarioState.Failure;
    PlayNarratorClip(currentScenario.failureClip);

    string debrief = GenerateDebrief(
        success: false,
        reason: reason
    );

    ShowResult("FAILURE", debrief, failureTitleColor);
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
        resultTitleText.fontSize = resultTitleFontSize;
        resultTitleText.alignment = TextAlignmentOptions.Center;
    }

    if (resultBodyText != null)
    {
        resultBodyText.text = body;
        resultBodyText.color = resultBodyColor;
        resultBodyText.fontSize = resultBodyFontSize;
        resultBodyText.alignment = TextAlignmentOptions.TopLeft;
        resultBodyText.enableWordWrapping = true;
        resultBodyText.overflowMode = TextOverflowModes.Overflow;
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
   private string GenerateDebrief(bool success, string reason)
{
    System.Text.StringBuilder sb = new System.Text.StringBuilder();

    sb.AppendLine(reason);
    sb.AppendLine();
    sb.AppendLine(GenerateScoreLine(success));
    sb.AppendLine();

    sb.AppendLine("Assessment summary:");

    if (askedQuestions.Count > 0)
    {
        sb.Append("- Questions asked: ");

        for (int i = 0; i < askedQuestions.Count; i++)
        {
            sb.Append(FormatQuestionName(askedQuestions[i]));

            if (i < askedQuestions.Count - 1)
                sb.Append(", ");
        }

        sb.AppendLine(".");
    }
    else
    {
        sb.AppendLine("- No focused questions asked.");
    }

    sb.AppendLine();

    sb.AppendLine("Inspection findings:");

    if (inspectedChest)
        sb.AppendLine("- Chest: " + ShortenInspectionClue(currentScenario.chestInspectionClue));
    else
        sb.AppendLine("- Chest: not inspected.");

    if (inspectedHand)
        sb.AppendLine("- Hand: " + ShortenInspectionClue(currentScenario.handInspectionClue));
    else
        sb.AppendLine("- Hand: not inspected.");

    sb.AppendLine();

    sb.AppendLine("Missed points:");

    bool missedAnything = false;

    if (currentScenario.keyQuestions != null)
    {
        foreach (MedicalQuestionType keyQuestion in currentScenario.keyQuestions)
        {
            if (!askedQuestions.Contains(keyQuestion))
            {
                sb.AppendLine("- Missed: " + FormatQuestionName(keyQuestion) + ".");
                missedAnything = true;
            }
        }
    }

    if (!inspectedChest)
    {
        sb.AppendLine("- Missed: chest inspection.");
        missedAnything = true;
    }

    if (!inspectedHand)
    {
        sb.AppendLine("- Missed: hand inspection.");
        missedAnything = true;
    }

    if (!missedAnything)
        sb.AppendLine("- No major assessment steps missed.");

    sb.AppendLine();

    if (success)
        sb.AppendLine($"Decision: {currentScenario.correctMedication} was correct.");
    else
        sb.AppendLine($"Correct treatment: {currentScenario.correctMedication}.");

    return sb.ToString();
}
private string GenerateScoreLine(bool success)
{
    int score = 0;
    int maxScore = 0;

    if (currentScenario.keyQuestions != null)
    {
        maxScore += currentScenario.keyQuestions.Length;

        foreach (MedicalQuestionType keyQuestion in currentScenario.keyQuestions)
        {
            if (askedQuestions.Contains(keyQuestion))
                score++;
        }
    }

    // Chest + hand inspection
    maxScore += 2;

    if (inspectedChest)
        score++;

    if (inspectedHand)
        score++;

    // Medication decision
    maxScore += 2;

    if (success)
        score += 2;

    string label = GetScoreLabel(score, maxScore);

    return $"Clinical Assessment Score: {score}/{maxScore} — {label}";
}
private string FormatQuestionName(MedicalQuestionType questionType)
{
    switch (questionType)
    {
        case MedicalQuestionType.CurrentFeeling:
            return "current symptoms";

        case MedicalQuestionType.Breathing:
            return "breathing difficulty";

        case MedicalQuestionType.Trigger:
            return "possible trigger or exposure";

        case MedicalQuestionType.Throat:
            return "throat tightness or swelling";

        case MedicalQuestionType.GI:
            return "gastrointestinal symptoms";

        case MedicalQuestionType.AsthmaHistory:
            return "asthma history";

        default:
            return questionType.ToString();
    }
}
private string GetScoreLabel(int score, int maxScore)
{
    float percentage = maxScore == 0 ? 0f : (float)score / maxScore;

    if (percentage >= 0.85f)
        return "Excellent assessment";

    if (percentage >= 0.65f)
        return "Good assessment";

    if (percentage >= 0.45f)
        return "Needs improvement";

    return "Incomplete assessment";
}
private string ShortenInspectionClue(string clue)
{
    if (string.IsNullOrWhiteSpace(clue))
        return "No clear finding.";

    clue = clue.Trim();

    if (clue.Length <= 85)
        return clue;

    return clue.Substring(0, 82) + "...";
}
}