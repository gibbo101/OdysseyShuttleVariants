using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace OdysseyShuttleVariants
{
    // When no cell on the destination map can fit the hull, vanilla DropShuttle logs
    // "Could not find a suitable cell for shuttle landing." and lands the craft at "near" regardless.
    // Vanilla's only player shuttle is 3x5, so it effectively never reaches that branch; our Cargo
    // (4x6) and Heavy Cargo (6x10) craft reach it routinely on the small space maps - asteroids and
    // orbital platforms - where the craft ends up dumped somewhere it does not fit.
    //
    // For our craft we take that branch out of play: park the shuttle, with its cargo and crew intact,
    // as a landed world object on the destination tile - the same holder the transport drone uses -
    // and let the player recall it or pick another destination.
    internal static class LandingGuard
    {
        // Exhaustive, Rand-free sweep. Deterministic (so it is multiplayer-safe) and it consumes no
        // random draws, leaving untouched the sequence vanilla itself uses further down DropShuttle.
        // We only pre-empt vanilla when the answer is provably "nowhere", which is precisely the case
        // vanilla would have failed at too - so a craft that could have landed still lands normally.
        public static bool AnyLandingCell(Map map, IntVec3 near, ThingDef shipThing, Rot4 rot)
        {
            if (near.IsValid && Fits(map, near, shipThing, rot)) return true;

            // Near first, mirroring vanilla's radius-20 search, then the whole map.
            if (near.IsValid)
            {
                foreach (IntVec3 c in GenRadial.RadialCellsAround(near, 20f, true))
                {
                    if (c.InBounds(map) && Fits(map, c, shipThing, rot)) return true;
                }
            }

            foreach (IntVec3 c in map.AllCells)
            {
                if (Fits(map, c, shipThing, rot)) return true;
            }
            return false;
        }

        private static bool Fits(Map map, IntVec3 c, ThingDef shipThing, Rot4 rot)
        {
            return (bool)RoyalTitlePermitWorker_CallShuttle.ShuttleCanLandHere(c, map, shipThing, rot);
        }
    }

    [HarmonyPatch(typeof(TransportersArrivalActionUtility), nameof(TransportersArrivalActionUtility.DropShuttle))]
    public static class Patch_DropShuttle_NoLandingCell
    {
        public static bool Prefix(ActiveTransporterInfo transporter, Map map, IntVec3 near, Rot4? rotation,
            ref Thing __result)
        {
            if (transporter == null || map == null) return true;

            // Only our craft. Quest shuttles, drop pods and the vanilla shuttle keep vanilla behaviour.
            Thing shuttle = transporter.GetShuttle();
            if (shuttle == null || shuttle.TryGetComp<CompTypedShuttleCapacity>() == null) return true;

            Rot4 rot = rotation ?? shuttle.def.defaultPlacingRot;
            if (LandingGuard.AnyLandingCell(map, near, shuttle.def, rot)) return true;

            // Nothing on this map fits. Vanilla would dump the craft at "near"; park it instead.
            // Mirrors the first thing vanilla DropShuttle does, so world pawns aren't double-tracked.
            TransportersArrivalActionUtility.RemovePawnsFromWorldPawns(Gen.YieldSingle(transporter));

            WorldObject_LandedDrone landed = WorldObject_LandedDrone.ParkAt(map.Tile, transporter);

            Messages.Message(
                shuttle.LabelCap + " could not land: no clear ground large enough for it. "
                + "It is holding position with its cargo aboard - recall it or send it elsewhere.",
                landed, MessageTypeDefOf.NegativeEvent);

            __result = null;
            return false;
        }
    }
}
