using System;
using UnityEngine;

[Serializable]
public class QuestionAnswerPair
{
    public MedicalQuestionType questionType;

    [TextArea(2, 4)]
    public string answer;
}

[Serializable]
public class SubScenarioData
{
    public string scenarioName;

    public string correctMedication;

    [Header("Patient Answers")]
    public QuestionAnswerPair[] answers;

    [Header("Key Questions (for feedback later)")]
    public MedicalQuestionType[] keyQuestions;
}