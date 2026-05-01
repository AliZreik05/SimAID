using UnityEngine;
using UnityEngine.XR;

[DisallowMultipleComponent]
public class VRTrackedCameraPose : MonoBehaviour
{
    private void LateUpdate()
    {
        if (TryApplyNodePose(XRNode.CenterEye))
            return;

        TryApplyNodePose(XRNode.Head);
    }

    private bool TryApplyNodePose(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position);
        bool hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation);

        if (!hasPosition && !hasRotation)
            return false;

        if (hasPosition)
            transform.localPosition = position;

        if (hasRotation)
            transform.localRotation = rotation;

        return true;
    }
}
