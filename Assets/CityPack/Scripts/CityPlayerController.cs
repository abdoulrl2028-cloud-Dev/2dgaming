using UnityEngine;

namespace CityPack
{
    /// <summary>
    /// Walks the city with the built-in CharacterController. WASD and mouse.
    /// The visible capsule is only a stand-in; the City Pack has no character mesh.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class CityPlayerController : MonoBehaviour
    {
        public Camera followCamera;
        public Transform lookPivot;
        public Vector3 cameraLocalOffset = new Vector3(0f, 0.35f, -4f);
        public float walkSpeed = 4.2f;
        public float sprintSpeed = 7f;
        public float lookSensitivity = 2.2f;
        public float pitchLimit = 70f;

        CharacterController _controller;
        float _pitch;
        float _vertical;
        bool _control;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public void SetControl(bool enabled)
        {
            _control = enabled;
            if (followCamera != null)
            {
                followCamera.enabled = enabled;
                if (followCamera.TryGetComponent<AudioListener>(out var listener))
                    listener.enabled = enabled;
            }

            if (!Application.isPlaying)
                return;

            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
            if (enabled)
                _pitch = 12f;
        }

        void Update()
        {
            if (!_control || !Application.isPlaying)
                return;

            var mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
            var mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;
            transform.Rotate(0f, mouseX, 0f);
            _pitch = Mathf.Clamp(_pitch - mouseY, -pitchLimit, pitchLimit);
            if (lookPivot != null)
                lookPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            var speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
            var planar = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            if (planar.sqrMagnitude > 1f)
                planar.Normalize();
            if (_controller.isGrounded && _vertical < 0f)
                _vertical = -2f;
            else
                _vertical += Physics.gravity.y * Time.deltaTime;

            var world = transform.TransformDirection(planar) * speed;
            world.y = _vertical;
            _controller.Move(world * Time.deltaTime);
        }

        void LateUpdate()
        {
            if (!_control || lookPivot == null || followCamera == null)
                return;

            var origin = lookPivot.position;
            var desired = lookPivot.TransformPoint(cameraLocalOffset);
            var delta = desired - origin;
            var distance = delta.magnitude;
            if (distance < 0.01f)
                return;

            var direction = delta / distance;
            if (Physics.SphereCast(origin, 0.2f, direction, out var hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                followCamera.transform.position = origin + direction * Mathf.Max(0.45f, hit.distance - 0.2f);
            else
                followCamera.transform.localPosition = cameraLocalOffset;
        }
    }
}
