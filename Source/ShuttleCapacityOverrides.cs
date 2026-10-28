using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OdysseyShuttleVariants
{
    // Applies the settings' per-shuttle overrides to the defs, once, at startup.
    //
    // Comps are built when a Thing is constructed - including when one is read back out of a save -
    // so injecting here, before any save is loaded, reaches both newly built craft and ones already
    // sitting in an existing colony. The flip side is that changing a setting mid-session does
    // nothing until the game is restarted, which the settings page says plainly.
    [StaticConstructorOnStartup]
    internal static class ShuttleCapacityOverrides
    {
        private static List<ThingDef> cachedEligible;

        // Craft that ship with typed capacity in their own defs - our five, plus the vanilla
        // passenger shuttle via Patches/. Captured before any override is applied, because applying
        // one adds the same comp and would otherwise make an overridden craft look built-in.
        public static readonly HashSet<string> NativeTypedCapacity = new HashSet<string>();

        // What each craft ships with, captured before any override touches it. Applying an override
        // writes straight onto the def's shared CompProperties, so this snapshot is the only record
        // of the original values that "Reset" restores.
        public sealed class ShippedValues
        {
            public int maxColonists;
            public int mechBandwidth;
            public bool requireMechanitor;
            public bool noPawns;
            public float massCapacity;
            public bool psychic;

            // The def's values as they stand, with the vanilla defaults for a craft lacking the comps.
            public static ShippedValues ReadFrom(ThingDef def)
            {
                CompProperties_TypedShuttleCapacity caps =
                    def.GetCompProperties<CompProperties_TypedShuttleCapacity>();
                CompProperties_Transporter transporter = def.GetCompProperties<CompProperties_Transporter>();
                return new ShippedValues
                {
                    maxColonists = caps?.maxColonists ?? -1,
                    mechBandwidth = caps?.mechBandwidthCapacity ?? 0,
                    requireMechanitor = caps?.requireMechanitor ?? false,
                    noPawns = caps?.noPawns ?? false,
                    massCapacity = transporter?.massCapacity ?? -1f,
                    psychic = def.GetCompProperties<CompProperties_PsychicResonanceField>() != null,
                };
            }
        }

        public static readonly Dictionary<string, ShippedValues> Shipped = new Dictionary<string, ShippedValues>();

        // The psycaster shuttle, whose meditation focus, resonance field and focus strength are the
        // psychic fit-out given to other craft. Null when Royalty is not active.
        public static ThingDef PsychicTemplate => DefDatabase<ThingDef>.GetNamedSilentFail("OSV_PsycasterShuttle");

        public static bool HasPsychicFitOut(ThingDef def, ShuttleOverride ov)
        {
            if (ov.psychic != PsychicFitOut.AsShipped) return ov.psychic == PsychicFitOut.On;
            return Shipped.TryGetValue(def.defName, out ShippedValues shipped)
                ? shipped.psychic
                : def.GetCompProperties<CompProperties_PsychicResonanceField>() != null;
        }

        static ShuttleCapacityOverrides()
        {
            foreach (ThingDef def in EligibleShuttles())
            {
                if (def.GetCompProperties<CompProperties_TypedShuttleCapacity>() != null)
                {
                    NativeTypedCapacity.Add(def.defName);
                }
                Shipped[def.defName] = ShippedValues.ReadFrom(def);
            }

            Apply();
        }

        // Player shuttles only. A craft qualifies when some TransportShipDef flags it as a player
        // shuttle - which is what vanilla itself uses to tell a buildable craft from a quest or
        // mechanoid one - so imperial shuttles, drop pods and NPC craft never appear.
        public static List<ThingDef> EligibleShuttles()
        {
            if (cachedEligible != null) return cachedEligible;

            HashSet<ThingDef> playerShips = new HashSet<ThingDef>();
            foreach (TransportShipDef shipDef in DefDatabase<TransportShipDef>.AllDefs)
            {
                if (shipDef.playerShuttle && shipDef.shipThing != null) playerShips.Add(shipDef.shipThing);
            }

            cachedEligible = playerShips
                .Where(d => d.HasAssignableCompFrom(typeof(CompTransporter)))
                .OrderBy(d => d.label ?? d.defName)
                .ToList();

            return cachedEligible;
        }

        private static void Apply()
        {
            OSVSettings settings = OSVMod.Settings;
            if (settings == null || settings.overrides.Count == 0) return;

            int applied = 0;
            int reverted = 0;
            int psychic = 0;
            ThingDef template = PsychicTemplate;
            foreach (ThingDef def in EligibleShuttles())
            {
                // Never opened in the settings page: leave the def exactly as it ships.
                if (!settings.overrides.TryGetValue(def.defName, out ShuttleOverride ov)) continue;

                if (template != null && ApplyPsychicFitOut(def, HasPsychicFitOut(def, ov), template)) psychic++;

                if (!ov.enabled)
                {
                    // Deliberately switched off. For a craft that ships with typed capacity that
                    // means stripping it, so the craft falls back to the vanilla single mass pool.
                    if (def.comps != null &&
                        def.comps.RemoveAll(c => c is CompProperties_TypedShuttleCapacity) > 0)
                    {
                        reverted++;
                    }
                    continue;
                }

                CompProperties_TypedShuttleCapacity props =
                    def.GetCompProperties<CompProperties_TypedShuttleCapacity>();
                if (props == null)
                {
                    props = new CompProperties_TypedShuttleCapacity();
                    if (def.comps == null) def.comps = new List<CompProperties>();
                    def.comps.Add(props);
                }

                props.maxColonists = ov.maxColonists;
                props.mechBandwidthCapacity = ov.mechBandwidth;
                props.requireMechanitor = ov.requireMechanitor;
                props.noPawns = ov.noPawns;

                if (ov.massCapacity >= 0f)
                {
                    CompProperties_Transporter transporter = def.GetCompProperties<CompProperties_Transporter>();
                    if (transporter != null) transporter.massCapacity = ov.massCapacity;
                }

                applied++;
            }

            if (applied > 0 || reverted > 0 || psychic > 0)
            {
                // Two clients logging different hashes are running different capacities and will desync.
                Log.Message("[OSV] Typed capacity: " + applied + " shuttle def(s) configured, "
                    + reverted + " reverted to the vanilla mass limit, " + psychic
                    + " with the psycaster fit-out. Config hash: " + settings.ConfigHash());
            }
        }

        // Gives the def the template's meditation focus, resonance field and focus strength, or takes
        // them away. A craft keeps any meditation focus of its own. Returns whether the def ends up
        // carrying the fit-out.
        private static bool ApplyPsychicFitOut(ThingDef def, bool wanted, ThingDef template)
        {
            CompProperties_PsychicResonanceField field = template.GetCompProperties<CompProperties_PsychicResonanceField>();
            CompProperties_MeditationFocus focus = template.GetCompProperties<CompProperties_MeditationFocus>();
            StatDef strength = StatDefOf.MeditationFocusStrength;
            bool has = def.GetCompProperties<CompProperties_PsychicResonanceField>() != null;

            if (wanted && !has)
            {
                if (def.comps == null) def.comps = new List<CompProperties>();
                if (def.GetCompProperties<CompProperties_MeditationFocus>() == null) def.comps.Add(focus);
                def.comps.Add(field);
                StatUtility.SetStatValueInList(ref def.statBases, strength, template.GetStatValueAbstract(strength));
            }
            else if (!wanted && has)
            {
                def.comps.RemoveAll(c => c is CompProperties_PsychicResonanceField || c is CompProperties_MeditationFocus);
                def.statBases?.RemoveAll(s => s.stat == strength);
            }
            return wanted;
        }
    }
}
