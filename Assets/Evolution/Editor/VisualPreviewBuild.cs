using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Evolution.Editor
{
    // Only the opt-in Visual Preview workflow builds this isolated scene/player.
    public static class VisualPreviewBuild
    {
        public static void Build()
        {
            if (UnityEngine.Application.unityVersion != "6000.6.9f1")
                throw new BuildFailedException("Visual Preview requires Unity 6000.6.9f1.");
            PrototypeSetup.CreatePrototype();
            ConfigurePlayerGraphics();
            var arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-customBuildPath");
            if (index < 0 || index + 1 >= arguments.Length)
                throw new BuildFailedException("Missing -customBuildPath.");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Evolution/Scenes/WaterPrototype3D.unity" },
                locationPathName = arguments[index + 1],
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            });
            if (result.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Water Visual Preview player build failed.");
        }

        // Both Mono previews and the real IL2CPP verification player use runtime
        // instanced materials. Their shader variants must survive build stripping.
        internal static void ConfigurePlayerGraphics()
        {
            // The prototype creates instanced materials at runtime, so scene usage cannot
            // tell the build stripper that INSTANCING_ON is needed.
            var graphics = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
            var instancing = graphics.FindProperty("m_InstancingStripping") ?? graphics.FindProperty("m_InstancingVariants");
            if (instancing == null)
                throw new BuildFailedException("Cannot locate Unity instancing stripping settings.");
            int keepAll = Array.FindIndex(instancing.enumDisplayNames,
                name => name.Replace(" ", "").Equals("KeepAll", StringComparison.OrdinalIgnoreCase));
            if (keepAll < 0)
                throw new BuildFailedException("Cannot locate Keep All instancing option: " + string.Join(", ", instancing.enumDisplayNames));
            instancing.enumValueIndex = keepAll;
            graphics.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore });
        }
    }
}
