using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// Steam (X19): one particle effect in two sizes — a small puff for a piece let go off the platform or replaced, and
    /// a burst for a thrown one. A single world-space system emits both wherever they happen, in metres rather than at
    /// board scale. The whistle that goes with the burst is audio, which comes with XR10.
    /// </summary>
    public sealed class XRSteam : MonoBehaviour
    {
        private const int TextureSize = 64;

        private ParticleSystem _system;
        private Texture2D _texture;
        private Material _material;

        public static XRSteam Create(Transform parent)
        {
            var go = new GameObject("Steam");
            go.transform.SetParent(parent, false);
            return go.AddComponent<XRSteam>();
        }

        private void Awake()
        {
            _system = gameObject.AddComponent<ParticleSystem>();
            _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.maxParticles = 400;
            // Steam rises, gently.
            main.gravityModifier = -0.02f;

            // Particles come only from Puff and Burst.
            var emission = _system.emission;
            emission.enabled = false;
            var shape = _system.shape;
            shape.enabled = false;

            var size = _system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            var colour = _system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(fade);

            // A puff billows out and slows, rather than flying on.
            var drag = _system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 0.04f;
            drag.dampen = 0.12f;

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = SteamMaterial();

            _system.Play();
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_texture != null) Destroy(_texture);
        }

        /// <summary>The small puff: a piece let go off the platform, an illegal drop with no way back, a replaced piece.</summary>
        public void Puff(Vector3 at) => Emit(at, 10, 0.022f, 0.08f);

        /// <summary>The burst at the end of a throw.</summary>
        public void Burst(Vector3 at) => Emit(at, 30, 0.035f, 0.35f);

        private void Emit(Vector3 at, int count, float size, float speed)
        {
            if (_system == null) return;
            var emit = new ParticleSystem.EmitParams();
            for (var i = 0; i < count; i++)
            {
                emit.position = at + Random.insideUnitSphere * (size * 0.5f);
                emit.velocity = Random.insideUnitSphere * speed + Vector3.up * (speed * 0.4f);
                emit.startSize = size * Random.Range(0.7f, 1.3f);
                emit.startLifetime = Random.Range(0.6f, 1.1f);
                emit.rotation = Random.Range(0f, 360f);
                emit.startColor = XRPalette.Steam;
                _system.Emit(emit, 1);
            }
        }

        /// <summary>A soft white disc on the occluded fade shader, so real hands hide the steam as they hide the board.</summary>
        private Material SteamMaterial()
        {
            _texture = SoftDisc();
            _material = XROcclusionMaterials.Fade("XR Steam", Color.white, _texture);
            if (_material != null)
            {
                // Particles carry their colour and fade in the vertex colour.
                _material.SetFloat("_VertexColour", 1f);
                return _material;
            }

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            _material = new Material(shader) { name = "XR Steam" };
            _material.SetTexture("_BaseMap", _texture);
            return _material;
        }

        private static Texture2D SoftDisc()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "XR Steam Puff",
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
            for (var x = 0; x < TextureSize; x++)
            {
                var dx = (x + 0.5f) / TextureSize * 2f - 1f;
                var dy = (y + 0.5f) / TextureSize * 2f - 1f;
                var a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * (3f - 2f * a);
                pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
