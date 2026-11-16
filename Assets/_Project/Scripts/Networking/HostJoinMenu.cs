using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AnimeFpsGame.Networking
{
    /// <summary>
    /// Minimal runtime-built uGUI host/join panel. Placeholder UI: replaced when a real menu exists.
    /// </summary>
    public class HostJoinMenu : MonoBehaviour
    {
        [SerializeField] NetworkBootstrap bootstrap;

        GameObject panel;
        InputField addressField;
        Text statusLabel;
        bool built;

        void Awake()
        {
            Build();
        }

        void Build()
        {
            if (built)
            {
                return;
            }

            built = true;

            if (bootstrap == null)
            {
                bootstrap = GetComponent<NetworkBootstrap>();
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            var canvasObject = new GameObject("HostJoinCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var background = CreateRect("Panel", canvasObject.transform, new Vector2(440f, 250f), Vector2.zero);
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.05f, 0.05f, 0.06f, 0.9f);
            panel = background.gameObject;

            CreateLabel("Title", background, "ANIME FPS — placeholder build", 22, new Vector2(0f, 92f), new Vector2(400f, 32f));
            CreateLabel("Hint", background, "Host or join a LAN session", 15, new Vector2(0f, 62f), new Vector2(400f, 24f));

            addressField = CreateInputField(background, new Vector2(360f, 38f), new Vector2(0f, 22f));
            addressField.text = "127.0.0.1";

            CreateButton("Host", background, new Vector2(165f, 42f), new Vector2(-92f, -32f), Host);
            CreateButton("Join", background, new Vector2(165f, 42f), new Vector2(92f, -32f), Join);
            statusLabel = CreateLabel("Status", background, "Not connected", 15, new Vector2(0f, -92f), new Vector2(400f, 28f));
        }

        void Update()
        {
            if (bootstrap == null || panel == null || statusLabel == null)
            {
                return;
            }

            var connected = bootstrap.IsConnected;
            panel.SetActive(!connected);
            statusLabel.text = bootstrap.Status;
        }

        void Host()
        {
            bootstrap.Host();
        }

        void Join()
        {
            bootstrap.Join(addressField != null ? addressField.text : null);
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

        static Text CreateLabel(string name, Transform parent, string content, int size, Vector2 position, Vector2 box)
        {
            var rect = CreateRect(name, parent, box, position);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.92f, 0.92f, 0.88f);
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        static InputField CreateInputField(Transform parent, Vector2 size, Vector2 position)
        {
            var rect = CreateRect("Address", parent, size, position);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.16f, 0.18f, 1f);
            var field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = image;

            var textRect = CreateRect("Text", rect, Vector2.zero, Vector2.zero);
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.supportRichText = false;
            var textTransform = (RectTransform)text.transform;
            textTransform.anchorMin = Vector2.zero;
            textTransform.anchorMax = Vector2.one;
            textTransform.offsetMin = new Vector2(10f, 4f);
            textTransform.offsetMax = new Vector2(-10f, -4f);

            field.textComponent = text;
            return field;
        }

        static void CreateButton(string label, Transform parent, Vector2 size, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateRect(label, parent, size, position);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.24f, 0.28f, 0.24f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            CreateLabel("Label", rect, label, 17, Vector2.zero, size);
        }

        static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
