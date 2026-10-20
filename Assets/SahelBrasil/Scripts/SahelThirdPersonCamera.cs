using UnityEngine;

namespace SahelBrasil
{
    /// <summary>
    /// Orbit camera with a short sphere cast so the view stays outside walls.
    /// </summary>
    public class SahelThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 pivotOffset = new Vector3(0f, 1.45f, 0f);
        public float distance = 4.4f;
        public float lookSensitivity = 2.3f;
        public float minPitch = -28f;
        public float maxPitch = 58f;
        public float minDistance = 2.1f;
        public float maxDistance = 7f;

        float _yaw = 0f;
        float _pitch = 14f;

        public Quaternion YawRotation => Quaternion.Euler(0f, _yaw, 0f);

        void Start()
        {
            if (target != null)
                _yaw = target.eulerAngles.y;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || target == null)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                var locked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = locked;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                _yaw += Input.GetAxis("Mouse X") * lookSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
                distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * 2.2f, minDistance, maxDistance);
            }

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var pivot = target.position + pivotOffset;
            var direction = rotation * Vector3.back;
            var desiredDistance = distance;
            if (Physics.SphereCast(pivot, 0.18f, direction, out var hit, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != target && !hit.transform.IsChildOf(target))
                    desiredDistance = Mathf.Max(0.45f, hit.distance - 0.12f);
            }

            transform.position = pivot + direction * desiredDistance;
            transform.rotation = rotation;
        }
    }
}
