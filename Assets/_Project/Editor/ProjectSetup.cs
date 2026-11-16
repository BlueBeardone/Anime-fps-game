using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            EditorSceneManager.SaveScene(scene, MainScenePath);
            if (hasSample)
            {
                AssetDatabase.DeleteAsset(SampleScenePath);
            }

            var buildScenes = EditorBuildSettings.scenes
                .Where(entry => AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path) != null)
                .ToList();
            if (buildScenes.All(entry => entry.path != MainScenePath))
            {
                buildScenes.Insert(0, new EditorBuildSettingsScene(MainScenePath, true));
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();

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

            var buildScenes = new List<EditorBuildSettingsScene>
            {
                new(MainMenuScenePath, true),
                new(MainScenePath, true),
            };
            buildScenes.AddRange(EditorBuildSettings.scenes.Where(entry =>
                entry.path != MainMenuScenePath &&
                entry.path != MainScenePath &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path) != null));
            EditorBuildSettings.scenes = buildScenes.ToArray();

            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Main menu ready: " + MainMenuScenePath);
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

            if (managerObject.GetComponent<AnimeFpsGame.Networking.HostJoinMenu>() == null)
            {
                managerObject.AddComponent<AnimeFpsGame.Networking.HostJoinMenu>();
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

            var root = new GameObject("Player");
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;

            root.AddComponent<NetworkObject>();
            var networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;

            var input = root.AddComponent<PlayerInput>();
            var inputSerialized = new SerializedObject(input);
            inputSerialized.FindProperty("m_Actions").objectReferenceValue = actions;
            inputSerialized.FindProperty("m_DefaultActionMap").stringValue = "Player";
            inputSerialized.ApplyModifiedPropertiesWithoutUndo();

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

            var weapons = root.AddComponent<AnimeFpsGame.Player.PlayerWeapons>();
            SetReference(weapons, "viewmodelAnchor", viewmodelAnchor.transform);
            SetReference(weapons, "visualsRoot", visuals.transform);
            SetReference(weapons, "animator", animator);
            SetReference(weapons, "viewCamera", cameraComponent);

            var weaponsSerialized = new SerializedObject(weapons);
            var slots = weaponsSerialized.FindProperty("slots");
            slots.arraySize = 2;
            ConfigureSlot(slots.GetArrayElementAtIndex(0), "K98", "status_k98", "K98", k98Viewmodel,
                new Vector3(0.24f, -0.22f, 0.48f), new Vector3(0f, 90f, 0f), 0.08f);
            ConfigureSlot(slots.GetArrayElementAtIndex(1), "Luger P08", "Status_LugerP08", "Luger_P08", lugerViewmodel,
                new Vector3(0.2f, -0.18f, 0.38f), new Vector3(0f, 90f, 0f), 0.08f);
            weaponsSerialized.ApplyModifiedPropertiesWithoutUndo();

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
            string meshName, GameObject viewmodel, Vector3 position, Vector3 euler, float scale)
        {
            slot.FindPropertyRelative("displayName").stringValue = displayName;
            slot.FindPropertyRelative("animatorInt").stringValue = animatorInt;
            slot.FindPropertyRelative("soldierMeshName").stringValue = meshName;
            slot.FindPropertyRelative("viewmodelPrefab").objectReferenceValue = viewmodel;
            slot.FindPropertyRelative("viewmodelLocalPosition").vector3Value = position;
            slot.FindPropertyRelative("viewmodelLocalEuler").vector3Value = euler;
            slot.FindPropertyRelative("viewmodelScale").floatValue = scale;
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
