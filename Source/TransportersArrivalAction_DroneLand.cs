using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace OdysseyShuttleVariants
{
    [DefOf]
    public static class OSV_WorldObjectDefOf
    {
        public static WorldObjectDef OSV_LandedDrone;
        public static WorldObjectDef OSV_DroneCamp;

        static OSV_WorldObjectDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(OSV_WorldObjectDefOf));
        }
    }

    // Arrival action for the autonomous drone landing on a tile with no map. Instead of vanilla's
    // "no colonist -> contents lost", it drops the shuttle + cargo into a persistent WorldObject_LandedDrone
    // that lives on the world map (so it survives abandoning the origin colony map) and can be recalled
    // or sent onward later. Injected into the launch float-menu by Patch_CompLaunchable_FloatMenu.
    public class TransportersArrivalAction_DroneLand : TransportersArrivalAction
    {
        public override bool GeneratesMap => false;

        public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, PlanetTile destinationTile)
        {
            return !Find.World.Impassable(destinationTile);
        }

        public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            if (transporters.Count == 0) return;

            ActiveTransporterInfo info = DroneArrivalUtility.MergeIntoFirst(transporters);
            WorldObject_LandedDrone landed = WorldObject_LandedDrone.ParkAt(tile, info);

            Messages.Message("The transport drone has landed and is holding its cargo.", landed, MessageTypeDefOf.TaskCompletion);
        }
    }

    internal static class DroneArrivalUtility
    {
        // Shuttles are max1PerGroup, so an arriving group is a single transporter; any others are
        // merged into the first, which is returned.
        public static ActiveTransporterInfo MergeIntoFirst(List<ActiveTransporterInfo> transporters)
        {
            ActiveTransporterInfo first = transporters[0];
            for (int i = transporters.Count - 1; i >= 1; i--)
            {
                transporters[i].innerContainer.TryTransferAllToContainer(first.innerContainer, canMergeWithExistingStacks: true);
            }
            return first;
        }
    }

    // Drone arriving at a (non-hostile) settlement: gift its CARGO to that faction for goodwill, but keep
    // the drone itself - it re-lands as a persistent WorldObject_LandedDrone at the tile, empty and free to
    // move again. (Vanilla TransportersArrivalAction_GiveGift would gift the shuttle too, losing the drone.)
    public class TransportersArrivalAction_DroneGift : TransportersArrivalAction
    {
        private Settlement settlement;

        public TransportersArrivalAction_DroneGift() { }

        public TransportersArrivalAction_DroneGift(Settlement settlement)
        {
            this.settlement = settlement;
        }

        public override bool GeneratesMap => false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref settlement, "settlement");
        }

        public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, PlanetTile destinationTile)
        {
            if (settlement == null || !settlement.Spawned || settlement.Tile != destinationTile) return false;
            if (settlement.Faction == null || settlement.Faction.IsPlayer || settlement.Faction.HostileTo(Faction.OfPlayer)) return false;
            return true;
        }

        public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            if (transporters.Count == 0) return;
            ActiveTransporterInfo info = DroneArrivalUtility.MergeIntoFirst(transporters);

            // Pull the shuttle out so the gift covers only the cargo, then gift + put the shuttle back.
            Thing shuttle = info.RemoveShuttle();
            FactionGiftUtility.GiveGift(transporters, settlement);
            if (shuttle != null) info.SetShuttle(shuttle);

            // Drone persists (now empty) at the settlement tile, free to be sent on again.
            WorldObject_LandedDrone landed = WorldObject_LandedDrone.ParkAt(tile, info);

            Messages.Message("The transport drone delivered its cargo as a gift and is holding at the settlement.",
                landed, MessageTypeDefOf.TaskCompletion);
        }
    }
}
