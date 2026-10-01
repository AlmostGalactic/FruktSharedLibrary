using System;
using System.Text;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Spawning;
using Il2CppLVA.Creatures;
using UnityEngine;

namespace FruktSharedLibrary.Utilities
{
    /// <summary>Debugging helpers that describe game objects and creatures as text.</summary>
    public static class DevTools
    {
        /// <summary>Text tree of a GameObject hierarchy, optionally listing components.</summary>
        public static string DescribeHierarchy(GameObject root, int maxDepth = 6, bool includeComponents = true)
        {
            var builder = new StringBuilder();
            if (root.Exists())
                Describe(root.transform, 0, maxDepth, includeComponents, builder);
            return builder.ToString();
        }

        /// <summary>Writes <see cref="DescribeHierarchy"/> to the log.</summary>
        public static void LogHierarchy(GameObject root, int maxDepth = 6, bool includeComponents = true)
            => FruktLog.Msg("\n" + DescribeHierarchy(root, maxDepth, includeComponents));

        /// <summary>Full report of a creature: vitals, parameters, systems, limbs and organs.</summary>
        public static string DescribeCreature(AbstractCreature creature)
        {
            var builder = new StringBuilder();
            if (!creature.Exists())
                return "(destroyed creature)";

            builder.AppendLine($"Creature '{creature.GetDisplayName()}' [{creature.GetIl2CppTypeName()}] id={creature.CreatureID}");
            builder.AppendLine($"  living={creature.IsLiving()} lifeless={creature.Lifeless} puppeteer={creature.HasPuppeteer()} limbs={creature.GetLimbCount()}");
            builder.AppendLine($"  blood={creature.GetBlood():0.##}/{creature.GetBloodCapacity():0.##} pain={creature.GetPain():0.###} cognition={creature.GetCognition():0.###}");
            builder.AppendLine("  parameters:");
            foreach (var parameter in creature.GetAllParameters())
                builder.AppendLine("    " + parameter);
            builder.AppendLine("  systems: " + string.Join(", ", creature.GetAllSystems().ConvertAll(s => s.GetIl2CppTypeName())));

            foreach (var limb in creature.GetLimbs())
            {
                builder.AppendLine($"  limb {limb.name} part={limb.GetHumanPart()?.ToString() ?? "-"} wholeness={limb.GetWholeness():0.###} wounds={limb.GetBleedingWoundCount()}");
                foreach (var organ in limb.GetAllOrgans())
                    builder.AppendLine($"    organ {organ.GetOrganName()} integrity={organ.GetIntegrity():0.###} efficiency={organ.GetEfficiency():0.###}");
            }
            return builder.ToString();
        }

        /// <summary>Writes <see cref="DescribeCreature"/> to the log.</summary>
        public static void LogCreature(AbstractCreature creature) => FruktLog.Msg("\n" + DescribeCreature(creature));

        /// <summary>Logs every prefab ID registered with the game.</summary>
        public static void LogPrefabIds()
        {
            var ids = Spawner.GetRegisteredPrefabIds();
            FruktLog.Msg($"{ids.Count} registered prefabs:\n  " + string.Join("\n  ", ids));
        }

        private static void Describe(Transform transform, int depth, int maxDepth, bool components, StringBuilder builder)
        {
            string indent = new(' ', depth * 2);
            builder.Append(indent).Append(transform.gameObject.activeSelf ? "" : "(inactive) ").Append(transform.name);
            if (components)
            {
                try
                {
                    var list = transform.GetComponents<Component>();
                    builder.Append("  [");
                    for (int i = 0; i < list.Length; i++)
                    {
                        if (i > 0)
                            builder.Append(", ");
                        builder.Append(list[i] == null ? "missing" : list[i].GetIl2CppTypeName());
                    }
                    builder.Append(']');
                }
                catch (Exception e)
                {
                    builder.Append("  [components unavailable: ").Append(e.Message).Append(']');
                }
            }
            builder.AppendLine();
            if (depth >= maxDepth)
                return;
            for (int i = 0; i < transform.childCount; i++)
                Describe(transform.GetChild(i), depth + 1, maxDepth, components, builder);
        }
    }
}
