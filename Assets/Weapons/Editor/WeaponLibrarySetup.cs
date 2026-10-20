using System.Collections.Generic;
using System.IO;
using CityPack.Characters;
using UnityEditor;
using UnityEngine;

namespace CityPack.Weapons.Editor
{
    public static class WeaponLibrarySetup
    {
        const string ModelRoot = "Assets/Weapons/Polygonal/Models";
        const string TexturePath = "Assets/Weapons/Polygonal/Textures/atlass_1_diffuse.png";
        const string MaterialPath = "Assets/Weapons/Polygonal/Materials/M_WeaponAtlas.mat";
        const string PrefabRoot = "Assets/Weapons/Polygonal/Prefabs";
        const string SwatPrefab = "Assets/Characters/SWAT/Prefabs/SWAT_Operator.prefab";
        const string ServiceRifle = "Assets/Weapons/Polygonal/Prefabs/Rifle_1.prefab";

        [MenuItem("Tools/Weapons/Build Weapon Library")]
        public static void BuildLibrary()
        {
            var material = AtlasMaterial();
            foreach (var path in FbxPaths())
                BuildPrefab(path, material);
            AssetDatabase.SaveAssets();
            Debug.Log("Weapon library built.");
        }

        [MenuItem("Tools/Weapons/Equip SWAT Rifle")]
        public static void EquipSwatRifle()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ServiceRifle) == null)
                BuildLibrary();
            Equip(ServiceRifle);
            AssetDatabase.SaveAssets();
            Debug.Log("SWAT equipped with Rifle_1.");
        }

        public static void BuildAndEquipBatch()
        {
            BuildLibrary();
            Equip(ServiceRifle);
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        static void Equip(string weaponPrefabPath)
        {
            var weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(weaponPrefabPath);
            var root = PrefabUtility.LoadPrefabContents(SwatPrefab);
            try
            {
                var weaponRoot = FindChild(root.transform, "WeaponRoot");
                if (weaponRoot == null)
                {
                    weaponRoot = new GameObject("WeaponRoot").transform;
                    weaponRoot.SetParent(root.transform, false);
                }
                else
                {
                    weaponRoot.SetParent(root.transform, false);
                }

                for (var i = weaponRoot.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(weaponRoot.GetChild(i).gameObject);

                var gun = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab, weaponRoot);
                gun.transform.localPosition = Vector3.zero;
                gun.transform.localRotation = Quaternion.identity;
                gun.name = weaponPrefab.name;

                var flashGo = new GameObject("MuzzleFlash");
                var flash = flashGo.AddComponent<Light>();
                flash.type = LightType.Point;
                flash.range = 3.5f;
                flash.intensity = 1.6f;
                flash.color = new Color(1f, 0.85f, 0.55f);
                flash.enabled = false;
                var muzzle = FindChild(gun.transform, "Muzzle");
                flashGo.transform.SetParent(muzzle != null ? muzzle : gun.transform, false);

                weaponRoot.localPosition = new Vector3(0.18f, 1.18f, 0.32f);
                weaponRoot.localRotation = Quaternion.Euler(16f, 10f, 0f);

                var rig = root.GetComponent<SWATWeaponRig>();
                var motor = root.GetComponent<SWATCharacterController>();
                if (rig == null)
                    rig = root.AddComponent<SWATWeaponRig>();
                rig.body = motor;
                rig.animator = motor != null ? motor.animator : root.GetComponentInChildren<Animator>();
                rig.weaponRoot = weaponRoot;
                rig.rightHandTarget = FindChild(gun.transform, "RightHandTarget");
                rig.leftHandTarget = FindChild(gun.transform, "LeftHandTarget");
                rig.muzzle = muzzle;
                rig.muzzleFlash = flash;
                if (rig.shotSource == null)
                    rig.shotSource = root.GetComponent<AudioSource>();
                var procedural = root.GetComponent<HumanoidProceduralLocomotion>();
                if (procedural != null)
                    procedural.weaponRoot = weaponRoot;

                PrefabUtility.SaveAsPrefabAsset(root, SwatPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static GameObject BuildPrefab(string fbxPath, Material material)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
                return null;

            var name = NiceName(Path.GetFileNameWithoutExtension(fbxPath));
            var holder = new GameObject(name);
            var visual = Object.Instantiate(model);
            visual.name = "Mesh";
            visual.transform.SetParent(holder.transform, false);
            var points = CollectPoints(visual);
            if (points.Count < 8)
            {
                Object.DestroyImmediate(holder);
                return null;
            }

            visual.transform.localRotation = Quaternion.identity;
            Align(visual);
            var length = Length(visual);
            var target = TargetLength(fbxPath, length);
            if (length > 0.001f)
            {
                var factor = target / length;
                visual.transform.localScale = visual.transform.localScale * factor;
            }

            var placed = CollectPoints(visual);
            var rear = float.MaxValue;
            var tip = float.MinValue;
            var bore = Vector3.zero;
            var boreCount = 0;
            for (var i = 0; i < placed.Count; i++)
            {
                rear = Mathf.Min(rear, placed[i].z);
                tip = Mathf.Max(tip, placed[i].z);
            }

            var span = Mathf.Max(0.001f, tip - rear);
            for (var i = 0; i < placed.Count; i++)
            {
                if (placed[i].z > tip - span * 0.12f)
                {
                    bore += placed[i];
                    boreCount++;
                }
            }

            if (boreCount > 0)
                bore /= boreCount;
            var grip = rear + span * GripFraction(fbxPath);
            visual.transform.localPosition -= new Vector3(bore.x, bore.y, grip);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(holder.transform, false);
            muzzle.localPosition = new Vector3(0f, 0f, tip - grip);
            var right = new GameObject("RightHandTarget").transform;
            right.SetParent(holder.transform, false);
            var left = new GameObject("LeftHandTarget").transform;
            left.SetParent(holder.transform, false);
            left.localPosition = new Vector3(-0.04f, -0.005f, span * 0.28f);

            if (material != null)
            {
                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                for (var i = 0; i < renderers.Length; i++)
                    renderers[i].sharedMaterial = material;
            }

            Directory.CreateDirectory(PrefabRoot);
            var path = PrefabRoot + "/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(holder, path);
            Object.DestroyImmediate(holder);
            return prefab;
        }

        static void Align(GameObject visual)
        {
            var size = BoundsOf(CollectPoints(visual)).size;
            var axis = 0;
            if (size.y > size[axis])
                axis = 1;
            if (size.z > size[axis])
                axis = 2;
            if (axis == 0)
                visual.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            else if (axis == 1)
                visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var pointing = CollectPoints(visual);
            if (EndRadius(pointing, true) > EndRadius(pointing, false))
                visual.transform.localRotation = visual.transform.localRotation * Quaternion.Euler(0f, 180f, 0f);

            var baseRotation = visual.transform.localRotation;
            var best = baseRotation;
            var bestScore = float.MaxValue;
            for (var roll = 0; roll < 4; roll++)
            {
                visual.transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, roll * 90f);
                var score = RearHeight(CollectPoints(visual));
                if (score < bestScore)
                {
                    bestScore = score;
                    best = visual.transform.localRotation;
                }
            }

            visual.transform.localRotation = best;
        }

        static float RearHeight(List<Vector3> points)
        {
            var rear = float.MaxValue;
            var tip = float.MinValue;
            for (var i = 0; i < points.Count; i++)
            {
                rear = Mathf.Min(rear, points[i].z);
                tip = Mathf.Max(tip, points[i].z);
            }

            var limit = rear + (tip - rear) * 0.4f;
            var sum = 0f;
            var count = 0;
            for (var i = 0; i < points.Count; i++)
            {
                if (points[i].z > limit)
                    continue;
                sum += points[i].y;
                count++;
            }

            return count == 0 ? 0f : sum / count;
        }

        static float EndRadius(List<Vector3> points, bool atTip)
        {
            var rear = float.MaxValue;
            var tip = float.MinValue;
            for (var i = 0; i < points.Count; i++)
            {
                rear = Mathf.Min(rear, points[i].z);
                tip = Mathf.Max(tip, points[i].z);
            }

            var span = Mathf.Max(0.001f, tip - rear);
            var sum = 0f;
            var count = 0;
            for (var i = 0; i < points.Count; i++)
            {
                var near = atTip ? points[i].z > tip - span * 0.15f : points[i].z < rear + span * 0.15f;
                if (!near)
                    continue;
                sum += Mathf.Sqrt(points[i].x * points[i].x + points[i].y * points[i].y);
                count++;
            }

            return count == 0 ? 0f : sum / count;
        }

        static float Length(GameObject visual)
        {
            var points = CollectPoints(visual);
            var rear = float.MaxValue;
            var tip = float.MinValue;
            for (var i = 0; i < points.Count; i++)
            {
                rear = Mathf.Min(rear, points[i].z);
                tip = Mathf.Max(tip, points[i].z);
            }

            return tip - rear;
        }

        static List<Vector3> CollectPoints(GameObject visual)
        {
            var points = new List<Vector3>();
            var filters = visual.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                var mesh = filters[i].sharedMesh;
                if (mesh == null)
                    continue;
                var vertices = mesh.vertices;
                for (var v = 0; v < vertices.Length; v++)
                    points.Add(filters[i].transform.TransformPoint(vertices[v]));
            }

            return points;
        }

        static Bounds BoundsOf(List<Vector3> points)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            for (var i = 1; i < points.Count; i++)
                bounds.Encapsulate(points[i]);
            return bounds;
        }

        static float TargetLength(string path, float current)
        {
            var lower = path.ToLowerInvariant();
            if (lower.Contains("pistol"))
                return 0.22f;
            if (lower.Contains("submachine"))
                return 0.62f;
            if (lower.Contains("shotgun"))
                return 0.95f;
            if (lower.Contains("machinegun") || lower.Contains("machine_gun"))
                return 1.05f;
            if (lower.Contains("rocket_launcher") || lower.Contains("rocketlauncher"))
                return 1.05f;
            if (lower.Contains("/rifle"))
                return 0.9f;
            if (lower.Contains("sword") || lower.Contains("bat"))
                return 0.85f;
            if (lower.Contains("knife") || lower.Contains("knive") || lower.Contains("hammer"))
                return 0.32f;
            if (lower.Contains("explosive"))
                return 0.22f;
            if (lower.Contains("ammunition") || lower.Contains("bullet") || lower.Contains("rocket_1"))
                return 0.12f;
            if (lower.Contains("attachment") || lower.Contains("scope") || lower.Contains("sight") || lower.Contains("grip") || lower.Contains("silencer") || lower.Contains("compensator") || lower.Contains("flashlight") || lower.Contains("fitment"))
                return 0.16f;
            return Mathf.Clamp(current, 0.15f, 1.1f);
        }

        static float GripFraction(string path)
        {
            var lower = path.ToLowerInvariant();
            if (lower.Contains("pistol"))
                return 0.62f;
            if (lower.Contains("knife") || lower.Contains("knive") || lower.Contains("sword") || lower.Contains("bat") || lower.Contains("hammer"))
                return 0.16f;
            return 0.32f;
        }

        static Material AtlasMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
                return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath) ?? "Assets");
            var material = new Material(Shader.Find("Standard"));
            material.SetFloat("_Metallic", 0.28f);
            material.SetFloat("_Glossiness", 0.42f);
            var texture = AssetDatabase.LoadAssetAtPath<Texture>(TexturePath);
            if (texture != null)
                material.SetTexture("_MainTex", texture);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        static List<string> FbxPaths()
        {
            var paths = new List<string>();
            if (!Directory.Exists(ModelRoot))
                return paths;
            foreach (var file in Directory.GetFiles(ModelRoot, "*.fbx", SearchOption.AllDirectories))
                paths.Add(file.Replace("\\", "/"));
            paths.Sort();
            return paths;
        }

        static string NiceName(string fileName)
        {
            var parts = fileName.Split('_');
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                    continue;
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            }

            return string.Join("_", parts);
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
