using System.Collections;
using UnityEngine;

public class PatientConversationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform player;

    [Header("Animation")]
    [SerializeField] private string talkingStateName = "Talking";
    [SerializeField] private bool stopAtConversationPose = false;

    [Header("Rotation")]
    [SerializeField] private float rotateSpeed = 6f;
    [SerializeField] private bool rotateOnlyY = true;

    private Coroutine rotateCoroutine;
    private Vector3 startLocalPosition;
    private Quaternion startRotation;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        startLocalPosition = transform.localPosition;
        startRotation = transform.rotation;

        if (animator != null)
            animator.applyRootMotion = false;
    }

    public void StartConversation()
    {
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.SetBool("IsTalking", true);

            if (stopAtConversationPose)
                StartCoroutine(FreezeAfterOneFrame());
        }

        if (rotateCoroutine != null)
            StopCoroutine(rotateCoroutine);

        rotateCoroutine = StartCoroutine(FacePlayer());
    }

    public void EndConversation()
    {
        if (rotateCoroutine != null)
        {
            StopCoroutine(rotateCoroutine);
            rotateCoroutine = null;
        }
        animator.SetBool("IsTalking", false);
        transform.localPosition = startLocalPosition;
    }

    private IEnumerator FreezeAfterOneFrame()
    {
        yield return null;
        if (animator != null)
            animator.speed = 0f;
    }

    private IEnumerator FacePlayer()
    {
        if (player == null)
            yield break;

        while (true)
{
    transform.localPosition = new Vector3(
    transform.localPosition.x,
    startLocalPosition.y,
    transform.localPosition.z
);

    Vector3 dir = player.position - transform.position;

    if (rotateOnlyY)
        dir.y = 0f;

    if (dir.sqrMagnitude > 0.001f)
    {
        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotateSpeed * Time.deltaTime
        );
    }

    yield return null;
}
    }
}