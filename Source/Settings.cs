using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace OdysseyShuttleVariants
{
    // Per-shuttle typed-capacity overrides, so the crew/mech/cargo split can be applied to any
    // player shuttle - including craft from other mods - instead of only our five hulls.
    //
    // The tick means "this craft uses the crew/mech/cargo split", not "this craft has been customised".
    // Our own hulls and the patched passenger shuttle therefore start ticked, because they already do;
    // craft from other mods start unticked. Un-ticking one of ours is a real choice - it strips typed
    // capacity and hands the craft back to the vanilla single mass pool. A craft never opened in the
    // settings page is untouched entirely. We deliberately do NOT
    // derive defaults from a craft's size or capacity: our own fleet shows there is no relationship
    // to derive from - the Troop and Passenger shuttles share a 3x5 hull but carry 12 seats/100kg and
    // 4 seats/250kg respectively. Seats and cargo encode a design intent, not a property of the hull,
    // so guessing them for someone else's craft would silently rebalance their mod.
    public class ShuttleOverride : IExposable
    {
        public bool enabled;
        public int maxColonists = -1;      // -1 = unlimited (vanilla, mass-gated)
        public float massCapacity = -1f;   // -1 = leave the def's own value alone
        public int mechBandwidth;
        public bool requireMechanitor;
        public bool noPawns;

        // Seeded from the craft as it already is, so a freshly-created entry is a no-op.
        public void PrefillFrom(ThingDef def)
        {
            CompProperties_TypedShuttleCapacity caps =
                def.GetCompProperties<CompProperties_TypedShuttleCapacity>();
            CompProperties_Transporter transporter = def.GetCompProperties<CompProperties_Transporter>();

            maxColonists = caps?.maxColonists ?? -1;
            mechBandwidth = caps?.mechBandwidthCapacity ?? 0;
            requireMechanitor = caps?.requireMechanitor ?? false;
            noPawns = caps?.noPawns ?? false;
            massCapacity = transporter?.massCapacity ?? -1f;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled");
            Scribe_Values.Look(ref maxColonists, "maxColonists", -1);
            Scribe_Values.Look(ref massCapacity, "massCapacity", -1f);
            Scribe_Values.Look(ref mechBandwidth, "mechBandwidth");
            Scribe_Values.Look(ref requireMechanitor, "requireMechanitor");
            Scribe_Values.Look(ref noPawns, "noPawns");
        }
    }

    public class OSVSettings : ModSettings
    {
        // Bumped whenever the meaning of what is stored changes; anything older is discarded rather
        // than reinterpreted. See ExposeData.
        private const int CurrentVersion = 2;

        public Dictionary<string, ShuttleOverride> overrides = new Dictionary<string, ShuttleOverride>();
        private int settingsVersion;

        private List<string> keyBuffer;
        private List<ShuttleOverride> valueBuffer;

        public ShuttleOverride For(ThingDef def)
        {
            if (!overrides.TryGetValue(def.defName, out ShuttleOverride ov))
            {
                // Craft that ship with typed capacity start ticked, because they are already using
                // it - the tick means "uses the crew/mech/cargo split", not "has been customised".
                ov = new ShuttleOverride
                {
                    enabled = ShuttleCapacityOverrides.NativeTypedCapacity.Contains(def.defName)
                };
                ov.PrefillFrom(def);
                overrides[def.defName] = ov;
            }
            return ov;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref settingsVersion, "settingsVersion", 0);
            Scribe_Collections.Look(ref overrides, "overrides", LookMode.Value, LookMode.Deep,
                ref keyBuffer, ref valueBuffer);
            if (overrides == null) overrides = new Dictionary<string, ShuttleOverride>();

            // Version 1 wrote an entry per craft the moment the settings page was opened, with the
            // tick meaning "has been customised" - so every craft, including ours, was stored as
            // unticked. Under the current meaning an unticked craft is stripped of typed capacity,
            // which would silently put our own fleet back on the vanilla mass limit. Old data is
            // therefore discarded rather than reinterpreted; the defaults are correct anyway.
            if (Scribe.mode != LoadSaveMode.Saving && settingsVersion < CurrentVersion)
            {
                overrides.Clear();
                settingsVersion = CurrentVersion;
            }
        }

        // Identifies the effective configuration, so a multiplayer mismatch can be spotted in the log
        // rather than showing up later as a desync. Capacity is baked into the defs at startup, so it
        // cannot be renegotiated mid-session - every client simply has to start with the same numbers.
        public int ConfigHash()
        {
            int hash = 17;
            foreach (string key in overrides.Keys.OrderBy(k => k))
            {
                ShuttleOverride ov = overrides[key];
                if (!ov.enabled) continue;
                hash = hash * 31 + key.GetHashCode();
                hash = hash * 31 + ov.maxColonists;
                hash = hash * 31 + Mathf.RoundToInt(ov.massCapacity);
                hash = hash * 31 + ov.mechBandwidth;
                hash = hash * 31 + (ov.requireMechanitor ? 1 : 0);
                hash = hash * 31 + (ov.noPawns ? 1 : 0);
            }
            return hash;
        }
    }

    public class OSVMod : Mod
    {
        public static OSVSettings Settings;

        private Vector2 scroll;

        // Capacity is baked into the defs at startup, so an edit made now does nothing until the game
        // is restarted. The header says so, but a header is easy to skim past - so say it again, on
        // the way out, and only when something actually changed.
        private bool hashCaptured;
        private int hashAtOpen;

        public OSVMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<OSVSettings>();
        }

        public override string SettingsCategory() => "Odyssey Shuttle Variants";

        public override void WriteSettings()
        {
            bool changed = hashCaptured && Settings.ConfigHash() != hashAtOpen;
            hashCaptured = false;

            base.WriteSettings();

            if (changed)
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "Shuttle capacity changes take effect when RimWorld is restarted.\n\n"
                    + "Craft already in your colony pick up the new limits then too - nothing needs rebuilding.",
                    null, null, null, null, "Odyssey Shuttle Variants"));
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (!hashCaptured)
            {
                hashAtOpen = Settings.ConfigHash();
                hashCaptured = true;
            }

            List<ThingDef> shuttles = ShuttleCapacityOverrides.EligibleShuttles();

            Text.Font = GameFont.Small;
            Rect header = new Rect(inRect.x, inRect.y, inRect.width, 60f);
            Widgets.Label(header,
                "Apply the crew / mech / cargo split to any player shuttle, including craft from other mods. "
                + "Anything left disabled keeps its normal behaviour, where a single mass limit covers everything.\n"
                + "Changes take effect on restart. In multiplayer every player must use identical settings.");

            float y = inRect.y + 64f;
            Rect outRect = new Rect(inRect.x, y, inRect.width, inRect.height - y - 40f);

            float totalHeight = shuttles.Sum(d => RowHeight(d) + 4f);
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, totalHeight);

            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float rowY = 0f;
            foreach (ThingDef def in shuttles)
            {
                float h = RowHeight(def);
                DrawShuttleRow(new Rect(0f, rowY, viewRect.width, h), def);
                rowY += h + 4f;
            }
            Widgets.EndScrollView();

            Rect footer = new Rect(inRect.x, inRect.yMax - 32f, 200f, 28f);
            if (Widgets.ButtonText(footer, "Reset all"))
            {
                Settings.overrides.Clear();
            }
        }

        private void DrawShuttleRow(Rect rect, ThingDef def)
        {
            Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.03f));
            Rect inner = rect.ContractedBy(6f);
            ShuttleOverride ov = Settings.For(def);

            Rect titleRect = new Rect(inner.x, inner.y, inner.width, 24f);
            bool wasEnabled = ov.enabled;
            Widgets.CheckboxLabeled(titleRect, def.LabelCap + "  (" + def.size.x + "x" + def.size.z + ")", ref ov.enabled);

            // Turning it on pre-fills from whatever the craft already has, so enabling alone changes nothing.
            if (ov.enabled && !wasEnabled) ov.PrefillFrom(def);

            if (!ov.enabled)
            {
                Rect offRect = new Rect(inner.x + 12f, inner.y + 26f, inner.width - 24f, 22f);
                GUI.color = Color.gray;
                Widgets.Label(offRect, "Using the vanilla single mass limit - crew and cargo share it.");
                GUI.color = Color.white;
                return;
            }

            float col = inner.width / 2f;
            Rect left = new Rect(inner.x + 12f, inner.y + 30f, col - 24f, 24f);
            Rect right = new Rect(inner.x + col, inner.y + 30f, col - 12f, 24f);

            int seats = ov.maxColonists;
            Widgets.Label(new Rect(left.x, left.y, 150f, 24f), "Colonist seats");
            string seatBuf = seats < 0 ? "" : seats.ToString();
            Widgets.TextFieldNumeric(new Rect(left.x + 155f, left.y, 70f, 24f), ref seats, ref seatBuf, -1f, 200f);
            ov.maxColonists = seats;

            float mass = ov.massCapacity;
            Widgets.Label(new Rect(right.x, right.y, 150f, 24f), "Cargo mass (kg)");
            string massBuf = mass < 0f ? "" : mass.ToString("F0");
            Widgets.TextFieldNumeric(new Rect(right.x + 155f, right.y, 70f, 24f), ref mass, ref massBuf, 0f, 100000f);
            ov.massCapacity = mass;

            Rect row2 = new Rect(inner.x + 12f, inner.y + 62f, col - 24f, 24f);
            int bandwidth = ov.mechBandwidth;
            Widgets.Label(new Rect(row2.x, row2.y, 150f, 24f), "Mech bandwidth");
            string bwBuf = bandwidth.ToString();
            Widgets.TextFieldNumeric(new Rect(row2.x + 155f, row2.y, 70f, 24f), ref bandwidth, ref bwBuf, 0f, 200f);
            ov.mechBandwidth = bandwidth;

            Rect togglesRect = new Rect(inner.x + col, inner.y + 62f, col - 12f, 24f);
            Widgets.CheckboxLabeled(new Rect(togglesRect.x, togglesRect.y, 220f, 24f),
                "Pilot must be a mechanitor", ref ov.requireMechanitor);

            Rect row3 = new Rect(inner.x + 12f, inner.y + 94f, inner.width - 24f, 24f);
            Widgets.CheckboxLabeled(new Rect(row3.x, row3.y, 300f, 24f),
                "Autonomous (cargo only)", ref ov.noPawns);

            // Starting points taken from our own calibrated craft - a hint, never applied on its own.
            Rect presetRect = new Rect(row3.x + 320f, row3.y, 90f, 24f);
            if (Widgets.ButtonText(presetRect, "Troop"))
            {
                ov.maxColonists = 12; ov.massCapacity = 100f; ov.mechBandwidth = 0;
                ov.requireMechanitor = false; ov.noPawns = false;
            }
            if (Widgets.ButtonText(new Rect(presetRect.x + 94f, presetRect.y, 90f, 24f), "Hauler"))
            {
                ov.maxColonists = 2; ov.massCapacity = 1250f; ov.mechBandwidth = 0;
                ov.requireMechanitor = false; ov.noPawns = false;
            }
            if (Widgets.ButtonText(new Rect(presetRect.x + 188f, presetRect.y, 90f, 24f), "Reset"))
            {
                ov.PrefillFrom(def);
            }
        }

        // A collapsed row only needs its title and one line; an expanded one needs the fields.
        private static float RowHeight(ThingDef def) => OSVMod.Settings.For(def).enabled ? 144f : 56f;

        // Craft that ship with typed capacity keep the values from their defs; anything else is
        // seeded from what it already has, so ticking one changes nothing until a number is edited.
    }
}
