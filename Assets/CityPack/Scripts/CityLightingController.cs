using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace CityPack
{
    /// <summary>
    /// Day, sunset and night. Artificial lights stay off in daylight.
    /// Keys: F1 day, F2 sunset, F3 night.
    /// </summary>
    [DisallowMultipleComponent]
    public class CityLightingController : MonoBehaviour
    {
        public Light sun;
        public Light[] streetLights = System.Array.Empty<Light>();
        public Light[] accentLights = System.Array.Empty<Light>();
        public Renderer[] windowRenderers = System.Array.Empty<Renderer>();
        public ReflectionProbe[] reflectionProbes = System.Array.Empty<ReflectionProbe>();
        public Material daySky;
        public Material sunsetSky;
        public Material nightSky;
        public PostProcessVolume volume;
        public CityTimePreset preset = CityTimePreset.Day;
        public bool listenForHotkeys = true;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Start()
        {
            Apply(preset);
        }

        void Update()
        {
            if (!listenForHotkeys || !Application.isPlaying)
                return;

            if (Input.GetKeyDown(KeyCode.F1))
                Apply(CityTimePreset.Day);
            else if (Input.GetKeyDown(KeyCode.F2))
                Apply(CityTimePreset.Sunset);
            else if (Input.GetKeyDown(KeyCode.F3))
                Apply(CityTimePreset.Night);
        }

        public void Apply(CityTimePreset next)
        {
            preset = next;
            switch (next)
            {
                case CityTimePreset.Sunset:
                    ApplySun(new Color(1f, 0.45f, 0.18f), 0.9f, new Vector3(8f, -35f, 0f), 0.9f);
                    ApplyAmbient(0.55f, new Color(0.85f, 0.45f, 0.28f));
                    ApplyFog(new Color(0.95f, 0.48f, 0.28f), 0.0045f);
                    SetSky(sunsetSky != null ? sunsetSky : daySky);
                    SetArtificial(true, 0.55f, new Color(1f, 0.72f, 0.42f));
                    SetWindows(new Color(1f, 0.62f, 0.28f) * 0.65f);
                    SetGrade(0.15f, 12f, 8f, 0.45f);
                    break;
                case CityTimePreset.Night:
                    ApplySun(new Color(0.55f, 0.68f, 1f), 0.07f, new Vector3(42f, -20f, 0f), 0.35f);
                    ApplyAmbient(0.18f, new Color(0.18f, 0.24f, 0.38f));
                    ApplyFog(new Color(0.05f, 0.07f, 0.12f), 0.008f);
                    SetSky(nightSky != null ? nightSky : daySky);
                    SetArtificial(true, 1f, new Color(1f, 0.82f, 0.55f));
                    SetWindows(new Color(1f, 0.78f, 0.45f) * 1.35f);
                    SetGrade(-0.15f, 6f, 4f, 0.85f);
                    break;
                default:
                    ApplySun(new Color(1f, 0.96f, 0.88f), 1.15f, new Vector3(48f, -28f, 0f), 0.82f);
                    ApplyAmbient(1f, Color.white);
                    ApplyFog(new Color(0.72f, 0.8f, 0.88f), 0.0018f);
                    SetSky(daySky);
                    SetArtificial(false, 0f, Color.black);
                    SetWindows(Color.black);
                    SetGrade(0.25f, 10f, 6f, 0.28f);
                    break;
            }

            RenderProbes();
        }

        public void SetFocusDistance(float distance)
        {
            if (volume == null || volume.profile == null)
                return;
            var dof = volume.profile.GetSetting<DepthOfField>();
            if (dof == null)
                return;
            dof.focusDistance.value = Mathf.Max(1.5f, distance);
        }

        public void SetCinematicEffects(bool enabled)
        {
            if (volume == null || volume.profile == null)
                return;

            var dof = volume.profile.GetSetting<DepthOfField>();
            if (dof != null)
                dof.active = enabled;
            var blur = volume.profile.GetSetting<MotionBlur>();
            if (blur != null)
                blur.active = enabled;
        }

        void ApplySun(Color color, float intensity, Vector3 euler, float shadowStrength)
        {
            if (sun == null)
                return;
            sun.color = color;
            sun.intensity = intensity;
            sun.transform.rotation = Quaternion.Euler(euler);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = shadowStrength;
        }

        void ApplyAmbient(float intensity, Color skyColor)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.ambientSkyColor = skyColor * intensity;
            RenderSettings.ambientEquatorColor = Color.Lerp(skyColor, new Color(0.45f, 0.42f, 0.38f), 0.45f) * intensity;
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.1f) * intensity;
        }

        void ApplyFog(Color color, float density)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = density;
        }

        void SetSky(Material sky)
        {
            if (sky != null)
                RenderSettings.skybox = sky;
        }

        void SetArtificial(bool enabled, float scale, Color color)
        {
            SetGroup(streetLights, enabled, scale, color, 1.4f);
            SetGroup(accentLights, enabled, scale, color, 1.8f);
        }

        static void SetGroup(IReadOnlyList<Light> lights, bool enabled, float scale, Color color, float baseIntensity)
        {
            if (lights == null)
                return;
            for (var i = 0; i < lights.Count; i++)
            {
                var light = lights[i];
                if (light == null)
                    continue;
                light.enabled = enabled;
                light.color = color;
                light.intensity = baseIntensity * scale;
                light.shadows = LightShadows.None;
            }
        }

        void SetWindows(Color emission)
        {
            if (windowRenderers == null)
                return;
            for (var i = 0; i < windowRenderers.Length; i++)
            {
                var renderer = windowRenderers[i];
                if (renderer == null)
                    continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                var flicker = 0.82f + (i % 5) * 0.045f;
                block.SetColor(EmissionId, emission * flicker);
                renderer.SetPropertyBlock(block);
            }
        }

        void SetGrade(float exposure, float contrast, float saturation, float bloom)
        {
            if (volume == null || volume.sharedProfile == null)
                return;
            var profile = volume.profile;
            var color = profile.GetSetting<ColorGrading>();
            if (color != null)
            {
                color.postExposure.value = exposure;
                color.contrast.value = contrast;
                color.saturation.value = saturation;
            }

            var bloomSetting = profile.GetSetting<Bloom>();
            if (bloomSetting != null)
                bloomSetting.intensity.value = bloom;
        }

        void RenderProbes()
        {
            if (reflectionProbes == null)
                return;
            for (var i = 0; i < reflectionProbes.Length; i++)
            {
                var probe = reflectionProbes[i];
                if (probe != null && probe.isActiveAndEnabled)
                    probe.RenderProbe();
            }
        }
    }
}
