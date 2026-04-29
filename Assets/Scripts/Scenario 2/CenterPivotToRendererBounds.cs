using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class CenterPivotToRendererBounds : MonoBehaviour
{
    [ContextMenu("Center Pivot To Child Renderers")]
    public void CenterPivotToChildRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"{name}: No child renderers found.");
            return;
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 visualCenter = bounds.center;

        // Save all direct children world transforms.
        Transform[] children = new Transform[transform.childCount];
        Vector3[] childPositions = new Vector3[transform.childCount];
        Quaternion[] childRotations = new Quaternion[transform.childCount];
        Vector3[] childScales = new Vector3[transform.childCount];

        for (int i = 0; i < transform.childCount; i++)
        {
            children[i] = transform.GetChild(i);
            childPositions[i] = children[i].position;
            childRotations[i] = children[i].rotation;
            childScales[i] = children[i].lossyScale;
        }

        // Move this pivot/root to the visual center.
        transform.position = visualCenter;

        // Restore children world positions/rotations so the mesh does not visually move.
        for (int i = 0; i < children.Length; i++)
        {
            children[i].position = childPositions[i];
            children[i].rotation = childRotations[i];

            // Do not try to restore lossy scale directly; this avoids weird scaling bugs.
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif

        Debug.Log($"{name}: Pivot centered to renderer bounds.");
    }
}