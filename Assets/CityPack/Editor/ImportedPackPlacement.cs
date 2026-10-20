using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityPack.Editor
{
    public static class ImportedPackPlacement
    {
        const string GameScene = "Assets/CityPack/Scenes/Game.unity";
        const float PersonGap = 2.8f;
        const float ZoneGap = 16f;

        [MenuItem("Tools/City Pack/Place Imported Packs")]
        public static void PlaceMenu()
        {
            Place();
        }

        public static void PlaceAndPlay()
        {
            Place();
            EditorApplication.delayCall += EnterPlaymode;
        }

        static void EnterPlaymode()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.EnterPlaymode();
        }

        public static void Place()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != GameScene)
                scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);

            var previous = GameObject.Find("ImportedPacks");
            if (previous != null)
                Object.DestroyImmediate(previous);

            var root = new GameObject("ImportedPacks");
            Physics.SyncTransforms();
            if (!Grounding.TryStreet(out var road))
            {
                Debug.LogWarning("No street surface found. Imported packs were not placed.");
                return;
            }

            var city = CityBounds();
            var groundY = road.min.y;
            var south = city.min.z - 36f;

            var soldier = PlaceCharacter(root.transform, "PolishSoldier", "Assets/Characters/PolishSoldier/Model/PolishSoldier.fbx", 1.82f);
            var doom = PlaceCharacter(root.transform, "DoomZombie", "Assets/Characters/DoomZombie/Model/DoomZombie.fbx", 1.95f);
            var portraits = PlacePortraitPair(root.transform);
            var psx = new GameObject("PSX").transform;
            psx.SetParent(root.transform, false);
            var killers = PlaceFolder(psx, "Killers", "Assets/Characters/PSX/Models/Killers");
            var males = PlaceFolder(psx, "Male", "Assets/Characters/PSX/Models/Male");
            var females = PlaceFolder(psx, "Female", "Assets/Characters/PSX/Models/Female");

            var x = city.min.x;
            x = LayoutRow(soldier, x, south, groundY, new Color(0.16f, 0.22f, 0.16f));
            x = LayoutRow(doom, x, south, groundY, new Color(0.24f, 0.12f, 0.12f));
            x = LayoutRow(portraits, x, south, groundY, new Color(0.12f, 0.16f, 0.24f));
            LayoutRow(killers, x, south, groundY, new Color(0.18f, 0.14f, 0.14f));

            var maleDepth = GridDepth(males.Count);
            var maleZ = south - ZoneGap - maleDepth;
            LayoutRow(males, city.min.x, maleZ, groundY, new Color(0.2f, 0.2f, 0.22f));

            var femaleZ = maleZ - ZoneGap - GridDepth(females.Count);
            LayoutRow(females, city.min.x, femaleZ, groundY, new Color(0.22f, 0.2f, 0.18f));

            var bedroom = PlaceBedroom(root.transform);
            var roomZ = femaleZ;
            if (bedroom != null)
            {
                var bounds = WorldBounds(bedroom);
                roomZ = femaleZ - ZoneGap - bounds.extents.z;
                bedroom.transform.position += new Vector3(city.center.x - bounds.center.x, groundY - bounds.min.y, roomZ - bounds.center.z);
            }

            var paths = new GameObject("Paths").transform;
            paths.SetParent(root.transform, false);
            var lane = city.min.x + 6f;
            AddPath(paths, new Vector3(road.center.x, 0f, road.min.z - 1f), new Vector3(road.center.x, 0f, south + 2f), groundY);
            AddPath(paths, new Vector3(road.center.x, 0f, south + 2f), new Vector3(lane, 0f, south + 2f), groundY);
            AddPath(paths, new Vector3(lane, 0f, south - 4f), new Vector3(lane, 0f, maleZ + 2f), groundY);
            AddPath(paths, new Vector3(lane, 0f, maleZ - maleDepth - 2f), new Vector3(lane, 0f, femaleZ + 2f), groundY);
            AddPath(paths, new Vector3(lane, 0f, femaleZ - GridDepth(females.Count) - 2f), new Vector3(city.center.x, 0f, roomZ + 8f), groundY);

            PlaceForest(root.transform, road, city);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Imported packs separated. soldier=" + soldier.Count + " doom=" + doom.Count + " portraits=" + portraits.Count + " killers=" + killers.Count + " male=" + males.Count + " female=" + females.Count);
        }

        static List<GameObject> PlaceCharacter(Transform root, string zoneName, string path, float height)
        {
            var created = new List<GameObject>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
                return created;
            var zone = new GameObject(zoneName).transform;
            zone.SetParent(root, false);
            var instance = Object.Instantiate(model, zone);
            instance.name = Path.GetFileNameWithoutExtension(path);
            FitHeight(instance, height);
            AddCapsule(instance);
            created.Add(instance);
            return created;
        }

        static List<GameObject> PlacePortraitPair(Transform root)
        {
            var zone = new GameObject("Portraits").transform;
            zone.SetParent(root, false);
            var created = new List<GameObject>();
            var paths = new[]
            {
                "Assets/Characters/Portraits/BlueBikini.fbx",
                "Assets/Characters/Portraits/BlueHibiscus.fbx"
            };
            for (var i = 0; i < paths.Length; i++)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (model == null)
                    continue;
                var instance = Object.Instantiate(model, zone);
                instance.name = Path.GetFileNameWithoutExtension(paths[i]);
                FitHeight(instance, 1.68f);
                AddCapsule(instance);
                created.Add(instance);
            }

            return created;
        }

        static List<GameObject> PlaceFolder(Transform root, string zoneName, string folder)
        {
            var zone = new GameObject(zoneName).transform;
            zone.SetParent(root, false);
            var created = new List<GameObject>();
            if (!Directory.Exists(folder))
                return created;
            var files = Directory.GetFiles(folder, "*.fbx");
            System.Array.Sort(files);
            for (var i = 0; i < files.Length; i++)
            {
                var file = Path.GetFileName(files[i]);
                if (file.StartsWith("Character_Monster") || file.StartsWith("Characters_PSX"))
                    continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(files[i].Replace("\\", "/"));
                if (model == null)
                    continue;
                var instance = Object.Instantiate(model, zone);
                instance.name = Path.GetFileNameWithoutExtension(files[i]);
                AssignPsxTexture(instance);
                FitHeight(instance, 1.7f);
                AddCapsule(instance);
                created.Add(instance);
            }

            return created;
        }

        static float LayoutRow(List<GameObject> people, float x, float z, float groundY, Color padColor)
        {
            if (people.Count == 0)
                return x;
            var columns = people.Count <= 10 ? people.Count : 8;
            for (var i = 0; i < people.Count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var probe = new Vector3(x + col * PersonGap, groundY + 2f, z - row * PersonGap);
                DropFeet(people[i], probe, groundY);
                people[i].transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            }

            var rows = (people.Count + columns - 1) / columns;
            var width = Mathf.Max(PersonGap, (columns - 1) * PersonGap);
            var depth = Mathf.Max(PersonGap, (rows - 1) * PersonGap);
            AddPad(people[0].transform.parent, new Vector3(x + width * 0.5f, groundY - 0.08f, z - depth * 0.5f), width + 4f, depth + 4f, padColor);
            return x + width + ZoneGap;
        }

        static float GridDepth(int count)
        {
            if (count <= 0)
                return 0f;
            var columns = count <= 10 ? count : 8;
            var rows = (count + columns - 1) / columns;
            return Mathf.Max(PersonGap, (rows - 1) * PersonGap);
        }

        static void AddPath(Transform parent, Vector3 from, Vector3 to, float groundY)
        {
            var delta = to - from;
            delta.y = 0f;
            var length = delta.magnitude;
            if (length < 0.5f)
                return;
            var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "Path";
            path.transform.SetParent(parent, true);
            path.transform.position = new Vector3((from.x + to.x) * 0.5f, groundY - 0.02f, (from.z + to.z) * 0.5f);
            path.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            path.transform.localScale = new Vector3(4f, 0.2f, length);
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(0.1f, 0.1f, 0.1f);
            material.SetFloat("_Glossiness", 0.04f);
            path.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void AddPad(Transform parent, Vector3 center, float width, float depth, Color color)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "Pad";
            pad.transform.SetParent(parent, true);
            pad.transform.position = center;
            pad.transform.localScale = new Vector3(width, 0.12f, depth);
            var material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Glossiness", 0.05f);
            pad.GetComponent<Renderer>().sharedMaterial = material;
        }

        static GameObject PlaceBedroom(Transform root)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/Bedroom/Bedroom.fbx");
            if (model == null)
                return null;
            var instance = Object.Instantiate(model, root);
            instance.name = "Bedroom";
            var bounds = WorldBounds(instance);
            var span = Mathf.Max(bounds.size.x, bounds.size.z);
            if (span > 0.01f)
                instance.transform.localScale *= 12f / span;
            AddGroundSlab(instance);
            return instance;
        }

        static void PlaceForest(Transform root, Bounds road, Bounds city)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/Forest/Forest.fbx");
            if (model == null)
                return;
            var instance = Object.Instantiate(model, root);
            instance.name = "Forest";
            var bounds = WorldBounds(instance);
            var span = Mathf.Max(bounds.size.x, bounds.size.z);
            if (span > 0.01f)
                instance.transform.localScale *= 150f / span;
            bounds = WorldBounds(instance);
            var x = city.max.x + 40f + bounds.extents.x;
            instance.transform.position += new Vector3(x - bounds.center.x, road.min.y - bounds.min.y, road.center.z - bounds.center.z);
            AddGroundSlab(instance);
        }

        static Bounds CityBounds()
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            var any = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].transform.root.name == "ImportedPacks")
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

        static void AssignPsxTexture(GameObject instance)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/PSX/Textures/" + instance.name + ".png");
            if (texture == null)
                return;
            var material = new Material(Shader.Find("Standard"));
            material.name = "M_" + instance.name;
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_Glossiness", 0.15f);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = material;
        }

        static void FitHeight(GameObject instance, float target)
        {
            var bounds = WorldBounds(instance);
            if (bounds.size.y < 0.0001f)
                return;
            instance.transform.localScale *= target / bounds.size.y;
        }

        static void DropFeet(GameObject instance, Vector3 probe, float groundY)
        {
            var bounds = WorldBounds(instance);
            instance.transform.position += new Vector3(probe.x - bounds.center.x, groundY - bounds.min.y, probe.z - bounds.center.z);
        }

        static void AddCapsule(GameObject instance)
        {
            var bounds = WorldBounds(instance);
            var scale = instance.transform.lossyScale;
            var capsule = instance.AddComponent<CapsuleCollider>();
            capsule.center = instance.transform.InverseTransformPoint(bounds.center);
            var sy = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
            var sx = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
            capsule.height = Mathf.Max(0.5f, bounds.size.y / sy);
            capsule.radius = Mathf.Clamp(bounds.extents.x / sx, 0.12f, 0.4f);
        }

        static void AddGroundSlab(GameObject instance)
        {
            var bounds = WorldBounds(instance);
            var scale = instance.transform.lossyScale;
            if (Mathf.Abs(scale.x) < 0.0001f || Mathf.Abs(scale.y) < 0.0001f || Mathf.Abs(scale.z) < 0.0001f)
                return;
            var slab = instance.AddComponent<BoxCollider>();
            slab.center = instance.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y + 0.08f, bounds.center.z));
            slab.size = new Vector3(bounds.size.x / scale.x, 0.16f / scale.y, bounds.size.z / scale.z);
        }

        static Bounds WorldBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(instance.transform.position, Vector3.one);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                if (renderers[i].bounds.size.sqrMagnitude <= 0f)
                    continue;
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}
