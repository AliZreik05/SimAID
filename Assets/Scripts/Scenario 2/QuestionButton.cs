using UnityEngine;

public class QuestionButton : MonoBehaviour
{
    [SerializeField] private MedicalScenarioManager scenarioManager;
    [SerializeField] private MedicalQuestionType questionType;

    public void Ask()
    {
        if (!scenarioManager)
        {
            Debug.LogWarning("QuestionButton: No MedicalScenarioManager assigned.");
            return;
        }

        scenarioManager.AskQuestion(questionType);
    }
}