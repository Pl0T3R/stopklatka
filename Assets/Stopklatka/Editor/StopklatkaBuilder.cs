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
using UnityEngine.SceneManagement;

namespace Stopklatka.EditorTools
{
    /// <summary>
    /// One click from a fresh Meta XR project to a buildable game:
    /// project settings for Quest 3 (90 Hz by default, 120 Hz in GameConfig), materials, the
    /// GameConfig asset, and the scene
    /// (camera rig with passthrough, MRUK, room colliders, NavMesh, Depth API, game objects).
    /// Safe to run again; it updates what is already there.
    /// </summary>
    public static class StopklatkaBuilder
    {
        const string Root = "Assets/Stopklatka";
        const string MaterialDir = Root + "/Resources/Materials";
        const string ScenePath = Root + "/Scenes/Stopklatka.unity";
        const string TimeTestScenePath = Root + "/Scenes/TimeTest.unity";
        const string ConfigPath = Root + "/Resources/" + GameConfig.ResourcePath + ".asset";

        [MenuItem("Stopklatka/Build Game (settings + materials + scene)", priority = 0)]
        public static void BuildAll()
        {
            ConfigureProject();
            CreateMaterials();
            EnsureGameConfig();
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
            Debug.Log("[Stopklatka] Project configured: Vulkan, IL2CPP ARM64, ASTC, URP tuned for 90/120 Hz, passthrough + scene + boundary features.");
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
            // Superhot palette: red enemies, glossy black weapons, white glass. Tune colors here.
            // Crystal(name, lit, shadow, rim color, rim strength, highlight strength, gloss, emission)
            Crystal(Mats.Enemy, new Color(0.92f, 0.13f, 0.09f), new Color(0.5f, 0.03f, 0.04f), new Color(1f, 0.6f, 0.52f), 0.65f, 0.55f, 40f, 0f);
            Crystal(Mats.EnemyDark, new Color(0.62f, 0.05f, 0.05f), new Color(0.3f, 0.01f, 0.02f), new Color(1f, 0.4f, 0.35f), 0.5f, 0.4f, 32f, 0f);
            var reveal = Crystal(Mats.EnemyReveal, new Color(0.92f, 0.13f, 0.09f), new Color(0.5f, 0.03f, 0.04f), new Color(1f, 0.6f, 0.52f), 0.65f, 0.55f, 40f, 0f);
            reveal.SetColor("_RevealColor", new Color(1f, 0.85f, 0.75f));
            reveal.EnableKeyword("_REVEAL");
            Crystal(Mats.Weapon, new Color(0.1f, 0.1f, 0.11f), new Color(0.03f, 0.03f, 0.035f), new Color(0.5f, 0.52f, 0.58f), 0.5f, 0.9f, 64f, 0f);
            Crystal(Mats.Blade, new Color(0.85f, 0.87f, 0.9f), new Color(0.5f, 0.53f, 0.58f), Color.white, 0.6f, 1.2f, 128f, 0f);
            Crystal(Mats.Glass, new Color(0.9f, 0.95f, 0.97f), new Color(0.62f, 0.72f, 0.78f), Color.white, 0.7f, 1f, 96f, 0.05f);
            Crystal(Mats.Target, new Color(0.98f, 0.16f, 0.1f), new Color(0.6f, 0.04f, 0.04f), new Color(1f, 0.7f, 0.6f), 0.8f, 0.8f, 48f, 0.25f);
            Flat(Mats.EnemyTrail, new Color(1f, 0.12f, 0.08f, 0.75f));
            Flat(Mats.PlayerTrail, new Color(0.08f, 0.08f, 0.1f, 0.6f));
            Flat(Mats.Flash, new Color(1f, 0.92f, 0.65f, 0.9f));
            Flat(Mats.RedFlash, new Color(0.9f, 0.02f, 0.02f, 0.6f));
            // things lying on the real floor: the bias stops the floor's own depth from hiding them
            Flat(Mats.FloorShadow, new Color(0.04f, 0.04f, 0.06f, 0.42f), 0.08f);
            Flat(Mats.RingFx, new Color(1f, 0.12f, 0.08f, 0.85f), 0.08f);
            Flat(Mats.Hidden, new Color(0f, 0f, 0f, 0f));
            var text = Material(Mats.Text, "Stopklatka/Text");
            text.SetColor("_BaseColor", Color.white);
            AssetDatabase.SaveAssets();
        }

        static Material Crystal(string name, Color lit, Color shadow, Color rim, float rimStrength, float specStrength, float gloss, float emission)
        {
            var m = Material(name, "Stopklatka/Crystal");
            m.SetColor("_BaseColor", lit);
            m.SetColor("_ShadowColor", shadow);
            m.SetColor("_RimColor", rim);
            m.SetFloat("_RimStrength", rimStrength);
            m.SetFloat("_SpecStrength", specStrength);
            m.SetFloat("_Gloss", gloss);
            m.SetFloat("_Emission", emission);
            m.DisableKeyword("_REVEAL");
            return m;
        }

