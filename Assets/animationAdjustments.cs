using UnityEngine;

public class CrowdVariation : MonoBehaviour
{
    Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();

        // Random animation speed
        anim.speed = Random.Range(0.75f, 1.25f);

        // Slight random rotation (not all facing perfectly same)
        transform.Rotate(0, Random.Range(-15f, 15f), 0);
    }
}