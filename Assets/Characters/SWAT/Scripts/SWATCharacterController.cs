using UnityEngine;

namespace CityPack.Characters
{
    /// <summary>
    /// Heavy-operator movement. Speed eases in and out so the body does not skate.
    /// Animation clips are optional; assign them on SWATAnimationSet.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class SWATCharacterController : MonoBehaviour
    {
        public Animator animator;
        public Transform cameraYaw;
        public SWATAnimationSet clips;
        public float walkSpeed = 1.55f;
        public float runSpeed = 3.3f;
        public float sprintSpeed = 5.1f;
        public float crouchSpeed = 1.15f;
        public float aimSpeed = 1.25f;
        public float acceleration = 5.5f;
        public float deceleration = 7.5f;
        public float rotationSharpness = 9f;
        public float jumpHeight = 1.05f;
        public float gravity = -18f;
        public bool startLocked;

        public float PlanarSpeed { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsAiming { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsShooting { get; private set; }
        public bool IsReloading { get; private set; }
        public Vector3 Velocity { get; private set; }

        CharacterController _controller;
        AudioSource _steps;
        float _vertical;
        float _shootTimer;
        float _reloadTimer;
        float _yaw;
        bool _control = true;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            _yaw = transform.eulerAngles.y;
            if (startLocked)
                _control = false;
            var steps = GameAudio.Clip("Step_Player");
            if (steps != null)
            {
                _steps = gameObject.AddComponent<AudioSource>();
                _steps.clip = steps;
                _steps.loop = true;
                _steps.spatialBlend = 0f;
                _steps.volume = 0f;
                _steps.Play();
            }
            if (Grounding.TryStreet(out _))
            {
                var grounded = Grounding.OnStreet(transform.position, 0.04f);
                _controller.enabled = false;
                transform.position = grounded;
                _controller.enabled = true;
            }
        }

        public void SetControl(bool enabled)
        {
            _control = enabled;
        }

        void Update()
        {
            if (_shootTimer > 0f)
                _shootTimer -= Time.deltaTime;
            if (_reloadTimer > 0f)
                _reloadTimer -= Time.deltaTime;
            IsShooting = _shootTimer > 0f;
            IsReloading = _reloadTimer > 0f;

            var input = Vector2.zero;
            if (_control && !IsReloading)
            {
                input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                if (input.sqrMagnitude > 1f)
                    input.Normalize();
                if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
                    IsCrouching = !IsCrouching;
                IsAiming = Input.GetMouseButton(1);
                IsSprinting = Input.GetKey(KeyCode.LeftShift) && !IsCrouching && !IsAiming && input.y > 0.1f;
                if (IsSprinting)
                    IsCrouching = false;
                if (Input.GetMouseButtonDown(0) && IsAiming)
                    _shootTimer = 0.18f;
                if (Input.GetKeyDown(KeyCode.R))
                    _reloadTimer = 1.4f;
                if (Input.GetKeyDown(KeyCode.F))
                    animator?.SetTrigger("Melee");
            }

            var target = 0f;
            if (input.sqrMagnitude > 0.01f)
            {
                if (IsCrouching)
                    target = crouchSpeed;
                else if (IsAiming)
                    target = aimSpeed;
                else if (IsSprinting)
                    target = sprintSpeed;
                else if (input.magnitude > 0.75f)
                    target = runSpeed;
                else
                    target = walkSpeed;
                target *= input.magnitude;
            }

            var rate = target > PlanarSpeed ? acceleration : deceleration;
            PlanarSpeed = Mathf.MoveTowards(PlanarSpeed, target, rate * Time.deltaTime);

            var yawSource = cameraYaw != null ? cameraYaw.eulerAngles.y : transform.eulerAngles.y;
            if (input.sqrMagnitude > 0.01f)
            {
                var moveYaw = yawSource + Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
                _yaw = Mathf.LerpAngle(_yaw, moveYaw, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
            }
            else if (IsAiming)
            {
                _yaw = Mathf.LerpAngle(_yaw, yawSource, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
            }

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            IsGrounded = _controller.isGrounded;
            if (IsGrounded && _vertical < 0f)
            {
                if (_vertical < -3.5f)
                    animator?.SetTrigger("Land");
                _vertical = -2f;
            }

            if (_control && IsGrounded && Input.GetKeyDown(KeyCode.Space) && !IsCrouching && !IsReloading)
                _vertical = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _vertical += gravity * Time.deltaTime;

            var world = transform.forward * PlanarSpeed;
            world.y = _vertical;
            _controller.Move(world * Time.deltaTime);
            Velocity = world;
            if (_steps != null)
            {
                var moving = IsGrounded && PlanarSpeed > 0.2f;
                _steps.volume = moving ? Mathf.Lerp(0.15f, 0.45f, PlanarSpeed / sprintSpeed) : 0f;
                _steps.pitch = Mathf.Lerp(0.85f, 1.15f, PlanarSpeed / sprintSpeed);
            }
            PushAnimator(input);
        }

        void PushAnimator(Vector2 input)
        {
            if (animator == null)
                return;
            animator.SetFloat("Speed", PlanarSpeed);
            animator.SetFloat("MoveX", input.x);
            animator.SetFloat("MoveY", input.y);
            animator.SetFloat("Direction", Mathf.Clamp(Mathf.DeltaAngle(transform.eulerAngles.y, _yaw) / 45f, -1f, 1f));
            animator.SetFloat("VerticalVelocity", _vertical);
            animator.SetBool("IsGrounded", IsGrounded);
            animator.SetBool("IsCrouching", IsCrouching);
            animator.SetBool("IsSprinting", IsSprinting);
            animator.SetBool("IsAiming", IsAiming);
            animator.SetBool("IsShooting", IsShooting);
            animator.SetBool("IsReloading", IsReloading);
        }

        public void DebugTrigger(string trigger)
        {
            animator?.SetTrigger(trigger);
        }
    }
}
