using AnimeFpsGame.Player;
using UnityEngine;
using UnityEngine.UI;

namespace AnimeFpsGame.UI
{
    /// <summary>
    /// Runtime crosshair, ammo readout and reload bar for the owning player. Built in code so it
    /// needs no prefab art and cannot go stale when ProjectSetup rebuilds the player.
    /// </summary>
    public class WeaponHud : MonoBehaviour
    {
        const float MinGap = 6f;
        const float MaxGap = 48f;
        const float BarThickness = 2f;
        const float BarLength = 14f;

        PlayerWeapons weapons;
        GameObject canvasRoot;
        Image[] bars;
        Text ammoText;
        Image reloadBar;
        bool built;

        /// <summary>Maps degrees of spread onto the crosshair gap in pixels.</summary>
        public static float ComputeCrosshairGap(float spread, float maxSpread)
        {
            if (maxSpread <= 0f)
            {
                return MinGap;
            }

            return Mathf.Lerp(MinGap, MaxGap, Mathf.Clamp01(spread / maxSpread));
        }

        void Awake()
        {
            weapons = GetComponent<PlayerWeapons>();
        }

        void Update()
        {
            if (weapons == null)
            {
                return;
            }

            if (weapons.IsSpawned && !weapons.IsOwner)
            {
                if (built)
                {
                    canvasRoot.SetActive(false);
                }

                enabled = false;
                return;
            }

            if (!built)
            {
                Build();
            }

            var gun = weapons.ActiveGun;
            if (gun == null)
            {
                return;
            }

            var gap = ComputeCrosshairGap(
                gun.CurrentSpreadDegrees,
                gun.Stats.maxBloomDegrees + gun.Stats.baseSpreadDegrees);

            SetGap(gap);
            ammoText.text = $"{gun.AmmoInMagazine} / {gun.ReserveAmmo}";
            reloadBar.transform.parent.gameObject.SetActive(gun.IsReloading);
            reloadBar.fillAmount = gun.ReloadProgress01;
        }

        void Build()
        {
            canvasRoot = new GameObject("WeaponHudCanvas");
            canvasRoot.transform.SetParent(transform, false);

            var canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            bars = new[]
            {
                CreateBar("Top", new Vector2(BarLength, BarThickness)),
                CreateBar("Bottom", new Vector2(BarLength, BarThickness)),
                CreateBar("Left", new Vector2(BarThickness, BarLength)),
                CreateBar("Right", new Vector2(BarThickness, BarLength))
            };

            ammoText = CreateText("Ammo", canvasRoot.transform, font, 30);
            var ammoRect = ammoText.rectTransform;
            ammoRect.anchorMin = new Vector2(1f, 0f);
            ammoRect.anchorMax = new Vector2(1f, 0f);
            ammoRect.pivot = new Vector2(1f, 0f);
            ammoRect.anchoredPosition = new Vector2(-40f, 36f);
            ammoRect.sizeDelta = new Vector2(220f, 48f);

            var reloadBackground = CreateImage("ReloadBar", canvasRoot.transform,
                new Color(1f, 1f, 1f, 0.18f));
            var reloadRect = reloadBackground.rectTransform;
            reloadRect.anchorMin = new Vector2(0.5f, 0f);
            reloadRect.anchorMax = new Vector2(0.5f, 0f);
            reloadRect.pivot = new Vector2(0.5f, 0f);
            reloadRect.anchoredPosition = new Vector2(0f, 90f);
            reloadRect.sizeDelta = new Vector2(160f, 8f);

            reloadBar = CreateImage("ReloadFill", reloadBackground.transform,
                new Color(1f, 0.85f, 0.3f, 0.95f));
            reloadBar.type = Image.Type.Filled;
            reloadBar.fillMethod = Image.FillMethod.Horizontal;
            reloadBar.fillAmount = 0f;
            var fillRect = reloadBar.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            built = true;
            SetGap(MinGap);
        }

        Image CreateBar(string name, Vector2 size)
        {
            var image = CreateImage(name, canvasRoot.transform, new Color(1f, 1f, 1f, 0.85f));
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.localPosition = Vector3.zero;
            return image;
        }

        void SetGap(float gap)
        {
            if (bars == null)
            {
                return;
            }

            bars[0].rectTransform.anchoredPosition = new Vector2(0f, gap);
            bars[1].rectTransform.anchoredPosition = new Vector2(0f, -gap);
            bars[2].rectTransform.anchoredPosition = new Vector2(-gap, 0f);
            bars[3].rectTransform.anchoredPosition = new Vector2(gap, 0f);
        }

        static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Text CreateText(string name, Transform parent, Font font, int size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.LowerRight;
            text.color = new Color(1f, 1f, 1f, 0.9f);
            text.raycastTarget = false;
            return text;
        }
    }
}
