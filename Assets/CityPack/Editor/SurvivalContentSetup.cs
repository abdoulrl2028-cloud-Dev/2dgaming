using System.Collections.Generic;
using System.IO;
using CityPack.Characters;
using CityPack.Cinematics;
using CityPack.UI;
using CityPack.Zombies;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace CityPack.Editor
{
    public static class SurvivalContentSetup
    {
        const string SwatModel = "Assets/Characters/SWAT/Model/SWAT.fbx";
        const string SwatAnimator = "Assets/Characters/SWAT/Animator/SWAT_Animator.controller";
        const string SwatPrefab = "Assets/Characters/SWAT/Prefabs/SWAT_Operator.prefab";
        const string SwatClips = "Assets/Characters/SWAT/Animations/SWATAnimationSet.asset";
        const string CityScene = "Assets/CityPack/Cinematics/City_Cinematic.unity";
        const string OpeningScene = "Assets/CityPack/Cinematics/Opening/Opening_Cinematic.unity";
        const string IntroScene = "Assets/Characters/SWAT/Cinematics/SWAT_Intro.unity";
        const string MenuScene = "Assets/UI/MainMenu/MainMenu.unity";
        const string GameScene = "Assets/CityPack/Scenes/Game.unity";

        [MenuItem("Tools/City Pack/Create Opening Cinematic")]
        public static void CreateOpeningMenu()
        {
            CreateOpening();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Zombies/Create Encounter Point")]
        public static void CreateEncounterPoint()
        {
            var point = new GameObject("ZombieEncounterPoint");
            var encounter = point.AddComponent<ZombieEncounterPoint>();
            encounter.encounterName = "Encounter";
            if (SceneView.lastActiveSceneView != null)
                point.transform.position = SceneView.lastActiveSceneView.pivot;
            Selection.activeGameObject = point;
        }

        [MenuItem("Tools/Zombies/Create Zombie Horde")]
        public static void CreateHorde()
        {
            var point = new GameObject("ZombieHorde");
            var encounter = point.AddComponent<ZombieEncounterPoint>();
            encounter.encounterName = "Horde";
            encounter.zombieCount = 8;
            encounter.cinematicEnabled = true;
            encounter.chaseSpeed = 2.8f;
            Selection.activeGameObject = point;
        }

        [MenuItem("Tools/Zombies/Validate Encounters")]
        public static void ValidateEncounters()
        {
            var points = Object.FindObjectsByType<ZombieEncounterPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log("Zombie encounters in the open scene: " + points.Length);
        }

        [MenuItem("Tools/Zombies/Bake Zombie Navigation")]
        public static void BakeNavigation()
        {
            var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            if (surface == null)
            {
                Debug.LogWarning("No NavMeshSurface in the open scene.");
                return;
            }

            surface.BuildNavMesh();
            Debug.Log("Zombie navigation baked.");
        }

        public static void SetupAllBatch()
        {
            SetupAll();
            EditorApplication.Exit(0);
        }

        public static void PlayGame()
        {
            EditorSceneManager.OpenScene(OpeningScene);
            EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
        }

        public static void SetupAll()
        {
            ConfigureImporter(SwatModel);
            var zombieModels = Directory.GetFiles("Assets/Characters/Zombies/Models", "*.fbx");
            for (var i = 0; i < zombieModels.Length; i++)
                ConfigureImporter(zombieModels[i].Replace("\\", "/"));

            var swatClip = FindClip(SwatModel);
            CreateAnimator(SwatAnimator, swatClip);
            var set = ScriptableObject.CreateInstance<SWATAnimationSet>();
            set.sourceMixamo = swatClip;
            AssetDatabase.CreateAsset(set, SwatClips);
            StyleSwatMaterials();
            var swat = BuildSwatPrefab();
            var zombies = BuildZombiePrefabs();
            CopyCity(OpeningScene);
            CopyCity(GameScene);
            CreateIntro(swat);
            CreateMenu();
            CreateOpening();
            PopulateGame(swat, zombies);
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("Survival content setup finished.");
        }

        static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();
        }

        static AnimationClip FindClip(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__"))
                    return clip;
            }

            return null;
        }

        static void CreateAnimator(string path, AnimationClip source)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AddParameters(controller);
            var root = controller.layers[0].stateMachine;
            var idle = AddState(root, "IDLE", null);
            var walk = AddState(root, "WALK", null);
            var run = AddState(root, "RUN", null);
            var sprint = AddState(root, "SPRINT", null);
            var crouch = AddState(root, "CROUCH", null);
            var crouchWalk = AddState(root, "CROUCH_WALK", null);
            AddState(root, "JUMP", null);
            AddState(root, "FALL", null);
            AddState(root, "LAND", null);
            var aim = AddState(root, "AIM", null);
            AddState(root, "SHOOT", null);
            AddState(root, "RELOAD", null);
            AddState(root, "MELEE", null);
            AddState(root, "ATTACK", null);
            AddState(root, "HIT", null);
            AddState(root, "DEATH", null);
            AddState(root, "MIXAMO_SOURCE", source);
            root.defaultState = idle;

            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            if (source != null)
            {
                tree.AddChild(source, 0f);
                tree.AddChild(source, 1.6f);
                tree.AddChild(source, 3.3f);
                tree.AddChild(source, 5.1f);
            }

            var locomotion = root.AddState("LOCOMOTION");
            locomotion.motion = tree;
            Link(idle, walk, "Speed", 0.2f, AnimatorConditionMode.Greater);
            Link(walk, idle, "Speed", 0.15f, AnimatorConditionMode.Less);
            Link(walk, run, "Speed", 2.4f, AnimatorConditionMode.Greater);
            Link(run, walk, "Speed", 2.1f, AnimatorConditionMode.Less);
            Link(run, sprint, "IsSprinting", 0f, AnimatorConditionMode.If);
            Link(sprint, run, "IsSprinting", 0f, AnimatorConditionMode.IfNot);
            Link(idle, crouch, "IsCrouching", 0f, AnimatorConditionMode.If);
            Link(crouch, crouchWalk, "Speed", 0.2f, AnimatorConditionMode.Greater);
            Link(crouch, idle, "IsCrouching", 0f, AnimatorConditionMode.IfNot);
            Link(idle, aim, "IsAiming", 0f, AnimatorConditionMode.If);
            Link(aim, idle, "IsAiming", 0f, AnimatorConditionMode.IfNot);
            Any(root, "SHOOT", "IsShooting", AnimatorConditionMode.If);
            Any(root, "RELOAD", "IsReloading", AnimatorConditionMode.If);
            Any(root, "HIT", "Hit", AnimatorConditionMode.If);
            Any(root, "DEATH", "Death", AnimatorConditionMode.If);
            Any(root, "JUMP", "Jump", AnimatorConditionMode.If);
            Any(root, "LAND", "Land", AnimatorConditionMode.If);
            Any(root, "MELEE", "Melee", AnimatorConditionMode.If);
            Any(root, "ATTACK", "Attack", AnimatorConditionMode.If);
            EditorUtility.SetDirty(controller);
        }

        static void AddParameters(AnimatorController controller)
        {
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Direction", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsShooting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsReloading", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Land", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Melee", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        }

        static AnimatorState AddState(AnimatorStateMachine root, string name, Motion motion)
        {
            var state = root.AddState(name);
            state.motion = motion;
            return state;
        }

        static void Link(AnimatorState from, AnimatorState to, string parameter, float threshold, AnimatorConditionMode mode)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.2f;
            transition.AddCondition(mode, threshold, parameter);
        }

        static void Any(AnimatorStateMachine root, string stateName, string parameter, AnimatorConditionMode mode)
        {
            AnimatorState state = null;
            var states = root.states;
            for (var i = 0; i < states.Length; i++)
            {
                if (states[i].state.name == stateName)
                    state = states[i].state;
            }

            if (state == null)
                return;
            var transition = root.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.duration = 0.12f;
            transition.AddCondition(mode, 0f, parameter);
        }

        static void StyleSwatMaterials()
        {
            var folder = "Assets/Characters/SWAT/Textures";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SwatModel);
            if (model == null)
                return;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var shared = renderers[i].sharedMaterials;
                for (var m = 0; m < shared.Length; m++)
                {
                    if (shared[m] == null)
                        continue;
                    shared[m] = BuildSwatMaterial(shared[m].name, folder);
                }

                renderers[i].sharedMaterials = shared;
            }
        }

        static Material BuildSwatMaterial(string sourceName, string folder)
        {
            var safe = Sanitize(sourceName);
            var path = "Assets/Characters/SWAT/Materials/M_" + safe + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            var key = sourceName.ToLowerInvariant();
            Texture albedo = FindTex(folder, key, "basecolor") ?? FindTex(folder, key, "base") ?? FindTex(folder, Alias(key), "basecolor");
            Texture normal = FindTex(folder, key, "normal") ?? FindTex(folder, Alias(key), "normal");
            if (albedo != null)
                material.SetTexture("_MainTex", albedo);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            var metal = 0.05f;
            var smooth = 0.28f;
            if (key.Contains("uniform") || key.Contains("glove") || key.Contains("mask"))
                smooth = 0.22f;
            else if (key.Contains("boot"))
                smooth = 0.32f;
            else if (key.Contains("helmet") || key.Contains("pouch"))
            {
                metal = 0.35f;
                smooth = 0.4f;
            }
            else if (key.Contains("goggle"))
            {
                metal = 0.1f;
                smooth = 0.9f;
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_ALPHABLEND_ON");
                material.renderQueue = 3000;
                var color = material.GetColor("_Color");
                color.a = 0.45f;
                material.SetColor("_Color", color);
            }
            else if (key.Contains("g2") || key.Contains("g3") || key.Contains("g4") || key.Contains("g5"))
            {
                metal = 0.72f;
                smooth = 0.5f;
            }

            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Glossiness", smooth);
            EditorUtility.SetDirty(material);
            return material;
        }

        static string Alias(string key)
        {
            if (key.Contains("boot")) return "army_boot";
            if (key.Contains("uniform")) return "scp_operator_uniform";
            if (key.Contains("helmet")) return "scp_operator_helmet";
            if (key.Contains("mask")) return "scp_operator_mask";
            if (key.Contains("goggle")) return "scp_operator_goggles";
            if (key.Contains("glove")) return "scp_operator_glove";
            if (key.Contains("pouch")) return "scp_operator_pouch";
            if (key.Contains("face")) return "swat_face";
            if (key.Contains("eye")) return "blue_eye";
            if (key.Contains("patch")) return "swat_patch";
            if (key.Contains("g2")) return "swat_g2";
            if (key.Contains("g3")) return "swat_g3";
            if (key.Contains("g4")) return "swat_g4";
            if (key.Contains("g5")) return "swat_g5";
            if (key.Contains("lambert")) return "swat_lambert";
            return key;
        }

        static Texture FindTex(string folder, string key, string token)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (name.Contains(key) && name.Contains(token))
                    return AssetDatabase.LoadAssetAtPath<Texture>(path);
            }

            return null;
        }

        static GameObject BuildSwatPrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SwatModel);
            var visual = Object.Instantiate(model);
            ApplySwatMaterials(visual);
            var instance = new GameObject("SWAT_Operator");
            visual.transform.SetParent(instance.transform, false);
            FitHeight(visual, 1.82f);
            var animator = visual.GetComponent<Animator>();
            if (animator == null)
                animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SwatAnimator);
            animator.applyRootMotion = false;
            var controller = instance.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;
            var motor = instance.AddComponent<SWATCharacterController>();
            motor.animator = animator;
            motor.clips = AssetDatabase.LoadAssetAtPath<SWATAnimationSet>(SwatClips);
            var procedural = instance.AddComponent<HumanoidProceduralLocomotion>();
            procedural.animator = animator;
            var weapon = new GameObject("WeaponRoot").transform;
            var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            weapon.SetParent(right != null ? right : instance.transform, false);
            var leftTarget = new GameObject("LeftHandTarget").transform;
            leftTarget.SetParent(weapon, false);
            leftTarget.localPosition = new Vector3(-0.12f, 0.02f, 0.28f);
            var rightTarget = new GameObject("RightHandTarget").transform;
            rightTarget.SetParent(weapon, false);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(weapon, false);
            muzzle.localPosition = new Vector3(0f, 0.02f, 0.55f);
            var flash = new GameObject("MuzzleFlash").AddComponent<Light>();
            flash.type = LightType.Point;
            flash.range = 4f;
            flash.intensity = 2f;
            flash.enabled = false;
            flash.transform.SetParent(muzzle, false);
            var rig = instance.AddComponent<SWATWeaponRig>();
            rig.body = motor;
            rig.weaponRoot = weapon;
            rig.rightHandTarget = rightTarget;
            rig.leftHandTarget = leftTarget;
            rig.muzzle = muzzle;
            rig.muzzleFlash = flash;
            rig.shotSource = instance.AddComponent<AudioSource>();
            procedural.weaponRoot = weapon;
            instance.AddComponent<SWATDebugController>().animator = animator;
            instance.tag = "Player";
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, SwatPrefab);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        static List<ZombieController> BuildZombiePrefabs()
        {
            var created = new List<ZombieController>();
            var models = Directory.GetFiles("Assets/Characters/Zombies/Models", "*.fbx");
            System.Array.Sort(models);
            for (var i = 0; i < models.Length; i++)
            {
                var path = models[i].Replace("\\", "/");
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                    continue;
                var visual = Object.Instantiate(model);
                var name = "Zombie_" + (i + 1).ToString("00");
                var instance = new GameObject(name);
                visual.transform.SetParent(instance.transform, false);
                FitHeight(visual, 1.75f);
                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                var controllerPath = "Assets/Characters/Zombies/Animator/" + name + ".controller";
                CreateAnimator(controllerPath, FindClip(path));
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                animator.applyRootMotion = false;
                var agent = instance.AddComponent<UnityEngine.AI.NavMeshAgent>();
                agent.height = 1.7f;
                agent.radius = 0.32f;
                agent.speed = 0.7f;
                agent.angularSpeed = 180f;
                agent.acceleration = 6f;
                var box = instance.AddComponent<CapsuleCollider>();
                box.height = 1.7f;
                box.center = new Vector3(0f, 0.85f, 0f);
                box.radius = 0.3f;
                var body = instance.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                var zombie = instance.AddComponent<ZombieController>();
                zombie.variantId = name;
                var health = instance.AddComponent<ZombieHealth>();
                var senses = instance.AddComponent<ZombieDetection>();
                var voice = instance.AddComponent<ZombieAudio>();
                voice.voice = instance.AddComponent<AudioSource>();
                var brain = instance.AddComponent<ZombieAI>();
                brain.senses = senses;
                brain.health = health;
                brain.audioPlayer = voice;
                brain.animator = animator;
                zombie.brain = brain;
                zombie.health = health;
                health.brain = brain;
                health.animator = animator;
                var procedural = instance.AddComponent<HumanoidProceduralLocomotion>();
                procedural.animator = animator;
                brain.procedural = procedural;
                var texture = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Characters/Zombies/Textures/" + Path.GetFileNameWithoutExtension(path) + ".png");
                if (texture != null)
                {
                    var matPath = "Assets/Characters/Zombies/Materials/M_" + name + ".mat";
                    var mat = new Material(Shader.Find("Standard"));
                    mat.SetTexture("_MainTex", texture);
                    mat.SetFloat("_Glossiness", 0.18f);
                    AssetDatabase.CreateAsset(mat, matPath);
                    var renderers = instance.GetComponentsInChildren<Renderer>();
                    for (var r = 0; r < renderers.Length; r++)
                        renderers[r].sharedMaterial = mat;
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, "Assets/Characters/Zombies/Prefabs/" + name + ".prefab");
                Object.DestroyImmediate(instance);
                created.Add(prefab.GetComponent<ZombieController>());
            }

            return created;
        }

        static void ApplySwatMaterials(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var shared = renderers[i].sharedMaterials;
                for (var m = 0; m < shared.Length; m++)
                {
                    if (shared[m] == null)
                        continue;
                    shared[m] = BuildSwatMaterial(shared[m].name, "Assets/Characters/SWAT/Textures");
                }

                renderers[i].sharedMaterials = shared;
            }
        }

        static void FitHeight(GameObject instance, float target)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y < 0.2f || bounds.size.y > 20f)
                return;
            var scale = target / bounds.size.y;
            instance.transform.localScale *= scale;
            renderers = instance.GetComponentsInChildren<Renderer>();
            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            instance.transform.position += new Vector3(0f, -bounds.min.y, 0f);
        }

        static void CopyCity(string destination)
        {
            var dir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            if (!File.Exists(destination))
                File.Copy(CityScene, destination);
            AssetDatabase.ImportAsset(destination);
        }

        static void CreateIntro(GameObject swat)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var subject = (GameObject)PrefabUtility.InstantiatePrefab(swat);
            subject.transform.position = Vector3.zero;
            var motor = subject.GetComponent<SWATCharacterController>();
            if (motor != null)
                motor.startLocked = true;
            var cameraGo = new GameObject("SWAT_IntroCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 36f;
            camera.transform.position = new Vector3(0.4f, 0.2f, 1.4f);
            camera.transform.LookAt(new Vector3(0f, 0.2f, 0f));
            cameraGo.AddComponent<AudioListener>();
            var root = new GameObject("SWATIntro");
            var player = root.AddComponent<SWATIntroPlayer>();
            player.subject = subject.transform;
            player.shotCamera = camera;
            player.nextScene = "MainMenu";
            var canvas = CreateFadeCanvas(out var group, out _);
            group.alpha = 1f;
            EditorSceneManager.SaveScene(scene, IntroScene);
        }

        static void CreateMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("MenuCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.06f);
            camera.gameObject.AddComponent<AudioListener>();
            var canvas = CreateFadeCanvas(out _, out var text);
            text.text = "[NOME DO MEU JOGO]";
            text.fontSize = 42;
            var buttonGo = new GameObject("Play", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(canvas.transform, false);
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240f, 56f);
            rect.anchoredPosition = new Vector2(0f, -80f);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(buttonGo.transform, false);
            var labelText = label.GetComponent<Text>();
            labelText.text = "PLAY";
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.black;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<RectTransform>().sizeDelta = rect.sizeDelta;
            var menu = canvas.gameObject.AddComponent<MainMenuController>();
            menu.titleLabel = text;
            menu.playButton = buttonGo.GetComponent<Button>();
            menu.gameScene = "Game";
            EditorSceneManager.SaveScene(scene, MenuScene);
        }

        static void CreateOpening()
        {
            var scene = EditorSceneManager.OpenScene(OpeningScene, OpenSceneMode.Single);
            SilenceCityGameplay();
            var bounds = SceneBounds();
            var root = new GameObject("Opening");
            var cameraGo = new GameObject("OpeningCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 42f;
            camera.farClipPlane = 800f;
            cameraGo.AddComponent<AudioListener>();
            var anchors = new List<Transform>();
            var looks = new List<Transform>();
            AddShot(root.transform, anchors, looks, "Camera_01_Overview", bounds.center + new Vector3(0f, bounds.size.y + 28f, -bounds.size.z * 0.45f), bounds.center);
            AddShot(root.transform, anchors, looks, "Camera_02_Street", bounds.center + new Vector3(0f, 8f, -bounds.extents.z * 0.2f), bounds.center);
            AddShot(root.transform, anchors, looks, "Camera_03_Buildings", bounds.center + new Vector3(bounds.extents.x * 0.4f, 6f, 0f), bounds.center + Vector3.up * 8f);
            AddShot(root.transform, anchors, looks, "Camera_04_LowAngle", new Vector3(bounds.center.x, bounds.min.y + 0.7f, bounds.min.z + 6f), bounds.center);
            AddShot(root.transform, anchors, looks, "Camera_05_Final", bounds.center + new Vector3(-12f, 5f, bounds.extents.z * 0.2f), bounds.center + Vector3.up * 4f);
            camera.transform.position = anchors[0].position;
            camera.transform.LookAt(looks[0]);
            var canvas = CreateFadeCanvas(out var fade, out var title);
            var directorGo = new GameObject("OpeningDirector");
            var director = directorGo.AddComponent<PlayableDirector>();
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, "Assets/CityPack/Cinematics/Opening/Opening_CinematicTimeline.playable");
            var track = timeline.CreateTrack<CinemachineTrack>(null, "Cameras");
            director.playableAsset = timeline;
            var brain = cameraGo.AddComponent<CinemachineBrain>();
            director.SetGenericBinding(track, brain);
            for (var i = 0; i < anchors.Count; i++)
            {
                var vcamGo = new GameObject(anchors[i].name + "_CM");
                var vcam = vcamGo.AddComponent<CinemachineCamera>();
                vcam.transform.position = anchors[i].position;
                vcam.transform.rotation = Quaternion.LookRotation(looks[i].position - anchors[i].position);
                var lens = vcam.Lens;
                lens.FieldOfView = 40f;
                vcam.Lens = lens;
                var clip = track.CreateClip<CinemachineShot>();
                clip.start = i == 0 ? 3d : 3d + i * 6d;
                clip.duration = 7d;
                var shot = (CinemachineShot)clip.asset;
                var name = new PropertyName(System.Guid.NewGuid().ToString());
                shot.VirtualCamera.exposedName = name;
                director.SetReferenceValue(name, vcam);
            }

            var control = root.AddComponent<OpeningCinematicController>();
            control.timeline = director;
            control.outputCamera = camera;
            control.shotAnchors = anchors.ToArray();
            control.shotLooks = looks.ToArray();
            control.fade = fade;
            control.titleLabel = title;
            control.nextScene = "SWAT_Intro";
            control.useTimeline = true;
            control.music = CreateAudio(root, "Music");
            control.ambience = CreateAudio(root, "Ambience");
            control.wind = CreateAudio(root, "Wind");
            control.traffic = CreateAudio(root, "Traffic");
            EditorUtility.SetDirty(timeline);
            EditorSceneManager.SaveScene(scene);
        }

        static void PopulateGame(GameObject swat, List<ZombieController> zombies)
        {
            var scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
            SilenceCityGameplay();
            var bounds = SceneBounds();
            var spawn = new Vector3(bounds.center.x, bounds.min.y + 0.1f, bounds.min.z + 4f);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(swat);
            player.transform.position = spawn;
            player.name = "Player";
            var yaw = new GameObject("CameraYaw").transform;
            yaw.SetParent(player.transform, false);
            yaw.localPosition = new Vector3(0f, 1.6f, 0f);
            var motor = player.GetComponent<SWATCharacterController>();
            motor.cameraYaw = yaw;
            var procedural = player.GetComponent<HumanoidProceduralLocomotion>();
            procedural.lookTarget = yaw;
            var main = CreateFollowCamera("SWAT_MainCamera", yaw, new Vector3(0.45f, 0.2f, 0f), 4.2f, 10);
            var aim = CreateFollowCamera("SWAT_AimCamera", yaw, new Vector3(0.35f, 0.15f, 0.2f), 1.7f, 0);
            var cine = CreateFollowCamera("SWAT_CinematicCamera", yaw, new Vector3(0.2f, 0.4f, 0f), 6f, 0);
            var rig = player.AddComponent<SWATCameraRig>();
            rig.body = motor;
            rig.mainCamera = main;
            rig.aimCamera = aim;
            rig.cinematicCamera = cine;
            var unityCam = new GameObject("Main Camera");
            var cam = unityCam.AddComponent<Camera>();
            cam.tag = "MainCamera";
            unityCam.AddComponent<AudioListener>();
            unityCam.AddComponent<CinemachineBrain>();

            var start = new GameObject("PathStart").transform;
            start.position = new Vector3(bounds.center.x, bounds.min.y, bounds.min.z);
            var end = new GameObject("PathEnd").transform;
            end.position = new Vector3(bounds.center.x, bounds.min.y, bounds.max.z);
            var tracker = new GameObject("Progress").AddComponent<PlayerProgressTracker>();
            tracker.player = player.transform;
            tracker.pathStart = start;
            tracker.pathEnd = end;
            var poolGo = new GameObject("ZombiePool");
            var pool = poolGo.AddComponent<ZombiePool>();
            pool.prefabs = zombies.ToArray();
            pool.maxAlive = 16;
            var director = new GameObject("ZombieEncounterDirector").AddComponent<ZombieEncounterDirector>();
            director.progress = tracker;
            director.pool = pool;
            director.player = player.transform;
            director.revealCamera = CreateShotCamera("ZombieRevealCamera", bounds.center + new Vector3(4f, 2f, 0f));
            director.streetCamera = CreateShotCamera("ZombieStreetCamera", bounds.center + new Vector3(0f, 1.2f, 6f));
            director.hordeCamera = CreateShotCamera("ZombieHordeCamera", bounds.center + new Vector3(-8f, 6f, 0f));

            CreatePoint("Encounter_01_First", 0.12f, 0.3f, 1, false, 0.6f, 1.4f, bounds, 6f);
            CreatePoint("Encounter_02_Alley", 0.28f, 0.45f, 3, true, 0.7f, 2.1f, bounds, -7f);
            CreatePoint("Encounter_03_Building", 0.45f, 0.62f, 3, true, 0.6f, 2.2f, bounds, 8f);
            CreatePoint("Encounter_04_Street", 0.62f, 0.78f, 4, false, 0.8f, 2.5f, bounds, -8f);
            CreatePoint("Encounter_05_Horde", 0.78f, 0.9f, 8, true, 0.9f, 2.8f, bounds, 9f);
            CreatePoint("Encounter_06_Final", 0.9f, 1f, 10, true, 0.8f, 3f, bounds, 0f);
            EditorSceneManager.SaveScene(scene);
        }

        static void CreatePoint(string name, float min, float max, int count, bool cinematic, float walk, float chase, Bounds bounds, float side)
        {
            var point = new GameObject(name).AddComponent<ZombieEncounterPoint>();
            var z = Mathf.Lerp(bounds.min.z, bounds.max.z, (min + max) * 0.5f);
            point.transform.position = new Vector3(bounds.center.x + side, bounds.min.y, z);
            point.encounterName = name;
            point.zombieCount = count;
            point.minimumPlayerProgress = min;
            point.maximumPlayerProgress = max;
            point.cinematicEnabled = cinematic;
            point.walkSpeed = walk;
            point.chaseSpeed = chase;
            point.activationDistance = 24f;
            point.triggerDistance = 12f;
            point.cinematicDuration = cinematic ? 4.5f : 0f;
        }

        static CinemachineCamera CreateFollowCamera(string name, Transform target, Vector3 shoulder, float distance, int priority)
        {
            var go = new GameObject(name);
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Follow = target;
            vcam.LookAt = target;
            var settings = vcam.Priority;
            settings.Value = priority;
            vcam.Priority = settings;
            var follow = go.AddComponent<CinemachineThirdPersonFollow>();
            follow.ShoulderOffset = shoulder;
            follow.CameraDistance = distance;
            follow.Damping = new Vector3(0.2f, 0.3f, 0.2f);
            follow.VerticalArmLength = 0.35f;
            follow.CameraSide = 1f;
            var avoid = follow.AvoidObstacles;
            avoid.Enabled = true;
            avoid.CollisionFilter = ~0;
            follow.AvoidObstacles = avoid;
            return vcam;
        }

        static CinemachineCamera CreateShotCamera(string name, Vector3 position)
        {
            var go = new GameObject(name);
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.transform.position = position;
            var settings = vcam.Priority;
            settings.Value = 0;
            vcam.Priority = settings;
            return vcam;
        }

        static void AddShot(Transform parent, List<Transform> anchors, List<Transform> looks, string name, Vector3 position, Vector3 look)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.position = position;
            var lookGo = new GameObject(name + "_Look").transform;
            lookGo.SetParent(parent, false);
            lookGo.position = look;
            anchors.Add(anchor);
            looks.Add(lookGo);
        }

        static AudioSource CreateAudio(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            return source;
        }

        static Canvas CreateFadeCanvas(out CanvasGroup group, out Text title)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            group = canvasGo.GetComponent<CanvasGroup>();
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text), typeof(CanvasGroup));
            titleGo.transform.SetParent(canvasGo.transform, false);
            var rect = titleGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(900f, 120f);
            rect.anchoredPosition = Vector2.zero;
            title = titleGo.GetComponent<Text>();
            title.text = "[NOME DO MEU JOGO]";
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.fontSize = 48;
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return canvas;
        }

        static void SilenceCityGameplay()
        {
            var directors = Object.FindObjectsByType<CityCinematicDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < directors.Length; i++)
            {
                directors[i].playOnStart = false;
                directors[i].enabled = false;
            }

            var old = GameObject.Find("Player");
            if (old != null && old.GetComponent<SWATCharacterController>() == null)
                old.SetActive(false);
        }

        static Bounds SceneBounds()
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var bounds = new Bounds(Vector3.zero, new Vector3(20f, 5f, 20f));
            var any = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled)
                    continue;
                if (!any)
                {
                    bounds = renderers[i].bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return bounds;
        }

        static void SetBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(OpeningScene, true),
                new EditorBuildSettingsScene(IntroScene, true),
                new EditorBuildSettingsScene(MenuScene, true),
                new EditorBuildSettingsScene(GameScene, true),
                new EditorBuildSettingsScene(CityScene, true)
            };
        }

        static string Sanitize(string name)
        {
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                    chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
