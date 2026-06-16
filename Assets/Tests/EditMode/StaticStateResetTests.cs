using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC3 regression guard for ticket 86ca9a39q (Configurable Enter Play Mode — "Do not reload
    /// Domain or Scene").
    ///
    /// With domain reload DISABLED, static fields PERSIST across editor play-entries. The catch Erik
    /// flagged: a mutable runtime static that lacks a per-play-entry reset accumulates stale state →
    /// subtle editor-only bugs (stale singletons / handlers). The fix discipline is a
    /// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] reset on the
    /// owning type.
    ///
    /// This guards the BUG CLASS, not just today's one instance: <see cref="EveryMutableRuntimeStatic_HasASubsystemRegistrationReset"/>
    /// reflects over the whole FarHorizon.Runtime asmdef and FAILS if any mutable static field exists
    /// whose declaring type has NO SubsystemRegistration reset method. So when a future persona adds a
    /// new mutable static and forgets the reset, THIS test goes red (the "every new static → add a
    /// reset" rule made mechanical).
    ///
    /// NOTE: headless test runs + CI builds RELOAD the domain regardless of the editor setting, so the
    /// statics here always start null in this very run — this is a STRUCTURAL guard (does the reset
    /// method EXIST + clear the field), not a live cross-play-entry accumulation test (which is only
    /// reproducible by a human clicking Play twice in the editor; see the doc's manual-verification note).
    /// </summary>
    public class StaticStateResetTests
    {
        // The runtime assembly under audit. Anchored on a known runtime type so the test is
        // refactor-proof (no string assembly name to drift).
        private static readonly Assembly RuntimeAsm = typeof(BuildInfo).Assembly;

        private const BindingFlags StaticFieldFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private const BindingFlags StaticMethodFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// The bug-class guard. Enumerate every MUTABLE static field declared in the runtime asmdef;
        /// for each, require its declaring type to carry a [RuntimeInitializeOnLoadMethod(SubsystemRegistration)]
        /// reset method. const, static readonly, compiler-generated (lambda caches / backing fields),
        /// and event backing fields are EXCLUDED — only genuinely mutable persistable state needs a reset.
        /// </summary>
        [Test]
        public void EveryMutableRuntimeStatic_HasASubsystemRegistrationReset()
        {
            var offenders = new List<string>();
            var auditedFields = new List<string>();

            foreach (var type in RuntimeAsm.GetTypes())
            {
                // Skip compiler-generated closure/display classes (lambda caches etc.).
                if (type.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() != null)
                    continue;

                var mutableStatics = type.GetFields(StaticFieldFlags)
                    .Where(IsMutablePersistableStatic)
                    .ToList();

                if (mutableStatics.Count == 0)
                    continue;

                foreach (var f in mutableStatics)
                    auditedFields.Add($"{type.FullName}.{f.Name} ({f.FieldType.Name})");

                if (!TypeHasSubsystemRegistrationReset(type))
                {
                    string fields = string.Join(", ", mutableStatics.Select(f => f.Name));
                    offenders.Add($"{type.FullName} has mutable static field(s) [{fields}] but NO " +
                                  "[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] reset");
                }
            }

            // Surface the audited set in the test log so the reset list is self-documenting + regenerable.
            UnityEngine.Debug.Log("[static-audit] mutable runtime statics under audit (" + auditedFields.Count + "): "
                                  + (auditedFields.Count == 0 ? "(none)" : string.Join(" | ", auditedFields)));

            Assert.IsEmpty(offenders,
                "Every mutable runtime static must have a SubsystemRegistration reset (Configurable Enter " +
                "Play Mode / domain-reload-disabled discipline, ticket 86ca9a39q). Offenders:\n  " +
                string.Join("\n  ", offenders));
        }

        /// <summary>
        /// The specific guard for the one field the audit currently finds: BuildInfo._stamp. Proves the
        /// reset method exists, is wired with SubsystemRegistration, and actually NULLS the cache (so a
        /// stale stamp can't survive a play-entry). If the audit above ever reports MORE statics, this
        /// stays as the concrete worked example of the discipline.
        /// </summary>
        [Test]
        public void BuildInfo_HasSubsystemRegistrationReset_ThatClearsTheStampCache()
        {
            Assert.IsTrue(TypeHasSubsystemRegistrationReset(typeof(BuildInfo)),
                "BuildInfo must carry a [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] reset");

            // Force the lazy cache to populate, then invoke the reset, then assert the cache is cleared.
            // (Reading Stamp loads Resources/BuildStamp.txt — or "unknown" headlessly; either way non-null.)
            string _ = BuildInfo.Stamp;
            var stampField = typeof(BuildInfo).GetField("_stamp", StaticFieldFlags);
            Assert.IsNotNull(stampField, "_stamp cache field must exist");
            Assert.IsNotNull(stampField.GetValue(null), "reading Stamp should have populated the cache");

            var reset = typeof(BuildInfo).GetMethods(StaticMethodFlags)
                .First(IsSubsystemRegistrationReset);
            reset.Invoke(null, null);

            Assert.IsNull(stampField.GetValue(null),
                "the SubsystemRegistration reset must NULL _stamp so the next play-entry re-reads BuildStamp.txt");
        }

        // === helpers ===

        private static bool IsMutablePersistableStatic(FieldInfo f)
        {
            if (f.IsLiteral) return false;                 // const — compile-time, never persists
            if (f.IsInitOnly) return false;                // static readonly — immutable after init
            // Compiler-generated backing storage (auto-prop backing fields, lambda caches, event fields):
            if (f.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() != null) return false;
            // Event backing fields are delegates Unity does not persist meaningfully across play-entries
            // in our codebase (the runtime audit found ZERO static events) — but guard defensively.
            if (typeof(System.Delegate).IsAssignableFrom(f.FieldType)) return false;
            return true;
        }

        private static bool TypeHasSubsystemRegistrationReset(Type t) =>
            t.GetMethods(StaticMethodFlags).Any(IsSubsystemRegistrationReset);

        private static bool IsSubsystemRegistrationReset(MethodInfo m)
        {
            var attr = m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            return attr != null && attr.loadType == RuntimeInitializeLoadType.SubsystemRegistration;
        }
    }
}
