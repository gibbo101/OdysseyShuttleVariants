using RimWorld;
using UnityEngine;
using Verse;

namespace OdysseyShuttleVariants
{
    // A service a landed shuttle provides from its own chemfuel tank (mech charging, the resonance
    // field). Off by default: running it spends the fuel the shuttle needs to fly home, so the
    // player opts in. The on/off state is saved and switched through SetEnabled, which is synced
    // under Multiplayer so every client switches together.
    public abstract class CompShuttleFuelledService : ThingComp
    {
        private bool enabled;

        private CompRefuelable fuelComp;

        public bool Enabled => enabled;

        public CompRefuelable Fuel => fuelComp ??= parent.GetComp<CompRefuelable>();

        // The key the on/off state is saved under; fixed per service once saves carry it.
        protected abstract string EnabledSaveKey { get; }

        public void SetEnabled(bool value)
        {
            enabled = value;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref enabled, EnabledSaveKey, defaultValue: false);
        }

        // The on/off switch for the shuttle's command bar.
        protected Command_Toggle ToggleGizmo(string label, string description, Texture2D icon)
        {
            return new Command_Toggle
            {
                defaultLabel = label,
                defaultDesc = description,
                icon = icon,
                isActive = () => enabled,
                toggleAction = () => SetEnabled(!enabled),
            };
        }

        // "<label>: on" or "<label>: off", with any detail appended while on, and a note when the
        // tank is dry.
        protected string StatusLine(string label, string detailWhileOn = null)
        {
            if (!enabled) return label + ": off";
            string line = label + ": on" + detailWhileOn;
            if (Fuel != null && !Fuel.HasFuel) line += " - out of chemfuel";
            return line;
        }
    }
}
