using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace CityPack.Editor
{
    public static class CityEnvironmentSetup
    {
        public const string ScenePath = "Assets/CityPack/Cinematics/City_Cinematic.unity";
        const string ProfilePath = "Assets/CityPack/Cinematics/City_PostProfile.asset";
        const string SunsetSkyPath = "Assets/CityPack/Lighting/Sky_Sunset.mat";
        const string GlassPath = "Assets/CityPack/Materials/M_Glass.mat";
        const string ReportPath = "Assets/CityPack/SetupReport.txt";

        [MenuItem("Tools/City Pack/Setup Environment")]
        public static void SetupEnvironment()
        {
            RebuildAll();
        }

        [MenuItem("Tools/City Pack/Setup Lighting")]
        public static void SetupLightingMenu()
        {
            var scene = OpenCinematicScene();
            var built = FindExisting(scene);
            if (built.Root == null)
            {
                RebuildAll();
                return;
            }

            var report = new StringBuilder();
            SetupLighting(built, report);
            SaveReport(report);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Tools/City Pack/Setup Cinematic")]
        public static void SetupCinematicMenu()
        {
            var scene = OpenCinematicScene();
            var built = FindExisting(scene);
            if (built.Root == null)
            {
                RebuildAll();
                return;
            }

            var report = new StringBuilder();
            SetupCinematic(built, report);
            SaveReport(report);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Tools/City Pack/Optimize Environment")]
        public static void OptimizeEnvironment()
        {
            var report = new StringBuilder();
            report.AppendLine("Optimize " + DateTime.Now.ToString("u"));
            var scene = OpenCinematicScene();
            var city = GameObject.Find("City");
            if (city != null)
            {
                CityBlockBuilder.EnsureColliders(city);
                CityBlockBuilder.MarkStatic(city);
            }

            EnableInstancing(report);
            ConfigureQuality(city);
            try
            {
                StaticOcclusionCulling.Compute();
                report.AppendLine("Occlusion culling: baked.");
            }
            catch (Exception exception)
            {
                report.AppendLine("Occlusion culling skipped: " + exception.Message);
            }

            SaveReport(report);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("City Pack optimize finished. See " + ReportPath);
        }

        [MenuItem("Tools/City Pack/Apply Day")]
        public static void ApplyDay()
        {
            ApplyPreset(CityTimePreset.Day);
        }

        [MenuItem("Tools/City Pack/Apply Sunset")]
        public static void ApplySunset()
        {
            ApplyPreset(CityTimePreset.Sunset);
        }

        [MenuItem("Tools/City Pack/Apply Night")]
        public static void ApplyNight()
        {
            ApplyPreset(CityTimePreset.Night);
        }

        public static void SetupAllBatch()
        {
            RebuildAll();
            EditorApplication.Exit(0);
        }

        static void RebuildAll()
        {
            EnsureFolders();
            var report = new StringBuilder();
            report.AppendLine("City Pack setup " + DateTime.Now.ToString("u"));
            report.AppendLine("Pipeline: Built-in. Unity " + Application.unityVersion);
            report.AppendLine("Original POLYGON city pack files were not moved or deleted.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var built = CityBlockBuilder.Build(report);
            if (built.Root == null)
            {
                SaveReport(report);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.LogError("City Pack setup failed. See " + ReportPath);
                return;
            }

            PrepareSurfaces(built, report);
            var player = CreatePlayer(built);
            var npc = CreateNpc(built);
            SetupLighting(built, report);
            SetupCinematic(built, report, player, npc);
            BakeNavigation(built, npc, report);
            ConfigureQuality(built.Root);
            CreateSafetyGround(built);

            var styler = built.Root.GetComponent<CitySurfaceStyler>();
            if (styler == null)
                styler = built.Root.AddComponent<CitySurfaceStyler>();
            styler.Apply();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            try
            {
                StaticOcclusionCulling.Compute();
                report.AppendLine("Occlusion culling: baked.");
            }
            catch (Exception exception)
            {
                report.AppendLine("Occlusion culling skipped: " + exception.Message);
            }

            EnableInstancing(report);
            EditorSceneManager.SaveScene(scene);
            SaveReport(report);
            AssetDatabase.SaveAssets();
            Debug.Log("City Pack environment ready: " + ScenePath);
        }

        static void SetupLighting(CityBuildResult built, StringBuilder report)
        {
            DestroyIfExists("Lighting");
            var root = new GameObject("Lighting");
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root.transform, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            RenderSettings.sun = sun;

            var day = AssetDatabase.LoadAssetAtPath<Material>("Assets/POLYGON city pack/Materials/Day.mat");
            var night = AssetDatabase.LoadAssetAtPath<Material>("Assets/POLYGON city pack/Materials/Night.mat");
            var sunset = CreateSunsetSky(day);

            var probeGo = new GameObject("CityReflectionProbe");
            probeGo.transform.SetParent(root.transform, false);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;
            probe.boxProjection = true;
            probe.hdr = true;
            probe.importance = 1;
            if (built.HasBounds)
            {
                probeGo.transform.position = built.Bounds.center;
                probe.size = built.Bounds.size + new Vector3(12f, 20f, 12f);
                probe.center = Vector3.zero;
            }

            var probes = new GameObject("LightProbeGrid");
            probes.transform.SetParent(root.transform, false);
            var group = probes.AddComponent<LightProbeGroup>();
            group.probePositions = BuildProbeGrid(built);

            var volumeGo = new GameObject("CityPostVolume");
            volumeGo.transform.SetParent(root.transform, false);
            var volume = volumeGo.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.priority = 0;
            volume.sharedProfile = CreateProfile();

            var controller = root.AddComponent<CityLightingController>();
            controller.sun = sun;
            controller.daySky = day;
            controller.nightSky = night;
            controller.sunsetSky = sunset;
            controller.streetLights = built.StreetLights.ToArray();
            controller.accentLights = built.AccentLights.ToArray();
            controller.windowRenderers = built.Windows.ToArray();
            controller.reflectionProbes = new[] { probe };
            controller.volume = volume;
            controller.preset = CityTimePreset.Day;
            controller.Apply(CityTimePreset.Day);
            report.AppendLine("Lighting presets: Day, Sunset, Night.");
            report.AppendLine("Reflection probe: realtime, via scripting, 128.");
            report.AppendLine("Light probes placed. Bake them from Tools only if you need indirect GI. Realtime sun already lights the scene.");
        }

        static void SetupCinematic(CityBuildResult built, StringBuilder report)
        {
            SetupCinematic(built, report, null, null);
        }

        static void SetupCinematic(CityBuildResult built, StringBuilder report, CityPlayerController player, CitySidewalkNpc npc)
        {
            DestroyIfExists("Cinematics");
            var root = new GameObject("Cinematics");
            var path = new GameObject("Path");
            path.transform.SetParent(root.transform, false);

            var eye = built.EyeHeight;
            var center = built.HasBounds ? built.Bounds.center : Vector3.zero;
            var size = built.HasBounds ? built.Bounds.size : new Vector3(40f, 10f, 40f);
            var overview = Marker(path.transform, "Overview", center + new Vector3(0f, Mathf.Max(18f, size.y + 24f), -size.z * 0.55f));
            var approach = Marker(path.transform, "Approach", center + new Vector3(-built.StreetHalfWidth * 0.2f, eye * 6f, -size.z * 0.15f));
            var street = Marker(path.transform, "Street", built.StreetStart + new Vector3(built.StreetHalfWidth * 0.15f, eye, 6f));
            var low = Marker(path.transform, "Low", built.LowPoint + Vector3.up * 0.35f);
            var facade = Marker(path.transform, "Building", built.BuildingFront + new Vector3(0f, eye * 0.8f, 0f));
            var ending = Marker(path.transform, "Interest", built.PoiFront + new Vector3(0f, eye, -4f));

            var lookCenter = Marker(path.transform, "LookCenter", center + Vector3.up * eye);
            var lookStreet = Marker(path.transform, "LookStreet", built.StreetEnd + Vector3.up * eye);
            var lookBuilding = Marker(path.transform, "LookBuilding", built.BuildingFront + Vector3.up * (eye * 2f));
            var lookPoi = Marker(path.transform, "LookPoi", built.PoiFront + Vector3.up * (eye * 1.4f));

            var cinematic = CreateCamera(root.transform, "Cam_Cinematic", overview.position, lookCenter.position, true);
            CreateCamera(root.transform, "Cam_Overview", overview.position, lookCenter.position, false);
            CreateCamera(root.transform, "Cam_Street", street.position, lookStreet.position, false);
            CreateCamera(root.transform, "Cam_Building", facade.position, lookBuilding.position, false);
            CreateCamera(root.transform, "Cam_Low", low.position, lookStreet.position, false);

            var directorGo = new GameObject("CinematicDirector");
            directorGo.transform.SetParent(root.transform, false);
            var director = directorGo.AddComponent<CityCinematicDirector>();
            director.cinematicCamera = cinematic;
            director.waypoints = new[] { overview, approach, street, low, facade, ending };
            director.lookTargets = new[] { lookCenter, lookCenter, lookStreet, lookStreet, lookBuilding, lookPoi };
            director.segmentDurations = new[] { 9f, 8f, 9f, 7f, 8f, 8f };
            director.player = player != null ? player : UnityEngine.Object.FindFirstObjectByType<CityPlayerController>();
            director.npc = npc != null ? npc : UnityEngine.Object.FindFirstObjectByType<CitySidewalkNpc>();
            director.lighting = UnityEngine.Object.FindFirstObjectByType<CityLightingController>();
            director.playOnStart = true;

            var hints = directorGo.AddComponent<CityHudHints>();
            hints.director = director;
            hints.lighting = director.lighting;

            TryAddCinemachineBrain(cinematic, report);
            report.AppendLine("Cinematic cameras: overview, street, building, low, follow, main.");
            report.AppendLine("Sequence: wide city, push in, street, low angle, facade, point of interest.");
        }

        static CityPlayerController CreatePlayer(CityBuildResult built)
        {
            DestroyIfExists("Player");
            var player = new GameObject("Player");
            player.layer = 2;
            var spawn = built.SidewalkPoints.Count > 0 ? built.SidewalkPoints[0] : built.StreetStart;
            player.transform.position = spawn + Vector3.up * 0.1f;
            player.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 45f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "PlayerStandIn";
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            body.layer = 2;

            var pivot = new GameObject("LookPivot");
            pivot.transform.SetParent(player.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            pivot.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);

            var cameraGo = new GameObject("Cam_Follow");
            cameraGo.transform.SetParent(pivot.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0.35f, -4f);
            cameraGo.transform.localRotation = Quaternion.identity;
            var camera = cameraGo.AddComponent<Camera>();
            ConfigureCamera(camera, false);
            cameraGo.AddComponent<AudioListener>().enabled = false;
            AddPostLayer(cameraGo);

            var motor = player.AddComponent<CityPlayerController>();
            motor.followCamera = camera;
            motor.lookPivot = pivot.transform;
            motor.cameraLocalOffset = new Vector3(0f, 0.35f, -4f);
            return motor;
        }

        static CitySidewalkNpc CreateNpc(CityBuildResult built)
        {
            DestroyIfExists("NPC");
            if (built.SidewalkPoints.Count < 2)
                return null;

            var npcGo = new GameObject("NPC");
            var point = built.SidewalkPoints[Mathf.Min(1, built.SidewalkPoints.Count - 1)];
            npcGo.transform.position = point + Vector3.up * 0.1f;
            var agent = npcGo.AddComponent<NavMeshAgent>();
            agent.height = 1.8f;
            agent.radius = 0.3f;
            agent.speed = 1.35f;
            agent.angularSpeed = 180f;
            agent.acceleration = 6f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "NpcStandIn";
            body.transform.SetParent(npcGo.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);

            var npc = npcGo.AddComponent<CitySidewalkNpc>();
            var markers = new List<Transform>();
            var path = new GameObject("NpcPath");
            for (var i = 0; i < built.SidewalkPoints.Count; i += 2)
            {
                var marker = new GameObject("NPC_Spawn_" + i.ToString("00"));
                marker.transform.SetParent(path.transform, false);
                marker.transform.position = built.SidewalkPoints[i] + Vector3.up * 0.1f;
                markers.Add(marker.transform);
            }

            npc.points = markers.ToArray();
            return npc;
        }

        static void BakeNavigation(CityBuildResult built, CitySidewalkNpc npc, StringBuilder report)
        {
            if (built.Root == null)
                return;

            var host = new GameObject("NavMesh");
            host.transform.SetParent(built.Root.transform, false);
            var surface = host.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            try
            {
                surface.BuildNavMesh();
                report.AppendLine("NavMesh: baked from physics colliders.");
            }
            catch (Exception exception)
            {
                report.AppendLine("NavMesh bake failed: " + exception.Message);
                return;
            }

            if (npc == null)
                return;

            if (NavMesh.SamplePosition(npc.transform.position, out var hit, 8f, NavMesh.AllAreas))
            {
                var agent = npc.GetComponent<NavMeshAgent>();
                if (agent != null && agent.Warp(hit.position))
                    report.AppendLine("NPC snapped to NavMesh.");
                else
                    npc.transform.position = hit.position;
            }
            else
            {
                report.AppendLine("NPC could not find a NavMesh point. The agent stays idle.");
            }
        }

        static void PrepareSurfaces(CityBuildResult built, StringBuilder report)
        {
            var glass = CreateGlassMaterial();
            var copies = new Dictionary<Material, Material>();
            var renderers = built.Root.GetComponentsInChildren<Renderer>(true);
            var glassSwaps = 0;
            var windowCopies = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                var materials = renderers[i].sharedMaterials;
                var changed = false;
                for (var m = 0; m < materials.Length; m++)
                {
                    var source = materials[m];
                    if (source == null || source.shader == null)
                        continue;
                    var label = source.name.ToLowerInvariant();
                    if (label.Contains("glass"))
                    {
                        materials[m] = source.mainTexture == null ? glass : TransparentCopy(source, copies);
                        changed = true;
                        glassSwaps++;
                        continue;
                    }

                    if (!label.Contains("window"))
                        continue;

                    if (!copies.TryGetValue(source, out var emissive))
                    {
                        emissive = new Material(source) { name = source.name + "_Emission" };
                        emissive.EnableKeyword("_EMISSION");
                        emissive.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        if (emissive.HasProperty("_EmissionColor"))
                            emissive.SetColor("_EmissionColor", Color.black);
                        emissive.enableInstancing = true;
                        var path = "Assets/CityPack/Materials/" + Sanitize(emissive.name) + ".mat";
                        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                            AssetDatabase.DeleteAsset(path);
                        AssetDatabase.CreateAsset(emissive, path);
                        copies[source] = emissive;
                        windowCopies++;
                    }

                    materials[m] = emissive;
                    changed = true;
                }

                if (changed)
                    renderers[i].sharedMaterials = materials;
            }

            if (!built.Windows.Contains(null))
            {
                built.Windows.Clear();
                for (var i = 0; i < renderers.Length; i++)
                {
                    var materials = renderers[i].sharedMaterials;
                    for (var m = 0; m < materials.Length; m++)
                    {
                        if (materials[m] != null && materials[m].name.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            built.Windows.Add(renderers[i]);
                            break;
                        }
                    }
                }
            }

            report.AppendLine("Glass material swaps: " + glassSwaps);
            report.AppendLine("Window emission copies: " + windowCopies);
            report.AppendLine("No normal, metallic or occlusion maps exist in the City Pack. Smoothness and metal are driven per renderer without editing the original materials.");
        }

        static Material TransparentCopy(Material source, Dictionary<Material, Material> copies)
        {
            if (copies.TryGetValue(source, out var existing))
                return existing;

            var copy = new Material(source) { name = source.name + "_Transparent" };
            copy.SetFloat("_Mode", 3f);
            copy.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            copy.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            copy.SetInt("_ZWrite", 0);
            copy.DisableKeyword("_ALPHATEST_ON");
            copy.EnableKeyword("_ALPHABLEND_ON");
            copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            copy.renderQueue = 3000;
            copy.SetFloat("_Glossiness", 0.9f);
            copy.SetFloat("_Metallic", 0.04f);
            var color = copy.HasProperty("_Color") ? copy.GetColor("_Color") : Color.white;
            color.a = 0.45f;
            copy.SetColor("_Color", color);
            copy.enableInstancing = true;
            var path = "Assets/CityPack/Materials/" + Sanitize(copy.name) + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            copies[source] = copy;
            return copy;
        }

        static Material CreateGlassMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
            if (existing != null)
                return existing;

            var glass = new Material(Shader.Find("Standard")) { name = "M_Glass" };
            glass.SetFloat("_Mode", 3f);
            glass.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glass.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glass.SetInt("_ZWrite", 0);
            glass.DisableKeyword("_ALPHATEST_ON");
            glass.EnableKeyword("_ALPHABLEND_ON");
            glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            glass.renderQueue = 3000;
            glass.SetColor("_Color", new Color(0.62f, 0.78f, 0.84f, 0.28f));
            glass.SetFloat("_Metallic", 0.02f);
            glass.SetFloat("_Glossiness", 0.92f);
            glass.SetFloat("_GlossyReflections", 1f);
            glass.enableInstancing = true;
            AssetDatabase.CreateAsset(glass, GlassPath);
            return glass;
        }

        static Material CreateSunsetSky(Material day)
        {
            if (day == null)
                return null;
            var existing = AssetDatabase.LoadAssetAtPath<Material>(SunsetSkyPath);
            if (existing != null)
                return existing;

            var sunset = new Material(day) { name = "Sky_Sunset" };
            if (sunset.HasProperty("_SkyTint"))
                sunset.SetColor("_SkyTint", new Color(0.95f, 0.38f, 0.16f));
            if (sunset.HasProperty("_GroundColor"))
                sunset.SetColor("_GroundColor", new Color(0.28f, 0.12f, 0.08f));
            if (sunset.HasProperty("_AtmosphereThickness"))
                sunset.SetFloat("_AtmosphereThickness", 1.4f);
            if (sunset.HasProperty("_Exposure"))
                sunset.SetFloat("_Exposure", 1.05f);
            AssetDatabase.CreateAsset(sunset, SunsetSkyPath);
            return sunset;
        }

        static PostProcessProfile CreateProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath);
            if (existing != null)
                return existing;

            var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            var color = profile.AddSettings<ColorGrading>();
            color.enabled.value = true;
            color.gradingMode.Override(GradingMode.HighDefinitionRange);
            color.tonemapper.Override(Tonemapper.ACES);
            color.postExposure.Override(0.25f);
            color.contrast.Override(10f);
            color.saturation.Override(6f);

            var bloom = profile.AddSettings<Bloom>();
            bloom.enabled.value = true;
            bloom.intensity.Override(0.28f);
            bloom.threshold.Override(1.05f);
            bloom.softKnee.Override(0.5f);
            bloom.diffusion.Override(5f);

            var vignette = profile.AddSettings<Vignette>();
            vignette.enabled.value = true;
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.38f);
            vignette.roundness.Override(0.1f);

            var dof = profile.AddSettings<DepthOfField>();
            dof.enabled.value = true;
            dof.focusDistance.Override(18f);
            dof.aperture.Override(5.6f);
            dof.focalLength.Override(55f);

            var blur = profile.AddSettings<MotionBlur>();
            blur.enabled.value = false;
            blur.shutterAngle.Override(140f);
            blur.sampleCount.Override(6);

            var exposure = profile.AddSettings<AutoExposure>();
            exposure.enabled.value = true;
            exposure.keyValue.Override(1f);
            exposure.minLuminance.Override(0f);
            exposure.maxLuminance.Override(1f);
            exposure.eyeAdaptation.Override(EyeAdaptation.Fixed);

            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        static void AddPostLayer(GameObject cameraObject)
        {
            var layer = cameraObject.AddComponent<PostProcessLayer>();
            layer.volumeLayer = ~0;
            layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
            var resources = AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
            if (resources == null)
            {
                var guids = AssetDatabase.FindAssets("t:PostProcessResources");
                if (guids.Length > 0)
                    resources = AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (resources == null)
                return;

            var serialized = new SerializedObject(layer);
            var property = serialized.FindProperty("m_Resources");
            if (property != null)
            {
                property.objectReferenceValue = resources;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static Camera CreateCamera(Transform parent, string name, Vector3 position, Vector3 look, bool enabled)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            if ((look - position).sqrMagnitude > 0.01f)
                go.transform.rotation = Quaternion.LookRotation((look - position).normalized, Vector3.up);
            var camera = go.AddComponent<Camera>();
            ConfigureCamera(camera, enabled);
            var listener = go.AddComponent<AudioListener>();
            listener.enabled = enabled;
            AddPostLayer(go);
            go.tag = enabled ? "MainCamera" : "Untagged";
            return camera;
        }

        static void ConfigureCamera(Camera camera, bool enabled)
        {
            camera.enabled = enabled;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 800f;
            camera.fieldOfView = 42f;
        }

        static Transform Marker(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go.transform;
        }

        static Vector3[] BuildProbeGrid(CityBuildResult built)
        {
            var points = new List<Vector3>();
            if (!built.HasBounds)
            {
                points.Add(new Vector3(0f, 1.5f, 0f));
                return points.ToArray();
            }

            var min = built.Bounds.min;
            var max = built.Bounds.max;
            var step = Mathf.Clamp(built.StreetHalfWidth * 1.5f, 6f, 18f);
            for (var x = min.x; x <= max.x; x += step)
            {
                for (var z = min.z; z <= max.z; z += step)
                    points.Add(new Vector3(x, min.y + built.EyeHeight, z));
            }

            if (points.Count > 80)
                points.RemoveRange(80, points.Count - 80);
            return points.ToArray();
        }

        static void CreateSafetyGround(CityBuildResult built)
        {
            if (!built.HasBounds)
                return;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "SafetyGround";
            var renderer = ground.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = false;
            ground.transform.position = new Vector3(built.Bounds.center.x, built.Bounds.min.y - 0.6f, built.Bounds.center.z);
            ground.transform.localScale = new Vector3(built.Bounds.size.x + 30f, 1f, built.Bounds.size.z + 30f);
        }

        static void ConfigureQuality(GameObject city)
        {
            QualitySettings.pixelLightCount = 8;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowCascades = 4;
            var radius = 80f;
            if (city != null && CityBlockBuilder.TryBounds(city, out var bounds))
                radius = Mathf.Clamp(bounds.extents.magnitude, 40f, 160f);
            QualitySettings.shadowDistance = radius;
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.softVegetation = true;
        }

        static void EnableInstancing(StringBuilder report)
        {
            var guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/POLYGON city pack" });
            var changed = 0;
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.enableInstancing)
                    continue;
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                changed++;
            }

            report.AppendLine("GPU instancing enabled on " + changed + " original materials. Colors were not changed.");
        }

        static void TryAddCinemachineBrain(Camera camera, StringBuilder report)
        {
            var brain = FindType("Unity.Cinemachine.CinemachineBrain") ?? FindType("Cinemachine.CinemachineBrain");
            if (brain == null)
            {
                report.AppendLine("Cinemachine brain type was not found. The sequence uses CityCinematicDirector.");
                return;
            }

            if (camera.GetComponent(brain) == null)
                camera.gameObject.AddComponent(brain);
            report.AppendLine("Cinemachine brain added to Cam_Cinematic. The playing sequence is still CityCinematicDirector so the shot stays smooth if the Cinemachine 6 API differs.");
        }

        static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }

        static void EnsureFolders()
        {
            var folders = new[]
            {
                "Assets/CityPack",
                "Assets/CityPack/Models",
                "Assets/CityPack/Materials",
                "Assets/CityPack/Textures",
                "Assets/CityPack/Prefabs",
                "Assets/CityPack/Environment",
                "Assets/CityPack/Lighting",
                "Assets/CityPack/Cinematics",
                "Assets/CityPack/Scenes",
                "Assets/CityPack/Scripts",
                "Assets/CityPack/Editor"
            };
            for (var i = 0; i < folders.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(folders[i]))
                    continue;
                var parent = Path.GetDirectoryName(folders[i])?.Replace("\\", "/");
                var leaf = Path.GetFileName(folders[i]);
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
                    AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        static Scene OpenCinematicScene()
        {
            EnsureFolders();
            if (File.Exists(ScenePath))
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RebuildAll();
            return SceneManager.GetActiveScene();
        }

        static CityBuildResult FindExisting(Scene scene)
        {
            var result = new CityBuildResult();
            var city = GameObject.Find("City");
            result.Root = city;
            if (city != null && CityBlockBuilder.TryBounds(city, out var bounds))
            {
                result.Bounds = bounds;
                result.HasBounds = true;
                result.StreetHalfWidth = Mathf.Max(4f, bounds.extents.x * 0.25f);
                result.EyeHeight = 1.7f;
                result.StreetStart = new Vector3(bounds.center.x, bounds.min.y, bounds.min.z);
                result.StreetEnd = new Vector3(bounds.center.x, bounds.min.y, bounds.max.z);
                result.StreetMid = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                result.PoiFront = result.StreetEnd;
                result.BuildingFront = bounds.center;
                result.LowPoint = result.StreetMid;
            }

            if (scene.IsValid())
            {
                var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (var i = 0; i < lights.Length; i++)
                {
                    if (lights[i].type == LightType.Point)
                        result.StreetLights.Add(lights[i]);
                }
            }

            return result;
        }

        static void ApplyPreset(CityTimePreset preset)
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<CityLightingController>();
            if (controller == null)
            {
                Debug.LogWarning("Open the cinematic scene or run Tools/City Pack/Setup Environment first.");
                return;
            }

            controller.Apply(preset);
            EditorUtility.SetDirty(controller);
            Debug.Log("City preset applied: " + preset);
        }

        static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (var i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                    return;
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void DestroyIfExists(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);
        }

        static void SaveReport(StringBuilder report)
        {
            File.WriteAllText(ReportPath, report.ToString());
            AssetDatabase.ImportAsset(ReportPath);
        }

        static string Sanitize(string name)
        {
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
