using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimeFpsGame.Combat;
using AnimeFpsGame.Weapons;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnimeFpsGame.EditorTools
{
    /// <summary>
    /// One-shot, batchmode-callable project setup. Each method is idempotent so it can be
    /// re-run after a mistake without duplicating objects.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ProjectRoot = "Assets/_Project";
        public const string ScenesDir = ProjectRoot + "/Scenes";
        public const string MainScenePath = ScenesDir + "/Main.unity";
        public const string MainMenuScenePath = ScenesDir + "/MainMenu.unity";
        public const string SampleScenePath = ScenesDir + "/SampleScene.unity";
        public const string PlayerPrefabPath = ProjectRoot + "/Prefabs/Player/Player.prefab";
        public const string ViewsDir = ProjectRoot + "/Prefabs/Weapons";
        public const string StatsDir = ProjectRoot + "/Settings/Weapons";
        public const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

        const string SoldiersDir = ProjectRoot + "/Art/WW2_German_Soldiers";
        const string SoldierPrefabPath = SoldiersDir + "/Soilders/FBX_soilders/Wehrmacht_A/Wehrmacht_A_prefab.prefab";
        const string GunsDir = SoldiersDir + "/Guns_prefab";

        static readonly Vector3[] SpawnPositions =
        {
            new Vector3(0f, 1f, -4f),
            new Vector3(2.5f, 1f, -4f),
            new Vector3(-2.5f, 1f, -4f),
            new Vector3(0f, 1f, -6.5f),
        };

        [MenuItem("Anime FPS/Run All")]
        public static void RunAll()
        {
            SetupScene();
            BuildPlayerPrefab();
            SetupNetworkManager();
            SetupMainMenu();
            Debug.Log("[ProjectSetup] RunAll complete");
        }

        [MenuItem("Anime FPS/1. Setup Scene")]
        public static void SetupScene()
        {
            var hasSample = AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath) != null;
            var scene = hasSample
                ? EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single)
                : EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            if (GameObject.Find("Ground") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(2f, 1f, 2f);
                ground.GetComponent<MeshRenderer>().sharedMaterial =
                    EnsureMaterial(ProjectRoot + "/Art/Materials/Ground.mat", new Color(0.42f, 0.45f, 0.38f));
            }

            if (GameObject.Find("SpawnPoint") != null)
            {
                // Older scenes used one unnumbered spawn marker; fold it into slot 1.
                GameObject.Find("SpawnPoint").name = "SpawnPoint 1";
            }

            for (var i = 0; i < SpawnPositions.Length; i++)
            {
                var name = "SpawnPoint " + (i + 1);
                if (GameObject.Find(name) == null)
                {
                    var spawn = new GameObject(name);
                    spawn.transform.position = SpawnPositions[i];
                }
            }

            EnsureTargetDummies();

            EditorSceneManager.SaveScene(scene, MainScenePath);
            if (hasSample)
            {
                AssetDatabase.DeleteAsset(SampleScenePath);
            }

            ApplyBuildOrder();

            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Scene ready: " + MainScenePath);
        }

        [MenuItem("Anime FPS/0. Setup Main Menu")]
        public static void SetupMainMenu()
        {
            var hasMenu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) != null;
            var scene = hasMenu
                ? EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (Object.FindFirstObjectByType<Camera>() == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.03f, 0.03f, 0.04f, 1f);
            }

            var menuObject = GameObject.Find("Main Menu");
            if (menuObject == null)
            {
                menuObject = new GameObject("Main Menu");
            }

            if (menuObject.GetComponent<AnimeFpsGame.Menu.MainMenuController>() == null)
            {
                menuObject.AddComponent<AnimeFpsGame.Menu.MainMenuController>();
            }

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);

            ApplyBuildOrder();

            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Main menu ready: " + MainMenuScenePath);
        }

        /// <summary>
        /// Build index 0 must be the menu so a build boots into it. Both setup steps call this;
        /// otherwise running "Setup Scene" after resetting Build Settings could put Main first.
        /// Only existing scene assets are registered.
        /// </summary>
        static void ApplyBuildOrder()
        {
            var ordered = new List<EditorBuildSettingsScene>();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) != null)
            {
                ordered.Add(new EditorBuildSettingsScene(MainMenuScenePath, true));
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) != null)
            {
                ordered.Add(new EditorBuildSettingsScene(MainScenePath, true));
            }

            ordered.AddRange(EditorBuildSettings.scenes.Where(entry =>
                entry.path != MainMenuScenePath &&
                entry.path != MainScenePath &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path) != null));

            EditorBuildSettings.scenes = ordered.ToArray();
        }

        [MenuItem("Anime FPS/2. Setup NetworkManager")]
        public static void SetupNetworkManager()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            var existing = Object.FindFirstObjectByType<NetworkManager>();
            var managerObject = existing != null ? existing.gameObject : new GameObject("NetworkManager");
            if (existing == null)
            {
                managerObject.AddComponent<UnityTransport>();
                managerObject.AddComponent<NetworkManager>();
            }

            var transport = managerObject.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = managerObject.AddComponent<UnityTransport>();
            }

            transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");

            if (managerObject.GetComponent<AnimeFpsGame.Networking.NetworkBootstrap>() == null)
            {
                managerObject.AddComponent<AnimeFpsGame.Networking.NetworkBootstrap>();
            }

            var legacyMenu = managerObject.GetComponent<AnimeFpsGame.Networking.HostJoinMenu>();
            if (legacyMenu != null)
            {
                Object.DestroyImmediate(legacyMenu);
            }

            var manager = managerObject.GetComponent<NetworkManager>();
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab != null)
            {
                var serialized = new SerializedObject(manager);
                var config = serialized.FindProperty("NetworkConfig");
                if (config != null)
                {
                    var transportProperty = config.FindPropertyRelative("NetworkTransport");
                    if (transportProperty != null)
                    {
                        transportProperty.objectReferenceValue = transport;
                    }

                    // Required for NetworkBootstrap's ConnectionApprovalCallback to run.
                    var approvalProperty = config.FindPropertyRelative("ConnectionApproval");
                    if (approvalProperty != null)
                    {
                        approvalProperty.boolValue = true;
                    }

                    var playerProperty = config.FindPropertyRelative("PlayerPrefab");
                    if (playerProperty != null)
                    {
                        playerProperty.objectReferenceValue = playerPrefab;
                    }

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var bootstrap = managerObject.GetComponent<AnimeFpsGame.Networking.NetworkBootstrap>();
            if (bootstrap != null)
            {
                var bootstrapSerialized = new SerializedObject(bootstrap);
                bootstrapSerialized.FindProperty("networkManager").objectReferenceValue = manager;

                var singlePlayerProperty = bootstrapSerialized.FindProperty("startSinglePlayer");
                if (singlePlayerProperty != null)
                {
                    singlePlayerProperty.boolValue = true;
                }

                var spawnProperty = bootstrapSerialized.FindProperty("spawnPoints");
                if (spawnProperty != null)
                {
                    spawnProperty.arraySize = SpawnPositions.Length;
                    for (var i = 0; i < SpawnPositions.Length; i++)
                    {
                        var marker = GameObject.Find("SpawnPoint " + (i + 1));
                        spawnProperty.GetArrayElementAtIndex(i).objectReferenceValue =
                            marker != null ? marker.transform : null;
                    }
                }

                bootstrapSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] NetworkManager ready, player prefab: " + (playerPrefab != null));
        }

        [MenuItem("Anime FPS/3. Build Player Prefab")]
        public static void BuildPlayerPrefab()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            var soldier = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPrefabPath);
            if (soldier == null)
            {
                Debug.LogError("[ProjectSetup] Soldier prefab not found: " + SoldierPrefabPath);
                return;
            }

            EnsureFolder(ViewsDir);
            EnsureFolder(ProjectRoot + "/Prefabs/Player");

            var k98Viewmodel = EnsureViewmodel("K98_prefab.prefab", "K98_Viewmodel.prefab");
            var lugerViewmodel = EnsureViewmodel("LugerP08_prefab.prefab", "LugerP08_Viewmodel.prefab");
            var mp40Viewmodel = EnsureViewmodel("MP40_prefab.prefab", "MP40_Viewmodel.prefab");

            var rifleStats = EnsureGunStatsAsset("Rifle", Rifle.CreateDefaultStats());
            var handgunStats = EnsureGunStatsAsset("Handgun", Handgun.CreateDefaultStats());
            var smgStats = EnsureGunStatsAsset("SMG", SubmachineGun.CreateDefaultStats());
            var shotgunStats = EnsureGunStatsAsset("Shotgun", Shotgun.CreateDefaultStats());
            EnsurePlayerActions();

            var root = new GameObject("Player");
            var playerLayer = EnsureLayer("Player");
            var playerMaterial = EnsurePlayerPhysicsMaterial();
            var movementProfile = EnsureMovementProfile();
            var body = root.AddComponent<Rigidbody>();
            body.mass = movementProfile.playerMass;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = movementProfile.capsuleHeight;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0f, movementProfile.capsuleHeight * 0.5f, 0f);
            capsule.sharedMaterial = playerMaterial;

            var movement = root.AddComponent<AnimeFpsGame.Movement.CharacterMovement>();
            SetReference(movement, "profile", movementProfile);

            root.AddComponent<NetworkObject>();
            var networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;

            var input = root.AddComponent<PlayerInput>();
            var inputSerialized = new SerializedObject(input);
            inputSerialized.FindProperty("m_Actions").objectReferenceValue = actions;
            inputSerialized.FindProperty("m_DefaultActionMap").stringValue = "Player";
            inputSerialized.ApplyModifiedPropertiesWithoutUndo();

            var inputReader = root.AddComponent<AnimeFpsGame.Input.InputReader>();

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.65f, 0f);

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(pivot.transform, false);
            var cameraComponent = cameraObject.AddComponent<Camera>();
            cameraComponent.nearClipPlane = 0.02f;
            cameraComponent.fieldOfView = 75f;
            cameraObject.AddComponent<AudioListener>();

            var viewmodelAnchor = new GameObject("ViewModel");
            viewmodelAnchor.transform.SetParent(cameraObject.transform, false);

            var visuals = (GameObject)PrefabUtility.InstantiatePrefab(soldier);
            visuals.name = "Visuals";
            visuals.transform.SetParent(root.transform, false);
            visuals.transform.localPosition = Vector3.zero;
            visuals.transform.localRotation = Quaternion.identity;

            var animator = visuals.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogWarning("[ProjectSetup] Soldier has no Animator; walk animation will not play");
            }
            else
            {
                var networkAnimator = root.AddComponent<NetworkAnimator>();
                networkAnimator.Animator = animator;
                networkAnimator.AuthorityMode = NetworkAnimator.AuthorityModes.Owner;
            }

            var motor = root.AddComponent<AnimeFpsGame.Player.PlayerMotor>();
            SetReference(motor, "animator", animator);

            var look = root.AddComponent<AnimeFpsGame.Player.PlayerLook>();
            SetReference(look, "cameraPivot", pivot.transform);
            SetReference(look, "input", inputReader);
            SetReference(look, "movement", movement);
            SetReference(look, "body", body);

            var weapons = root.AddComponent<AnimeFpsGame.Player.PlayerWeapons>();
            var health = root.AddComponent<Health>();
            var effects = root.AddComponent<AnimeFpsGame.Effects.ShotEffects>();
            root.AddComponent<AnimeFpsGame.UI.WeaponHud>();
            SetReference(weapons, "viewmodelAnchor", viewmodelAnchor.transform);
            SetReference(weapons, "visualsRoot", visuals.transform);
            SetReference(weapons, "animator", animator);
            SetReference(weapons, "viewCamera", cameraComponent);
            SetReference(weapons, "health", health);
            SetReference(weapons, "effects", effects);
            SetReference(weapons, "input", inputReader);

            var respawn = root.AddComponent<PlayerRespawn>();
            SetReference(respawn, "health", health);
            SetReference(respawn, "weapons", weapons);
            SetReference(respawn, "movement", movement);
            SetReference(respawn, "playerLook", look);
            SetReference(motor, "movement", movement);
            SetReference(motor, "input", inputReader);
            SetReference(motor, "playerLook", look);
            SetReference(motor, "health", health);

            var weaponsSerialized = new SerializedObject(weapons);
            var slots = weaponsSerialized.FindProperty("slots");
            slots.arraySize = 4;
            ConfigureSlot(slots.GetArrayElementAtIndex(0), "Rifle", "status_k98", "K98", k98Viewmodel,
                new Vector3(0.24f, -0.22f, 0.48f), new Vector3(0f, 0f, 0f), 0.5f, rifleStats);
            ConfigureSlot(slots.GetArrayElementAtIndex(1), "Handgun", "Status_LugerP08", "Luger_P08", lugerViewmodel,
                new Vector3(0.2f, -0.18f, 0.38f), new Vector3(0f, 0f, 0f), 0.5f, handgunStats);
            ConfigureSlot(slots.GetArrayElementAtIndex(2), "SMG", "status_MP40", "MP40", mp40Viewmodel,
                new Vector3(0.22f, -0.2f, 0.45f), new Vector3(0f, 0f, 0f), 0.5f, smgStats);
            ConfigureSlot(slots.GetArrayElementAtIndex(3), "Shotgun", "status_k98", "K98", k98Viewmodel,
                new Vector3(0.24f, -0.22f, 0.48f), new Vector3(0f, 0f, 0f), 0.5f, shotgunStats);
            weaponsSerialized.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursively(root, playerLayer);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Player prefab saved: " + PlayerPrefabPath);
        }

        static GameObject EnsureViewmodel(string sourceName, string targetName)
        {
            var source = GunsDir + "/" + sourceName;
            var target = ViewsDir + "/" + targetName;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(target) == null)
            {
                if (!AssetDatabase.CopyAsset(source, target))
                {
                    Debug.LogError("[ProjectSetup] Could not copy " + source);
                    return null;
                }
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(target);
        }

        static void ConfigureSlot(SerializedProperty slot, string displayName, string animatorInt,
            string meshName, GameObject viewmodel, Vector3 position, Vector3 euler, float scale,
            GunStatsAsset stats)
        {
            slot.FindPropertyRelative("displayName").stringValue = displayName;
            slot.FindPropertyRelative("animatorInt").stringValue = animatorInt;
            slot.FindPropertyRelative("soldierMeshName").stringValue = meshName;
            slot.FindPropertyRelative("viewmodelPrefab").objectReferenceValue = viewmodel;
            slot.FindPropertyRelative("viewmodelLocalPosition").vector3Value = position;
            slot.FindPropertyRelative("viewmodelLocalEuler").vector3Value = euler;
            slot.FindPropertyRelative("viewmodelScale").floatValue = scale;
            slot.FindPropertyRelative("stats").objectReferenceValue = stats;
        }

        static GunStatsAsset EnsureGunStatsAsset(string fileName, GunStats defaults)
        {
            EnsureFolder(StatsDir);
            var path = StatsDir + "/" + fileName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<GunStatsAsset>(path);
            if (existing != null)
            {
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<GunStatsAsset>();
            asset.stats = defaults;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void EnsurePlayerActions()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (asset == null)
            {
                Debug.LogWarning("[ProjectSetup] Input actions asset not found: " + ActionsPath);
                return;
            }

            var map = asset.FindActionMap("Player", false);
            if (map == null)
            {
                return;
            }

            // PlayerInput components can leave the imported asset enabled while the scene is open.
            var wasEnabled = asset.enabled;
            asset.Disable();
            var changed = false;

            if (map.FindAction("Reload", false) == null)
            {
                var reload = map.AddAction("Reload", InputActionType.Button);
                reload.AddBinding("<Keyboard>/r");
                reload.AddBinding("<Gamepad>/dpad/down");
                changed = true;
            }

            if (map.FindAction("Pickup", false) == null)
            {
                var pickup = map.AddAction("Pickup", InputActionType.Button);
                pickup.AddBinding("<Keyboard>/f");
                pickup.AddBinding("<Gamepad>/buttonNorth");
                changed = true;
            }

            if (changed)
            {
                // The .inputactions file is a ScriptedImporter source asset, so AssetDatabase.SaveAssets
                // alone does not rewrite its JSON. Serialize explicitly, then reimport so the newly
                // loaded instance reflects the added actions.
                EditorUtility.SetDirty(asset);
                File.WriteAllText(ActionsPath, asset.ToJson());
                AssetDatabase.ImportAsset(ActionsPath, ImportAssetOptions.ForceUpdate);
                asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            }

            if (wasEnabled && asset != null)
            {
                asset.Enable();
            }
        }

        static void SetReference(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Three networked practice targets. Scene NetworkObjects, so the server spawns them when
        /// the host starts; ignoreOwnerDamage is off because the server owns the dummy itself.
        /// </summary>
        static void EnsureTargetDummies()
        {
            var material = EnsureMaterial(ProjectRoot + "/Art/Materials/TargetDummy.mat",
                new Color(0.72f, 0.18f, 0.15f));

            var positions = new[]
            {
                new Vector3(0f, 1f, 8f),
                new Vector3(-3f, 1f, 10f),
                new Vector3(3f, 1f, 10f)
            };

            for (var i = 0; i < positions.Length; i++)
            {
                var name = "TargetDummy " + (i + 1);
                var dummy = GameObject.Find(name);
                if (dummy == null)
                {
                    dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    dummy.name = name;
                    dummy.transform.position = positions[i];
                }
                else
                {
                    // Existing dummies keep any hand tuning from the editor.
                    dummy.name = name;
                }

                var renderer = dummy.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }

                if (dummy.GetComponent<NetworkObject>() == null)
                {
                    dummy.AddComponent<NetworkObject>();
                }

                var health = dummy.GetComponent<Health>();
                if (health == null)
                {
                    health = dummy.AddComponent<Health>();
                }

                var healthSerialized = new SerializedObject(health);
                healthSerialized.FindProperty("maxHealth").floatValue = 100f;
                healthSerialized.FindProperty("ignoreOwnerDamage").boolValue = false;
                healthSerialized.ApplyModifiedPropertiesWithoutUndo();

                if (dummy.GetComponent<TargetDummy>() == null)
                {
                    dummy.AddComponent<TargetDummy>();
                }
            }
        }

        /// <summary>
        /// Finds a layer by name, or claims the first empty user layer (6..31) if it is missing.
        /// Returns 0 (Default) only if every user layer is taken.
        /// </summary>
        static int EnsureLayer(string name)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[ProjectSetup] TagManager.asset not found; player stays on Default");
                return 0;
            }

            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");

            var firstFree = -1;
            for (var i = 6; i < layers.arraySize; i++)
            {
                var value = layers.GetArrayElementAtIndex(i).stringValue;
                if (value == name)
                {
                    return i;
                }

                if (firstFree < 0 && string.IsNullOrEmpty(value))
                {
                    firstFree = i;
                }
            }

            if (firstFree < 0)
            {
                Debug.LogError("[ProjectSetup] No free layer for '" + name + "'; player stays on Default");
                return 0;
            }

            layers.GetArrayElementAtIndex(firstFree).stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return firstFree;
        }

        static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>
        /// The movement model applies Source friction itself, so the capsule must not also get PhysX
        /// friction from the ground: that contact impulse cancels horizontal velocity.
        /// </summary>
        static PhysicsMaterial EnsurePlayerPhysicsMaterial()
        {
            // A .asset extension: Unity 6 warns (and will soon fail) CreateAsset() with a
            // .physicsMaterial extension.
            var path = ProjectRoot + "/Art/Materials/PlayerPhysicsMaterial.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var material = new PhysicsMaterial("PlayerPhysicsMaterial")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>Run All only creates the profile; it never overwrites tuned values.</summary>
        static AnimeFpsGame.Movement.MovementProfile EnsureMovementProfile()
        {
            var path = ProjectRoot + "/Settings/Movement/DefaultMovementProfile.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AnimeFpsGame.Movement.MovementProfile>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var profile = AnimeFpsGame.Movement.MovementProfile.CreateDefault();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static Material EnsureMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>Creates every folder in an "Assets/a/b/c" path that does not exist yet.</summary>
        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
