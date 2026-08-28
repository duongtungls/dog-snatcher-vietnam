# DOG SNATCHER: VIETNAM
### Game Design Document — v0.2 (draft)

| | |
|---|---|
| **Title** | Dog Snatcher: Vietnam |
| **Genre** | Endless Runner / Arcade / Action |
| **Platform** | Mobile (Android first, iOS second) |
| **View** | Top-down 2D, portrait (9:16) |
| **Art style** | **Vector cartoon** (see §8 — locked) |
| **Engine** | Unity 2022.3.62f2 LTS + URP 2D |
| **Language** | English UI/text; Vietnamese street signage as set dressing (§8.5) |
| **Session** | Single player, offline, 60–180 seconds per run |
| **Controls** | One-handed: swipe left/right (change lane), tap (swing the snare), swipe up (nitro) |

---

## 0. Tone & Positioning

This is **satire**, not simulation. The whole experience is cartoon comedy — closer to *Crossy Road* or a Saturday-morning cartoon than to anything gritty.

**Hard rules for art, animation, audio and copy:**

- **Dogs are never harmed.** A successful snatch is slapstick: the dog pops up with spinning ⭐ stars, lets out a comic yelp, and lands softly in the crate on the back of the bike. No blood, no pain sounds, no weapons.
- **The player is never the hero.** Every run ends in failure — caught, dumped in a ditch, surrounded by angry neighbours. Game Over copy is comedic karma ("Busted!", "See you down at the station").
- **The best outcome is giving the dogs back.** The `Drop Point` system (§4.6) is framed as an **animal rescue station** — the highest scores come from returning your haul. Dogs still in the crate at Game Over are only worth 50%.
- **Police are anonymous cartoons.** No real insignia, unit numbers, or state emblems. This is a hard legal and store-compliance constraint, not a style preference.

**Target rating:** Google Play `Teen` / App Store `12+`. No blood, no adult content.

---

## 1. Core Loop

```
RIDE ──► SPOT A DOG ──► SWERVE TO OUTER LANE ──► TAP TO SNARE ──► +SCORE, +WANTED
  ▲                                                                       │
  │                                                                       ▼
  └── SHAKE THEM OFF (alley / wedding tent / drop point) ◄── PURSUIT SPAWNS
```

The central tension is **spatial**:

> Dogs always sit on the **sidewalk**. To reach one, you must ride the **outermost lane** — exactly where the power poles, parked bikes and tea-stall stools are, and where there is nowhere to dodge. The middle lanes are safe but score nothing.

Every second the player is answering one question: *one more dog, or cut back to the middle?*

---

## 2. Screen Layout & Play Space

### 2.1 Road layout

World width ≈ **8.8 units** (1 unit = 1 metre):

```
 │ L.WALK │ L0 │ L1 │ L2 │ L3 │ R.WALK │
 │  1.6u  │1.4u│1.4u│1.4u│1.4u│  1.6u  │
 └────────┴────┴────┴────┴────┴────────┘
  (dogs,        (rideable lanes)   (dogs,
  static                           static
  hazards)                         hazards)
```

- **Four rideable lanes** (`L0`–`L3`), index 0 = far left.
- **Sidewalks** are not rideable — hitting one means going down — but they are where targets and static hazards live.
- Riding `L0` puts the left sidewalk in snare range; `L3` reaches the right. `L1`/`L2` reach neither, unless the snare is fully upgraded (§6.2).

### 2.2 Camera

- Orthographic, `size = 8.5` → viewport ≈ 9.56 × 17 units at 9:16.
- Player anchored at **35% up from the bottom** — maximum read-ahead for reflexes, while still showing what's chasing you.
- The world scrolls top-to-bottom (world is static, player + camera travel along `+Y`).
- Light screen shake on impact; subtle pull-back (`size → 9.0`) at top speed to sell velocity.

### 2.3 HUD

```
┌──────────────────────────────┐
│ 🐕 x4        1,240m    ⚡⚡⚡  │  ← crate / distance / nitro
│ ★★☆☆  ← Wanted Level         │
│                              │
│         (play area)          │
│                              │
│                      x2.4    │  ← combo
│  ⚠️ ← incoming brick warning │
└──────────────────────────────┘
```

Minimal HUD, clear of the thumb zone. The entire screen is the input surface.

---

## 3. Controls

