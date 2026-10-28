using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace OdysseyShuttleVariants
{
    // Compatibility with "Instant Loot Shuttle" (Workshop 3646387656).
    //
    // That mod loads a shuttle in one click instead of through hauling jobs. It charges every selected
    // pawn's full body mass against the shuttle's capacity, in two separate places:
    //
    //   1. CalcSelectedAdditionalMass() - the up-front check, three copies of it. Returns a float, so
    //      we postfix it and take the crew/mech mass back off.
    //   2. InstantLoadSelected() - the actual load loop, which charges pawn mass inline against a
    //      running total shared with the items, and bails with "return false" the moment the total
    //      exceeds capacity. Pawns are processed first and are already in the container by then, so a
    //      bail there strands the cargo and leaves the crew aboard.
    //
    // On our craft crew is free - governed by the seat count, not the mass pool - so both charges
    // over-count here. Their arithmetic is right for vanilla; it is the meaning of pawn mass on these
    // craft that differs, so the correction lives on our side.
    //
    // (2) has no float to postfix and its loop is left unpatched. Instead we grant the transporter
    // exactly the capacity their loop is about to over-charge, for the duration of that one call, and
    // withdraw it immediately after. Their maths then works out untouched, with no assumption about
    // the order they process the selection in.
    //
    // Fully soft: with the mod absent - or its methods changed - nothing is patched and its behaviour
    // is left exactly as it is.
    [StaticConstructorOnStartup]
    internal static class InstantLootShuttleCompat
    {
        // Three copies of the up-front calculation live in the mod; patch whichever are present.
        private static readonly string[] CalcHolders =
        {
            "InstantLootShuttle.ShuttleLoadUtility",
            "InstantLootShuttle.ILS_SafeInstantLoadFromDialog",
            "InstantLootShuttle.Patch_DialogLoadTransporters_TryAccept",
        };

        // Extra capacity granted per transporter while an instant-load call is in flight. Empty at all
        // other times, which is also the fast path for the MassCapacity postfix.
        private static readonly Dictionary<CompTransporter, float> Allowance =
            new Dictionary<CompTransporter, float>();

        static InstantLootShuttleCompat()
        {
            Type loadType = AccessTools.TypeByName("InstantLootShuttle.ILS_SafeInstantLoadFromDialog");
            if (loadType == null) return; // mod not present - do nothing at all

            Harmony harmony = new Harmony("luke.odysseyshuttlevariants.instantlootshuttle");
            int calcPatched = 0;

            MethodInfo calcPostfix = AccessTools.Method(typeof(InstantLootShuttleCompat), nameof(ExcludeCrewMass));
            foreach (string typeName in CalcHolders)
            {
                Type type = AccessTools.TypeByName(typeName);
                if (type == null) continue;

                MethodInfo target = AccessTools.Method(type, "CalcSelectedAdditionalMass",
                    new[] { typeof(CompTransporter), typeof(List<TransferableOneWay>) });
                if (target == null) continue;

                try
                {
                    harmony.Patch(target, postfix: new HarmonyMethod(calcPostfix));
                    calcPatched++;
                }
                catch (Exception e)
                {
                    Log.Warning("[OSV] Instant Loot Shuttle compat: could not patch " + typeName + " - " + e.Message);
                }
            }

            // The load loop itself: grant the allowance around it, and inflate MassCapacity while it runs.
            bool loadPatched = false;
            MethodInfo loadTarget = AccessTools.Method(loadType, "InstantLoadSelected",
                new[] { typeof(List<CompTransporter>), typeof(List<TransferableOneWay>), typeof(Map) });
            MethodInfo capacityGetter = AccessTools.PropertyGetter(typeof(CompTransporter), "MassCapacity");

            if (loadTarget != null && capacityGetter != null)
            {
                try
                {
                    harmony.Patch(loadTarget,
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(InstantLootShuttleCompat), nameof(GrantAllowance))),
                        finalizer: new HarmonyMethod(AccessTools.Method(typeof(InstantLootShuttleCompat), nameof(ClearAllowance))));
                    harmony.Patch(capacityGetter,
                        postfix: new HarmonyMethod(AccessTools.Method(typeof(InstantLootShuttleCompat), nameof(InflateCapacity))));
                    loadPatched = true;
                }
                catch (Exception e)
                {
                    Log.Warning("[OSV] Instant Loot Shuttle compat: could not patch the load loop - " + e.Message);
                }
            }

            Log.Message("[OSV] Instant Loot Shuttle detected; crew mass excluded on " + calcPatched
                + " call site(s), load loop " + (loadPatched ? "guarded" : "NOT guarded - cargo may be left behind"));
        }

        // The crew/mech mass their code would charge for this selection: the portion not aboard yet,
        // counted the same way they count it, so we neither over- nor under-compensate.
        private static float SelectionCrewMass(CompTransporter transporter, List<TransferableOneWay> transferables)
        {
            if (transferables == null) return 0f;
            if (transporter?.parent?.TryGetComp<CompTypedShuttleCapacity>() == null) return 0f;

            ThingOwner container = transporter.innerContainer;
            if (container == null) return 0f;

            HashSet<int> aboard = new HashSet<int>();
            for (int i = 0; i < container.Count; i++)
            {
                if (container[i] is Pawn p) aboard.Add(p.thingIDNumber);
            }

            float total = 0f;
            for (int i = 0; i < transferables.Count; i++)
            {
                TransferableOneWay tr = transferables[i];
                if (tr == null || tr.CountToTransfer <= 0) continue;
                if (!(tr.AnyThing is Pawn rep)) continue;
                if (!MassSplit.IsExcludedFromCargo(rep)) continue;

                int already = 0;
                List<Thing> things = tr.things;
                if (things != null)
                {
                    for (int j = 0; j < things.Count; j++)
                    {
                        if (things[j] is Pawn p2 && aboard.Contains(p2.thingIDNumber)) already++;
                    }
                }

                int additional = Mathf.Max(0, tr.CountToTransfer - already);
                if (additional > 0) total += rep.GetStatValue(StatDefOf.Mass) * additional;
            }
            return total;
        }

        // (1) Up-front check: drop the crew mass out of the projected total.
        public static void ExcludeCrewMass(CompTransporter transporter, List<TransferableOneWay> transferables,
            ref float __result)
        {
            if (__result <= 0f) return;
            __result = MassSplit.Deduct(__result, SelectionCrewMass(transporter, transferables));
        }

        // (2) Load loop: hand each of our transporters the capacity its crew is about to consume.
        public static void GrantAllowance(List<CompTransporter> transporters, List<TransferableOneWay> transferables)
        {
            Allowance.Clear();
            if (transporters == null) return;

            for (int i = 0; i < transporters.Count; i++)
            {
                CompTransporter t = transporters[i];
                if (t == null) continue;
                float crew = SelectionCrewMass(t, transferables);
                if (crew > 0f) Allowance[t] = crew;
            }
        }

        // Finalizer, so the allowance is withdrawn even if their loop throws.
        public static void ClearAllowance()
        {
            Allowance.Clear();
        }

        public static void InflateCapacity(CompTransporter __instance, ref float __result)
        {
            if (Allowance.Count == 0) return;
            if (__instance != null && Allowance.TryGetValue(__instance, out float extra)) __result += extra;
        }
    }
}
