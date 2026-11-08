# Submarines for Nuclear Option

A BepInEx 5 plugin that adds two submarines to Nuclear Option:

| Unit | jsonKey | Role |
|---|---|---|
| Kestrel-class SSN | `SUB_SSN_Kestrel` | Attack sub. 18 wire-guided heavyweight torpedoes, hunts ships and other subs. |
| Leviathan-class SSBN | `SUB_SSBN_Leviathan` | Ballistic missile sub. 12 SLBMs (fired as stock ballistic missiles), 6 torpedoes for self defence. |

Both show up in the mission editor's ship list like any stock ship.

> **Status: untested in game.** I wrote this without a copy of the game, so it has never been
> loaded into Nuclear Option. The game API calls come from other open source NO mods (MK-88
> Hydra, MK-65 Crosswim, Blueprinter, PlayableVehicles, DetectionDisable), and the code
> type-checks against stubs of that API (`tools/check.sh`). Expect a round of fixing the first
> time it meets the real `Assembly-CSharp.dll`. The "Known risks" section below lists the likely
> problems.

## How it works

### Hulls

The mod has no 3D models of its own. At `Encyclopedia.AfterLoad` it clones a stock warship
prefab for each sub, gives the clone a new `ShipDefinition` and a unique Mirage prefab hash, and
registers it in the encyclopedia. A sub has its own prefab, so saves, the editor and multiplayer
spawns all keep it separate from the donor ship.

The donor hull comes from keywords in the config (`corvette,frigate,destroyer` for the SSN by
default). On startup the BepInEx log lists every stock ship and missile with its jsonKey, so you
can point the config at the right one.

### Diving

Ships float on stock buoyancy. The sub fights that with a depth controller that commands
vertical speed and runs a disturbance observer: it measures how hard buoyancy and waves push
the hull and cancels that push. A second loop keeps the hull level. Depths: periscope 14 m,
patrol 120 m, max 300 m, and the boat always stays 25 m above the seabed.

### Staying off radar

Two Harmony prefixes on `DetectorManager.RequestRadarCheck` and `RequestLoSCheck` decide what
sensors can see:

- **Below periscope depth:** no radar return and nothing to see. Turrets can't lock on
  (`Turret_OnDetectTarget` is blocked too).
- **At periscope depth:** the masts stick out. Radar within 9 km and eyes within 2.5 km can
  still pick them up.
- **Surfaced:** a normal ship.

A submerged sub's own radar and turrets are under water, so they go quiet as well.

### Sonar

Every sub, and by default every stock warship, carries a `SonarArray`. It runs a simplified
sonar equation every 2 seconds:

```
passive SE = SL - TL - NL + DI - DT
active  SE = 220 - 2*TL + TS - NL + DI - DT
TL = 20 log10(r) + 0.5 dB/km, + 14 dB if the thermocline (60 m) is between you
```

Subs are quiet (102 to 108 dB at 5 kn) and get louder with speed, at shallow depth and when
surfaced. A destroyer at 20 kn comes out around 145 dB. Passive tracks start with a good
bearing and a bad range, and the range tightens over about 40 seconds of tracking. An active
ping gives range at once, but every hostile array within 55 km hears the ping and gets a
bearing on whoever sent it.

When the server holds a firm track on a hostile unit, it calls `FactionHQ.RpcUpdateTrackingInfo`,
so sonar contacts show up on the faction map like radar contacts.

### Spoofing enemy sonar

Each boat carries 8 decoys and an acoustic mimicry mode:

- **Noisemaker.** A loud broadband source at about 138 dB. Any array that hears the noisemaker
  on roughly the same bearing as the boat loses the boat, and torpedo seekers go for whatever
  is loudest. A noisemaker sits still in the water, though, and after about 25 seconds of good
  tracking a sonar operator works that out and labels it `DECOY`.
- **Repeater.** Plays back the boat's own signature 6 dB louder and answers active pings, from a
  phantom position that slides away from the boat up to 1.8 km (range-gate pull-off). Enemy
  sonar sees a moving submarine contact that isn't there. Torpedoes chase it, find nothing,
  and re-attack. Ships fire ASW torpedoes at it.