| Input | Action | Detail |
|---|---|---|
| **Swipe left/right** (≥ 40px) | Change lane | One lane per swipe. `laneChangeTime` = 0.18s (0.12s fully upgraded). One input may be queued mid-transition. |
| **Tap** | Swing the snare | The pillion rider swings toward the **nearest sidewalk**. Cooldown 0.55s; 0.25s animation with the catch window open from **0.08 → 0.20s**. |
| **Swipe up** | Nitro | +60% speed for 2.5s, immune to light collisions, smashes through roadblocks. |
| **Swipe down** | Hard brake | −40% speed for 1.2s. Beats roadblocks and cross-traffic. 3s cooldown. |
| **Diagonal swipe at an alley** | Alley cut | Only accepted while in an outer lane with an `AlleyMouth` on screen. |

No virtual buttons. No second hand. No tilt.

---

## 4. Systems

### 4.1 The player bike

- **HP = 1.** Riding without a helmet is a *mechanic*: any hard impact is an instant Game Over.
- **Light contact** (side-scrape, pothole, clipping a slower bike) doesn't kill, but costs **35% speed for 1.5s** and **resets the combo**. Losing speed is the real punishment — it's how you get caught.
- Three light hits within 5 seconds = you go down. No grinding through it.
- **Lucky Charm** (§6.2): one revive per run, 2s invulnerability, clears one Wanted star.

### 4.2 Speed & difficulty

Base speed follows a saturating curve over distance:

| Phase | Distance | Speed | Notes |
|---|---|---|---|
| 1 — *Back Lanes* | 0–400m | 8.0 → 10.0 m/s | Sparse traffic, street mutts only. Teaches the loop. |
| 2 — *Main Street* | 400–1200m | 10.0 → 13.5 m/s | Dense traffic, Ninja Leads appear, pedigree dogs appear. |
| 3 — *The Boulevard* | 1200–2500m | 13.5 → 17.0 m/s | Trucks, oversized loads, roadblocks. |
| 4 — *Endless* | 2500m+ | 17.0 → 20.0 m/s (saturating) | Max density, all pursuit types. |

Reference formula (tuned in a `DifficultyCurve` ScriptableObject — **never hardcoded**):

```
speed(d) = minSpeed + (maxSpeed - minSpeed) * (1 - exp(-d / 1400))
```

`spawnDensity` and `pursuerAggression` use separate `AnimationCurve` fields so a designer can hand-shape them in the Inspector.

### 4.3 Targets — Dogs

Dogs spawn on the **sidewalk**, typically outside doorways, under trees, beside tea stalls.

| Type | Score | Behaviour | Notes |
|---|---|---|---|
| **Street Mutt** | 50 | Idle or ambling slowly along the sidewalk. | The staple, ~65% of spawns. |
| **Phu Quoc Ridgeback** | 120 | Alert — bolts **forward** at ~6 m/s once you're within 6 units. | Forces a short chase; you must accelerate to close. |
| **Husky / pedigree** | 200 | **Wanders into the road** on a random cycle. | Best score in the game, but it is simultaneously a target and a lethal obstacle. |
| **Chained Yard Dog** | 80 | Stationary, but **chained** — a successful snare yanks you back, −25% speed. | A greed trap. |
| **Police K9** ⚠️ | 0 | Snaring it grants **+2 Wanted stars instantly**. | Clearly telegraphed (red harness, bigger build, distinct bark cue). Teaches you to look before you tap. |

**Catch window:** the snare hitbox is a cone of radius `snareReach` (default 1.0u, max 2.2u) opening toward the nearest sidewalk. A dog is caught only if its centre is inside the cone during the **0.08–0.20s** window of the swing.

**Crate capacity:** 6 by default. A full crate blocks further snaring, forcing a run to a Drop Point (§4.6). Max 12 upgraded.

### 4.4 Traffic & hazards

**Moving (in lanes):**

| Type | Speed | Behaviour |
|---|---|---|
| Commuter bikes (Wave / Sirius) | 6–9 m/s | Straight, lane-keeping. |
| **"Ninja Lead"** | 5–8 m/s | Rider in full sun-protection gear on a scooter. **Changes lanes at random, never signals, never checks mirrors.** The most dangerous thing in the game. |
| Gas-cylinder bike | 4 m/s | Slow, one lane, long hitbox. Lethal. |
| **Oversized load** | 4 m/s | **Hitbox is wider than the sprite** — steel bars and roofing sheets jut 0.8u past each side. Teaches players to read hitboxes, not silhouettes. |
| Cargo trike / cyclo | 3 m/s | Very slow, occupies 1.5 lanes. |
| Bus cutting across | 12 m/s | Enters from the top edge and **cuts two lanes** with a 1.2s telegraph (blinker). |
| Paired cars | 10 m/s | Travel side by side, blocking two lanes. The tool for losing the traffic police (§4.5). |

