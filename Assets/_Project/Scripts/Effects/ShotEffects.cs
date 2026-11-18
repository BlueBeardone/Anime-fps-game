using System.Collections.Generic;
using UnityEngine;

namespace AnimeFpsGame.Effects
{
    /// <summary>
    /// Short-lived, pooled shot feedback: tracer per pellet, an impact puff and one muzzle flash.
    /// Created in code so the player prefab stays art-free and builds stay cheap.
    /// </summary>
    public class ShotEffects : MonoBehaviour
    {
        const float TracerSeconds = 0.04f;
        const float PuffSeconds = 0.2f;
        const float FlashSeconds = 0.03f;

        static readonly int MaxTracers = 32;
        static readonly int MaxPuffs = 32;

        [SerializeField] Color tracerColor = new Color(1f, 0.82f, 0.4f, 0.9f);
        [SerializeField] Color impactColor = new Color(1f, 0.9f, 0.65f, 0.85f);
        [SerializeField] float tracerMissDistance = 60f;

        readonly List<LineRenderer> tracers = new();
        readonly List<float> tracerExpiry = new();
        readonly List<Transform> puffs = new();
        readonly List<float> puffExpiry = new();
        MaterialPropertyBlock colorBlock;
        LineRenderer[] tracerPool;
        Transform[] puffPool;
        Material tracerMaterial;
        Material puffMaterial;
        Light muzzleLight;
        float muzzleExpiry;

        void Awake()
        {
            colorBlock = new MaterialPropertyBlock();
            BuildPool();
        }

        /// <summary>Owner-side feedback from the actual raycast hits.</summary>
        public void PlayLocal(Vector3 muzzleOrigin, Vector3 rayOrigin, Vector3[] directions,
            RaycastHit[] hits)
        {
            if (directions == null)
            {
                return;
            }

            PlayFlash(muzzleOrigin);

            for (var i = 0; i < directions.Length; i++)
            {
                var end = rayOrigin + directions[i] * tracerMissDistance;
                var hasHit = hits != null && i < hits.Length && hits[i].collider != null;
                if (hasHit)
                {
                    end = hits[i].point;
                }

                PlayTracer(muzzleOrigin, end);

                if (hasHit)
                {
                    PlayPuff(end, hits[i].normal);
                }
            }
        }

        /// <summary>Feedback for everyone else, from the server's validated hit points.</summary>
        public void PlayRemote(Vector3 origin, Vector3[] directions, Vector3[] points)
        {
            if (directions == null)
            {
                return;
            }

            PlayFlash(origin);

            for (var i = 0; i < directions.Length; i++)
            {
                var hasPoint = points != null && i < points.Length;
                var end = hasPoint ? points[i] : origin + directions[i] * tracerMissDistance;
                PlayTracer(origin, end);

                if (hasPoint)
                {
                    PlayPuff(end, -directions[i]);
                }
            }
        }

        void Update()
        {
            var now = Time.time;

            for (var i = tracers.Count - 1; i >= 0; i--)
            {
                var remaining = tracerExpiry[i] - now;
                if (remaining <= 0f)
                {
                    tracers[i].gameObject.SetActive(false);
                    tracers.RemoveAt(i);
                    tracerExpiry.RemoveAt(i);
                    continue;
                }

                var t = Mathf.Clamp01(remaining / TracerSeconds);
                var line = tracers[i];
                line.startWidth = 0.012f * t + 0.001f;
                line.endWidth = 0.004f * t;
                var faded = new Color(tracerColor.r, tracerColor.g, tracerColor.b, tracerColor.a * t);
                colorBlock.SetColor("_BaseColor", faded);
                colorBlock.SetColor("_Color", faded);
                line.SetPropertyBlock(colorBlock);
            }

            for (var i = puffs.Count - 1; i >= 0; i--)
            {
                var remaining = puffExpiry[i] - now;
                if (remaining <= 0f)
                {
                    puffs[i].gameObject.SetActive(false);
                    puffs.RemoveAt(i);
                    puffExpiry.RemoveAt(i);
                    continue;
                }

                var t = Mathf.Clamp01(remaining / PuffSeconds);
                var puff = puffs[i];
                // Grow while fading out.
                puff.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.05f, t);
                var puffRenderer = puff.GetComponent<MeshRenderer>();
                if (puffRenderer != null)
                {
                    var faded = new Color(
                        impactColor.r, impactColor.g, impactColor.b, impactColor.a * t);
                    colorBlock.SetColor("_BaseColor", faded);
                    colorBlock.SetColor("_Color", faded);
                    puffRenderer.SetPropertyBlock(colorBlock);
                }
            }

