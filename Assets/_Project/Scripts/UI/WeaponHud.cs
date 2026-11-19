using AnimeFpsGame.Player;
using AnimeFpsGame.Weapons;
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
        const string PickupActionLabel = "Press F to switch";
        const float PreviewLongestAxis = 1.2f;

        PlayerWeapons weapons;
        PlayerPickupInteractor pickupInteractor;
        GameObject canvasRoot;
        Image[] bars;
        Text ammoText;
        Text reloadLabel;
        Image reloadBar;
        bool built;

        GameObject pickupPrompt;
        RawImage pickupPreview;
        Text pickupName;
        Text pickupAction;

        GameObject previewRig;
        GameObject previewInstance;
        RenderTexture previewTexture;
        Camera previewCamera;
        GunArchetype? previewArchetype;

        /// <summary>True while the pickup panel is actually on screen (false on death).</summary>
        public bool PickupPromptVisible => pickupPrompt != null && pickupPrompt.activeInHierarchy;

        public string PickupPromptActionText => pickupAction != null ? pickupAction.text : null;

        public string PickupPromptNameText => pickupName != null ? pickupName.text : null;

        public GunArchetype? PreviewArchetype => previewArchetype;

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

            if (pickupInteractor == null)
            {
                pickupInteractor = GetComponent<PlayerPickupInteractor>();
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

            if (weapons.IsDead)
            {
                canvasRoot.SetActive(false);
                return;
            }

            if (!canvasRoot.activeSelf)
            {
                canvasRoot.SetActive(true);
            }

            UpdatePickupPrompt();

            var gun = weapons.ActiveGun;
            if (gun == null)
            {
                return;
            }

            // Shots are fixed-cone for the shotgun, so its crosshair must not follow bloom.
            var isShotgun = gun.Stats.pellets > 1;
            var spread = isShotgun ? gun.Stats.pelletSpreadDegrees : gun.CurrentSpreadDegrees;
            var maxSpread = isShotgun
                ? gun.Stats.pelletSpreadDegrees * 2f
                : gun.Stats.maxBloomDegrees + gun.Stats.baseSpreadDegrees;

            SetGap(ComputeCrosshairGap(spread, maxSpread));
            ammoText.text = $"{gun.AmmoInMagazine} / {gun.ReserveAmmo}";
            var reloading = gun.IsReloading;
            reloadBar.transform.parent.gameObject.SetActive(reloading);
            reloadLabel.gameObject.SetActive(reloading);
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

            reloadLabel = CreateText("ReloadLabel", canvasRoot.transform, font, 20);
            var labelRect = reloadLabel.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 102f);
            labelRect.sizeDelta = new Vector2(200f, 26f);
            reloadLabel.alignment = TextAnchor.LowerCenter;
            reloadLabel.text = "Reloading";
            reloadLabel.gameObject.SetActive(false);

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

            BuildPickupPrompt(font);

            built = true;
            SetGap(MinGap);
        }

        void BuildPickupPrompt(Font font)
        {
            var panel = CreateImage("PickupPrompt", canvasRoot.transform,
                new Color(0f, 0f, 0f, 0.45f));
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 200f);
            panelRect.sizeDelta = new Vector2(280f, 190f);
            pickupPrompt = panel.gameObject;

            var previewObject = new GameObject("PickupPreview");
            previewObject.transform.SetParent(pickupPrompt.transform, false);
            pickupPreview = previewObject.AddComponent<RawImage>();
            pickupPreview.raycastTarget = false;
            var previewRect = pickupPreview.rectTransform;
            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.anchoredPosition = new Vector2(0f, 38f);
            previewRect.sizeDelta = new Vector2(220f, 110f);

            pickupName = CreateText("PickupName", pickupPrompt.transform, font, 18);
            var nameRect = pickupName.rectTransform;
            nameRect.anchorMin = new Vector2(0.5f, 0.5f);
            nameRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.anchoredPosition = new Vector2(0f, -24f);
            nameRect.sizeDelta = new Vector2(260f, 24f);
            pickupName.alignment = TextAnchor.MiddleCenter;

            pickupAction = CreateText("PickupAction", pickupPrompt.transform, font, 20);
            var actionRect = pickupAction.rectTransform;
            actionRect.anchorMin = new Vector2(0.5f, 0.5f);
            actionRect.anchorMax = new Vector2(0.5f, 0.5f);
            actionRect.pivot = new Vector2(0.5f, 0.5f);
            actionRect.anchoredPosition = new Vector2(0f, -52f);
            actionRect.sizeDelta = new Vector2(260f, 24f);
            pickupAction.alignment = TextAnchor.MiddleCenter;
            pickupAction.text = PickupActionLabel;

            pickupPrompt.SetActive(false);
        }

        void UpdatePickupPrompt()
        {
            var target = pickupInteractor != null ? pickupInteractor.NearestPickup : null;
            var visible = target != null && !weapons.IsDead;
            pickupPrompt.SetActive(visible);

            if (!visible)
            {
                ReleasePreview();
                return;
            }

            var definition = weapons.DefinitionFor(target.Archetype);
            var name = definition != null ? definition.displayName : target.Archetype.ToString();
            if (pickupName.text != name)
            {
                pickupName.text = name;
            }

            if (pickupAction.text != PickupActionLabel)
            {
                pickupAction.text = PickupActionLabel;
            }

            if (previewArchetype != target.Archetype)
            {
                BuildPreview(target.Archetype, definition);
            }
        }

        void BuildPreview(GunArchetype archetype, WeaponDefinition definition)
        {
            EnsurePreviewRig();
            previewCamera.enabled = true;

            if (previewInstance != null)
            {
                previewInstance.SetActive(false);
                previewInstance.transform.SetParent(null, true);
                Destroy(previewInstance);
                previewInstance = null;
            }

            previewArchetype = archetype;
            pickupPreview.texture = previewTexture;

            if (definition == null || definition.viewmodelPrefab == null)
            {
                return;
            }

            previewInstance = Instantiate(definition.viewmodelPrefab, previewRig.transform, false);
            previewInstance.name = "PickupPreviewInstance";
            previewInstance.transform.localPosition = Vector3.zero;
            previewInstance.transform.localRotation = Quaternion.Euler(10f, 130f, 0f);
            previewInstance.transform.localScale = Vector3.one;

            if (TryGetRendererBounds(previewInstance, out var initialBounds))
            {
                var size = initialBounds.size;
                var longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                if (longest > 0.0001f)
                {
                    previewInstance.transform.localScale *= PreviewLongestAxis / longest;
                }

                if (TryGetRendererBounds(previewInstance, out var scaledBounds))
                {
                    var localCentre = previewRig.transform.InverseTransformPoint(scaledBounds.center);
                    previewInstance.transform.localPosition = -localCentre;
                }
            }
        }

        void EnsurePreviewRig()
        {
            if (previewRig != null)
            {
                return;
            }

            if (previewTexture == null)
            {
                previewTexture = new RenderTexture(512, 256, 16, RenderTextureFormat.ARGB32);
            }

            previewRig = new GameObject("PickupPreviewRig");
            previewRig.transform.SetParent(transform, false);
            previewRig.transform.localPosition = new Vector3(0f, -1000f, 0f);
            previewRig.transform.localRotation = Quaternion.identity;

            var cameraObject = new GameObject("PickupPreviewCamera");
            cameraObject.transform.SetParent(previewRig.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -2.4f);
            cameraObject.transform.localRotation = Quaternion.identity;
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 0.75f;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.cullingMask = ~0;
            previewCamera.targetTexture = previewTexture;
            // The rig is built hidden; BuildPreview turns the camera on for the frame that needs it.
            previewCamera.enabled = false;

            var lightObject = new GameObject("PickupPreviewLight");
            lightObject.transform.SetParent(previewRig.transform, false);
            lightObject.transform.localPosition = new Vector3(2f, 3f, -3f);
            var previewLight = lightObject.AddComponent<Light>();
            previewLight.type = LightType.Point;
            previewLight.range = 8f;
            previewLight.intensity = 2.5f;
        }

        static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            var found = false;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        void ReleasePreview()
        {
            if (previewInstance != null)
            {
                previewInstance.SetActive(false);
                previewInstance.transform.SetParent(null, true);
                Destroy(previewInstance);
                previewInstance = null;
            }

            previewArchetype = null;
            if (previewCamera != null)
            {
                previewCamera.enabled = false;
            }

            if (pickupPreview != null)
            {
                pickupPreview.texture = null;
            }
        }

        void OnDestroy()
        {
            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }

            if (previewRig != null)
            {
                Destroy(previewRig);
                previewRig = null;
            }

            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
                previewTexture = null;
            }
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
