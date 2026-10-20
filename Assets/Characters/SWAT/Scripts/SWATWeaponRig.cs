using UnityEngine;

namespace CityPack.Characters
{
    /// <summary>
    /// Holds the equipped weapon in front of the body and plants both hands on it.
    /// Aiming raises the weapon toward the camera. Recoil stays short.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class SWATWeaponRig : MonoBehaviour
    {
        public SWATCharacterController body;
        public Animator animator;
        public Transform weaponRoot;
        public Transform rightHandTarget;
        public Transform leftHandTarget;
        public Transform muzzle;
        public Light muzzleFlash;
        public AudioSource shotSource;
        public AudioClip shotClip;
        public float recoilPitch = 2.2f;
        public float recoilReturn = 14f;
        public Vector3 carryLocal = new Vector3(0.18f, 1.18f, 0.32f);
        public Vector3 aimLocal = new Vector3(0.1f, 1.4f, 0.4f);
        public float loweredPitch = 16f;

        float _kick;
        Vector3 _position;
        Quaternion _rotation;
        bool _posed;

        void Awake()
        {
            if (weaponRoot == null)
                return;
            if (shotClip == null)
                shotClip = GameAudio.Clip("Shot");
            if (muzzle == null)
                muzzle = FindNamed(weaponRoot, "Muzzle");
            if (rightHandTarget == null)
                rightHandTarget = FindNamed(weaponRoot, "RightHandTarget");
            if (leftHandTarget == null)
                leftHandTarget = FindNamed(weaponRoot, "LeftHandTarget");
        }

        static Transform FindNamed(Transform root, string childName)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name == childName)
                    return all[i];
            }

            return null;
        }

        void LateUpdate()
        {
            if (body == null || weaponRoot == null)
                return;
            if (animator == null)
                animator = body.animator;

            var aiming = body.IsAiming;
            var desiredPos = transform.TransformPoint(aiming ? aimLocal : carryLocal);
            var forward = aiming && body.cameraYaw != null ? body.cameraYaw.forward : transform.forward;
            if (forward.sqrMagnitude < 0.001f)
                forward = transform.forward;
            var desiredRot = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(aiming ? 0f : loweredPitch, aiming ? 0f : 10f, 0f);

            if (body.IsShooting && _kick < 0.2f)
            {
                _kick = 1f;
                if (muzzleFlash != null)
                    muzzleFlash.enabled = true;
                if (shotSource != null && shotClip != null)
                    shotSource.PlayOneShot(shotClip);
            }

            _kick = Mathf.MoveTowards(_kick, 0f, recoilReturn * Time.deltaTime);
            desiredRot *= Quaternion.Euler(-recoilPitch * _kick, 0f, 0f);
            if (muzzleFlash != null && _kick <= 0.05f)
                muzzleFlash.enabled = false;

            var blend = 1f - Mathf.Exp(-10f * Time.deltaTime);
            if (!_posed)
            {
                _position = desiredPos;
                _rotation = desiredRot;
                _posed = true;
            }

            _position = Vector3.Lerp(_position, desiredPos, blend);
            _rotation = Quaternion.Slerp(_rotation, desiredRot, blend);
            weaponRoot.SetPositionAndRotation(_position, _rotation);

            if (animator == null || !animator.isHuman)
                return;

            var rightWrist = rightHandTarget != null
                ? rightHandTarget.position - weaponRoot.forward * 0.05f
                : weaponRoot.position;
            var leftWrist = leftHandTarget != null
                ? leftHandTarget.position - weaponRoot.forward * 0.04f
                : weaponRoot.position - weaponRoot.right * 0.08f;
            SolveArm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, rightWrist, transform.right);
            SolveArm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, leftWrist, -transform.right);
            AlignGrip(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightThumbProximal);
            AlignGrip(HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftThumbProximal);
        }

        void SolveArm(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Vector3 target, Vector3 outDirection)
        {
            var upper = animator.GetBoneTransform(upperBone);
            var lower = animator.GetBoneTransform(lowerBone);
            var hand = animator.GetBoneTransform(handBone);
            if (upper == null || lower == null || hand == null)
                return;

            var upperLen = Vector3.Distance(upper.position, lower.position);
            var lowerLen = Vector3.Distance(lower.position, hand.position);
            if (upperLen < 0.01f || lowerLen < 0.01f)
                return;

            var shoulder = upper.position;
            var toTarget = target - shoulder;
            var reach = upperLen + lowerLen - 0.01f;
            var dist = Mathf.Clamp(toTarget.magnitude, 0.05f, reach);
            var aimDir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
            var pole = shoulder + outDirection * 0.45f - transform.up * 0.25f - transform.forward * 0.05f;
            var bendAxis = Vector3.Cross(aimDir, (pole - shoulder).normalized);
            if (bendAxis.sqrMagnitude < 0.0001f)
                bendAxis = transform.right;
            bendAxis.Normalize();

            var cosUpper = Mathf.Clamp((upperLen * upperLen + dist * dist - lowerLen * lowerLen) / (2f * upperLen * dist), -1f, 1f);
            var bend = Mathf.Acos(cosUpper) * Mathf.Rad2Deg;
            var positive = Quaternion.AngleAxis(bend, bendAxis) * aimDir;
            var negative = Quaternion.AngleAxis(-bend, bendAxis) * aimDir;
            var upperDir = Vector3.Distance(shoulder + positive * upperLen, pole) < Vector3.Distance(shoulder + negative * upperLen, pole)
                ? positive
                : negative;

            var currentUpper = (lower.position - upper.position).normalized;
            upper.rotation = Quaternion.FromToRotation(currentUpper, upperDir) * upper.rotation;
            var currentLower = (hand.position - lower.position).normalized;
            var desiredLower = ((shoulder + aimDir * dist) - lower.position).normalized;
            if (desiredLower.sqrMagnitude > 0.0001f && currentLower.sqrMagnitude > 0.0001f)
                lower.rotation = Quaternion.FromToRotation(currentLower, desiredLower) * lower.rotation;
        }

        void AlignGrip(HumanBodyBones handBone, HumanBodyBones fingerBone, HumanBodyBones thumbBone)
        {
            var hand = animator.GetBoneTransform(handBone);
            var finger = animator.GetBoneTransform(fingerBone);
            if (hand == null || finger == null || weaponRoot == null)
                return;

            var barrel = weaponRoot.forward;
            var fingerDir = finger.position - hand.position;
            if (fingerDir.sqrMagnitude > 0.0001f && barrel.sqrMagnitude > 0.0001f)
                hand.rotation = Quaternion.FromToRotation(fingerDir.normalized, barrel) * hand.rotation;

            var thumb = animator.GetBoneTransform(thumbBone);
            if (thumb == null)
                return;
            var thumbDir = Vector3.ProjectOnPlane(thumb.position - hand.position, barrel);
            var up = Vector3.ProjectOnPlane(weaponRoot.up, barrel);
            if (thumbDir.sqrMagnitude < 0.0001f || up.sqrMagnitude < 0.0001f)
                return;
            var twist = Vector3.SignedAngle(thumbDir, up, barrel);
            hand.rotation = Quaternion.AngleAxis(twist, barrel) * hand.rotation;
        }
    }
}
