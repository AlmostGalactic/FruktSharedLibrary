using System;
using System.Collections.Generic;
using FruktSharedLibrary.Interop;
using Il2CppInterop.Runtime;
using Il2CppLVA.Core;

namespace FruktSharedLibrary.Entities
{
    /// <summary>A snapshot of one LVA parameter (pain, blood, cognition, wholeness, integrity...).</summary>
    public readonly struct LvaParameterInfo
    {
        /// <summary>Parameter class name, e.g. "CreaturePain" or "LimbWholeness".</summary>
        public string Name { get; init; }
        public float Value { get; init; }
        public float Min { get; init; }
        public float Max { get; init; }
        /// <summary>Value mapped to 0..1 between Min and Max.</summary>
        public float Normalized { get; init; }
        /// <summary>The live parameter object.</summary>
        public LVAParameter Parameter { get; init; }

        public override string ToString() => $"{Name} = {Value:0.###} [{Min:0.###}..{Max:0.###}]";
    }

    /// <summary>
    /// Access to the game's LVA simulation ("living" entities: creatures, limbs and organs).
    /// Every LVA entity owns a set of <b>parameters</b> (numbers such as pain or blood) and
    /// <b>systems</b> (logic such as the blood tank or pain perception).
    /// </summary>
    public static class LvaExtensions
    {
        /// <summary>
        /// Gets one of the entity's own parameters, e.g. <c>creature.GetParameter&lt;CreaturePain&gt;()</c>
        /// or <c>limb.GetParameter&lt;LimbWholeness&gt;()</c>. Returns null if the entity doesn't have it.
        /// </summary>
        public static T GetParameter<T>(this LVAEntity entity) where T : LVAInternalParameter
        {
            var module = Module(entity);
            if (module == null)
                return null;
            try
            {
                LVAInternalParameter parameter = null;
                return module.TryGetInternalParameter(Il2CppType.Of<T>(), out parameter) ? parameter?.TryCast<T>() : null;
            }
            catch (Exception e)
            {
                Core.FruktLog.Debug($"GetParameter<{typeof(T).Name}> failed: {e.Message}");
                return null;
            }
        }

        /// <summary>Current value of one of the entity's parameters, or null if it doesn't have it.</summary>
        public static float? GetParameterValue<T>(this LVAEntity entity) where T : LVAInternalParameter
        {
            var parameter = entity.GetParameter<T>();
            return parameter == null ? null : parameter.Value;
        }

        /// <summary>Snapshots every parameter of an entity (great for debugging and inspection UIs).</summary>
        public static List<LvaParameterInfo> GetAllParameters(this LVAEntity entity)
        {
            var result = new List<LvaParameterInfo>();
            var module = Module(entity);
            var map = module?.m_parameters;
            if (map == null)
                return result;
            foreach (var parameter in map.Values)
            {
                if (parameter == null)
                    continue;
                result.Add(Describe(parameter));
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return result;
        }

        /// <summary>Snapshots a single parameter.</summary>
        public static LvaParameterInfo Describe(this LVAParameter parameter) => new()
        {
            Name = parameter.GetIl2CppTypeName(),
            Value = parameter.Value,
            Min = parameter.MinValue,
            Max = parameter.MaxValue,
            Normalized = parameter.ValueNormalized,
            Parameter = parameter,
        };

        /// <summary>
        /// Overwrites a parameter's stored value. Parameters that are driven by dependencies (most of them)
        /// get recalculated by the simulation, so the change may only last until the next update; use it for
        /// parameters with no inputs, or re-apply it every frame.
        /// </summary>
        public static void ForceValue(this LVAParameter parameter, float value) => parameter?.Inner?.SetValue(value);

        /// <summary>
        /// Gets one of the entity's systems, e.g. <c>creature.GetSystem&lt;BloodTank&gt;()</c> or
        /// <c>limb.GetSystem&lt;BloodSystem&gt;()</c>. Returns null if the entity doesn't run it.
        /// </summary>
        public static T GetSystem<T>(this LVAEntity entity) where T : LVAEntity.LVASystem
        {
            var systems = entity?.m_internalSystems?.m_systems;
            if (systems == null)
                return null;
            for (int i = 0; i < systems.Count; i++)
            {
                var cast = systems[i]?.TryCast<T>();
                if (cast != null)
                    return cast;
            }
            return null;
        }

        /// <summary>All systems an entity runs.</summary>
        public static List<LVAEntity.LVASystem> GetAllSystems(this LVAEntity entity)
            => entity?.m_internalSystems?.m_systems.ToManagedList() ?? new List<LVAEntity.LVASystem>();

        /// <summary>True when the entity still exists and its simulation hasn't been torn down.</summary>
        public static bool IsLvaActive(this LVAEntity entity)
            => entity.Exists() && !entity.LVADestroyed;

        private static LVAEntity.InternalParametersModule Module(LVAEntity entity)
            => entity.Exists() ? entity.m_internalParameters : null;
    }
}
