using CityPack.Zombies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityPack.Editor
{
    public static class GroundPlacement
    {
        const string GameScene = "Assets/CityPack/Scenes/Game.unity";

        [MenuItem("Tools/City Pack/Place Actors On Street")]
        public static void PlaceMenu()
        {
            Place();
        }

        public static void Place()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != GameScene)
                scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);

            Physics.SyncTransforms();
            if (!Grounding.TryStreet(out var road))
            {
                Debug.LogWarning("No street surface found.");
                return;
            }
            var player = GameObject.Find("Player");
            if (player != null)
            {
                var spawn = new Vector3(road.center.x, road.max.y + 8f, road.min.z + 8f);
                if (Grounding.Sample(spawn, out var ground))
                    player.transform.position = ground;
            }

            var start = GameObject.Find("PathStart");
            var end = GameObject.Find("PathEnd");
            if (start != null)
                start.transform.position = Grounding.OnStreet(new Vector3(road.center.x, road.max.y, road.min.z), 0f);
            if (end != null)
                end.transform.position = Grounding.OnStreet(new Vector3(road.center.x, road.max.y, road.max.z), 1f);

            var points = Object.FindObjectsByType<ZombieEncounterPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < points.Length; i++)
            {
                var point = points[i];
                var along = Mathf.Clamp01((point.minimumPlayerProgress + point.maximumPlayerProgress) * 0.5f);
                var side = point.transform.position.x - road.center.x;
                side = Mathf.Clamp(side, -(road.extents.x - 2f), road.extents.x - 2f);
                var probe = new Vector3(road.center.x + side, road.max.y + 8f, Mathf.Lerp(road.min.z + 6f, road.max.z - 6f, along));
                if (Grounding.Sample(probe, out var ground))
                    point.transform.position = ground;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Player and encounters placed on the street.");
        }
    }
}
