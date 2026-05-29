using System.Collections.Generic;
using UnityEngine;

namespace Ludus.UsableItems.Core
{
    /// <summary>
    /// Pure, dependency-free flashbang decision math. Lives in its own assembly so it
    /// can be unit-tested in EditMode without a NetworkManager. No Netcode / game types here.
    /// </summary>
    public static class FlashbangMath
    {
        /// <summary>Eye-height offset added to a player's foot position before the radius test.</summary>
        public const float EyeHeight = 1f;

        /// <summary>
        /// Fills <paramref name="result"/> with the indices of <paramref name="targetPositions"/>
        /// whose (position + eye height) fall within <paramref name="radius"/> of
        /// <paramref name="explosionPoint"/>. Clears the result list first.
        /// </summary>
        public static void SelectAffectedIndices(
            IReadOnlyList<Vector3> targetPositions,
            Vector3 explosionPoint,
            float radius,
            List<int> result)
        {
            result.Clear();
            if (targetPositions == null || radius <= 0f) return;

            float sqrRadius = radius * radius;
            for (int i = 0; i < targetPositions.Count; i++)
            {
                Vector3 eye = targetPositions[i] + Vector3.up * EyeHeight;
                if ((eye - explosionPoint).sqrMagnitude <= sqrRadius)
                    result.Add(i);
            }
        }

        /// <summary>
        /// Server-side guard: the throw origin a client supplied must be finite and close
        /// to the player (anti-cheat — clients cannot detonate across the map).
        /// </summary>
        public static bool IsThrowOriginValid(Vector3 playerPosition, Vector3 origin, float maxDistance)
        {
            if (float.IsNaN(origin.x) || float.IsNaN(origin.y) || float.IsNaN(origin.z)) return false;
            if (float.IsInfinity(origin.x) || float.IsInfinity(origin.y) || float.IsInfinity(origin.z)) return false;
            return Vector3.Distance(playerPosition + Vector3.up, origin) <= maxDistance;
        }
    }
}
