using System;
using System.Collections.Generic;
using FruktSharedLibrary.Core;
using UnityEngine;

namespace FruktSharedLibrary.Assets
{
    /// <summary>
    /// Makes imported materials render with the game's own shaders. A bundle carries its own copy of each shader,
    /// which can be missing the variants the game needs (or come from another Unity version), and then shows up
    /// pink. Swapping in the game's copy of the same shader fixes that and keeps the material's textures and colours.
    /// </summary>
    public static class Shaders
    {
        private const string Fallback = "Universal Render Pipeline/Lit";
        private static readonly Dictionary<string, Shader> Cache = new();

        /// <summary>The game's copy of a shader by name, or null if the game doesn't have it.</summary>
        public static Shader Find(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (Cache.TryGetValue(name, out var cached) && cached != null)
                return cached;
            var shader = Shader.Find(name);
            Cache[name] = shader;
            return shader;
        }

        /// <summary>
        /// Switches a material to the game's copy of its shader. Shaders the game doesn't have become URP Lit.
        /// Returns false if nothing could be done.
        /// </summary>
        public static bool FixMaterial(Material material)
        {
            if (material == null)
                return false;
            try
            {
                var current = material.shader;
                var name = current != null ? current.name : null;
                var replacement = Find(name);
                if (replacement == null && (current == null || !current.isSupported))
                {
                    replacement = Find(Fallback);
                    FruktLog.Debug($"Material '{material.name}' uses shader '{name}', which the game doesn't have; using {Fallback}");
                }
                if (replacement == null)
                    return false;
                if (replacement != current)
                {
                    // Keep the render queue: switching shaders resets it, which breaks transparent materials.
                    int queue = material.renderQueue;
                    material.shader = replacement;
                    material.renderQueue = queue;
                }
                return true;
            }
            catch (Exception e)
            {
                FruktLog.Debug($"Fixing material '{material.name}' failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Fixes every material on every renderer in an object and its children.</summary>
        public static int FixMaterials(GameObject root)
        {
            if (root == null)
                return 0;
            int fixedCount = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (FixMaterial(material))
                        fixedCount++;
                }
            }
            return fixedCount;
        }
    }
}
