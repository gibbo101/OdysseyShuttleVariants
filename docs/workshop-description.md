# Workshop description

Paste this into the Steam Workshop page's description box by hand.

RimWorld only sends a mod's description to Steam when the item is FIRST published -
`Workshop.SetWorkshopItemDataFrom` guards `SteamUGC.SetItemDescription` with `if (creating)`.
Every later upload sends the code, version, preview and tags but never the description, so
editing About.xml alone will never change what the Workshop page shows.

Generated from `About/About.xml`; edit that, not this file, then regenerate.
Steam uses BBCode, not Markdown - the bare capital headings below are deliberate.

---

Expands RimWorld's Odyssey transport-shuttle system with a family of buildable, role-distinct craft and a typed-capacity system that tracks colonists, mechs and cargo as independent pools instead of one shared mass limit.

THE CRAFT
- Passenger Shuttle (vanilla, folded into the system) - the balanced all-rounder: a handful of seats and modest cargo.
- Troop Shuttle - fast squad insertion and extraction: many seats, light cargo, long range.
- Cargo Shuttle - dedicated hauler: a large hold with just enough crew to fly it.
- Heavy Cargo Ship - the flagship hauler: the biggest hold in the fleet, but slow, fuel-hungry and short-ranged.
- Mechanitor Shuttle (Biotech) - a mech strike carrier flown by a mechanitor; it will not launch without one aboard. It ferries a mech warband against a bandwidth budget rather than a seat count. When landed it also doubles as a chemfuel-powered field charger: nearby colony mechs walk up to the hull and recharge from the shuttle's own fuel, no power grid required (an opt-in toggle, off by default so it never strands itself).
- Transport Drone (Biotech) - a fully autonomous courier with no crew. Send it anywhere, land and hold its cargo in the field, then recall it, send it onward, form a camp, or gift its cargo to a friendly settlement for goodwill.
- Psycaster Shuttle (Royalty) - an Imperial-style troop shuttle for psycasters: a full squad, longer range and a quicker turnaround. Landed, it is a dignified and artistic meditation focus, and it can project a resonance field that draws neural heat off your psycasters nearby (an opt-in toggle that burns a little chemfuel).

FEATURES
- Typed capacity: colonists (seats), mechs (bandwidth budget) and cargo (mass, excluding crew and mech weight) are capped independently, with clear load-dialog readouts and over-cap warnings.
- Paintable: paint any craft and it goes on the hull plating, leaving glass, engines, doors and fittings as they are.
- Autonomous drone: hands-free supply runs and automated diplomacy gifts; it survives the origin map being abandoned.
- Mech field charging at a landed Mechanitor Shuttle, run off its own chemfuel.
- Mech-bay expansion research: two tiers that raise the Mechanitor Shuttle's carried-mech bandwidth (6 to 12 to 18) and widen its field-charging line (2 to 3 to 4 mechs at once). Already-built ships benefit the moment the research completes.
- Every craft is research-gated off the vanilla Shuttles project, built in the Odyssey architect category, and costs a rare shuttle engine (trader/reward only; larger craft need more).
- Works on other mods' shuttles: the mod options page lists every player shuttle you have installed, and any of them can be given seats, cargo mass and a mech bandwidth budget of your choosing, or the Psycaster Shuttle's meditation focus and resonance field. Craft from other mods are left exactly as their author made them until you opt them in.

COMPATIBILITY
- Instant Loot Shuttle: supported. Crew no longer count against the cargo hold when instant-loading, so a full squad will not refuse the launch or leave your cargo behind.

MULTIPLAYER
Built with the Multiplayer mod and played through multiplayer sessions without trouble. One player has reported mass and landing problems that only appear in multiplayer, which I have not yet been able to reproduce - if you run into anything like that, please say so in the comments, it genuinely helps. It also runs fine without Multiplayer installed. Multiplayer isn't out for RimWorld 1.7 yet.
https://steamcommunity.com/sharedfiles/filedetails/?id=2606448745

COMPANION MODS
Two mods that handle the other half of the same job - getting one of these craft loaded and away without ten minutes of clicking:

Crew Presets - save named groups of colonists, animals and mechs - "Strike Team", "Pack Train" - and apply them with one click in the load dialog, instead of hand-picking the same pawns every launch.
https://steamcommunity.com/sharedfiles/filedetails/?id=3756372833

Transporter Loadouts - every transporter and shuttle remembers its own default cargo, pre-filled each time you open its load dialog. Crew Presets picks the people; this one picks the freight.
https://steamcommunity.com/sharedfiles/filedetails/?id=3756377821

Requires Odyssey. The Mechanitor Shuttle and Transport Drone additionally require Biotech, and the Psycaster Shuttle requires Royalty. Supports RimWorld 1.6 and 1.7.

CHANGES
1.0.4
- RimWorld 1.7 support. 1.6 keeps working.
- New: the Psycaster Shuttle (Royalty), and a Psycaster option on the mod options page that gives any player shuttle its meditation focus and resonance field.
- Painting returns, with new paint masks for every craft: the paint goes on the hull and nothing else.
- Every craft now looks like the same ship from every side: the Mechanitor Shuttle and Transport Drone no longer look thin head-on, the Cargo Shuttle fits its footprint, and the Heavy Cargo Ship's roof hatches sit in the same place from every angle.
- Fixed an empty Transport Drone being unable to launch after a load was cancelled.
- Fixed mech charging looking for charger shuttles left over from a previously loaded game, and the multiplayer settings check reporting a mismatch for identical settings on RimWorld 1.7.

1.0.3
- Fixed a bug in this mod that could leave the info card ("i") blank.
- The info card and the architect menu now show cargo capacity, maximum range, passenger capacity and mech bay capacity, for both built craft and the build preview.
- Mech bay capacity in the build preview now reflects completed mech-bay expansion research instead of always showing the base figure.

1.0.2
- Typed capacity can now be applied to any player shuttle, including craft from other mods, from the mod options page. Your other mods are untouched until you opt a craft in, and changes take effect on restart.

1.0.1
- Fixed crew being charged against cargo capacity when loading with Instant Loot Shuttle, which could refuse a launch outright or load the crew and leave the cargo behind.
- A craft arriving somewhere with no room large enough to land now holds position with its cargo aboard, to be recalled or redirected, instead of being set down where it does not fit.

Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.

AI tools were used in the making of this mod.
