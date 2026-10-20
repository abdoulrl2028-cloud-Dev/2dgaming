using UnityEngine;

namespace SahelBrasil
{
    /// <summary>
    /// Third-person locomotion for the Sahel explorer.
    /// Uses the legacy Input Manager already configured in this project.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class SahelPlayerController : MonoBehaviour
    {
        public SahelThirdPersonCamera view;
        public Animator animator;
        public float walkSpeed = 1.7f;
        public float runSpeed = 4.2f;
        public float rotationSharpness = 12f;
        public float gravity = -20f;
        public float jumpHeight = 0.85f;

        CharacterController _controller;
        float _vertical;

        public bool ControlEnabled { get; set; } = true;
        public bool IsGrounded => _controller != null && _controller.isGrounded;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        void Update()
        {
            if (!ControlEnabled || !Application.isPlaying)
                return;

            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            var yaw = view != null ? view.YawRotation : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            var move = yaw * input;
            var running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var speed = running ? runSpeed : walkSpeed;

            if (move.sqrMagnitude > 0.001f)
            {
                var target = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSharpness * Time.deltaTime);
            }

            if (_controller.isGrounded && _vertical < 0f)
                _vertical = -2.5f;
            if (_controller.isGrounded && Input.GetButtonDown("Jump"))
                _vertical = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _vertical += gravity * Time.deltaTime;

            var velocity = move * speed;
            velocity.y = _vertical;
            _controller.Move(velocity * Time.deltaTime);

            if (animator != null)
            {
                var planar = new Vector3(velocity.x, 0f, velocity.z).magnitude;
                animator.SetFloat("Speed", planar, 0.12f, Time.deltaTime);
            }
        }
    }
}
