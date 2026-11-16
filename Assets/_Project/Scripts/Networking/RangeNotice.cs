using UnityEngine;
using UnityEngine.UI;

namespace AnimeFpsGame.Networking
{
    /// <summary>
    /// On-screen notice shown when a session fails to start. Range is entered without any UI, so
    /// without this a failed auto-host would leave the player staring at an empty preview scene.
    /// </summary>
    public class RangeNotice : MonoBehaviour
    {
        public string Message { get; private set; } = string.Empty;

        static RangeNotice instance;

        Text label;

        /// <summary>Shows <paramref name="message"/>, creating the notice on demand.</summary>
        public static RangeNotice Show(string message)
        {
            if (instance == null)
            {
                instance = Object.FindAnyObjectByType<RangeNotice>();
            }

            if (instance == null)
            {
                var go = new GameObject("Range Notice", typeof(RangeNotice));
                instance = go.GetComponent<RangeNotice>();
            }

            instance.Display(message);
            return instance;
        }

        void Awake()
        {
            instance = this;
            Build();
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void Display(string message)
        {
            Message = message ?? string.Empty;

            if (label != null)
            {
                label.text = Message;
            }
        }

        void Build()
        {
            if (label != null)
            {
                return;
            }

            var canvasObject = new GameObject("RangeNoticeCanvas",
                typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            var panel = (RectTransform)panelObject.transform;
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(760f, 64f);
            panel.anchoredPosition = new Vector2(0f, 40f);
            var background = panelObject.GetComponent<Image>();
            background.color = new Color(0.32f, 0.08f, 0.08f, 0.94f);
            background.raycastTarget = false;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(panel, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 8f);
            labelRect.offsetMax = new Vector2(-16f, -8f);
            label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.9f, 0.88f, 1f);
            label.raycastTarget = false;
            label.text = Message;
        }
    }
}