**Static (sidewalk + lanes):** potholes and open manholes (light hit + camera shake + 0.4s steering wobble); power poles, sagging cable bundles, fallen billboards; tea stalls, plastic stools, bins (these also *occlude dogs*, rewarding read-ahead); construction barriers claiming a lane.

### 4.5 Wanted Level & Pursuit

**0–3 stars.** Heat accumulates:

| Action | Heat |
|---|---|
| Snare a dog | +8 |
| Snare the Police K9 | +60 |
| Cause a crash (traffic wrecks itself avoiding you) | +12 |
| Nitro through a roadblock | +25 |
| Each second without a snare or a crash | −1.5 |
| Successful alley cut | −40 |
| Through a wedding tent | −25 |
| Deliver at a Drop Point | −50 |

Thresholds: `★ = 30`, `★★ = 75`, `★★★ = 140`. Heat **does not decay** while a pursuer is on screen.

---

#### 4.5.1 ★ — The Locals

- **Vehicles:** on foot out of side alleys, or on commuter bikes. Armed with brooms, sticks, bricks.
- **Spawn:** **at the exact spot you just robbed**, 4–8 units behind you, in packs of 2–5.
- **Speed:** 0.85× yours — they can **never** catch you on speed alone. They only matter once you've lost speed.
- **Attack:** thrown bricks, telegraphed by a **red impact marker on the road** 0.9s before landing. A hit is light contact — which cascades into being caught.
- **Weakness:** their AI **does not avoid obstacles**. Weave through traffic and they wipe themselves out (comic tumble, +5 **Karma** bonus).
- **Audio:** overlapping shouts of *"Trộm chó!"* — the more of them, the louder the chaos.

#### 4.5.2 ★★ — Traffic Police

- **Vehicles:** white patrol motorcycles, siren, blue-red light sweeping the wet asphalt.
- **Spawn:** from the bottom edge, or out of an intersection ahead.
- **Speed:** **1.15× yours** — they always close. You cannot outrun them.
- **Behaviour:** tail you for 2–3s to "lock on", then **pull alongside** and **shoulder you sideways**, pushing you one lane toward the kerb. Pushed off the road = down.
- **Danger ring:** inside 2.5 units, a **pulsing red ring** appears around the player. Stay inside it for **3.0 continuous seconds** and you're forced down. The timer resets when you break range.
- **Weakness:** they **can't thread tight gaps.** Cut in front of a truck, or slip between two paired cars, and they must brake — dropping 5–8 units back. Also lost entirely in a wedding tent.
- Cap: 2 on screen.

#### 4.5.3 ★★★ — Order Patrol

- **Vehicle:** green municipal pickup with a roof loudspeaker.
- **Behaviour:** does **not** chase from behind. Enters from the **top of the screen**, drives toward you, then **turns broadside to form a roadblock** across 2–3 of the 4 lanes. Telegraphed 1.8s in advance (loudspeaker + HUD arrow).
- **Counterplay:**
  1. Read the gap early and thread the remaining 1–2 lanes.
  2. **Nitro** straight through (+25 heat, splintering-wood VFX).
  3. Alley or wedding tent just before it.
- The truck is huge and slow to turn — once you're past, it can't follow. It sets a fresh block ~15s later.
- At ★★★ the mob, the patrol bikes and a roadblock are all on screen at once. This is the climax; most runs are *meant* to end here, or end in a spectacular escape.

### 4.6 Escape systems

| System | Placement | Effect | Requirement |
|---|---|---|---|
| **Alley cut** | Perpendicular into the sidewalk, every 250–400m | Loses **every** pursuer. `−40 heat`. A 2.5s corridor sequence — still dodging buckets and laundry lines. | Outer lane + correctly timed diagonal swipe. Miss it and it's gone. |
| **Wedding tent** | Marquee covering 1–2 lanes, every ~500m | Loses **police and patrol** (vehicles too big to fit). The mob on foot **keeps coming**. `−25 heat`. | Just drive through — but dodge tables and chairs inside. |
| **Drop Point** | Every ~700m, appears once the crate holds ≥ 3 | **Deliver**: banks score × combo, empties the crate, `−50 heat`, refunds one nitro. | Must pass the gate under 12 m/s — dangerous while being chased. |
| **Nitro** | Pickup on the road | +60% speed for 2.5s, immune to light contact, breaks roadblocks. | Carry 3 (5 upgraded). |

