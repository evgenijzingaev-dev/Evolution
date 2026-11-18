using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Evolution.Editor
{
    // Invoked only by CI. No changes to simulation or interactive startup.
    public static class ContinuousIntegration
    {
        public static void ValidateCompilation()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetName().Name).ToArray();
            foreach (var name in new[] { "Evolution.Core", "Evolution.Localization", "Evolution.Runtime", "Evolution.Editor" })
                if (!assemblies.Contains(name))
                    throw new BuildFailedException("Expected compiled assembly is missing: " + name);

            // Batch mode intentionally skips the project's interactive setup callback.
            PrototypeSetup.CreatePrototype();
            Debug.Log("CI: Core, Localization, Runtime and Editor assemblies loaded; prototype assets prepared.");
        }

        public static void BuildLinux()
        {
            const BuildTarget target = BuildTarget.StandaloneLinux64;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new BuildFailedException("Linux build module is missing from the selected GameCI image.");

            PrototypeSetup.CreatePrototype();
            // Retain the legacy startup/input smoke test and include the real 3D
            // game for the opt-in in-player URP capture in the same IL2CPP build.
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path)
                .Concat(new[] { "Assets/Evolution/Scenes/WaterPrototype3D.unity" }).Distinct().ToArray();
            if (scenes.Length == 0 || scenes.Any(scene => !File.Exists(scene)))
                throw new BuildFailedException("Verification build requires existing, enabled scenes.");

            var arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-customBuildPath");
            if (index < 0 || index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
                throw new BuildFailedException("GameCI must supply -customBuildPath.");

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            VisualPreviewBuild.ConfigurePlayerGraphics();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes,
                locationPathName = arguments[index + 1],
                target = target,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Linux verification build failed: " + report.summary.result);
            Debug.Log("CI: Linux IL2CPP verification build succeeded.");
        }
    }
}
