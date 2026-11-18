using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
namespace Evolution.Editor
{
    public static class WaterPrototypeSetup
    {
        public const string ScenePath="Assets/Evolution/Scenes/WaterPrototype3D.unity";
        [MenuItem("Evolution/Experiments/Open 3D Water Prototype")]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(GraphicsSettings.defaultRenderPipeline==null)PrototypeSetup.CreatePrototype();
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        }
    }
}