> **Design note:** the Drop Point is skinned as an **animal rescue station** (§0). Mechanically it's a bank; narratively it's where you hand the dogs back. Score only counts once delivered — dogs still in the crate at Game Over are worth **50%**.

---

## 5. Scoring & Combo

```
Run score = (distance / 10) + Σ(dog value × combo at delivery) + bonuses
```

- **Combo** starts at ×1.0, `+0.15` per successful snare, capped at **×5.0**.
- Resets to ×1.0 on any collision (even light) or after 8 seconds without a snare.
- **Bonuses:**
  - **Clean Getaway** — ★★★ down to ★ without a single collision: +500
  - **Karma** — each pursuer that wrecks itself: +5
  - **Close Call** — passing within 0.15u of a hazard: +2 (pays for riding dirty)
  - **Full House** — deliver a completely full crate: +300

**Cash (₫)** = `run score × 0.1` plus pickups. Spent in the shop.

---

## 6. Meta & Economy

### 6.1 Out-of-run structure

```
Main Menu ─┬─ PLAY      (into a run in < 2 taps)
           ├─ GARAGE    (bike, snare, crate, nitro)
           ├─ MISSIONS  (3 dailies, 24h refresh)
           └─ SCORES    (local + friends leaderboard)
```

### 6.2 Shop / Upgrades

| Item | Tiers | Benefit | Cost |
|---|---|---|---|
| **Bike** | Wave → Sirius → Exciter → Tuned Exciter | Higher top speed, `laneChangeTime` 0.18 → 0.12s | The louder the exhaust, the **faster heat builds** (×1.0 → ×1.35). Skilled players take the loud bike for speed; new players take the quiet one to survive. |
| **Snare** | 1.0u → 1.4u → 1.8u → 2.2u | Longer reach; at max you can reach the sidewalk from `L1`/`L2` | Each tier adds 0.05s to the swing, so cooldown grows. |
| **Crate** | 6 → 8 → 10 → 12 | More dogs between deliveries | A loaded crate is heavy: `laneChangeTime` +0.01s per 2 dogs. |
| **Nitro** | 3 → 4 → 5 | More outs | — |
| **Lucky Charm** | Consumable | One revive per run, clears a star | Expensive; not purchasable with real money at launch. |

**Economy rule:** every upgrade has a downside. There are no strictly-better purchases. Players build toward a style — *safe farming* vs *high-risk, high-yield*.

### 6.3 Monetisation (post-launch, not in v1)

- Opt-in ads: watch to ×2 end-of-run score, or to revive.
- IAP: remove-ads bundle, character skins (poncho, jelly sandals, pith helmet).
- **No** gameplay-affecting power-ups for sale. No pay-to-win.

---

## 7. Audio

| Layer | Content |
|---|---|
| **Music** | Vinahouse / hard-dance remix, 140–150 BPM. **Tempo and intensity track the Wanted Level** — at ★★★ the high-energy stem drops and the filter opens. |
| **Player bike** | Tuned Exciter exhaust, pitch bound to speed; popping and burbling off-throttle. |
| **Snare** | A *whoosh* on the swing, a comic yelp + ⭐ on the catch, a rattling cage when full. |
| **The mob** | *"Trộm chó!"* ("Dog thief!") — a pool of ≥ 8 lines, layered by crowd size. Sweeping brooms, bricks landing. |
| **Police** | Siren with distance-based Doppler; whistle bursts when they pull alongside. |
| **Order Patrol** | Distorted loudspeaker: *"Pull over. Pull over immediately."* |
| **Ambience** | Horns, clattering crockery from street stalls, distant barking. |

> **Design note:** the distant barking is **gameplay, not decoration** — it pans left/right to warn you which sidewalk the next dog is on before it enters frame, so you can pick your lane early.

---

## 8. Art Direction — Vector Cartoon *(locked)*

Bold, flat, high-contrast cartoon vector. Think modern mobile animation: clean uniform outlines, flat fills with a single shadow tone, no gradients, no texture noise, no pixel art.

### 8.1 Authoring pipeline

**Author in vector, ship as raster.** Unity's `com.unity.vectorgraphics` is experimental on 2022.3 — do **not** ship runtime SVG.

```
Figma / Illustrator (vector master)
   └─► export PNG @ PPU 128 reference
         └─► Unity Sprite Atlas (max 2048, ASTC 6×6 on Android)
```

