using UnityEngine;

public class AnaphylaxisPatientController : MonoBehaviour
{
    [Header("References")]
    public Animator animator;

    public void SetExhausted(bool value)
    {
        if (animator != null)
            animator.SetBool("IsExhausted", value);
    }

    public void SetHoldingNeck(bool value)
{
    Debug.Log("SetHoldingNeck called with: " + value);

    if (animator != null)
        animator.SetBool("IsHoldingNeck", value);
    else
        Debug.LogError("Animator reference is NULL in AnaphylaxisPatientController.");
}

    public void TriggerCollapse()
    {
        if (animator != null)
            animator.SetTrigger("Collapse");
    }

    public void TriggerRecover()
    {
        if (animator != null)
        {
            animator.SetBool("IsExhausted", false);
            animator.SetBool("IsHoldingNeck", false);
            animator.SetTrigger("Recover");
        }
    }

    public void ResetSymptoms()
    {
        if (animator != null)
        {
            animator.SetBool("IsExhausted", false);
            animator.SetBool("IsHoldingNeck", false);
        }
    }
}