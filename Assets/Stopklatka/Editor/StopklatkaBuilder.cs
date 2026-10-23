using System.Collections.Generic;
using System.IO;
using System.Linq;
using Meta.XR.EnvironmentDepth;
using Meta.XR.MRUtilityKit;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Stopklatka.EditorTools
{
    /// <summary>
    /// One click from a fresh Meta XR project to a buildable game:
    /// project settings for Quest 3 at 120 Hz, materials, and the scene
    /// (camera rig with passthrough, MRUK, room colliders, NavMesh, Depth API, game objects).
    /// Safe to run again; it updates what is already there.
    /// </summary>
    public static class StopklatkaBuilder
    {
        const string Root = "Assets/Stopklatka";
        const string MaterialDir = Root + "/Resources/Materials";
        const string ScenePath = Root + "/Scenes/Stopklatka.unity";

        [MenuItem("Stopklatka/Build Game (settings + materials + scene)", priority = 0)]
        public static void BuildAll()
        {
            ConfigureProject();
            CreateMaterials();
            BuildScene();
            Debug.Log("[Stopklatka] Done. Next: Meta > Tools > Project Setup Tool > Fix All, then File > Build And Run.");
        }

        // --- project settings -----------------------------------------------------

        [MenuItem("Stopklatka/1. Configure Project", priority = 20)]
        public static void ConfigureProject()
        {
            PlayerSettings.companyName = "Pl0T3R";
            PlayerSettings.productName = "Stopklatka";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.pl0t3r.stopklatka");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel32)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            QualitySettings.shadows = ShadowQuality.Disable;

            SetLayer(Layers.Room, "Room");
            SetLayer(Layers.Enemy, "Enemy");
            SetLayer(Layers.Grabbable, "Grabbable");
            SetLayer(Layers.Player, "Player");
            SetLayer(Layers.Shard, "Shard");
            SetLayer(Layers.Nav, "NavBake");

            ConfigureUrp();
            ConfigureMetaFeatures();
            AssetDatabase.SaveAssets();
            Debug.Log("[Stopklatka] Project configured: Vulkan, IL2CPP ARM64, ASTC, URP tuned for 120 Hz, passthrough + scene + boundary features.");
        }

        static void ConfigureUrp()
        {
            var assets = new HashSet<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset d) assets.Add(d);
            for (int i = 0; i < QualitySettings.count; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset q) assets.Add(q);
            if (assets.Count == 0) Debug.LogWarning("[Stopklatka] No URP asset found. The project must use URP.");
            foreach (var urp in assets)
            {
                urp.msaaSampleCount = 4;
                urp.supportsHDR = false;
                urp.shadowDistance = 0f;
                urp.supportsCameraDepthTexture = false;
                urp.supportsCameraOpaqueTexture = false;
                urp.useSRPBatcher = true;
                urp.renderScale = 1f;   // OVRManager raises it to the dynamic-resolution maximum at runtime
                EditorUtility.SetDirty(urp);
            }
        }

        static void ConfigureMetaFeatures()
        {
            var config = OVRProjectConfig.CachedProjectConfig;
            if (config == null)
            {
                Debug.LogWarning("[Stopklatka] OVRProjectConfig not ready yet; run Stopklatka > 1. Configure Project again in a moment.");
                return;
            }
            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
            config.sceneSupport = OVRProjectConfig.FeatureSupport.Required;
            config.boundaryVisibilitySupport = OVRProjectConfig.FeatureSupport.Required;
            config.anchorSupport = OVRProjectConfig.AnchorSupport.Enabled;
            config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersOnly;
            config.targetDeviceTypes = new List<OVRProjectConfig.DeviceType>
            {
                OVRProjectConfig.DeviceType.Quest3, OVRProjectConfig.DeviceType.Quest3S,
            };
            OVRProjectConfig.CommitProjectConfig(config);
        }

        static void SetLayer(int index, string name)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != name)
                Debug.LogWarning($"[Stopklatka] Layer {index} was '{layer.stringValue}', renaming to '{name}'.");
            layer.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- materials ------------------------------------------------------------

        [MenuItem("Stopklatka/2. Create Materials", priority = 21)]
        public static void CreateMaterials()
        {
            Directory.CreateDirectory(MaterialDir);
            Crystal(Mats.Enemy, new Color(0.86f, 0.1f, 0.08f), new Color(1f, 0.62f, 0.55f), 0.55f, 0.6f, 0f);
            Crystal(Mats.Weapon, new Color(0.07f, 0.07f, 0.08f), new Color(0.55f, 0.55f, 0.6f), 0.5f, 0.8f, 0f);
            Crystal(Mats.Blade, new Color(0.8f, 0.82f, 0.86f), Color.white, 0.6f, 0.7f, 0f);
            Crystal(Mats.Glass, new Color(0.86f, 0.93f, 0.96f), Color.white, 0.7f, 0.7f, 0.05f);
            Crystal(Mats.Target, new Color(0.95f, 0.15f, 0.1f), new Color(1f, 0.7f, 0.6f), 0.6f, 0.8f, 0.25f);
            Flat(Mats.EnemyTrail, new Color(1f, 0.15f, 0.1f, 0.55f));
            Flat(Mats.PlayerTrail, new Color(0.12f, 0.12f, 0.14f, 0.45f));
            Flat(Mats.Flash, new Color(1f, 0.92f, 0.65f, 0.9f));
            Flat(Mats.RedFlash, new Color(1f, 0f, 0f, 0.5f));
            Flat(Mats.Hidden, new Color(0f, 0f, 0f, 0f));
            var text = Material(Mats.Text, "Stopklatka/Text");
            text.SetColor("_BaseColor", Color.white);
            AssetDatabase.SaveAssets();
        }

        static void Crystal(string name, Color baseColor, Color rim, float ambient, float rimStrength, float emission)
        {
            var m = Material(name, "Stopklatka/Crystal");
            m.SetColor("_BaseColor", baseColor);
            m.SetColor("_RimColor", rim);
            m.SetFloat("_Ambient", ambient);
            m.SetFloat("_RimStrength", rimStrength);
            m.SetFloat("_Emission", emission);
        }

        static void Flat(string name, Color color) => Material(name, "Stopklatka/Transparent").SetColor("_BaseColor", color);

        static Material Material(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) Debug.LogError($"[Stopklatka] Shader {shaderName} not found. Check the console for shader compile errors.");
            var path = $"{MaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (shader != null) m.shader = shader;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // --- scene ----------------------------------------------------------------

        [MenuItem("Stopklatka/3. Build Scene", priority = 22)]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;

            // Camera rig with passthrough
            var rigGo = InstantiatePrefab("OVRCameraRig", "com.meta.xr.sdk.core");
            if (rigGo == null) { Debug.LogError("[Stopklatka] OVRCameraRig prefab not found. Is Meta XR Core SDK installed?"); return; }
            var ovr = rigGo.GetComponent<OVRManager>() ?? rigGo.AddComponent<OVRManager>();
            ovr.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            ovr.isInsightPassthroughEnabled = true;
            ovr.shouldBoundaryVisibilityBeSuppressed = true;
            ovr.enableDynamicResolution = true;
            ovr.quest3MinDynamicResolutionScale = 1.0f;
            ovr.quest3MaxDynamicResolutionScale = 1.205f;   // ~2024 x 2120 per eye
            var ovrSo = new SerializedObject(ovr);
            SetBool(ovrSo, "requestScenePermissionOnStartup", true);
            ovrSo.ApplyModifiedPropertiesWithoutUndo();
            if (rigGo.GetComponent<OVRPassthroughLayer>() == null) rigGo.AddComponent<OVRPassthroughLayer>();

            foreach (var cam in rigGo.GetComponentsInChildren<Camera>(true))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 60f;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = false;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.requiresColorOption = CameraOverrideOption.Off;
            }

            // Real-world occlusion
            var depthGo = new GameObject("EnvironmentDepth");
            var depth = depthGo.AddComponent<EnvironmentDepthManager>();
            var depthSo = new SerializedObject(depth);
            var mode = depthSo.FindProperty("_occlusionShadersMode");
            if (mode != null) mode.enumValueIndex = (int)OcclusionShadersMode.HardOcclusion;
            SetBool(depthSo, "_removeHands", true);   // your own hands don't hide the gun you hold
            depthSo.ApplyModifiedPropertiesWithoutUndo();

            // Scene understanding: load every room ourselves after the permission prompt
            var mrukGo = InstantiatePrefab("MRUK", "com.meta.xr.mrutilitykit") ?? new GameObject("MRUK");
            var mruk = mrukGo.GetComponent<MRUK>() ?? mrukGo.AddComponent<MRUK>();
            mruk.SceneSettings ??= new MRUK.MRUKSettings();
            mruk.SceneSettings.DataSource = MRUK.SceneDataSource.Device;
            mruk.SceneSettings.LoadSceneOnStartup = false;
            mruk.EnableWorldLock = true;
            EditorUtility.SetDirty(mruk);

            // Walls, floor and furniture become invisible colliders that stop bullets
            var colliderGo = new GameObject("RoomColliders");
            var effect = colliderGo.AddComponent<EffectMesh>();
            effect.SpawnOnStart = MRUK.RoomFilter.AllRooms;
            effect.Colliders = true;
            effect.Labels = MRUKAnchor.SceneLabels.WALL_FACE | MRUKAnchor.SceneLabels.INVISIBLE_WALL_FACE |
                            MRUKAnchor.SceneLabels.INNER_WALL_FACE | MRUKAnchor.SceneLabels.FLOOR |
                            MRUKAnchor.SceneLabels.CEILING | MRUKAnchor.SceneLabels.TABLE |
                            MRUKAnchor.SceneLabels.COUCH | MRUKAnchor.SceneLabels.STORAGE |
                            MRUKAnchor.SceneLabels.BED | MRUKAnchor.SceneLabels.SCREEN |
                            MRUKAnchor.SceneLabels.OTHER;
            effect.MeshMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{Mats.Hidden}.mat");
            effect.Layer = Layers.Room;
            var effectSo = new SerializedObject(effect);
            SetBool(effectSo, "hideMesh", true);
            SetBool(effectSo, "castShadows", false);
            effectSo.ApplyModifiedPropertiesWithoutUndo();

            // NavMesh across all rooms, furniture carved out
            var navGo = new GameObject("SceneNavigation");
            var nav = navGo.AddComponent<SceneNavigation>();
            nav.BuildOnSceneLoaded = MRUK.RoomFilter.AllRooms;
            nav.NavigableSurfaces = MRUKAnchor.SceneLabels.FLOOR;
            nav.SceneObstacles = MRUKAnchor.SceneLabels.TABLE | MRUKAnchor.SceneLabels.COUCH | MRUKAnchor.SceneLabels.STORAGE |
                                 MRUKAnchor.SceneLabels.BED | MRUKAnchor.SceneLabels.SCREEN | MRUKAnchor.SceneLabels.PLANT |
                                 MRUKAnchor.SceneLabels.LAMP | MRUKAnchor.SceneLabels.OTHER;
            nav.Layers = 1 << Layers.Nav;
            nav.AgentRadius = 0.25f;
            nav.AgentHeight = 1.75f;
            nav.AgentClimb = 0.05f;
            nav.UseSceneData = true;
            nav.CustomAgent = true;
            if (navGo.GetComponent<NavMeshSurface>() == null) navGo.AddComponent<NavMeshSurface>();

            // The game
            var game = new GameObject("Stopklatka");
            game.AddComponent<PlayerRig>();
            game.AddComponent<TimeController>();
            game.AddComponent<PerformanceManager>();
            game.AddComponent<RoomService>();
            game.AddComponent<PassthroughStyle>();
            game.AddComponent<SoundBank>();
            game.AddComponent<Hud>();
            game.AddComponent<GameManager>();
            var left = new GameObject("LeftHand");
            left.transform.SetParent(game.transform, false);
            left.AddComponent<ControllerHand>().IsLeft = true;
            var right = new GameObject("RightHand");
            right.transform.SetParent(game.transform, false);
            right.AddComponent<ControllerHand>().IsLeft = false;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[Stopklatka] Scene saved to {ScenePath} and set as the only scene in the build.");
        }

        static GameObject InstantiatePrefab(string name, string packageHint)
        {
            var path = AssetDatabase.FindAssets($"{name} t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p) == name)
                .OrderByDescending(p => p.Contains(packageHint))
                .FirstOrDefault();
            if (path == null) return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            return go;
        }

        static void SetBool(SerializedObject so, string field, bool value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.boolValue = value;
            else Debug.LogWarning($"[Stopklatka] Field {field} not found on {so.targetObject.GetType().Name}; set it in the Inspector.");
        }
    }
}