- **Mimicry.** For 90 seconds the boat sounds like merchant traffic (`MERCH`), so passive arrays
  don't report it and AI won't shoot at it. Active pings and torpedo seekers aren't fooled.

The AI uses all of this on its own. When it hears a torpedo inside 6 km it drops a decoy and
jumps across the thermocline. When it gets pinged it switches on mimicry and drops a repeater.

### Weapons

- **Torpedoes** are scripted and run on the server only. They aren't game units, so radar never
  sees them. Each one runs on the wire toward the launcher's track, switches on its seeker
  3 km out, and homes on the loudest hostile source in a 40° cone. A hit applies damage directly
  to the hull parts in the bubble radius, then spawns and detonates a stock missile at the
  waterline so every client sees the explosion. You can turn that last part off with
  `StockWarheadVisuals`.
- **SLBMs.** The SSBN climbs to 30 m, cold-launches a stock ballistic missile (picked by keyword)
  up out of the water, and aims it at the best enemy building or ship its faction has a known
  position for, between 8 km and 150 km away. Every launch makes a loud transient that gives
  away its position.
- **Surface ASW.** Stock warships with sonar ping when a hostile sub is within 30 km, and they
  fire up to 6 ASW torpedoes at firm submarine contacts within 9 km. Without them a submerged
  boat would be invulnerable.

### Sonar display (F8)

A small plot in the bottom left shows what your sub's sonar believes, decoys and phantoms
included. Ctrl+F8 cycles the range (10/30/60 km), and F9 picks the next boat.

| Key | Order |
|---|---|
| PageDown / PageUp | 20 m deeper / shallower (takes the boat off AI control) |
| Shift+F9 | Hand the boat back to the AI |
| Insert | Torpedo at the best hostile contact |
| Delete | Decoy (noisemaker and repeater alternate) |
| End | Acoustic mimicry |
| Home | SSBN: climb to launch depth and fire at the best known target |
| Pause | Active ping |

Orders only work in single player or on the host. In multiplayer, clients see the display but
can't give orders.

## Install

1. Install BepInEx 5 (x64) into the Nuclear Option folder.
2. Build (below) and put `Submarines.dll` in `BepInEx/plugins/Submarines/`.
3. Launch once. Check `BepInEx/LogOutput.log` for `Registered Kestrel-class SSN` and look at
   the ship and missile list it prints.
4. Tune `BepInEx/config/com.badnew21.submarines.cfg` if you want to (donor hulls, missile
   keywords, depths, ranges, damage, keys).

## Build

```bash
cp Directory.Build.props.template Directory.Build.props
```

Edit `NuclearOptionRoot` in that file to point at your game folder, then:

```bash
dotnet build -c Release
```

The Release build copies the DLL into `BepInEx/plugins/Submarines/` for you.

To type-check without the game (mono `mcs` only):

```bash
./tools/check.sh
```

## Known risks

These are the places most likely to break on first contact with the real game:

- **Cloned network prefab.** The other mods only clone mount prefabs that have no
  `NetworkIdentity`. Here a whole ship gets cloned under an inactive holder with a fresh prefab
  hash. If Mirage rejects that on spawn, switching to a Blueprinter `.nobp` hull is the fallback.
- **Stock damage or flooding logic** might treat a hull below the waterline as sinking.
- **Depth controller gains** were picked on paper. If the boat porpoises, lower the vertical
  speed. If it can't hold depth, the observer gain in `Submarine.FixedUpdate` needs a bump.
- **Damage numbers.** I don't know stock ship hitpoints, so `TorpedoDamage` might kill too fast
  or too slow.
- **Method names patched by string** (`AfterLoad`, `RegisterPrefabs`, `InitializeUnit`,
  `RequestRadarCheck`, `RequestLoSCheck`, `Turret_OnDetectTarget`). Each patch is applied
  separately, so a renamed method only switches off that one feature and logs an error.
- **The hulls still look like the donor ship.** A proper submarine model needs a Unity asset
  bundle.
- **Multiplayer.** Torpedoes and decoys live only on the server. Clients see their effects
  (explosions, map tracks) but not the objects themselves.
