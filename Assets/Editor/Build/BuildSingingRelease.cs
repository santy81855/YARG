using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Editor.Build
{
    public static class BuildSingingRelease
    {
        public static string Argument(string option)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, option);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        // Start Unity with -buildTarget Linux64 or Win64 to compile the correct platform code.
        public static void Build()
        {
            string platform = Argument("-yarg-platform");
            var target = platform switch
            {
                "linux" => BuildTarget.StandaloneLinux64,
                "windows" => BuildTarget.StandaloneWindows64,
                _ => throw new ArgumentException("Supply -yarg-platform linux or windows.")
            };
            string output = Argument("-yarg-output") ?? throw new ArgumentException("Supply -yarg-output.");
            string version = Argument("-yarg-version") ?? throw new ArgumentException("Supply -yarg-version.");
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new InvalidOperationException("Start Unity with the matching -buildTarget argument.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException("The target's Unity build module is not installed.");

            string previousName = PlayerSettings.productName;
            string previousVersion = PlayerSettings.bundleVersion;
            var previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            bool previousDefaultGraphics = PlayerSettings.GetUseDefaultGraphicsAPIs(target);
            var previousGraphics = PlayerSettings.GetGraphicsAPIs(target);
            try
            {
                // A separate product name keeps this fork's profiles and settings apart from YARG.
                PlayerSettings.productName = "YARG Singing";
                PlayerSettings.bundleVersion = version;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                if (target == BuildTarget.StandaloneWindows64)
                {
                    PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
                    PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Direct3D11 });
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                        .Select(scene => scene.path).ToArray(),
                    locationPathName = output,
                    target = target,
                    options = BuildOptions.None,
                    // Enables the version resource. The distinct product name isolates its dev folder.
                    extraScriptingDefines = new[] { "YARG_TEST_BUILD" }
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"{platform} release build failed; see the Unity log.");
                Debug.Log($"YARG_SINGING_RELEASE_SUCCESS: {platform} {version} {output}");
            }
            finally
            {
                PlayerSettings.productName = previousName;
                PlayerSettings.bundleVersion = previousVersion;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previousBackend);
                if (target == BuildTarget.StandaloneWindows64)
                {
                    PlayerSettings.SetGraphicsAPIs(target, previousGraphics);
                    PlayerSettings.SetUseDefaultGraphicsAPIs(target, previousDefaultGraphics);
                }
            }
        }
    }
}
