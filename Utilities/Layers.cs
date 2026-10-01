using System;
using Il2CppEtc;
using UnityEngine;

namespace FruktSharedLibrary.Utilities
{
    /// <summary>The game's physics layer masks, for raycasts and overlap queries.</summary>
    public static class Layers
    {
        /// <summary>Static world geometry (ground, walls).</summary>
        public static int Environment => Read(() => CEPhysicLayerMasks.Environment);

        /// <summary>Bullets.</summary>
        public static int Bullet => Read(() => CEPhysicLayerMasks.Bullet);

        /// <summary>Creature limbs (the physical ragdoll).</summary>
        public static int Puppet => Read(() => CEPhysicLayerMasks.Puppet);

        /// <summary>Animation rigs that drive creatures (usually not something you want to hit).</summary>
        public static int Puppeteer => Read(() => CEPhysicLayerMasks.Puppeteer);

        /// <summary>Everything except the puppeteer rigs: what you normally want for gameplay raycasts.</summary>
        public static int Gameplay => Physics.DefaultRaycastLayers & ~Puppeteer;

        /// <summary>True when the GameObject's layer is part of <paramref name="mask"/>.</summary>
        public static bool IsInMask(GameObject gameObject, int mask) => gameObject != null && (mask & (1 << gameObject.layer)) != 0;

        private static int Read(Func<int> read)
        {
            try
            {
                return read();
            }
            catch
            {
                return 0;
            }
        }
    }
}