        static void Flat(string name, Color color, float occlusionBias = 0f)
        {
            var m = Material(name, "Stopklatka/Transparent");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_OcclusionBias", occlusionBias);
        }

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

        // --- game config ----------------------------------------------------------

        [MenuItem("Stopklatka/4. Create Game Config (if missing)", priority = 23)]
        public static void EnsureGameConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Stopklatka] Created {ConfigPath} with default settings. Tune the game there.");
        }

        // --- scenes ---------------------------------------------------------------

        [MenuItem("Stopklatka/3. Build Scene", priority = 22)]
        public static void BuildScene()
        {
            var scene = NewScene();
            var rigGo = CreateCameraRig(suppressBoundary: true);
            if (rigGo == null) return;
            var ovrSo = new SerializedObject(rigGo.GetComponent<OVRManager>());
            SetBool(ovrSo, "requestScenePermissionOnStartup", true);
            ovrSo.ApplyModifiedPropertiesWithoutUndo();

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
            effect.CutHoles = MRUKAnchor.SceneLabels.DOOR_FRAME;   // bullets and enemy sight pass through doorways
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
            var game = CreateGameRoot("Stopklatka", alwaysShowTimeIndicator: false);
            game.AddComponent<RoomService>();
            game.AddComponent<PassthroughStyle>();
            game.AddComponent<GameManager>();

            SaveScene(scene, ScenePath);
            UseGameScene();
        }

        /// <summary>
        /// A scene for checking the time system on its own: no scan, no menu, no waves. A turret fires
        /// slow bullets at you, a pendulum swings in game time, one enemy, floating weapons and the
        /// wrist time gauge. The boundary stays on, because nothing here knows where your walls are.
        /// </summary>
        [MenuItem("Stopklatka/Build Time Test Scene", priority = 30)]
        public static void BuildTimeTestScene()
        {
            EnsureGameConfig();
            if (AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{Mats.Enemy}.mat") == null) CreateMaterials();
            var scene = NewScene();
            if (CreateCameraRig(suppressBoundary: false) == null) return;
            var game = CreateGameRoot("TimeTest", alwaysShowTimeIndicator: true);
            game.AddComponent<TimeTestScene>();
            SaveScene(scene, TimeTestScenePath);
            UseTimeTestScene();
            Debug.Log("[Stopklatka] Build And Run now starts the time test. Stopklatka > Build Settings: Game Scene switches back.");
        }

        [MenuItem("Stopklatka/Build Settings: Game Scene", priority = 40)]
        public static void UseGameScene() => UseScene(ScenePath);

        [MenuItem("Stopklatka/Build Settings: Time Test Scene", priority = 41)]
        public static void UseTimeTestScene() => UseScene(TimeTestScenePath);

        static void UseScene(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogError($"[Stopklatka] {path} doesn't exist yet. Build it from the Stopklatka menu first.");
                return;
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            Debug.Log($"[Stopklatka] {path} is now the only scene in the build.");
        }

        static Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            return scene;
        }

        static void SaveScene(Scene scene, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[Stopklatka] Scene saved to {path}.");
        }

        /// <summary>Camera rig with passthrough, ~2024 px per eye with dynamic resolution, floor-level tracking.</summary>
        static GameObject CreateCameraRig(bool suppressBoundary)
        {
            var rigGo = InstantiatePrefab("OVRCameraRig", "com.meta.xr.sdk.core");
            if (rigGo == null) { Debug.LogError("[Stopklatka] OVRCameraRig prefab not found. Is Meta XR Core SDK installed?"); return null; }
            var ovr = rigGo.GetComponent<OVRManager>() ?? rigGo.AddComponent<OVRManager>();
            ovr.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            ovr.isInsightPassthroughEnabled = true;
            ovr.shouldBoundaryVisibilityBeSuppressed = suppressBoundary;
            ovr.enableDynamicResolution = true;
            ovr.quest3MinDynamicResolutionScale = 1.0f;
            ovr.quest3MaxDynamicResolutionScale = 1.205f;   // ~2024 x 2120 per eye
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
            return rigGo;
        }

        /// <summary>The managers every scene needs, plus both hands.</summary>
        static GameObject CreateGameRoot(string name, bool alwaysShowTimeIndicator)
        {
            var game = new GameObject(name);
            game.AddComponent<PlayerRig>();
            game.AddComponent<PlayerHealth>();
            game.AddComponent<TimeController>();
            game.AddComponent<PerformanceManager>();
            game.AddComponent<SoundBank>();
            game.AddComponent<Hud>();
            game.AddComponent<TimeIndicator>().AlwaysOn = alwaysShowTimeIndicator;
            var left = new GameObject("LeftHand");
            left.transform.SetParent(game.transform, false);
            left.AddComponent<ControllerHand>().IsLeft = true;
            var right = new GameObject("RightHand");
            right.transform.SetParent(game.transform, false);
            right.AddComponent<ControllerHand>().IsLeft = false;
            return game;
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
