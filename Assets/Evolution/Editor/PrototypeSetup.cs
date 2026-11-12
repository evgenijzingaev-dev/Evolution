using System.IO;
using Evolution.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Evolution.Editor
{
    public static class PrototypeSetup
    {
        const string ScenePath = "Assets/Evolution/Scenes/CellPrototype.unity";
        [InitializeOnLoadMethod]
        static void OnLoad() { EditorApplication.delayCall += EnsureProject; }
        static void EnsureProject()
        {
            if ((!File.Exists(ScenePath) || GraphicsSettings.defaultRenderPipeline == null) && !Application.isBatchMode) CreatePrototype();
        }
        [MenuItem("Evolution/Create or Open Cell Prototype")]
        public static void CreatePrototype()
        {
            const string pipelinePath = "Assets/Evolution/Settings/PrototypeURP.asset";
            if (!AssetDatabase.IsValidFolder("Assets/Evolution/Settings"))
                AssetDatabase.CreateFolder("Assets/Evolution", "Settings");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (!pipeline) {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Evolution/Settings/PrototypeRenderer.asset");
                if (!renderer) {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, "Assets/Evolution/Settings/PrototypeRenderer.asset");
                }
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            if (!File.Exists(ScenePath)) {
                // Additive creation preserves any unsaved scene the user has open.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                new GameObject("Evolution Simulation", typeof(EvolutionRunner));
                EditorSceneManager.SaveScene(scene, ScenePath);
            } else if (!Application.isBatchMode) {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
                if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
