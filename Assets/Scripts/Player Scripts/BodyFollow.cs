using UnityEngine;

public class BodyFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    void LateUpdate()
    {
        if (target != null)
        {
            Vector3 pos = target.position;
            transform.position = new Vector3(pos.x, transform.position.y, pos.z);
        }
    }
}
