using TMPro;
using UnityEngine;

public class QuestionButton : MonoBehaviour
{
    [SerializeField] private MedicalScenarioManager scenarioManager;
    [SerializeField] private MedicalQuestionType questionType;
    [SerializeField] private TMP_Text questionLabel;

    private void Awake()
    {
        if (questionLabel == null)
            questionLabel = GetComponentInChildren<TMP_Text>();
    }

    public void Ask()
    {
        if (scenarioManager == null)
        {
            Debug.LogWarning("QuestionButton: No MedicalScenarioManager assigned.");
            return;
        }

        if (questionLabel == null)
        {
            Debug.LogWarning("QuestionButton: No question label found.");
            return;
        }

        scenarioManager.AskQuestion(questionType, questionLabel.text);
    }
}