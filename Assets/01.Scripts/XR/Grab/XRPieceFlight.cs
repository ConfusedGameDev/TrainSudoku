using System;
using System.Collections;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>
    /// A piece on its way out of the hand (XR-PRD 4.4, 8): the quick arc back to where an illegal drop came from, or a
    /// thrown piece tumbling along the throw under gravity before it bursts into steam. The throw ignores the room:
    /// nothing lands on a real surface (X19). It is only a show — the board has changed before it starts — and the piece
    /// is destroyed when it is over.
    /// </summary>
    public sealed class XRPieceFlight : MonoBehaviour
    {
        private const float TumbleDegreesPerSecond = 540f;

        /// <summary>Flies <paramref name="piece"/> to <paramref name="to"/>, rising <paramref name="height"/> metres midway, then calls <paramref name="landed"/>.</summary>
        public static void Arc(GameObject piece, Vector3 to, float seconds, float height, Action landed)
        {
            var flight = piece.AddComponent<XRPieceFlight>();
            flight.StartCoroutine(flight.ArcTo(to, seconds, height, landed));
        }

        /// <summary>Throws <paramref name="piece"/> at <paramref name="velocity"/>, then calls <paramref name="burst"/> with where it got to.</summary>
        public static void Throw(GameObject piece, Vector3 velocity, float seconds, Action<Vector3> burst)
        {
            var flight = piece.AddComponent<XRPieceFlight>();
            flight.StartCoroutine(flight.Fly(velocity, seconds, burst));
        }

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

        private IEnumerator Fly(Vector3 velocity, float seconds, Action<Vector3> burst)
        {
            var axis = UnityEngine.Random.onUnitSphere;
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                var dt = Time.unscaledDeltaTime;
                elapsed += dt;
                velocity += Physics.gravity * dt;
                transform.position += velocity * dt;
                transform.Rotate(axis, TumbleDegreesPerSecond * dt, Space.World);
                yield return null;
            }

            burst?.Invoke(transform.position);
            Destroy(gameObject);
        }
    }
}
