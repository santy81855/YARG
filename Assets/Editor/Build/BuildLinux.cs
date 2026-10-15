using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor.Build
{
    public static class BuildLinux
    {
        // Batch entry point: -executeMethod Editor.Build.BuildLinux.Local -yarg-output /path/YARG.x86_64
        public static void Local()
        {
            var args = Environment.GetCommandLineArgs();
            int outputOption = Array.IndexOf(args, "-yarg-output");
            string output = outputOption >= 0 && outputOption + 1 < args.Length ? args[outputOption + 1] :
                Path.GetFullPath("Builds/Linux/YARG.x86_64");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
                // Match the editor's dev settings folder so this local build keeps the user's
                // existing profiles, song folders, calibration, and remembered song keys.
                extraScriptingDefines = new[] { "YARG_TEST_BUILD" }
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Linux build failed. Check the build log.");
            Debug.Log("YARG_LINUX_BUILD_SUCCESS: " + output);
        }
    }
}
