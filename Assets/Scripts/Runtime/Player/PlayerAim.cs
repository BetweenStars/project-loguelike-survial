using UnityEngine;

public class PlayerAim : MonoBehaviour
{
    [Tooltip("The output Camera with CinemachineBrain (usually Main Camera), not the Cinemachine Camera.")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private RectTransform crosshair;
    [SerializeField] private LayerMask aimMask = ~0;
    [SerializeField, Min(1)] private float aimDistance = 150f;
    public Camera AimCamera => aimCamera != null ? aimCamera : Camera.main;
    public Ray GetRay()
    {
        Camera camera = AimCamera;
        if (camera == null)
            return new Ray(transform.position, transform.forward);
        Vector2 screenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (crosshair != null)
        {
            Canvas canvas = crosshair.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, crosshair.position);
        }
        return camera.ScreenPointToRay(screenPoint);
    }

    public Vector3 GetAimPoint()
    {
        Ray ray = GetRay();
        RaycastHit[] hits = Physics.RaycastAll(ray, aimDistance, aimMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
            if (!hit.transform.IsChildOf(transform))
                return hit.point;
        return ray.GetPoint(aimDistance);
    }
}
