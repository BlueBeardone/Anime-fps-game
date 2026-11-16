using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimeFpsGame.Menu
{
    /// <summary>
    /// Runtime-built main menu. Campaign and Multiplayer are placeholders; Settings is a no-op;
    /// Range loads the single-player test scene.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public const string RangeSceneName = "Main";
        public const string CampaignLabel = "CAMPAIGN";
        public const string MultiplayerLabel = "MULTIPLAYER";
        public const string RangeLabel = "RANGE";
        public const string SettingsLabel = "SETTINGS";
        public const string ExitLabel = "EXIT";

        static readonly Color BackdropColor = new(0.05f, 0.05f, 0.06f, 1f);
        static readonly Color ButtonColor = new(0.24f, 0.28f, 0.24f, 1f);
        static readonly Color LabelColor = new(0.92f, 0.92f, 0.88f, 1f);
        static readonly Color DisabledLabelColor = new(0.42f, 0.42f, 0.42f, 1f);

        public Button[] Buttons { get; private set; }

        bool built;

        void Awake()
        {
            Build();
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(GetButton(RangeLabel).gameObject);
            }
        }

        public Button GetButton(string label)
        {
            if (Buttons == null)
            {
                return null;
            }

            foreach (var button in Buttons)
            {
                if (button != null && button.name == label)
                {
                    return button;
                }
            }

            return null;
        }

        public void StartRange()
        {
            SceneManager.LoadScene(RangeSceneName);
        }

        public void OpenSettings()
        {
            Debug.Log("[Menu] Settings is not implemented yet.");
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Build()
        {
            if (built)
            {
                return;
            }

            built = true;

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            var canvasObject = new GameObject("MainMenuCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var backdrop = CreateRect("Backdrop", canvasObject.transform, Vector2.zero, Vector2.zero);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = Vector2.zero;
            backdrop.offsetMax = Vector2.zero;
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = BackdropColor;
            backdropImage.raycastTarget = false;

            CreateLabel("Title", backdrop, "ANIME FPS", 54, new Vector2(0f, 230f), new Vector2(700f, 70f));
            CreateLabel("Subtitle", backdrop, "MAIN MENU", 16, new Vector2(0f, 180f), new Vector2(400f, 24f));

            Buttons = new[]
            {
                CreateButton(CampaignLabel, backdrop, new Vector2(0f, 90f)),
                CreateButton(MultiplayerLabel, backdrop, new Vector2(0f, 20f)),
                CreateButton(RangeLabel, backdrop, new Vector2(0f, -50f)),
                CreateButton(SettingsLabel, backdrop, new Vector2(0f, -120f)),
                CreateButton(ExitLabel, backdrop, new Vector2(0f, -190f)),
            };

            Buttons[0].interactable = false;
            Buttons[1].interactable = false;
            DimLabel(Buttons[0]);
            DimLabel(Buttons[1]);

            Buttons[2].onClick.AddListener(StartRange);
            Buttons[3].onClick.AddListener(OpenSettings);
            Buttons[4].onClick.AddListener(QuitGame);

            var footer = CreateLabel("Footer", backdrop, "RANGE — single-player test area", 14,
                new Vector2(0f, -280f), new Vector2(600f, 22f));
            footer.color = new Color(0.55f, 0.55f, 0.5f, 1f);
        }

        static void DimLabel(Button button)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = DisabledLabelColor;
            }
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        static Text CreateLabel(string name, Transform parent, string content, int size,
            Vector2 position, Vector2 box)
        {
            var rect = CreateRect(name, parent, box, position);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = LabelColor;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        static Button CreateButton(string label, Transform parent, Vector2 position)
        {
            var rect = CreateRect(label, parent, new Vector2(360f, 56f), position);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
            button.colors = colors;
            CreateLabel("Label", rect, label, 18, Vector2.zero, new Vector2(360f, 56f));
            return button;
        }

        static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
