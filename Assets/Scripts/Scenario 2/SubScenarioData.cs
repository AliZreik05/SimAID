using System;
using UnityEngine;

[Serializable]
public class QuestionAnswerPair
{
    public MedicalQuestionType questionType;

    [TextArea(2, 4)]
    public string answer;

    [Header("Voice Clips")]
    public AudioClip userVoiceClip;
    public AudioClip patientVoiceClip;
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

    [Header("Inspection Clues")]
    [TextArea(2, 4)] public string chestInspectionClue;
    [TextArea(2, 4)] public string handInspectionClue;

    [Header("Narrator Clips")]
    public AudioClip introSceneClip;
    public AudioClip assessmentObjectiveClip;
    public AudioClip questioningObjectiveClip;
    public AudioClip medkitObjectiveClip;
    public AudioClip successClip;
    public AudioClip failureClip;
}