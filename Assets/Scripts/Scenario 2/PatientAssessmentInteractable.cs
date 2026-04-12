using UnityEngine;

public class PatientAssessmentInteractable : MonoBehaviour
{
    public AnaphylaxisScenarioManager manager;

    public void Interact()
    {
        if (manager != null)
            manager.CompleteAssessment();
    }
}