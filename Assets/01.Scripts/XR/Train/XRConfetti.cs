using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The arrival's confetti, as on the web edition: one burst of paper squares in the line's colours from the exit
    /// tunnel as the train comes in. A single world-space particle system, in metres like the steam, on the occluded
    /// fade shader so real hands hide it as they hide the board.
    /// </summary>
    public sealed class XRConfetti : MonoBehaviour
    {
        private const int Pieces = 90;

        /// <summary>Sizes and speeds at the standard 6 cm cell; a scaled board scales its confetti with it.</summary>
        private const float StandardCell = 0.06f;
        private const float PieceSize = 0.008f;
        private const float RiseSpeed = 0.95f;
        private const float SpreadSpeed = 0.28f;

        private ParticleSystem _system;
        private Material _material;

        public static XRConfetti Create(Transform parent)
        {
            var go = new GameObject("Confetti");
            go.transform.SetParent(parent, false);
            return go.AddComponent<XRConfetti>();
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
            main.maxParticles = Pieces * 2;
            // Paper, not shot: it goes up fast and comes down slowly.
            main.gravityModifier = 0.32f;

            // Particles come only from Burst.
            var emission = _system.emission;
            emission.enabled = false;
            var shape = _system.shape;
            shape.enabled = false;

            var drag = _system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 0.5f;
            drag.dampen = 0.06f;

            var spin = _system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-12f, 12f);

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            var colour = _system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(fade);

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = PaperMaterial();

            _system.Play();
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        /// <summary>One burst at <paramref name="at"/>, in the room, mostly in <paramref name="line"/>'s colour, for a board whose cell is <paramref name="cell"/> metres.</summary>
        public void Burst(Vector3 at, Color line, float cell)
        {
            if (_system == null) return;
            var scale = cell > 0f ? cell / StandardCell : 1f;
            var deep = new Color(line.r * 0.72f, line.g * 0.72f, line.b * 0.72f, 1f);
            var emit = new ParticleSystem.EmitParams();
            for (var i = 0; i < Pieces; i++)
            {
                var side = Random.insideUnitCircle * SpreadSpeed;
                emit.position = at + Random.insideUnitSphere * (0.01f * scale);
                emit.velocity = (new Vector3(side.x, 0f, side.y) + Vector3.up * (RiseSpeed * Random.Range(0.55f, 1f))) * scale;
                emit.startSize = PieceSize * scale * Random.Range(0.7f, 1.4f);
                emit.startLifetime = Random.Range(1.3f, 2.2f);
                emit.rotation = Random.Range(0f, 360f);
                // Half in the line's colour, the rest its deeper shade, the platform yellow and white.
                var pick = i % 6;
                emit.startColor = pick < 3 ? line : pick == 3 ? deep : pick == 4 ? XRPalette.Warn : XRPalette.White;
                _system.Emit(emit, 1);
            }
        }

        /// <summary>Plain squares that carry their colour in the vertex colour, on the occluded fade shader when it is configured.</summary>
        private Material PaperMaterial()
        {
            _material = XROcclusionMaterials.Fade("XR Confetti", Color.white, Texture2D.whiteTexture);
            if (_material != null)
            {
                _material.SetFloat("_VertexColour", 1f);
                return _material;
            }

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            _material = new Material(shader) { name = "XR Confetti" };
            return _material;
        }
    }
}
