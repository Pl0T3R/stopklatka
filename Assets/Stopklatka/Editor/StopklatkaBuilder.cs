using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Meta.XR.EnvironmentDepth;
using Meta.XR.MRUtilityKit;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
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
        const string MixerPath = Root + "/Resources/" + SoundBank.MixerResource + ".mixer";

        [MenuItem("Stopklatka/Build Game (settings + materials + scene)", priority = 0)]
        public static void BuildAll()
        {
            ConfigureProject();
            CreateMaterials();
            EnsureGameConfig();
            EnsureAudioMixer();
            EnsureTextMeshPro();
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
            // Colors from STYLE_GUIDE.md (sections 3 and 5): red is what wants to kill you, black is what you
            // can use, white is the world, yellow-orange only where an enemy breaks. Tune colors here.
            // Crystal(name, lit, shadow, rim color, rim strength, highlight strength, gloss, emission, highlight color, hatching)
            var enemyRim = Hex("#FF6A5A");
            Crystal(Mats.Enemy, Hex("#FF2B1C"), Hex("#9E0A08"), enemyRim, 0.6f, 0.5f, 40f, 0f, hatching: 0.25f);
            // a third of the shards: the darker red (#9E0A08 lit)
            Crystal(Mats.EnemyDark, Hex("#9E0A08"), Hex("#6A0505"), enemyRim, 0.45f, 0.4f, 32f, 0f, hatching: 0.25f);
            var reveal = Crystal(Mats.EnemyReveal, Hex("#FF2B1C"), Hex("#9E0A08"), enemyRim, 0.6f, 0.5f, 40f, 0f, hatching: 0.25f);
            reveal.SetColor("_RevealColor", new Color(1f, 0.93f, 0.88f));
            reveal.EnableKeyword("_REVEAL");
            // weapons, throwables and your fist: blue-black, half matte, a navy-teal sheen on the edges
            var coolHighlight = Hex("#8FB0C4");
            Crystal(Mats.Weapon, Hex("#1E3242"), Hex("#0B141A"), Hex("#3A5A6E"), 0.5f, 0.9f, 64f, 0f, coolHighlight);
            // katana: a black blade, the steel-grey edge comes from a strong rim
            Crystal(Mats.Blade, Hex("#1A2630"), Hex("#0B141A"), Hex("#AEB8BF"), 0.9f, 1.2f, 128f, 0f, Hex("#E0E6EA"));
            // the bottle: dark smoky glass (everything you can use is black)
            Crystal(Mats.DarkGlass, Hex("#22384A"), Hex("#0B141A"), Hex("#7FA3B8"), 0.9f, 1f, 96f, 0f, Hex("#C8D8E2"));
            // white crystals (menu toggles, wrist gauge tick, sparks) and white props for the levels (stage 6)
            Crystal(Mats.Glass, Hex("#F2F4F5"), Hex("#C7D1D6"), Color.white, 0.6f, 0.8f, 96f, 0.05f);
            // statues and props (STYLE_GUIDE 3, 5): white with cool facets, and the light cyan haze of
            // STYLE_GUIDE 4 (#DDEFF4) on far ones; LevelProps applies GameConfig props.haze* at runtime
            var statue = Crystal(Mats.Statue, Hex("#ECEFF0"), Hex("#D9E0E3"), Color.white, 0.3f, 0.2f, 16f, 0f);
            var prop = Crystal(Mats.PropWhite, Hex("#F2F4F5"), Hex("#D3DCE0"), Color.white, 0.15f, 0.1f, 12f, 0f);
            foreach (var m in new[] { statue, prop }) Haze(m, Hex("#DDEFF4"), 3f, 15f, 0.55f);
            // window light on walls (#FFFFFF, soft edges from vertex alpha) and its pale rectangle on the floor
            Flat(Mats.WindowLight, WithAlpha(Hex("#FFFFFF"), 0.85f), 0.06f);   // 2 cm off the real wall: the bias keeps the wall from hiding it
            Flat(Mats.FloorLight, WithAlpha(Hex("#FFFFFF"), 0.35f), 0.08f);
            Crystal(Mats.Target, Hex("#FF2B1C"), Hex("#9E0A08"), Hex("#FF8A7A"), 0.8f, 0.8f, 48f, 0.25f);
            // bullet streaks: enemy #FF1E1E at 80%, fading to clear at the tail; yours blue-black
            Flat(Mats.EnemyTrail, WithAlpha(Hex("#FF1E1E"), 0.8f));
            Flat(Mats.PlayerTrail, WithAlpha(Hex("#0B141A"), 0.6f));
            // muzzle flash: white (the yellow-orange accent is only for enemies breaking)
            Flat(Mats.Flash, new Color(1f, 1f, 1f, 0.85f));
            Flat(Mats.RedFlash, new Color(0.9f, 0.02f, 0.02f, 0.6f));
            // things lying on the real floor: the bias stops the floor's own depth from hiding them.
            // The shadow is a cool grey-blue: over a white floor it comes out near #B9C6CC, never black.
            Flat(Mats.FloorShadow, WithAlpha(Hex("#5E6E78"), 0.38f), 0.08f);
            Flat(Mats.RingFx, WithAlpha(Hex("#FF2B1C"), 0.85f), 0.08f);
            Flat(Mats.Hidden, new Color(0f, 0f, 0f, 0f));
            // fracture glow (STYLE_GUIDE 8): white base, the cap/crack/flash meshes carry #FFD84D (core) to #FF9A1F (edge)
            // as vertex colors, so one material serves every glowing break
            Flat(Mats.Glow, Color.white);
            // glowing shards: lit #FFD84D, shadow #FF9A1F, strong emission so they read as light, not paint
            Crystal(Mats.GlowShard, Hex("#FFD84D"), Hex("#FF9A1F"), new Color(1f, 0.95f, 0.75f), 0.5f, 0.4f, 24f, 0.45f);
            var text = Material(Mats.Text, "Stopklatka/Text");
            text.SetColor("_BaseColor", Color.white);
            AssetDatabase.SaveAssets();
        }

        static Material Crystal(string name, Color lit, Color shadow, Color rim, float rimStrength, float specStrength, float gloss,
                                float emission, Color? highlight = null, float hatching = 0f)
        {
            var m = Material(name, "Stopklatka/Crystal");
            m.SetColor("_HighlightColor", highlight ?? new Color(1f, 0.88f, 0.84f));
            m.SetFloat("_HatchStrength", hatching);   // diagonal stripes, #B80E0A at about 25% on enemies
            m.SetColor("_HatchColor", Hex("#B80E0A"));
            m.SetFloat("_HatchDensity", 60f);
            m.SetColor("_BaseColor", lit);
            m.SetColor("_ShadowColor", shadow);
            m.SetColor("_RimColor", rim);
            m.SetFloat("_RimStrength", rimStrength);
            m.SetFloat("_SpecStrength", specStrength);
            m.SetFloat("_Gloss", gloss);
            m.SetFloat("_Emission", emission);
            Haze(m, Hex("#DDEFF4"), 3f, 15f, 0f);   // no haze unless a prop material turns it on
            m.DisableKeyword("_REVEAL");
            return m;
        }

        /// <summary>The Crystal shader's haze: far surfaces fade toward color between start and end metres.</summary>
        static void Haze(Material m, Color color, float start, float end, float strength)
        {
            m.SetColor("_FogColor", color);
            m.SetFloat("_FogStart", start);
            m.SetFloat("_FogEnd", end);
            m.SetFloat("_FogStrength", strength);
        }

        /// <summary>A palette color from STYLE_GUIDE.md ("#RRGGBB", sRGB like the Inspector).</summary>
        static Color Hex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) Debug.LogError($"[Stopklatka] Bad color {hex}");
            return c;
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

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

        // --- audio mixer ----------------------------------------------------------

        /// <summary>
        /// The AudioMixer of GAME_SPEC 7: groups World (slows down with time), UI (never slowed) and
        /// Ambient, with World's pitch exposed as "WorldPitch" and Ambient's volume as "AmbientVolume".
        /// Unity has no public API to build a mixer, so this calls the editor's internal
        /// AudioMixerController (methods checked against Unity 6000.0.85). If that fails, the log shows
        /// the manual steps (also in CLAUDE.md) and the game sets the pitch on each sound instead.
        /// An existing mixer is never touched, so your own changes stay.
        /// </summary>
        [MenuItem("Stopklatka/5. Create Audio Mixer (if missing)", priority = 24)]
        public static void EnsureAudioMixer()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(MixerPath));
            try
            {
                var controllerType = EditorType("UnityEditor.Audio.AudioMixerController");
                var pathType = EditorType("UnityEditor.Audio.AudioGroupParameterPath");
                var exposedType = EditorType("UnityEditor.Audio.ExposedAudioParameter");
                const BindingFlags pub = BindingFlags.Public | BindingFlags.Instance;

                var mixer = controllerType.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { MixerPath });
                var master = controllerType.GetProperty("masterGroup", pub).GetValue(mixer);
                var createGroup = controllerType.GetMethod("CreateNewGroup", pub, null, new[] { typeof(string), typeof(bool) }, null);
                var addChild = controllerType.GetMethod("AddChildToParent", pub);
                var addToView = controllerType.GetMethod("AddGroupToCurrentView", pub);

                object Group(string name)
                {
                    var g = createGroup.Invoke(mixer, new object[] { name, false });
                    addChild.Invoke(mixer, new[] { g, master });
                    // only so the group shows in the Audio Mixer window; the mixer works without it
                    try { addToView?.Invoke(mixer, new[] { g }); }
                    catch (Exception e) { Debug.LogWarning($"[Stopklatka] Mixer group {name} not added to the window view: {e.GetBaseException().Message}"); }
                    return g;
                }

                void Expose(object group, string guidMethod, string name)
                {
                    var guid = group.GetType().GetMethod(guidMethod, pub).Invoke(group, null);
                    var path = Activator.CreateInstance(pathType, group, guid);
                    controllerType.GetMethod("AddExposedParameter", pub).Invoke(mixer, new[] { path });
                    // rename it from Unity's "MyExposedParam" to the name the game uses
                    var prop = controllerType.GetProperty("exposedParameters", pub);
                    var list = (Array)prop.GetValue(mixer);
                    for (int i = 0; i < list.Length; i++)
                    {
                        var p = list.GetValue(i);
                        if (!exposedType.GetField("guid").GetValue(p).Equals(guid)) continue;
                        exposedType.GetField("name").SetValue(p, name);   // p is a boxed copy: write it back
                        list.SetValue(p, i);
                    }
                    prop.SetValue(mixer, list);
                }

                var world = Group(SoundBank.WorldGroup);
                Group(SoundBank.UiGroup);
                var ambient = Group(SoundBank.AmbientGroup);
                Expose(world, "GetGUIDForPitch", SoundBank.WorldPitchParam);
                Expose(ambient, "GetGUIDForVolume", SoundBank.AmbientVolumeParam);

                // the same steps as Unity's own Audio Mixer window: CreateNewGroup, AddChildToParent,
                // AddGroupToCurrentView, then OnSubAssetChanged
                controllerType.GetMethod("OnSubAssetChanged", pub, null, Type.EmptyTypes, null)?.Invoke(mixer, null);
                EditorUtility.SetDirty((UnityEngine.Object)mixer);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Stopklatka] Audio mixer created at {MixerPath}: groups World, UI, Ambient; " +
                          $"exposed {SoundBank.WorldPitchParam} and {SoundBank.AmbientVolumeParam}.");
            }
            catch (Exception e)
            {
                // don't leave a half-built mixer behind: the manual steps start from nothing
                if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null) AssetDatabase.DeleteAsset(MixerPath);
                Debug.LogWarning($"[Stopklatka] Couldn't create the audio mixer from code ({e.GetBaseException().Message}). " +
                                 "The game still works: it sets the pitch on each sound. To use a mixer, make it by hand:\n" + ManualMixerSteps);
            }
        }

        // --- TextMeshPro ------------------------------------------------------------------

        const string TmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        /// <summary>
        /// Imports TMP Essential Resources (LiberationSans SDF, its dynamic fallback for Polish letters,
        /// the drop-shadow material, TMP Settings) if they are missing. In Unity 6 TextMeshPro lives in
        /// com.unity.ugui; older projects may have com.unity.textmeshpro. Without them the game uses
        /// its legacy TextMesh texts (GameConfig ui).
        /// </summary>
        [MenuItem("Stopklatka/6. Import TextMeshPro Essentials (if missing)", priority = 25)]
        public static void EnsureTextMeshPro()
        {
            if (AssetDatabase.LoadMainAssetAtPath(TmpFontPath) != null) return;
            foreach (var package in new[] { "com.unity.ugui", "com.unity.textmeshpro" })
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{package}");
                if (info == null || string.IsNullOrEmpty(info.resolvedPath)) continue;
                var unityPackage = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
                if (!File.Exists(unityPackage)) continue;
                AssetDatabase.ImportPackage(unityPackage, false);
                Debug.Log($"[Stopklatka] Importing TMP Essential Resources from {package}. If texts still use the legacy font, run Build Game again once the import is done.");
                return;
            }
            Debug.LogWarning("[Stopklatka] TMP Essential Resources not found in com.unity.ugui or com.unity.textmeshpro. " +
                             "Import them by hand: Window > TextMeshPro > Import TMP Essential Resources. Until then the game uses its legacy texts.");
        }

        const string ManualMixerSteps =
            "1. Project window: right-click Assets/Stopklatka/Resources/Audio > Create > Audio Mixer, name it StopklatkaMixer.\n" +
            "2. Window > Audio > Audio Mixer. Next to Groups click + three times (all under Master): World, UI, Ambient.\n" +
            "3. Select the World group. In the Inspector, right-click Pitch > Expose 'Pitch (of World)' to script.\n" +
            "4. Select the Ambient group. Right-click Volume > Expose 'Volume (of Ambient)' to script.\n" +
            "5. In the Audio Mixer window, top right: Exposed Parameters. Rename them WorldPitch and AmbientVolume.\n" +
            "6. Save (Ctrl+S). Nothing else: SoundBank finds the mixer and the groups by name.";

        static Type EditorType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null)
            ?? throw new MissingMemberException(fullName);

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
            // TryGetComponent, not '??': in the editor a missing component is a fake null that '??' doesn't see
            if (!mrukGo.TryGetComponent(out MRUK mruk)) mruk = mrukGo.AddComponent<MRUK>();
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
            game.AddComponent<LevelManager>();   // the playing loop (levels of waves); GameManager also adds it to older scenes
            game.AddComponent<LevelProps>();     // the white props of each level (stage 6)

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
            EnsureAudioMixer();
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
            if (!rigGo.TryGetComponent(out OVRManager ovr)) ovr = rigGo.AddComponent<OVRManager>();
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

        /// <summary>The managers every scene needs, plus both hands (grab, throw, fist).</summary>
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
            left.AddComponent<Fist>();
            var right = new GameObject("RightHand");
            right.transform.SetParent(game.transform, false);
            right.AddComponent<ControllerHand>().IsLeft = false;
            right.AddComponent<Fist>();
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
