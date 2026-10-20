using Unity.Cinemachine;
using UnityEngine;

namespace CityPack.Characters
{
    public class SWATCameraRig : MonoBehaviour
    {
        public SWATCharacterController body;
        public CinemachineCamera mainCamera;
        public CinemachineCamera aimCamera;
        public CinemachineCamera cinematicCamera;
        public float walkFov = 52f;
        public float sprintFov = 58f;
        public float aimFov = 40f;
        public float recoil = 0.6f;

        float _kick;
        CinemachineThirdPersonFollow _mainFollow;
        CinemachineThirdPersonFollow _aimFollow;

        void Awake()
        {
            if (mainCamera != null)
                _mainFollow = mainCamera.GetComponent<CinemachineThirdPersonFollow>();
            if (aimCamera != null)
                _aimFollow = aimCamera.GetComponent<CinemachineThirdPersonFollow>();
        }

        void Update()
        {
            if (body == null || mainCamera == null)
                return;

            var aiming = body.IsAiming;
            if (aimCamera != null)
            {
                SetPriority(aimCamera, aiming ? 20 : 0);
                SetPriority(mainCamera, aiming ? 0 : 10);
            }

            if (body.IsShooting)
                _kick = recoil;
            _kick = Mathf.Lerp(_kick, 0f, 1f - Mathf.Exp(-10f * Time.deltaTime));

            var sprint = body.IsSprinting ? 1f : 0f;
            var moving = Mathf.Clamp01(body.PlanarSpeed / 3.3f);
            var fov = Mathf.Lerp(walkFov, sprintFov, sprint);
            if (aiming)
                fov = aimFov;
            SetFov(mainCamera, fov);
            if (aimCamera != null)
                SetFov(aimCamera, aimFov);

            if (_mainFollow != null)
            {
                _mainFollow.Damping = Vector3.Lerp(new Vector3(0.25f, 0.35f, 0.25f), new Vector3(0.12f, 0.16f, 0.12f), Mathf.Max(sprint, moving * 0.5f));
                _mainFollow.CameraDistance = Mathf.Lerp(4.1f, 4.6f, sprint);
            }

            if (_aimFollow != null)
                _aimFollow.CameraDistance = 1.7f;

            if (_kick > 0.01f && body.cameraYaw != null)
                body.cameraYaw.localRotation *= Quaternion.Euler(-_kick, 0f, 0f);
        }

        public void SetCinematic(bool enabled)
        {
            if (cinematicCamera == null)
                return;
            SetPriority(cinematicCamera, enabled ? 30 : 0);
            if (body != null)
                body.SetControl(!enabled);
        }

        static void SetPriority(CinemachineCamera camera, int value)
        {
            var priority = camera.Priority;
            priority.Value = value;
            camera.Priority = priority;
        }

        static void SetFov(CinemachineCamera camera, float fov)
        {
            var lens = camera.Lens;
            lens.FieldOfView = fov;
            camera.Lens = lens;
        }
    }
}
