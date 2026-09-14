using System;
using System.Collections;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// A piece on its way out of the hand (XR-PRD 4.4, 8): the quick arc back to where an illegal drop came from, or a
    /// piece let go or thrown off the platform falling into the room as a real body. It is only a show — the board has
    /// changed before it starts — and the piece is destroyed when it is over.
    /// </summary>
    /// <remarks>
    /// A falling piece tumbles and bounces off whatever has a collider: the room's detected surfaces, whose colliders stay
    /// on after placement with only their renderers hidden, and the board. The room lets it bounce about for a few
    /// seconds; the board ends it at once, because a piece lying on the platform would read as laid (X19, revised after
    /// the XR7 headset check, which asked for pieces that land in the room).
    /// </remarks>
    public sealed class XRPieceFlight : MonoBehaviour
    {
        /// <summary>
        /// The least thickness of a falling piece's collider, in cells (9 mm): the track alone is flat enough to slip
        /// through a surface between physics steps.
        /// </summary>
        private const float MinThickness = 0.15f;

        private const float TumbleRadiansPerSecond = 8f;

        private static PhysicsMaterial _bounce;

        private Transform _board;
        private Action<Vector3> _ended;
        private bool _over;

        /// <summary>Flies <paramref name="piece"/> to <paramref name="to"/>, rising <paramref name="height"/> metres midway, then calls <paramref name="landed"/>.</summary>
        public static void Arc(GameObject piece, Vector3 to, float seconds, float height, Action landed)
        {
            var flight = piece.AddComponent<XRPieceFlight>();
            flight.StartCoroutine(flight.ArcTo(to, seconds, height, landed));
        }

        /// <summary>
        /// Lets <paramref name="piece"/> go at <paramref name="velocity"/> to fall, tumble and bounce. After
        /// <paramref name="seconds"/>, or as soon as it touches anything under <paramref name="board"/>, it calls
        /// <paramref name="ended"/> with where it got to and is destroyed.
        /// </summary>
        public static void Fall(GameObject piece, Vector3 velocity, float seconds, Transform board, Action<Vector3> ended)
        {
            var flight = piece.AddComponent<XRPieceFlight>();
            flight._board = board;
            flight._ended = ended;

            var box = piece.AddComponent<BoxCollider>();
            if (piece.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
            {
                var bounds = filter.sharedMesh.bounds;
                box.center = bounds.center;
                box.size = new Vector3(bounds.size.x, Mathf.Max(bounds.size.y, MinThickness), bounds.size.z);
            }

            box.sharedMaterial = Bounce;
            var body = piece.AddComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            body.angularVelocity = UnityEngine.Random.onUnitSphere * TumbleRadiansPerSecond;
            flight.StartCoroutine(flight.EndAfter(seconds));
        }

        private static PhysicsMaterial Bounce => _bounce != null ? _bounce : _bounce = new PhysicsMaterial("XR Falling Piece")
        {
            bounciness = 0.45f,
            bounceCombine = PhysicsMaterialCombine.Maximum,
            dynamicFriction = 0.5f,
            staticFriction = 0.5f,
        };

        private IEnumerator ArcTo(Vector3 to, float seconds, float height, Action landed)
        {
            var from = transform.position;
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);
                var eased = 1f - (1f - t) * (1f - t);
                transform.position = Vector3.Lerp(from, to, eased) + Vector3.up * (height * 4f * t * (1f - t));
                yield return null;
            }

            landed?.Invoke();
            Destroy(gameObject);
        }

        private IEnumerator EndAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            End();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (OnBoard(collision.collider != null ? collision.collider.transform : null)) End();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (OnBoard(other != null ? other.transform : null)) End();
        }

        private bool OnBoard(Transform other) => _board != null && other != null && other.IsChildOf(_board);

        private void End()
        {
            if (_over) return;
            _over = true;
            _ended?.Invoke(transform.position);
            Destroy(gameObject);
        }
    }
}
