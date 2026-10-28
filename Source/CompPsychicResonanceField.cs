using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace OdysseyShuttleVariants
{
    public class CompProperties_PsychicResonanceField : CompProperties
    {
        // Cells from the shuttle's centre that the field reaches.
        public float radius = 12.9f;

        // Chemfuel burned per day while the field is on.
        public float fuelPerDay = 10f;

        // Applied to psycasters inside the field and kept topped up while they stay; it lapses
        // shortly after they leave or the field stops.
        public HediffDef hediff;

        public CompProperties_PsychicResonanceField()
        {
            compClass = typeof(CompPsychicResonanceField);
        }

        public override void DrawGhost(IntVec3 center, Rot4 rot, ThingDef thingDef, Color ghostCol,
            AltitudeLayer drawAltitude, Thing thing = null)
        {
            GenDraw.DrawRadiusRing(center, radius);
        }
    }

    // A landed psycaster shuttle's resonance field: while switched on and fuelled, the shuttle's own
    // faction's psycasters within the radius carry the field's hediff, which speeds their neural
    // heat recovery. Runs in the simulation tick, so every Multiplayer client applies it alike.
    public class CompPsychicResonanceField : CompShuttleFuelledService
    {
        public CompProperties_PsychicResonanceField Props => (CompProperties_PsychicResonanceField)props;

        protected override string EnabledSaveKey => "OSV_resonanceFieldEnabled";

        public bool Active => Enabled && parent.Spawned && Fuel != null && Fuel.HasFuel;

        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            if (!parent.IsHashIntervalTick(GenTicks.TickRareInterval, delta) || !Active) return;

            Fuel.ConsumeFuel(Props.fuelPerDay * GenTicks.TickRareInterval / GenDate.TicksPerDay);
            if (parent.Faction == null || Props.hediff == null) return;

            IReadOnlyList<Pawn> pawns = parent.Map.mapPawns.SpawnedPawnsInFaction(parent.Faction);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.HasPsylink && pawn.Position.InHorDistOf(parent.Position, Props.radius))
                {
                    Resonate(pawn);
                }
            }
        }

        private void Resonate(Pawn pawn)
        {
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediff)
                ?? pawn.health.AddHediff(Props.hediff);
            hediff.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
            if (!parent.Spawned) yield break;

            yield return ToggleGizmo("Resonance field",
                "Project a psychic resonance field around this landed shuttle. Your psycasters inside it "
                + "shed neural heat faster. The field burns " + Props.fuelPerDay.ToString("0")
                + " chemfuel a day from the fuel the shuttle needs to fly home.",
                FieldIcon);
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            if (parent.Spawned) GenDraw.DrawRadiusRing(parent.Position, Props.radius);
        }

        public override string CompInspectStringExtra()
        {
            return parent.Spawned ? StatusLine("Resonance field") : null;
        }

        // Royalty's neural-heat-dump psycast icon, looked up when the gizmo is drawn (ContentFinder
        // caches it): the comp only exists on Royalty craft, and a startup load would log a missing
        // texture for players without Royalty.
        private static Texture2D FieldIcon => ContentFinder<Texture2D>.Get("UI/Abilities/EntropyDump");
    }
}
