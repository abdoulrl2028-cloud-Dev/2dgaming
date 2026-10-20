using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SahelBrasil.Editor
{
    /// <summary>
    /// Builds the phase-1 Sahel hamlet scene from the Blender kit.
    /// Does not open or modify CityPack, character, or UI scenes.
    /// </summary>
    public static class SahelSceneBuilder
    {
        const string Root = "Assets/SahelBrasil";
        const string ScenePath = Root + "/Scenes/Sahel_Aldeia.unity";

        static readonly Dictionary<string, string[]> Slots = new Dictionary<string, string[]>
        {
            { "SM_HutRound", new[] { "M_Laterite", "M_Thatch", "M_Wood" } },
            { "SM_HouseBanco", new[] { "M_Laterite", "M_Wood" } },
            { "SM_Granary", new[] { "M_Laterite", "M_Thatch", "M_Wood", "M_Stone" } },
            { "SM_Well", new[] { "M_Stone", "M_Wood" } },
            { "SM_MarketStall", new[] { "M_Wood", "M_Awning", "M_Laterite" } },
            { "SM_Acacia", new[] { "M_Wood", "M_Leaf" } },
            { "SM_Baobab", new[] { "M_Wood", "M_Leaf" } },
            { "SM_RoadSegment", new[] { "M_Road" } },
            { "SM_CompoundWall", new[] { "M_Laterite" } },
            { "SM_ClayPot", new[] { "M_Laterite" } },
            { "SM_Rock", new[] { "M_Stone" } },
            { "SM_GrassTuft", new[] { "M_DryGrass" } },
            { "SK_Body", new[] { "M_Skin" } },
            { "SK_Clothes", new[] { "M_ClothOchre", "M_ClothDark" } },
            { "SK_Gear", new[] { "M_Hair", "M_Leather", "M_Sandal" } },
            { "SK_Eyes", new[] { "M_Eye" } },
        };

        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        const string WatchKey = "SahelBrasil.WatchPlay";
        const string StartKey = "SahelBrasil.PlayStart";
        const string ShotKey = "SahelBrasil.ShotAt";

        [InitializeOnLoadMethod]
        static void HookPlayWatch()
        {
            EditorApplication.update -= WatchPlay;
            EditorApplication.update += WatchPlay;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Tools/Sahel Brasil/Build Aldeia Scene")]
        public static void Build()
        {
            try
            {
                ConfigureTextures();
                ConfigureModels();
                CreateMaterials();
                var controller = CreateAnimator();
                var terrainData = CreateTerrain();
                EditorSceneManager.SaveOpenScenes();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildScene(scene, controller, terrainData);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("SAHEL_BUILD_OK " + ScenePath);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("SAHEL_BUILD_FAIL " + exception);
                throw;
            }
        }

        [MenuItem("Tools/Sahel Brasil/Build And Play")]
        public static void BuildAndPlay()
        {
            Build();
            SessionState.SetBool(WatchKey, true);
            SessionState.SetFloat(StartKey, -1f);
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.EnterPlaymode();
            };
        }

        static void WatchPlay()
        {
            if (SessionState.GetBool("SahelBrasil.ExitPlay", false))
            {
                if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup - SessionState.GetFloat(ShotKey, 0f) > 1.6)
                {
                    SessionState.SetBool("SahelBrasil.ExitPlay", false);
                    EditorApplication.ExitPlaymode();
                }
                return;
            }

            if (!SessionState.GetBool(WatchKey, false) || !EditorApplication.isPlaying)
                return;
            var started = SessionState.GetFloat(StartKey, -1f);
            if (started < 0f || EditorApplication.timeSinceStartup - started < 3.5)
                return;

            var player = Object.FindAnyObjectByType<SahelPlayerController>();
            var grounded = player != null && player.IsGrounded;
            var position = player != null ? player.transform.position : Vector3.zero;
            Debug.Log("SAHEL_PLAY grounded=" + grounded + " pos=" + position);
            ScreenCapture.CaptureScreenshot("/tmp/sahel_preview/playmode.png");
            SessionState.SetBool(WatchKey, false);
            SessionState.SetBool("SahelBrasil.ExitPlay", true);
            SessionState.SetFloat(ShotKey, (float)EditorApplication.timeSinceStartup);
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(WatchKey, false))
                SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            if (state != PlayModeStateChange.EnteredEditMode)
                return;
            if (SessionState.GetFloat(StartKey, -1f) > 0f)
            {
                SessionState.SetFloat(StartKey, -1f);
                Debug.Log("SAHEL_PLAY_DONE");
            }
        }

        static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var file = Path.GetFileName(path);
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Compressed;
                if (file.StartsWith("N_"))
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.sRGBTexture = false;
                }
                else
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.anisoLevel = 4;
                }
                importer.SaveAndReimport();
            }
        }

        static void ConfigureModels()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { Root + "/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                var character = Path.GetFileName(path).StartsWith("SK_");
                importer.importAnimation = character;
                importer.animationType = character
                    ? ModelImporterAnimationType.Human
                    : ModelImporterAnimationType.None;
                if (character)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.SaveAndReimport();
                    var clips = importer.defaultClipAnimations;
                    foreach (var clip in clips)
                    {
                        clip.loopTime = true;
                        clip.lockRootRotation = true;
                        clip.lockRootHeightY = true;
                        clip.lockRootPositionXZ = true;
                        clip.keepOriginalPositionY = true;
                        clip.keepOriginalPositionXZ = true;
                        if (clip.lastFrame > clip.firstFrame + 1f)
                            clip.lastFrame -= 1f;
                    }
                    importer.clipAnimations = clips;
                }
                importer.SaveAndReimport();
            }
        }

        static void CreateMaterials()
        {
            Materials.Clear();
            var shader = Shader.Find("Standard");
            if (shader == null)
                throw new System.InvalidOperationException("Standard shader missing. Built-in pipeline is required.");

            CreateMaterial("M_Laterite", "T_Laterite", shader, 0.08f, false);
            CreateMaterial("M_DryGrass", "T_DryGrass", shader, 0.06f, true);
            CreateMaterial("M_Sand", "T_Sand", shader, 0.1f, false);
            CreateMaterial("M_Thatch", "T_Thatch", shader, 0.05f, false);
            CreateMaterial("M_Wood", "T_Wood", shader, 0.18f, false);
            CreateMaterial("M_ClothOchre", "T_ClothOchre", shader, 0.16f, false);
            CreateMaterial("M_ClothDark", "T_ClothDark", shader, 0.14f, false);
            CreateMaterial("M_ClothIndigo", "T_ClothIndigo", shader, 0.14f, false);
            CreateMaterial("M_Skin", "T_Skin", shader, 0.28f, false);
            CreateMaterial("M_Leaf", "T_Leaf", shader, 0.12f, true);
            CreateMaterial("M_Road", "T_Road", shader, 0.05f, false);
            CreateMaterial("M_Leather", "T_Leather", shader, 0.22f, false);
            CreateMaterial("M_Hair", "T_Hair", shader, 0.12f, false);
            CreateMaterial("M_Stone", "T_Stone", shader, 0.16f, false);
            CreateMaterial("M_Awning", "T_Awning", shader, 0.2f, true);
            CreateMaterial("M_Sandal", "T_Sandal", shader, 0.14f, false);
            CreateMaterial("M_Eye", "T_Hair", shader, 0.4f, false);
        }

        static void CreateMaterial(string name, string texture, Shader shader, float smoothness, bool doubleSided)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            var material = new Material(shader) { name = name };
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + texture + ".png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/N_" + texture + ".png");
            material.SetTexture("_MainTex", albedo);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", smoothness);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale", 0.55f);
            }
            if (doubleSided)
                material.SetInt("_Cull", 0);
            AssetDatabase.CreateAsset(material, path);
            Materials[name] = material;
        }

        static RuntimeAnimatorController CreateAnimator()
        {
            var modelPath = Root + "/Models/SK_Explorer.fbx";
            var assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            AnimationClip idle = null, walk = null, run = null;
            Avatar avatar = null;
            foreach (var asset in assets)
            {
                if (asset is Avatar candidate)
                    avatar = candidate;
                if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                {
                    if (clip.name.Contains("Idle")) idle = clip;
                    else if (clip.name.Contains("Walk")) walk = clip;
                    else if (clip.name.Contains("Run")) run = clip;
                }
            }

            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogWarning("SAHEL humanoid avatar was not created. Switching the explorer to a Generic rig so the authored clips still play.");
                var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
                assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
                idle = walk = run = null;
                foreach (var asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                    {
                        if (clip.name.Contains("Idle")) idle = clip;
                        else if (clip.name.Contains("Walk")) walk = clip;
                        else if (clip.name.Contains("Run")) run = clip;
                    }
                }
            }
            else
            {
                Debug.Log("SAHEL humanoid avatar valid.");
            }

            if (idle == null || walk == null)
                throw new System.InvalidOperationException("Explorer Idle/Walk clips were not imported.");

            var controllerPath = Root + "/Animations/Explorer.controller";
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            if (machine.defaultState != null)
                machine.RemoveState(machine.defaultState);

            var tree = new BlendTree
            {
                name = "Locomotion",
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                blendType = BlendTreeType.Simple1D
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1.7f);
            if (run != null)
                tree.AddChild(run, 4.2f);
            var state = machine.AddState("Locomotion");
            state.motion = tree;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("SAHEL clips idle=" + idle.name + " walk=" + walk.name + " run=" + (run != null ? run.name : "none"));
            return controller;
        }

        static TerrainData CreateTerrain()
        {
            var dataPath = Root + "/Terrain/SahelTerrain.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath) != null)
                AssetDatabase.DeleteAsset(dataPath);

            var data = new TerrainData
            {
                heightmapResolution = 129,
                alphamapResolution = 128,
                baseMapResolution = 256,
                size = new Vector3(200f, 6f, 200f)
            };
            data.SetDetailResolution(0, 16);

            var heights = new float[data.heightmapResolution, data.heightmapResolution];
            for (var y = 0; y < data.heightmapResolution; y++)
            {
                for (var x = 0; x < data.heightmapResolution; x++)
                {
                    var nx = x / (float)(data.heightmapResolution - 1);
                    var nz = y / (float)(data.heightmapResolution - 1);
                    var dx = nx - 0.5f;
                    var dz = nz - 0.5f;
                    var dist = Mathf.Sqrt(dx * dx + dz * dz);
                    var mask = Mathf.SmoothStep(0.24f, 0.5f, dist);
                    var noise = Mathf.PerlinNoise(nx * 3.2f + 0.4f, nz * 3.2f) * 0.7f
                                + Mathf.PerlinNoise(nx * 8f, nz * 8f) * 0.3f;
                    heights[y, x] = noise * mask;
                }
            }
            data.SetHeights(0, 0, heights);

            var laterite = new TerrainLayer { diffuseTexture = Tex("T_Laterite"), tileSize = new Vector2(7f, 7f) };
            var grass = new TerrainLayer { diffuseTexture = Tex("T_DryGrass"), tileSize = new Vector2(6f, 6f) };
            var sand = new TerrainLayer { diffuseTexture = Tex("T_Sand"), tileSize = new Vector2(8f, 8f) };
            AssetDatabase.CreateAsset(data, dataPath);
            AssetDatabase.AddObjectToAsset(laterite, data);
            AssetDatabase.AddObjectToAsset(grass, data);
            AssetDatabase.AddObjectToAsset(sand, data);
            data.terrainLayers = new[] { laterite, grass, sand };

            var mapRes = data.alphamapResolution;
            var map = new float[mapRes, mapRes, 3];
            for (var y = 0; y < mapRes; y++)
            {
                for (var x = 0; x < mapRes; x++)
                {
                    var nx = x / (float)(mapRes - 1);
                    var nz = y / (float)(mapRes - 1);
                    var dx = nx - 0.5f;
                    var dz = nz - 0.5f;
                    var grassWeight = Mathf.Clamp01((Mathf.PerlinNoise(nx * 12f, nz * 12f) - 0.45f) * 2.2f);
                    var roadWeight = Mathf.Clamp01(1f - Mathf.Abs(dx) / 0.028f) * Mathf.Clamp01(1f - Mathf.Abs(dz) / 0.32f);
                    var lateriteWeight = 1f;
                    var sum = lateriteWeight + grassWeight + roadWeight;
                    map[y, x, 0] = lateriteWeight / sum;
                    map[y, x, 1] = grassWeight / sum;
                    map[y, x, 2] = roadWeight / sum;
                }
            }
            data.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        static Texture2D Tex(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png");
        }

        static void BuildScene(Scene scene, RuntimeAnimatorController controller, TerrainData terrainData)
        {
            var sunObject = new GameObject("SahelSun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.85f, 0.66f);
            sun.intensity = 1.18f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sun.shadowBias = 0.06f;
            sun.shadowNormalBias = 0.4f;
            sunObject.transform.rotation = Quaternion.Euler(46f, -36f, 0f);

            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", new Color(0.45f, 0.58f, 0.78f));
            sky.SetColor("_GroundColor", new Color(0.55f, 0.34f, 0.18f));
            sky.SetFloat("_Exposure", 1.05f);
            var skyPath = Root + "/Materials/M_SahelSky.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(skyPath) != null)
                AssetDatabase.DeleteAsset(skyPath);
            AssetDatabase.CreateAsset(sky, skyPath);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.42f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.18f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.82f, 0.68f, 0.48f);
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.reflectionIntensity = 0.35f;

            var terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "SahelTerrain";
            terrainObject.transform.position = new Vector3(-100f, 0f, -100f);
            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.heightmapPixelError = 7f;
            terrain.basemapDistance = 90f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Physics.SyncTransforms();

            var village = new GameObject("Aldeia");
            var roadRoot = new GameObject("Pista");

            var roadSize = Measure(Root + "/Models/SM_RoadSegment.fbx");
            var roadLong = Mathf.Max(roadSize.x, roadSize.z);
            var roadAlongZ = roadSize.z >= roadSize.x;
            Debug.Log("SAHEL road bounds " + roadSize);
            for (var i = -5; i <= 4; i++)
            {
                var position = roadAlongZ ? new Vector3(0f, 0.03f, i * roadLong * 0.98f) : new Vector3(i * roadLong * 0.98f, 0.03f, 0f);
                var yaw = roadAlongZ ? 0f : 90f;
                var road = Place("SM_RoadSegment", "Pista_" + (i + 6), position, yaw, roadRoot.transform, true);
                IgnoreRaycastPadding(road);
            }

            Place("SM_HutRound", "Hut_A", new Vector3(-8.2f, 0f, -4f), 25f, village.transform, true);
            Place("SM_HutRound", "Hut_B", new Vector3(-11.4f, 0f, 6.5f), -15f, village.transform, true);
            Place("SM_HutRound", "Hut_C", new Vector3(-6.6f, 0f, 15f), 40f, village.transform, true);
            Place("SM_HutRound", "Hut_D", new Vector3(9.2f, 0f, -1.5f), 170f, village.transform, true);
            Place("SM_HutRound", "Hut_E", new Vector3(12.4f, 0f, 9f), 200f, village.transform, true);
            Place("SM_HouseBanco", "House_A", new Vector3(-15f, 0f, 1.5f), 80f, village.transform, true);
            Place("SM_HouseBanco", "House_B", new Vector3(15.5f, 0f, 3.5f), -100f, village.transform, true);
            Place("SM_Granary", "Granary_A", new Vector3(-5.5f, 0f, 19f), 12f, village.transform, true);
            Place("SM_Granary", "Granary_B", new Vector3(11f, 0f, 18.5f), -20f, village.transform, true);
            var market = Place("SM_MarketStall", "Market", new Vector3(0.4f, 0f, 7.5f), 90f, village.transform, true);
            var well = Place("SM_Well", "Well", new Vector3(-3.2f, 0f, 12.5f), 0f, village.transform, true);
            var baobab = Place("SM_Baobab", "Baobab", new Vector3(6.5f, 0f, 32f), 18f, village.transform, true);
            Place("SM_CompoundWall", "Wall_A", new Vector3(-12.5f, 0f, -1.2f), 90f, village.transform, true);
            Place("SM_CompoundWall", "Wall_B", new Vector3(13.5f, 0f, 14f), 8f, village.transform, true);

            float[] trees =
            {
                -18f, -12f, 18f, -8f, -16f, 18f, 20f, 14f, 16f, 26f,
                -14f, 28f, 11f, -18f, -20f, 2f, 22f, 4f, -18f, 8f
            };
            for (var i = 0; i < trees.Length; i += 2)
                Place("SM_Acacia", "Acacia_" + (i / 2), new Vector3(trees[i], 0f, trees[i + 1]), (i * 37) % 360, village.transform, true);
            Place("SM_Baobab", "Baobab_Far", new Vector3(-22f, 0f, 24f), 40f, village.transform, true);

            Place("SM_ClayPot", "Pot_A", new Vector3(-7.2f, 0f, -2.6f), 0f, village.transform, true);
            Place("SM_ClayPot", "Pot_B", new Vector3(1.8f, 0f, 6.4f), 20f, village.transform, true);
            Place("SM_ClayPot", "Pot_C", new Vector3(-2.4f, 0f, 11.2f), 0f, village.transform, true);
            Place("SM_Rock", "Rock_A", new Vector3(3.4f, 0f, -8f), 10f, village.transform, true);
            Place("SM_Rock", "Rock_B", new Vector3(-4.8f, 0f, 22f), 40f, village.transform, true);
            Place("SM_Rock", "Rock_C", new Vector3(8.8f, 0f, 24f), 0f, village.transform, true);

            for (var i = 0; i < 16; i++)
            {
                var angle = i * 1.7f;
                var radius = 7f + (i % 5) * 2.4f;
                var position = new Vector3(Mathf.Cos(angle) * radius, 0f, -6f + Mathf.Sin(angle) * radius);
                if (Mathf.Abs(position.x) < 3.2f)
                    position.x += 4f;
                var tuft = Place("SM_GrassTuft", "Grass_" + i, position, i * 24f, village.transform, false);
                tuft.transform.localScale = Vector3.one * (0.85f + (i % 4) * 0.12f);
            }

            Physics.SyncTransforms();
            var spawn = SnapToGround(new Vector3(0f, 4f, -28f));
            var player = CreateExplorer("Explorer", spawn, controller, true);
            CreateExplorer("Villager_Market", SnapToGround(new Vector3(2.1f, 4f, 9.2f)), controller, false);
            var villager = CreateExplorer("Villager_Well", SnapToGround(new Vector3(-1.8f, 4f, 10.4f)), controller, false);
            if (Materials.TryGetValue("M_ClothIndigo", out var indigo))
                SwapCloth(villager, indigo);

            var marketPoint = new GameObject("Mission_Market").transform;
            marketPoint.position = market.transform.position;
            var baobabPoint = new GameObject("Mission_Baobab").transform;
            baobabPoint.position = baobab.transform.position + new Vector3(0f, 0f, -2.2f);
            var spawnPoint = new GameObject("SpawnPoint");
            spawnPoint.transform.position = player.transform.position;

            var cameraObject = new GameObject("ThirdPersonCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.12f;
            camera.farClipPlane = 280f;
            cameraObject.AddComponent<AudioListener>();
            var rig = cameraObject.AddComponent<SahelThirdPersonCamera>();
            rig.target = player.transform;

            var control = player.GetComponent<SahelPlayerController>();
            control.view = rig;

            CreateHud(player.transform, marketPoint, baobabPoint);
            Validate(player, market, well);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static Vector3 Measure(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var renderer = instance.GetComponentInChildren<Renderer>();
            var size = renderer != null ? renderer.bounds.size : Vector3.one;
            Object.DestroyImmediate(instance);
            return size;
        }

        static GameObject Place(string model, string name, Vector3 position, float yaw, Transform parent, bool solid)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + model + ".fbx");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.position = SnapToGround(position + Vector3.up * 6f) + Vector3.up * 0.03f;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Paint(instance);
            if (solid)
            {
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null)
                        continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                }
            }
            return instance;
        }

        static void IgnoreRaycastPadding(GameObject road)
        {
            // Roads stay colliders. Grounding uses the terrain when the mesh normal faces down.
        }

        static Vector3 SnapToGround(Vector3 from)
        {
            var origin = new Vector3(from.x, 12f, from.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(from.x, 0f, from.z);
        }

        static void Paint(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!Slots.TryGetValue(renderer.gameObject.name, out var slotNames))
                {
                    var key = Slots.Keys.FirstOrDefault(candidate => renderer.gameObject.name.StartsWith(candidate));
                    if (key == null)
                        continue;
                    slotNames = Slots[key];
                }

                var current = renderer.sharedMaterials;
                var count = Mathf.Max(current.Length, 1);
                var next = new Material[count];
                for (var i = 0; i < count; i++)
                {
                    var slot = slotNames[Mathf.Min(i, slotNames.Length - 1)];
                    next[i] = Materials[slot];
                }
                renderer.sharedMaterials = next;
            }
        }

        static GameObject CreateExplorer(string name, Vector3 position, RuntimeAnimatorController controller, bool player)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            if (player)
                root.tag = "Player";

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/SK_Explorer.fbx");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            Paint(model);
            FitCharacter(model);

            var animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (player)
            {
                var body = root.AddComponent<CharacterController>();
                body.height = 1.72f;
                body.radius = 0.26f;
                body.center = new Vector3(0f, 0.9f, 0f);
                body.stepOffset = 0.28f;
                body.slopeLimit = 48f;
                body.skinWidth = 0.04f;
                var control = root.AddComponent<SahelPlayerController>();
                control.animator = animator;
            }
            else
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.height = 1.7f;
                capsule.radius = 0.26f;
                capsule.center = new Vector3(0f, 0.9f, 0f);
                root.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
            }

            return root;
        }

        static void FitCharacter(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            Debug.Log("SAHEL character height " + bounds.size.y.ToString("0.00"));
            if (bounds.size.y > 3f)
                model.transform.localScale = Vector3.one * (1.75f / bounds.size.y);

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            var parentY = model.transform.parent != null ? model.transform.parent.position.y : 0f;
            model.transform.localPosition = new Vector3(0f, -(bounds.min.y - parentY), 0f);
        }

        static void SwapCloth(GameObject root, Material cloth)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].name.StartsWith("M_ClothOchre"))
                    {
                        materials[i] = cloth;
                        changed = true;
                    }
                }
                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }

        static void CreateHud(Transform player, Transform market, Transform baobab)
        {
            var canvasObject = new GameObject("MissionCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -24f);
            panelRect.sizeDelta = new Vector2(640f, 118f);
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.08f, 0.05f, 0.03f, 0.72f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var title = CreateText(panel.transform, "Title", font, 22, FontStyle.Bold, new Vector2(16f, -12f), new Vector2(600f, 32f));
            var body = CreateText(panel.transform, "Body", font, 18, FontStyle.Normal, new Vector2(16f, -48f), new Vector2(600f, 58f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;

            var missionObject = new GameObject("ExplorationMission");
            var mission = missionObject.AddComponent<SahelExplorationMission>();
            mission.player = player;
            mission.title = title;
            mission.body = body;
            mission.steps = new[]
            {
                new SahelMissionStep
                {
                    prompt = "Caminhe pela pista de terra até o mercado da aldeia.",
                    point = market,
                    radius = 4.2f
                },
                new SahelMissionStep
                {
                    prompt = "Siga até o grande baobá no fim do caminho.",
                    point = baobab,
                    radius = 4.5f
                }
            };
        }

        static Text CreateText(Transform parent, string name, Font font, int size, FontStyle style, Vector2 position, Vector2 dimensions)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = new Color(0.96f, 0.9f, 0.78f);
            text.alignment = TextAnchor.UpperLeft;
            return text;
        }

        static void Validate(GameObject player, GameObject market, GameObject well)
        {
            Physics.SyncTransforms();
            var origin = player.transform.position + Vector3.up * 2f;
            var ground = Physics.Raycast(origin, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore);
            Debug.Log(ground
                ? "SAHEL ground hit " + hit.collider.name + " at " + hit.point
                : "SAHEL ground MISS");

            var blocked = Physics.Raycast(market.transform.position + Vector3.up, Vector3.down, out var marketHit, 6f);
            Debug.Log("SAHEL market support " + blocked + " " + (blocked ? marketHit.collider.name : ""));
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
            var colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
            Debug.Log("SAHEL counts renderers=" + renderers + " colliders=" + colliders + " well=" + well.name);
            if (!ground)
                Debug.LogError("SAHEL_VALIDATE player has no ground.");
            var animator = player.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
                Debug.LogError("SAHEL_VALIDATE explorer has no animator.");
        }
    }
}
