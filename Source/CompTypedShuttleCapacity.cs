using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace OdysseyShuttleVariants
{
    // Typed-capacity layer for the mod's buildable shuttles: a crew-seat cap, a mech bandwidth
    // budget, an autonomous (no-pawn) drone mode, and the inspect line reporting them. These
    // fields hold the per-def limits; the load-time and launch-time enforcement lives in
    // HarmonyPatches.
    public class CompProperties_TypedShuttleCapacity : CompProperties
    {
        // Max colonist passengers (seats). -1 leaves the vanilla default (unlimited, mass-gated).
        public int maxColonists = -1;

        // Mech "slots" as a bandwidth budget: sum of each aboard mech's vanilla BandwidthCost must
        // stay <= this. 0 = not a mech carrier.
        public int mechBandwidthCapacity = 0;

        // The single allowed passenger must be a mechanitor.
        public bool requireMechanitor = false;

        // Autonomous craft: refuse ALL pawns/animals - items only.
        public bool noPawns = false;

        public CompProperties_TypedShuttleCapacity()
        {
            compClass = typeof(CompTypedShuttleCapacity);
        }

        // The architect menu builds its info card from the DEF, which never touches a live comp - so
        // the stats have to be produced here as well as on the comp itself, or a craft shows nothing
        // useful until it has actually been built.
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            ThingDef def = req.Def as ThingDef;
            if (def == null) yield break;

            // There is no live comp here, but the mech-bay research is global game state, so the
            // build card can still show what a craft would actually be worth right now. Outside a
            // running game MechBandwidthBonus returns 0 and this falls back to the base figure.
            int bonus = CompTypedShuttleCapacity.MechBandwidthBonus();
            int effective = mechBandwidthCapacity > 0 ? mechBandwidthCapacity + bonus : mechBandwidthCapacity;

            foreach (StatDrawEntry e in ShuttleStats.BuildSafe(def, this, effective, bonus))
            {
                yield return e;
            }
        }
    }

    // Cargo mass and range come from vanilla's transporter and launchable comps, neither of which
    // reports anything to the info card - so a shuttle's build card shows no capacity, no range and
    // no seats. We fill that in for any craft carrying typed capacity, and use the same list for the
    // built craft's own info card so the two always agree.
    internal static class ShuttleStats
    {
        // A stat provider must never be able to break the info card. If ours throws, RimWorld's
        // gather aborts with the entry list already assigned but its parallel value list left short,
        // so StatsReportUtility.DrawStatsWorker then indexes past the end on EVERY later frame - and
        // the card stays blank for every thing in the game, not just ours, until the dialog resets.
        // Collect into a list behind a catch so a fault here costs a few stat lines and nothing else.
        public static List<StatDrawEntry> BuildSafe(ThingDef def, CompProperties_TypedShuttleCapacity caps,
            int effectiveBandwidth, int bandwidthBonus)
        {
            List<StatDrawEntry> entries = new List<StatDrawEntry>();
            if (def == null) return entries;

            try
            {
                entries.AddRange(Build(def, caps, effectiveBandwidth, bandwidthBonus));
            }
            catch (System.Exception e)
            {
                entries.Clear();
                Log.ErrorOnce("[OSV] Failed building shuttle stats for " + def.defName + ": " + e, 0x05F1A11E);
            }
            return entries;
        }

        private static IEnumerable<StatDrawEntry> Build(ThingDef def, CompProperties_TypedShuttleCapacity caps,
            int effectiveBandwidth, int bandwidthBonus)
        {
            CompProperties_Transporter transporter = def.GetCompProperties<CompProperties_Transporter>();
            if (transporter != null)
            {
                yield return new StatDrawEntry(StatCategoryDefOf.Building, "Cargo capacity",
                    transporter.massCapacity.ToString("F0") + " kg",
                    "How much cargo this craft can carry. Crew and mechs are counted separately and do "
                    + "not use up this allowance.", 4907);
            }

            int tiles = MaxRangeTiles(def);
            if (tiles > 0)
            {
                yield return new StatDrawEntry(StatCategoryDefOf.Building, "Maximum range",
                    tiles + " tiles",
                    "How far this craft can fly on a full fuel load, one way.", 4906);
            }

            if (caps == null) yield break;

            if (caps.noPawns)
            {
                yield return new StatDrawEntry(StatCategoryDefOf.Building, "Passenger capacity",
                    "None (autonomous)",
                    "This craft is autonomous and carries no crew - it launches and returns on its own.", 4905);
            }
            else if (caps.requireMechanitor)
            {
                yield return new StatDrawEntry(StatCategoryDefOf.Building, "Crew capacity",
                    caps.maxColonists + " (mechanitor only)",
                    "Only a mechanitor may pilot this craft.", 4905);
            }
            else if (caps.maxColonists >= 1)
            {
                yield return new StatDrawEntry(StatCategoryDefOf.Building, "Passenger capacity",
                    caps.maxColonists.ToString(),
                    "The maximum number of colonist passengers this shuttle can carry.", 4905);
            }

            if (caps.mechBandwidthCapacity > 0)
            {
                string val = effectiveBandwidth + " bandwidth";
                if (bandwidthBonus > 0)
                {
                    val += " (base " + caps.mechBandwidthCapacity + " + " + bandwidthBonus + " researched)";
                }
                yield return new StatDrawEntry(StatCategoryDefOf.Building, "Mech bay capacity",
                    val,
                    "Mechs are carried against a bandwidth budget - each mech's bandwidth cost "
                    + "(e.g. cleansweeper 1, tunneler 3). The total carried must not exceed this. "
                    + "Mech-bay expansion research raises this budget.", 4904);
            }
            else
            {
                // Stated rather than omitted, so craft can be compared from the architect menu
                // without building one of each. "None" rather than 0 - a zero reads like a mech bay
                // that is broken or not yet researched.
                yield return new StatDrawEntry(StatCategoryDefOf.Building, "Mech bay capacity",
                    "None",
                    "This craft has no mech bay and cannot carry mechanoids.", 4904);
            }
        }

        // A fixed cap if the craft declares one, otherwise whatever a full tank buys.
        private static int MaxRangeTiles(ThingDef def)
        {
            CompProperties_Launchable launchable = def.GetCompProperties<CompProperties_Launchable>();
            if (launchable == null) return -1;
            if (launchable.fixedLaunchDistanceMax >= 0) return launchable.fixedLaunchDistanceMax;

            CompProperties_Refuelable fuel = def.GetCompProperties<CompProperties_Refuelable>();
            if (fuel == null || launchable.fuelPerTile <= 0f) return -1;
            return Mathf.FloorToInt(fuel.fuelCapacity / launchable.fuelPerTile);
        }
    }

    // DefOf for the mech-bay expansion research (Biotech-only). Each finished tier raises the
    // mechanitor shuttle's carried-mech bandwidth budget - see CompTypedShuttleCapacity.EffectiveMechBandwidth.
    [DefOf]
    public static class OSV_ResearchDefOf
    {
        [MayRequireBiotech] public static ResearchProjectDef OSV_MechBayExpansion1;
        [MayRequireBiotech] public static ResearchProjectDef OSV_MechBayExpansion2;

        static OSV_ResearchDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(OSV_ResearchDefOf));
        }
    }

    public class CompTypedShuttleCapacity : ThingComp
    {
        public CompProperties_TypedShuttleCapacity Props => (CompProperties_TypedShuttleCapacity)props;

        // Mech bandwidth budget as actually enforced: the def's base plus any completed mech-bay
        // expansion research (+3 per tier). Research is per-save/global, so every mech shuttle shares
        // the same upgraded budget. Returns the base (0) unchanged for non-mech craft. All cap reads
        // (inspect line, info card, load-dialog readout + accept check, enter-job budget) use this.
        public int EffectiveMechBandwidth
        {
            get
            {
                int cap = Props.mechBandwidthCapacity;
                return cap > 0 ? cap + MechBandwidthBonus() : cap;
            }
        }

        // +6 bandwidth per finished mech-bay expansion tier.
        public static int MechBandwidthBonus() => MechBayExpansionTiers() * 6;

        // Number of finished mech-bay expansion tiers (0-2). Also drives how many mechs can charge at a
        // landed mech shuttle at once (see CompMechChargerShuttle.EffectiveMaxSimultaneous).
        // Guarded against a null game context: ResearchProjectDef.IsFinished reads Find.ResearchManager,
        // which is null when this comp's stats are shown outside a live game (e.g. the architect/def
        // info card) - without this guard that NREs the whole info-card stats list.
        public static int MechBayExpansionTiers()
        {
            if (Current.Game == null) return 0;
            int tiers = 0;
            ResearchProjectDef r1 = OSV_ResearchDefOf.OSV_MechBayExpansion1;
            ResearchProjectDef r2 = OSV_ResearchDefOf.OSV_MechBayExpansion2;
            if (r1 != null && r1.IsFinished) tiers++;
            if (r2 != null && r2.IsFinished) tiers++;
            return tiers;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // Caps are scribed by CompShuttle, so we only set them once, on first spawn. This comp
            // is only ever on our own buildable craft (never quest shuttles), so no faction guard.
            if (!respawningAfterLoad)
            {
                CompShuttle shuttle = parent.TryGetComp<CompShuttle>();
                if (shuttle != null)
                {
                    // Runs after CompShuttle.PostSpawnSetup (listed after CompProperties_Shuttle in
                    // the def), which force-sets accept* true for player shuttles - so our values win.
                    if (Props.noPawns)
                    {
                        shuttle.acceptColonists = false;
                        shuttle.acceptChildren = false;
                        shuttle.acceptColonyPrisoners = false;
                        shuttle.allowSlaves = false;
                    }
                    else if (Props.maxColonists >= 1)
                    {
                        shuttle.maxColonistCount = Props.maxColonists;
                    }
                }

                // Make the launch cooldown count from LANDING, not launch. Vanilla stamps
                // CompLaunchable.lastLaunchTick at launch, so a long flight burns the cooldown mid-air
                // (the "ready to launch again" message can fire before the shuttle even lands). A
                // shuttle spawning fresh (not a save reload) with lastLaunchTick > 0 is landing from a
                // flight - not freshly constructed (that's -1) - so restart the cooldown from now.
                CompLaunchable launchable = parent.TryGetComp<CompLaunchable>();
                if (launchable != null && launchable.lastLaunchTick > 0)
                {
                    launchable.lastLaunchTick = Find.TickManager.TicksGame;
                }
            }

            EnsureLaunchReady();
        }

        // Shuttles are Normal tickers, which never receive CompTickRare.
        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            if (parent.IsHashIntervalTick(GenTicks.TickRareInterval, delta))
            {
                EnsureLaunchReady();
            }
        }

        // The autonomous drone has no crew/cargo to "load", but vanilla blocks launch until a load
        // group is initiated (groupID >= 0 -> LoadingInProgressOrReadyToLaunch). Keep an empty group
        // assigned so the drone is always ready to take off, with or without cargo. (Self-heals if a
        // load is cancelled.) InitiateLoading only assigns a groupID - it creates no hauling lord.
        private void EnsureLaunchReady()
        {
            if (!Props.noPawns || !parent.Spawned) return;
            CompTransporter transporter = parent.TryGetComp<CompTransporter>();
            if (transporter != null && transporter.groupID < 0)
            {
                TransporterUtility.InitiateLoading(Gen.YieldSingle(transporter));
            }
        }

        private int LoadedColonists()
        {
            int n = 0;
            CompTransporter transporter = parent.TryGetComp<CompTransporter>();
            if (transporter != null)
            {
                foreach (Thing t in transporter.innerContainer)
                {
                    if (t is Pawn p && p.IsColonist) n++;
                }
            }
            return n;
        }

        private int LoadedMechBandwidth()
        {
            float total = 0f;
            CompTransporter transporter = parent.TryGetComp<CompTransporter>();
            if (transporter != null)
            {
                foreach (Thing t in transporter.innerContainer)
                {
                    if (t is Pawn p && p.RaceProps.IsMechanoid) total += p.GetStatValue(StatDefOf.BandwidthCost);
                }
            }
            return Mathf.RoundToInt(total);
        }

        public override string CompInspectStringExtra()
        {
            if (Props.noPawns)
            {
                return "Crew: none (autonomous)";
            }

            List<string> lines = new List<string>();

            if (Props.requireMechanitor)
            {
                lines.Add("Crew (mechanitor): " + LoadedColonists() + " / " + Props.maxColonists);
            }
            else if (Props.maxColonists >= 1)
            {
                lines.Add("Passengers: " + LoadedColonists() + " / " + Props.maxColonists);
            }

            if (Props.mechBandwidthCapacity > 0)
            {
                lines.Add("Mechs: " + LoadedMechBandwidth() + " / " + EffectiveMechBandwidth + " bandwidth");
            }

            return lines.Count > 0 ? string.Join("\n", lines.ToArray()) : null;
        }

        // No SpecialDisplayStats override here on purpose. A built thing's info card gathers from
        // BOTH thing.def.SpecialDisplayStats (which walks def.comps and reaches our CompProperties
        // version) AND ThingWithComps.SpecialDisplayStats (which reaches the comp instance), so
        // implementing it in both places listed every stat twice. The CompProperties version reads
        // the mech-bay research from global game state, so it is correct for built craft as well as
        // for the architect menu's build preview.
    }
}