- **Project PPU = 128.** On the reference device (1080 × 1920, 9.56-unit viewport) that's ~113 screen px per unit — sprites are always slightly *downscaled*, which stays crisp, with headroom for larger screens.
- Filter mode **Bilinear**, **mipmaps off** (2D, fixed camera distance), compression ASTC 6×6.
- Keep the vector masters in `Docs/ArtSource/` (or a linked design file) — they are the source of truth, the PNGs are build output.

### 8.2 The outline rule *(the classic vector pitfall)*

Outline weight must be uniform **in world space**, not in the source file. If every asset is drawn on the same canvas and then scaled differently on export, a dog ends up with a hairline and a truck with a slab.

> **Rule: every asset is drawn at its true in-game size, at PPU 128, with a 3px stroke.** A 1-unit-tall dog is drawn 128px tall; a 4-unit truck is drawn 512px tall. Both get the same 3px outline.

### 8.3 Palette & lighting

Night city. Deep blue-black asphalt as the ground tone, punched through with **neon signage** (magenta, amber, red), warm sodium streetlights, and the police light sweeping blue-red across wet road.

- Flat fill + **one** darker shadow tone per object. No gradients except a single soft radial for light pools.
- Master palette lives in a `PaletteAsset` ScriptableObject; sprites are authored in palette colours so a global retune is possible.

### 8.4 Readability first

At 20 m/s a player has ~0.2s to identify anything. Silhouette carries the read; colour confirms it.

- **Lethal hazards:** consistent red-orange outline.
- **Collectables and dogs:** warm yellow outline.
- **Background:** desaturated 40% and outline-free, so it never competes with the play layer.
- Test every new asset as a **black silhouette**: if you can't tell what it is, redraw it.

### 8.5 Environment identity

The setting is unmistakably Vietnamese even though the UI is English. Required set dressing: Vietnamese neon shopfront signs, red plastic stools outside tea stalls, power poles with cable nests, walls stencilled with *"khoan cắt bê tông"* ads, red-and-yellow wedding marquees, banyan trees, bougainvillea on balconies, a rubbish cart.

> Signage stays in Vietnamese — it's environment texture, not text the player must read. Nothing gameplay-critical is ever communicated by in-world text.

### 8.6 Animation & tooling

- **Skeletal, not frame-by-frame.** Use the **PSD Importer + 2D Animation** package (already in the 2D feature set) to rig the bike, riders, dogs and pursuers with bones. This suits flat vector art, keeps atlas memory low, and lets one dog rig drive many recolours.
- **SpriteShape** for kerbs, road edges and alley walls — continuous outlines without tiling seams.
- Juice budget: screen shake, hit-stop on impact, squash-and-stretch on the snare, particle bursts of ⭐, tweened UI. Cartoon reads as *bouncy*; static vector reads as cheap.

### 8.7 Parallax

Four layers: road surface (1.0×), sidewalk + hazards (1.0×), buildings (0.85×), skyline and cable lines (0.6×).

---

## 9. Technical Architecture

### 9.1 Principles

- **Pool everything.** No `Instantiate`/`Destroy` during a run. **Zero GC allocation per frame** is a hard target.
- **Static world, moving player.** Simpler physics and stable maths to ~50km. A `WorldRebaser` recentres everything once `player.y > 5000` to protect float precision.
- **Data lives outside code.** Every number in §4–§6 is a ScriptableObject field. Nothing hardcoded.
- **Chunk-based procedural road.** 30-unit `RoadChunk` prefabs declare their spawn slots (which lane, which sidewalk). The director picks by difficulty phase with weighted random plus an anti-repeat rule (no chunk twice within the last three).
- **Seeded RNG** per system (traffic / dogs / pursuit) so runs are reproducible for bug repro and daily challenges.

### 9.2 Core systems