            if (muzzleLight != null && muzzleLight.enabled && now >= muzzleExpiry)
            {
                muzzleLight.enabled = false;
            }
        }

        void OnDestroy()
        {
            if (puffPool != null)
            {
                foreach (var puff in puffPool)
                {
                    if (puff != null)
                    {
                        Destroy(puff.gameObject);
                    }
                }
            }

            if (tracerMaterial != null)
            {
                Destroy(tracerMaterial);
            }

            if (puffMaterial != null)
            {
                Destroy(puffMaterial);
            }
        }

        void PlayTracer(Vector3 start, Vector3 end)
        {
            var line = TakeTracer();
            if (line == null)
            {
                return;
            }

            line.gameObject.SetActive(true);
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            tracers.Add(line);
            tracerExpiry.Add(Time.time + TracerSeconds);
        }

        void PlayPuff(Vector3 point, Vector3 normal)
        {
            var puff = TakePuff();
            if (puff == null)
            {
                return;
            }

            puff.gameObject.SetActive(true);
            puff.position = point + normal.normalized * 0.01f;
            puffs.Add(puff);
            puffExpiry.Add(Time.time + PuffSeconds);
        }

        void PlayFlash(Vector3 origin)
        {
            if (muzzleLight == null)
            {
                return;
            }

            muzzleLight.transform.position = origin;
            muzzleLight.enabled = true;
            muzzleExpiry = Time.time + FlashSeconds;
        }

        LineRenderer TakeTracer()
        {
            for (var i = 0; i < tracerPool.Length; i++)
            {
                if (!tracerPool[i].gameObject.activeSelf)
                {
                    return tracerPool[i];
                }
            }

            return null;
        }

        Transform TakePuff()
        {
            for (var i = 0; i < puffPool.Length; i++)
            {
                if (!puffPool[i].gameObject.activeSelf)
                {
                    return puffPool[i];
                }
            }

            return null;
        }

        void BuildPool()
        {
            tracerMaterial = CreateMaterial(tracerColor);
            puffMaterial = CreateMaterial(impactColor);

            tracerPool = new LineRenderer[MaxTracers];
            for (var i = 0; i < tracerPool.Length; i++)
            {
                var go = new GameObject($"Tracer {i}");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.material = tracerMaterial;
                line.startWidth = 0.012f;
                line.endWidth = 0.004f;
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                go.SetActive(false);
                tracerPool[i] = line;
            }

            puffPool = new Transform[MaxPuffs];
            for (var i = 0; i < puffPool.Length; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Impact {i}";
                // Puffs are placed in world space, so they must not inherit the player's motion.
                sphere.transform.SetParent(null, false);
                sphere.transform.localScale = Vector3.one * 0.06f;
                var collider = sphere.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                sphere.GetComponent<MeshRenderer>().sharedMaterial = puffMaterial;
                sphere.SetActive(false);
                puffPool[i] = sphere.transform;
            }

            var flash = new GameObject("MuzzleFlash");
            flash.transform.SetParent(transform, false);
            muzzleLight = flash.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.color = new Color(1f, 0.8f, 0.45f);
            muzzleLight.range = 3f;
            muzzleLight.intensity = 2f;
            muzzleLight.shadows = LightShadows.None;
            muzzleLight.enabled = false;

            tracers.Clear();
            tracerExpiry.Clear();
            puffs.Clear();
            puffExpiry.Clear();
        }

        static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            material.color = color;
            return material;
        }
    }
}
