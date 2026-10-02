using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace FruktSharedLibrary.Internal
{
    /// <summary>
    /// Finds the library's types and members that a mod uses but this version of the library doesn't have. It
    /// reads the mod's metadata and asks the runtime to resolve every reference into the library, so it finds
    /// exactly what would throw a MissingMethodException (or similar) once the mod's code ran.
    /// </summary>
    internal static class ModCompatibility
    {
        internal const string LibraryName = "FruktSharedLibrary";

        /// <summary>The library version a mod was built against (null if it doesn't reference the library).</summary>
        internal static Version ReferencedVersion(Assembly assembly)
        {
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name == LibraryName)
                    return reference.Version;
            }
            return null;
        }

        /// <summary>
        /// The library types and members <paramref name="assembly"/> uses that can't be found, as readable names
        /// ("ModBundle.Load", "new ModBundle", "Assets.ModBundle"). Empty when everything is there, or when the
        /// assembly's file can't be read.
        /// </summary>
        internal static List<string> FindMissing(Assembly assembly, string path)
        {
            var missing = new List<string>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return missing;
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
                return missing;
            var reader = pe.GetMetadataReader();
            var module = assembly.ManifestModule;

            var library = new HashSet<AssemblyReferenceHandle>();
            foreach (var handle in reader.AssemblyReferences)
            {
                if (reader.GetString(reader.GetAssemblyReference(handle).Name) == LibraryName)
                    library.Add(handle);
            }
            if (library.Count == 0)
                return missing;

            foreach (var handle in reader.TypeReferences)
            {
                if (!IsLibraryType(reader, handle, library))
                    continue;
                try
                {
                    module.ResolveType(MetadataTokens.GetToken(handle));
                }
                catch (Exception e) when (IsMissing(e))
                {
                    Add(missing, TypeName(reader, handle, true));
                }
            }

            foreach (var handle in reader.MemberReferences)
            {
                var member = reader.GetMemberReference(handle);
                if (!TryGetLibraryParent(reader, member.Parent, library, out var parent))
                    continue;
                try
                {
                    module.ResolveMember(MetadataTokens.GetToken(handle));
                }
                catch (ArgumentException e) when (e is not ArgumentOutOfRangeException && !IsMissing(e))
                {
                    // A member of a generic type instantiated with the mod's own type parameters. It can't be
                    // resolved without knowing those, so it's left out rather than reported.
                }
                catch (Exception e) when (IsMissing(e))
                {
                    string name = reader.GetString(member.Name);
                    string type = TypeName(reader, parent, false);
                    Add(missing, name == ".ctor" ? "new " + type : type + "." + name);
                }
            }
            return missing;
        }

        private static bool IsMissing(Exception e)
            => e is MissingMemberException || e is TypeLoadException || e is FileLoadException || e is FileNotFoundException
               || e is BadImageFormatException;

        private static void Add(List<string> list, string name)
        {
            if (!list.Contains(name))
                list.Add(name);
        }

        private static bool IsLibraryType(MetadataReader reader, TypeReferenceHandle handle, HashSet<AssemblyReferenceHandle> library)
        {
            var scope = reader.GetTypeReference(handle).ResolutionScope;
            return scope.Kind switch
            {
                HandleKind.AssemblyReference => library.Contains((AssemblyReferenceHandle)scope),
                HandleKind.TypeReference => IsLibraryType(reader, (TypeReferenceHandle)scope, library), // a nested type
                _ => false,
            };
        }

        /// <summary>The library type a member reference belongs to, also through a generic instance like Foo&lt;int&gt;.</summary>
        private static bool TryGetLibraryParent(MetadataReader reader, EntityHandle parent, HashSet<AssemblyReferenceHandle> library,
            out TypeReferenceHandle type)
        {
            type = default;
            if (parent.Kind == HandleKind.TypeReference)
            {
                type = (TypeReferenceHandle)parent;
                return IsLibraryType(reader, type, library);
            }
            if (parent.Kind != HandleKind.TypeSpecification)
                return false;
            var blob = reader.GetBlobReader(reader.GetTypeSpecification((TypeSpecificationHandle)parent).Signature);
            if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance)
                return false;
            blob.ReadSignatureTypeCode(); // class or value type
            var definition = blob.ReadTypeHandle();
            if (definition.Kind != HandleKind.TypeReference)
                return false;
            type = (TypeReferenceHandle)definition;
            return IsLibraryType(reader, type, library);
        }

        /// <summary>"ModBundle", or "Assets.ModBundle" with the namespace after the library's own.</summary>
        private static string TypeName(MetadataReader reader, TypeReferenceHandle handle, bool withNamespace)
        {
            var type = reader.GetTypeReference(handle);
            string name = reader.GetString(type.Name);
            int tick = name.IndexOf('`');
            if (tick > 0)
                name = name.Substring(0, tick);
            if (type.ResolutionScope.Kind == HandleKind.TypeReference)
                return TypeName(reader, (TypeReferenceHandle)type.ResolutionScope, withNamespace) + "." + name;
            if (!withNamespace)
                return name;
            string ns = reader.GetString(type.Namespace);
            if (ns.StartsWith(LibraryName + ".", StringComparison.Ordinal))
                ns = ns.Substring(LibraryName.Length + 1);
            else if (ns == LibraryName)
                ns = "";
            return ns.Length == 0 ? name : ns + "." + name;
        }
    }
}