| Class | Responsibility |
|---|---|
| `GameManager` | State machine `Boot → Menu → Playing → Paused → GameOver`. No gameplay logic. |
| `RunDirector` | Owns one run: tracks distance, samples `DifficultyCurve`, drives the spawners. |
| `RoadStreamer` | Pools and stitches `RoadChunk`s ahead of and behind the camera. |
| `PlayerBike` | Input, lane transitions, speed, collisions, state (`Normal / Stunned / Nitro / Invulnerable`). |
| `SnarePole` | Swing animation, cone hitbox, catch window, cooldown. |
| `DogCage` | Capacity, held dogs, unbanked score. |
| `HeatSystem` | Heat accrual and decay; raises `OnWantedLevelChanged`. |
| `PursuitDirector` | Listens to `HeatSystem`; spawns and despawns pursuers per tier, enforces caps. |
| `Pursuer` (abstract) | → `LocalPursuer`, `TrafficPolicePursuer`, `OrderPatrolPursuer`. Each has its own state machine. |
| `TrafficSpawner` / `DogSpawner` / `PickupSpawner` | Spawn into the current chunk's slots at the director's density. |
| `ScoreSystem` | Score, combo, bonuses. |
| `EconomyService` | Cash, purchases, equipped upgrades. |
| `SaveService` | JSON to `Application.persistentDataPath`, versioned with migrations. |
| `AudioDirector` | Music layers by heat, `AudioSource` pool, ducking. |

**Inter-system communication:** ScriptableObject event channels (`GameEvent`, `GameEvent<T>`). No singleton web, and **no `FindObjectOfType` at runtime.**

### 9.3 Layers & Physics 2D

`Player`, `Traffic`, `StaticObstacle`, `Dog`, `SnareHitbox` (trigger, collides only with `Dog`), `Pursuer`, `Trigger`.

Keep the collision matrix minimal. `Traffic` does **not** collide with `Traffic` (lane logic handles that). `Pursuer` **does** collide with `Traffic` and `StaticObstacle` — that collision *is* the Karma mechanic.

### 9.4 Performance budget

Reference device: **Redmi Note 9 / Snapdragon 662**.

| Metric | Budget |
|---|---|
| Framerate | 60 FPS stable (fall back to 30 via `Application.targetFrameRate` when thermally throttled) |
| Draw calls | < 60 (atlas per environment set) |
| GC alloc during a run | **0 B/frame** |
| Load into a run | < 1.5s |
| RAM | < 400 MB |
| APK size | < 80 MB |

Vector-cartoon note: flat art atlases beautifully. Budget breaks come from too many *distinct* atlases, not from too many sprites — keep one atlas per environment set and one for characters.

---

## 10. Roadmap

### Milestone 1 — *Grey-box Vertical Slice* ← current target
White boxes, no art. Answers one question: **is "ride the outer lane to snatch" actually fun?**
- [ ] `RoadStreamer` + chunk pooling, endless road
- [ ] `PlayerBike`: 4 lanes, swipe steering, ramping speed
- [ ] `SnarePole` + `Dog` (street mutt only) + scoring
- [ ] 2 traffic types + lethal collision
- [ ] Game Over + restart

### Milestone 2 — *Pursuit*
- [ ] `HeatSystem` + star HUD
- [ ] `LocalPursuer` (bricks, self-wrecking)
- [ ] `TrafficPolicePursuer` (danger ring, shoulder-check)
- [ ] Alleys + wedding tents
- [ ] Combo + bonuses

### Milestone 3 — *Full Loop*
- [ ] `OrderPatrolPursuer` + roadblocks + nitro
- [ ] All 5 dog types, full traffic roster
- [ ] Drop Points / banking
- [ ] Shop + save + economy

### Milestone 4 — *Art & Juice*
- [ ] Vector art pass, parallax, neon lighting
- [ ] Skeletal rigs for bike, riders, dogs, pursuers
- [ ] Audio pass, heat-driven music layers
- [ ] Screen shake, hit-stop, particles, UI tweens

### Milestone 5 — *Soft Launch*
- [ ] Daily missions, leaderboards, analytics
- [ ] Performance pass, Android build
- [ ] ≥ 20 playtesters, retune the difficulty curve

---

## 11. Open Questions

1. **Are four lanes too many in portrait?** Grey-box with 3 and 4 before committing. If four makes sprites unreadably small, drop to 3 lanes + sidewalks.
2. **Does the Drop Point break the rhythm?** Forcing a slow pass while being chased may read as frustrating rather than tense. Fallback: trigger on contact at any speed.
3. **Is the Police K9 trap too punishing?** At top speed, can a player identify it in time? It needs a dedicated audio cue, not just a visual one.
4. **Maximum simultaneous pursuers at ★★★** — where's the line between tense and unreadable? Start testing at 5 locals + 2 police + 1 roadblock.
5. **Vietnamese localisation at launch, or post-launch?** English ships first; the Vietnamese audience is the most likely early community, so a Vietnamese UI pass may be worth pulling forward.

---

*Living document. Every number here is a **starting point for tuning**, not a constant. Update after playtests.*
