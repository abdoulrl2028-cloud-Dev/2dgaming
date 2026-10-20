using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CityPack.Editor
{
    public static class WebClientBuild
    {
        public static void Build()
        {
            AssetDatabase.ImportAsset("Assets/CityPack/Audio/Resources/GameAudio", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            Directory.CreateDirectory("Builds");
            PlayerSettings.productName = "2Dgaming";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("Web client build " + report.summary.result + " size=" + report.summary.totalSize);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
