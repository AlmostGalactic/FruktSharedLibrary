using FruktSharedLibrary.Interop;
using Il2CppLVA.Core;
using Il2CppLVA.Creatures;
using Il2CppLVA.Limbs;
using Il2CppLVA.Organs;
using Il2CppLVA.Organs.Parameters;

namespace FruktSharedLibrary.Entities
{
    /// <summary>Reading organ state. Organ classes live in <c>Il2CppLVA.Organs.Variants</c> (Brain, Heart, Lung, Bone...).</summary>
    public static class OrganExtensions
    {
        /// <summary>The organ's type name, e.g. "Brain", "Heart", "Bone".</summary>
        public static string GetOrganName(this AbstractOrgan organ) => organ.GetIl2CppTypeName();

        /// <summary>The limb that contains the organ.</summary>
        public static AbstractLimb GetLimb(this AbstractOrgan organ) => organ.Exists() ? organ.References?.AssignedLimb : null;

        /// <summary>The creature that owns the organ.</summary>
        public static AbstractCreature GetCreature(this AbstractOrgan organ) => organ.Exists() ? organ.References?.AssignedCreature : null;

        /// <summary>Structural integrity (how much of the organ's tissue is left).</summary>
        public static float GetIntegrity(this AbstractOrgan organ) => organ.GetParameterValue<OrganIntegrity>() ?? 0f;

        /// <summary>How well the organ is working.</summary>
        public static float GetEfficiency(this AbstractOrgan organ)
        {
            var value = organ.GetParameterValue<OrganEfficiency>();
            if (value.HasValue)
                return value.Value;
            var readOnly = organ.Exists() ? organ.References?.Efficiency : null;
            return readOnly?.TryCast<LVAParameter>()?.Value ?? 0f;
        }
    }
}
