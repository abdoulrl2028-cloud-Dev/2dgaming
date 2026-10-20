using UnityEngine;

namespace CityPack.Characters
{
    /// <summary>
    /// Pose fallback used only while the Animator state has no authored clip.
    /// It does not create animation files.
    /// </summary>
    [DisallowMultipleComponent]
    public class HumanoidProceduralLocomotion : MonoBehaviour
    {
        public Animator animator;
        public float strideLength = 1.35f;
        public float legSwing = 28f;
        public float armSwing = 22f;
        public float spineLean = 6f;
        public float headYawLimit = 70f;
        public float headPitchLimit = 35f;
        public Transform lookTarget;
        public Transform weaponRoot;

        float _phase;
        float _yawLag;
        float _equipmentLag;
        float _bob;
        Quaternion _spine0;
        Quaternion _chest0;
        Quaternion _head0;
        Quaternion _lUpper0;
        Quaternion _rUpper0;
        Quaternion _lLower0;
        Quaternion _rLower0;
        Quaternion _lArm0;
        Quaternion _rArm0;
        Quaternion _lFore0;
        Quaternion _rFore0;
        bool _cached;

        public bool HasAuthoredClip
        {
            get
            {
                if (animator == null || animator.runtimeAnimatorController == null)
                    return false;
                var clips = animator.GetCurrentAnimatorClipInfo(0);
                return clips != null && clips.Length > 0 && clips[0].clip != null && clips[0].weight > 0.2f;
            }
        }

        void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        void LateUpdate()
        {
            if (animator == null || !animator.isHuman || !animator.enabled)
                return;

            CacheBind();
            var groundedSpeed = 0f;
            if (animator.GetFloat("Speed") > 0f)
                groundedSpeed = animator.GetFloat("Speed");

            var turn = animator.GetFloat("Direction");
            _yawLag = Mathf.Lerp(_yawLag, Mathf.Clamp(turn, -1f, 1f) * spineLean, 1f - Mathf.Exp(-3f * Time.deltaTime));
            _equipmentLag = Mathf.Lerp(_equipmentLag, _yawLag * 1.4f, 1f - Mathf.Exp(-2.2f * Time.deltaTime));
            _bob = Mathf.Lerp(_bob, groundedSpeed > 0.15f ? Mathf.Sin(_phase * 2f) * 0.015f * groundedSpeed : Mathf.Sin(Time.time * 1.4f) * 0.004f, 0.2f);

            LookHead();
            PlantFeet();

            if (HasAuthoredClip)
            {
                ApplyEquipmentLag();
                return;
            }

            if (groundedSpeed > 0.05f)
                _phase += groundedSpeed * Time.deltaTime / Mathf.Max(0.4f, strideLength);
            else
                _phase = Mathf.Lerp(_phase, Mathf.Round(_phase), 1f - Mathf.Exp(-6f * Time.deltaTime));

            var swing = Mathf.Sin(_phase * Mathf.PI * 2f);
            var crouch = animator.GetBool("IsCrouching") ? 0.55f : 1f;
            Pose(HumanBodyBones.LeftUpperLeg, _lUpper0, swing * legSwing * crouch);
            Pose(HumanBodyBones.RightUpperLeg, _rUpper0, -swing * legSwing * crouch);
            Pose(HumanBodyBones.LeftLowerLeg, _lLower0, Mathf.Max(0f, -swing) * 35f * crouch);
            Pose(HumanBodyBones.RightLowerLeg, _rLower0, Mathf.Max(0f, swing) * 35f * crouch);
            Pose(HumanBodyBones.LeftUpperArm, _lArm0, -swing * armSwing);
            Pose(HumanBodyBones.RightUpperArm, _rArm0, swing * armSwing * 0.35f);
            Pose(HumanBodyBones.LeftLowerArm, _lFore0, 12f);
            Pose(HumanBodyBones.RightLowerArm, _rFore0, 18f);
            Pose(HumanBodyBones.Spine, _spine0, _bob * 40f + _yawLag);
            Pose(HumanBodyBones.Chest, _chest0, -_yawLag * 0.4f);
            ApplyEquipmentLag();
        }

        void CacheBind()
        {
            if (_cached)
                return;
            _spine0 = Local(HumanBodyBones.Spine);
            _chest0 = Local(HumanBodyBones.Chest);
            _head0 = Local(HumanBodyBones.Head);
            _lUpper0 = Local(HumanBodyBones.LeftUpperLeg);
            _rUpper0 = Local(HumanBodyBones.RightUpperLeg);
            _lLower0 = Local(HumanBodyBones.LeftLowerLeg);
            _rLower0 = Local(HumanBodyBones.RightLowerLeg);
            _lArm0 = Local(HumanBodyBones.LeftUpperArm);
            _rArm0 = Local(HumanBodyBones.RightUpperArm);
            _lFore0 = Local(HumanBodyBones.LeftLowerArm);
            _rFore0 = Local(HumanBodyBones.RightLowerArm);
            _cached = true;
        }

        void LookHead()
        {
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null)
                return;
            var aim = lookTarget != null ? lookTarget.forward : transform.forward;
            var local = transform.InverseTransformDirection(aim);
            var yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -headYawLimit, headYawLimit);
            var pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -headPitchLimit, headPitchLimit);
            var desired = _head0 * Quaternion.Euler(pitch, yaw * 0.65f, -yaw * 0.15f);
            head.localRotation = Quaternion.Slerp(head.localRotation, desired, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        void PlantFeet()
        {
            Plant(HumanBodyBones.LeftFoot);
            Plant(HumanBodyBones.RightFoot);
        }

        void Plant(HumanBodyBones bone)
        {
            var foot = animator.GetBoneTransform(bone);
            if (foot == null)
                return;
            var origin = foot.position + Vector3.up * 0.4f;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 0.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;
            var sink = foot.position.y - hit.point.y;
            if (sink > 0.02f && sink < 0.25f)
                foot.position += Vector3.up * sink;
            else if (sink < -0.02f && sink > -0.2f)
                foot.position += Vector3.up * sink;
        }

        void ApplyEquipmentLag()
        {
            if (weaponRoot == null || GetComponent<SWATWeaponRig>() != null)
                return;
            var lag = Quaternion.Euler(2f, -_equipmentLag, _equipmentLag * 0.3f);
            weaponRoot.localRotation = Quaternion.Slerp(weaponRoot.localRotation, lag, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        Quaternion Local(HumanBodyBones bone)
        {
            var t = animator.GetBoneTransform(bone);
            return t != null ? t.localRotation : Quaternion.identity;
        }

        void Pose(HumanBodyBones bone, Quaternion bind, float pitch)
        {
            var t = animator.GetBoneTransform(bone);
            if (t == null)
                return;
            t.localRotation = bind * Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
