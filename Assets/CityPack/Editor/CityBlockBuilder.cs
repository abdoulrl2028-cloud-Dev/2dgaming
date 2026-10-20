using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CityPack.Editor
{
    public sealed class CityBuildResult
    {
        public GameObject Root;
        public Bounds Bounds;
        public bool HasBounds;
        public Vector3 StreetStart;
        public Vector3 StreetMid;
        public Vector3 StreetEnd;
        public Vector3 BuildingFront;
        public Vector3 PoiFront;
        public Vector3 LowPoint;
        public float EyeHeight = 1.7f;
        public float StreetHalfWidth = 4f;
        public readonly List<Light> StreetLights = new List<Light>();
        public readonly List<Light> AccentLights = new List<Light>();
        public readonly List<Renderer> Windows = new List<Renderer>();
        public readonly List<Vector3> SidewalkPoints = new List<Vector3>();
        public GameObject ParkingBarrier;
        public GameObject ParkingMeter;
        public int BuildingCount;
    }

    public static class CityBlockBuilder
    {
        const string PrefabRoot = "Assets/POLYGON city pack/Prefabs";

        public static CityBuildResult Build(StringBuilder report)
        {
            var result = new CityBuildResult();
            var catalog = LoadCatalog();
            report.AppendLine("Prefabs found: " + catalog.Count);
            if (catalog.Count == 0)
            {
                report.AppendLine("ERROR: POLYGON city pack prefabs were not found.");
                return result;
            }

            var streetPrefab = Find(catalog, "Street 4 16M prefab") ?? Find(catalog, "Street 4 8M prefab") ?? Find(catalog, "Street 1 Prefab");
            var sidewalkPrefab = Find(catalog, "Sideway 1 prefab") ?? Find(catalog, "Walkway 1 Prefab") ?? Find(catalog, "WalkWay 5 prefab");
            if (streetPrefab == null)
            {
                report.AppendLine("ERROR: no street prefab.");
                return result;
            }

            var root = new GameObject("City");
            result.Root = root;
            var roads = Child(root, "Roads");
            var walks = Child(root, "Sidewalks");
            var buildings = Child(root, "Buildings");
            var props = Child(root, "Props");
            var parking = Child(root, "Parking");
            var artificial = Child(root, "ArtificialLights");

            var cursor = 0f;
            var placedRoads = 0;
            for (var i = 0; i < 6; i++)
            {
                var piece = Spawn(streetPrefab, roads);
                OrientLongAxis(piece, alongX: false);
                if (!PlaceMin(piece, 0f, ref cursor, out var bounds))
                    continue;
                Remember(result, bounds);
                result.StreetHalfWidth = Mathf.Max(result.StreetHalfWidth, bounds.extents.x);
                placedRoads++;
            }

            if (placedRoads == 0)
            {
                report.AppendLine("ERROR: street pieces have no renderers.");
                return result;
            }

            var roadBounds = result.Bounds;
            result.StreetStart = new Vector3(roadBounds.center.x, roadBounds.min.y, roadBounds.min.z);
            result.StreetEnd = new Vector3(roadBounds.center.x, roadBounds.min.y, roadBounds.max.z);
            result.StreetMid = roadBounds.center;
            result.StreetMid.y = roadBounds.min.y;

            if (sidewalkPrefab != null)
            {
                var sideWidth = MeasureShortWidth(sidewalkPrefab);
                var leftX = -(result.StreetHalfWidth + sideWidth * 0.5f + 0.2f);
                var rightX = result.StreetHalfWidth + sideWidth * 0.5f + 0.2f;
                PlaceStrip(sidewalkPrefab, walks, leftX, roadBounds.min.z, roadBounds.max.z, result);
                PlaceStrip(sidewalkPrefab, walks, rightX, roadBounds.min.z, roadBounds.max.z, result);
            }
            else
            {
                report.AppendLine("No sidewalk prefab. Streets only.");
            }

            var crossZ = roadBounds.min.z;
            var crossCursor = roadBounds.max.x + 0.2f;
            for (var i = 0; i < 2; i++)
            {
                var piece = Spawn(streetPrefab, roads);
                OrientLongAxis(piece, alongX: true);
                if (!PlaceMinX(piece, crossZ, ref crossCursor, out var bounds))
                    continue;
                Remember(result, bounds);
            }

            crossCursor = roadBounds.min.x - 0.2f;
            for (var i = 0; i < 2; i++)
            {
                var piece = Spawn(streetPrefab, roads);
                OrientLongAxis(piece, alongX: true);
                if (!PlaceMaxX(piece, crossZ, ref crossCursor, out var bounds))
                    continue;
                Remember(result, bounds);
            }

            var buildingPrefabs = CollectBuildings(catalog);
            report.AppendLine("Building prefabs selected: " + buildingPrefabs.Count);
            var rowGap = result.StreetHalfWidth + 6f;
            if (sidewalkPrefab != null)
                rowGap = result.StreetHalfWidth + 3.5f;

            PlaceBuildingRow(buildingPrefabs, buildings, rowGap, roadBounds, result, faceNegativeX: true);
            PlaceBuildingRow(buildingPrefabs, buildings, -rowGap, roadBounds, result, faceNegativeX: false);

            var poi = Find(catalog, "Supermaket_prefab") ?? Find(catalog, "Shop_A_prefab") ?? Find(catalog, "Bank_prefab");
            if (poi != null)
            {
                var poiObject = Spawn(poi, buildings);
                OrientLongAxis(poiObject, alongX: true);
                if (TryBounds(poiObject, out var bounds))
                {
                    var shift = new Vector3(roadBounds.center.x - bounds.center.x, -bounds.min.y, roadBounds.max.z + 1.5f - bounds.min.z);
                    poiObject.transform.position += shift;
                    if (TryBounds(poiObject, out bounds))
                    {
                        Remember(result, bounds);
                        result.PoiFront = new Vector3(bounds.center.x, bounds.min.y, bounds.min.z - 2f);
                        AddAccent(poiObject, artificial, result, new Color(0.75f, 0.9f, 1f));
                    }
                }
            }

            if (result.PoiFront == Vector3.zero)
                result.PoiFront = result.StreetEnd + new Vector3(0f, 0f, 4f);

            ScatterProps(catalog, props, artificial, result, roadBounds);
            PlaceParking(catalog, streetPrefab, parking, result, roadBounds);
            CollectWindows(root, result);
            EnsureColliders(root);
            MarkStatic(root);
            AddPropLods(props);

            result.EyeHeight = Mathf.Clamp(result.StreetHalfWidth * 0.28f, 1.55f, 2.4f);
            result.LowPoint = result.StreetMid + new Vector3(result.StreetHalfWidth * 0.35f, 0.45f, 0f);
            if (result.BuildingFront == Vector3.zero)
                result.BuildingFront = result.StreetMid + new Vector3(rowGap, result.EyeHeight, 0f);

            report.AppendLine("Buildings placed: " + result.BuildingCount);
            report.AppendLine("Street prefab: " + streetPrefab.name);
            report.AppendLine("Street half width: " + result.StreetHalfWidth.ToString("0.00"));
            report.AppendLine("City size: " + result.Bounds.size);
            report.AppendLine("Street lights: " + result.StreetLights.Count);
            report.AppendLine("Window renderers: " + result.Windows.Count);
            report.AppendLine("Sidewalk samples: " + result.SidewalkPoints.Count);
            return result;
        }

        static void PlaceStrip(GameObject prefab, GameObject parent, float xCenter, float minZ, float maxZ, CityBuildResult result)
        {
            var guard = 0;
            var cursor = minZ;
            while (cursor < maxZ - 0.5f && guard < 24)
            {
                guard++;
                var piece = Spawn(prefab, parent);
                OrientLongAxis(piece, alongX: false);
                if (!PlaceMin(piece, xCenter, ref cursor, out var bounds))
                    break;
                Remember(result, bounds);
                result.SidewalkPoints.Add(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                if (bounds.size.z < 0.2f)
                    break;
            }
        }

        static void PlaceBuildingRow(List<GameObject> prefabs, GameObject parent, float xEdge, Bounds road, CityBuildResult result, bool faceNegativeX)
        {
            if (prefabs.Count == 0)
                return;

            var cursor = road.min.z + result.StreetHalfWidth * 2f;
            var end = road.max.z;
            var index = 0;
            var guard = 0;
            var placed = 0;
            while (cursor < end - 0.5f && guard < 12)
            {
                guard++;
                var prefab = prefabs[index % prefabs.Count];
                index++;
                var building = Spawn(prefab, parent);
                OrientLongAxis(building, alongX: false);
                if (!faceNegativeX)
                    building.transform.Rotate(0f, 180f, 0f, Space.World);
                if (!TryBounds(building, out var bounds))
                {
                    UnityEngine.Object.DestroyImmediate(building);
                    cursor += 4f;
                    continue;
                }

                var shift = new Vector3(
                    faceNegativeX ? xEdge - bounds.min.x : xEdge - bounds.max.x,
                    -bounds.min.y,
                    cursor - bounds.min.z);
                building.transform.position += shift;
                if (!TryBounds(building, out bounds))
                {
                    UnityEngine.Object.DestroyImmediate(building);
                    cursor += 4f;
                    continue;
                }

                Remember(result, bounds);
                placed++;
                if (result.BuildingFront == Vector3.zero)
                    result.BuildingFront = new Vector3(faceNegativeX ? bounds.min.x - 3f : bounds.max.x + 3f, bounds.min.y + 1.6f, bounds.center.z);
                cursor = bounds.max.z + 0.6f;
            }

            result.BuildingCount += placed;
        }

        static void ScatterProps(List<GameObject> catalog, GameObject props, GameObject lights, CityBuildResult result, Bounds road)
        {
            var lamp = Find(catalog, "street_lamp 1 prefab") ?? Find(catalog, "Lamp_1_prefab") ?? Find(catalog, "street lamp 2 prefab");
            var stop = Find(catalog, "stop sign");
            var parkingSign = Find(catalog, "parking sign");
            var hydrant = Find(catalog, "Hydrant");
            var trash = Find(catalog, "trashcan prefab");
            var bench = Find(catalog, "bench prefab");
            var tree = Find(catalog, "Tree prefab");
            var bus = Find(catalog, "Bus stop prefab");
            var barrier = Find(catalog, "Parking_barrier prefab");
            var meter = Find(catalog, "parking meter prefab");

            var spacing = Mathf.Max(10f, result.StreetHalfWidth * 1.6f);
            for (var i = 0; i < result.SidewalkPoints.Count; i++)
            {
                var point = result.SidewalkPoints[i];
                if (lamp != null && i % 4 == 0 && result.StreetLights.Count < 10)
                {
                    var lampObject = Spawn(lamp, props);
                    DropOnPoint(lampObject, point);
                    var light = CreateLight(lights.transform, lampObject, 16f, LightType.Point);
                    result.StreetLights.Add(light);
                }

                if (bench != null && i % 3 == 1)
                    DropOnPoint(Spawn(bench, props), point + new Vector3(0f, 0f, 1.2f));
                if (tree != null && i % 3 == 2)
                    DropOnPoint(Spawn(tree, props), point + new Vector3(Mathf.Sign(point.x) * 1.5f, 0f, -1f));
                if (hydrant != null && i == 1)
                    DropOnPoint(Spawn(hydrant, props), point);
                if (trash != null && i == 2)
                    DropOnPoint(Spawn(trash, props), point + new Vector3(0f, 0f, 1.5f));
            }

            if (stop != null)
                DropOnPoint(Spawn(stop, props), result.StreetStart + new Vector3(result.StreetHalfWidth + 1.2f, 0f, 2f));
            if (parkingSign != null)
                DropOnPoint(Spawn(parkingSign, props), result.StreetEnd + new Vector3(-result.StreetHalfWidth - 1.2f, 0f, -2f));
            if (bus != null)
                DropOnPoint(Spawn(bus, props), new Vector3(-result.StreetHalfWidth - 2f, road.min.y, road.center.z));

            result.ParkingBarrier = barrier;
            result.ParkingMeter = meter;
            if (result.StreetLights.Count > 8)
            {
                for (var i = 8; i < result.StreetLights.Count; i++)
                    result.StreetLights[i].renderMode = LightRenderMode.ForceVertex;
            }
        }

        static void PlaceParking(List<GameObject> catalog, GameObject streetPrefab, GameObject parking, CityBuildResult result, Bounds road)
        {
            var pad = Find(catalog, "Street 4 grey 8M prefab") ?? streetPrefab;
            if (pad == null)
                return;

            var originX = road.max.x + result.StreetHalfWidth;
            var cursor = road.center.z - result.StreetHalfWidth;
            for (var i = 0; i < 2; i++)
            {
                var piece = Spawn(pad, parking);
                OrientLongAxis(piece, alongX: false);
                if (!PlaceMin(piece, originX, ref cursor, out var bounds))
                    continue;
                Remember(result, bounds);
            }

            if (result.ParkingBarrier != null)
            {
                for (var i = 0; i < 3; i++)
                    DropOnPoint(Spawn(result.ParkingBarrier, parking), new Vector3(originX, road.min.y, road.center.z + i * 3f));
            }

            if (result.ParkingMeter != null)
                DropOnPoint(Spawn(result.ParkingMeter, parking), new Vector3(originX + 2f, road.min.y, road.center.z));
        }

        public static void EnsureColliders(GameObject root)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                var host = filters[i].gameObject;
                var mesh = filters[i].sharedMesh;
                var label = host.name.ToLowerInvariant();
                var decorative = label.Contains("grass") || label.Contains("flower") || label.Contains("leaf") || label.Contains("plant");
                var collider = host.GetComponent<Collider>();
                if (decorative)
                {
                    if (collider != null)
                        UnityEngine.Object.DestroyImmediate(collider);
                    continue;
                }

                var structural = label.Contains("street") || label.Contains("side") || label.Contains("walk") || label.Contains("build") || label.Contains("shop") || label.Contains("wall");
                if (collider is MeshCollider meshCollider && mesh != null && mesh.vertexCount > 900 && !structural)
                {
                    UnityEngine.Object.DestroyImmediate(meshCollider);
                    AddBox(host);
                    continue;
                }

                if (collider == null)
                    AddBox(host);
            }
        }

        public static void MarkStatic(GameObject root)
        {
            var flags = StaticEditorFlags.BatchingStatic |
                        StaticEditorFlags.OccludeeStatic |
                        StaticEditorFlags.OccluderStatic |
                        StaticEditorFlags.ReflectionProbeStatic |
                        StaticEditorFlags.ContributeGI;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var go = transforms[i].gameObject;
                if (go.GetComponent<Light>() != null)
                    continue;
                GameObjectUtility.SetStaticEditorFlags(go, flags);
            }
        }

        static void AddPropLods(GameObject props)
        {
            for (var i = 0; i < props.transform.childCount; i++)
            {
                var child = props.transform.GetChild(i).gameObject;
                if (child.GetComponent<LODGroup>() != null)
                    continue;
                var renderers = child.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    continue;
                var group = child.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.CrossFade;
                group.SetLODs(new[]
                {
                    new LOD(0.045f, renderers),
                    new LOD(0.01f, Array.Empty<Renderer>())
                });
                group.RecalculateBounds();
            }
        }

        static void CollectWindows(GameObject root, CityBuildResult result)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var materials = renderers[i].sharedMaterials;
                for (var m = 0; m < materials.Length; m++)
                {
                    if (materials[m] == null)
                        continue;
                    if (materials[m].name.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.Windows.Add(renderers[i]);
                        break;
                    }
                }
            }
        }

        static void AddAccent(GameObject target, GameObject lights, CityBuildResult result, Color color)
        {
            var light = CreateLight(lights.transform, target, 10f, LightType.Point);
            light.color = color;
            if (TryBounds(target, out var bounds))
                light.transform.position = bounds.center + Vector3.up * Mathf.Min(3f, bounds.extents.y);
            result.AccentLights.Add(light);
        }

        static Light CreateLight(Transform parent, GameObject anchor, float range, LightType type)
        {
            var go = new GameObject(anchor.name + "_Light");
            go.transform.SetParent(parent, false);
            if (TryBounds(anchor, out var bounds))
                go.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.15f, bounds.center.z);
            else
                go.transform.position = anchor.transform.position + Vector3.up * 3f;
            var light = go.AddComponent<Light>();
            light.type = type;
            light.range = range;
            light.intensity = 1.2f;
            light.enabled = false;
            light.shadows = LightShadows.None;
            light.color = new Color(1f, 0.84f, 0.6f);
            return light;
        }

        static void AddBox(GameObject host)
        {
            if (!TryBounds(host, out var bounds))
                return;
            var box = host.AddComponent<BoxCollider>();
            box.center = host.transform.InverseTransformPoint(bounds.center);
            var size = bounds.size;
            var scale = host.transform.lossyScale;
            box.size = new Vector3(
                scale.x != 0f ? size.x / Mathf.Abs(scale.x) : size.x,
                scale.y != 0f ? size.y / Mathf.Abs(scale.y) : size.y,
                scale.z != 0f ? size.z / Mathf.Abs(scale.z) : size.z);
        }

        static void DropOnPoint(GameObject go, Vector3 point)
        {
            if (!TryBounds(go, out var bounds))
            {
                go.transform.position = point;
                return;
            }

            go.transform.position += new Vector3(point.x - bounds.center.x, point.y - bounds.min.y, point.z - bounds.center.z);
        }

        static float MeasureShortWidth(GameObject prefab)
        {
            var probe = Spawn(prefab, null);
            OrientLongAxis(probe, alongX: false);
            var width = 2f;
            if (TryBounds(probe, out var bounds))
                width = Mathf.Min(bounds.size.x, bounds.size.z);
            UnityEngine.Object.DestroyImmediate(probe);
            return Mathf.Max(0.5f, width);
        }

        static bool PlaceMin(GameObject go, float xCenter, ref float cursorZ, out Bounds bounds)
        {
            if (!TryBounds(go, out bounds))
                return false;
            go.transform.position += new Vector3(xCenter - bounds.center.x, -bounds.min.y, cursorZ - bounds.min.z);
            if (!TryBounds(go, out bounds))
                return false;
            cursorZ = bounds.max.z - 0.04f;
            return true;
        }

        static bool PlaceMinX(GameObject go, float zCenter, ref float cursorX, out Bounds bounds)
        {
            if (!TryBounds(go, out bounds))
                return false;
            go.transform.position += new Vector3(cursorX - bounds.min.x, -bounds.min.y, zCenter - bounds.center.z);
            if (!TryBounds(go, out bounds))
                return false;
            cursorX = bounds.max.x - 0.04f;
            return true;
        }

        static bool PlaceMaxX(GameObject go, float zCenter, ref float cursorX, out Bounds bounds)
        {
            if (!TryBounds(go, out bounds))
                return false;
            go.transform.position += new Vector3(cursorX - bounds.max.x, -bounds.min.y, zCenter - bounds.center.z);
            if (!TryBounds(go, out bounds))
                return false;
            cursorX = bounds.min.x + 0.04f;
            return true;
        }

        static void OrientLongAxis(GameObject go, bool alongX)
        {
            if (!TryBounds(go, out var bounds))
                return;
            var longerX = bounds.size.x > bounds.size.z;
            if (alongX != longerX)
                go.transform.Rotate(0f, 90f, 0f, Space.World);
        }

        static void Remember(CityBuildResult result, Bounds bounds)
        {
            if (!result.HasBounds)
            {
                result.Bounds = bounds;
                result.HasBounds = true;
                return;
            }

            result.Bounds.Encapsulate(bounds);
        }

        static List<GameObject> CollectBuildings(List<GameObject> catalog)
        {
            var wanted = new[]
            {
                "Shop_A_prefab", "Shop_B_prefab", "Shop_C_prefab", "Building_A_prefab", "Building_B_prefab",
                "Building_D_prefab", "Building_E_prefab", "Building_H_prefab", "Bank_prefab", "Building_F_prefab"
            };
            var list = new List<GameObject>();
            for (var i = 0; i < wanted.Length; i++)
            {
                var found = Find(catalog, wanted[i]);
                if (found != null)
                    list.Add(found);
            }

            return list;
        }

        static GameObject Spawn(GameObject prefab, GameObject parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null)
                instance.transform.SetParent(parent.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        public static bool TryBounds(GameObject go, out Bounds bounds)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(go.transform.position, Vector3.zero);
            var any = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer || !renderers[i].enabled)
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

            return any;
        }

        static List<GameObject> LoadCatalog()
        {
            var list = new List<GameObject>();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    list.Add(prefab);
            }

            return list;
        }

        static GameObject Find(List<GameObject> catalog, string name)
        {
            for (var i = 0; i < catalog.Count; i++)
            {
                if (string.Equals(catalog[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return catalog[i];
            }

            for (var i = 0; i < catalog.Count; i++)
            {
                if (catalog[i].name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return catalog[i];
            }

            return null;
        }
    }
}
