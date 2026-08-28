# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

**Dog Snatcher: Vietnam** — a satirical top-down 2D endless runner for mobile. The player rides a motorbike through Vietnamese night streets, snatching stray dogs off the sidewalk while dodging traffic and shaking off three tiers of pursuers (locals → traffic police → order patrol) driven by a Wanted Level system.

The repo folder is still `Trom-cho` (the working title) — the shipping name is **Dog Snatcher: Vietnam**. Use the English name everywhere in code, assets and UI.

**The design document is `Docs/GameDesign.md`. Read it before implementing any gameplay system.** It is the source of truth for mechanics, tuning values, entity behaviour and architecture. If a request conflicts with the GDD, say so and ask — don't silently diverge.

### Language

- **All UI, code, comments, identifiers and player-facing text are in English.**
- Vietnamese appears only as **environment set dressing** — neon shopfront signs, wall stencils, market banners. Nothing gameplay-critical is ever communicated through in-world text.
- A Vietnamese localisation pass is planned but not scoped for v1. Route all player-facing strings through a lookup key from day one; do not hardcode display strings in MonoBehaviours.

### Tone rules (non-negotiable)

This is comedy satire, not simulation. For anything player-facing — art, animation, audio, copy, VFX:

- **Dogs are never harmed.** A snatch is slapstick: spinning stars, a comic yelp, a soft landing in the crate. No blood, no pain sounds, no weapons.
- **The player is never the hero.** Every run ends in getting caught. Game Over copy is comedic karma.
- **The best outcome is returning the dogs.** The Drop Point system is framed as an animal rescue station — the top scores come from handing your haul back. Dogs still in the crate at Game Over are worth 50%.
- **Police are anonymous cartoons.** No real insignia, unit numbers or state emblems. Hard legal and store-compliance constraint.

Target rating: Google Play Teen / App Store 12+.

## Tech stack

- **Unity 2022.3.62f2 LTS** — do not upgrade the editor version unless asked.
- **URP 2D** (`com.unity.render-pipelines.universal` 14.0.12), 2D feature set, Renderer2D at `Assets/Settings/Renderer2D.asset`.
- **Portrait only**, 9:16 target.
- **Unity MCP plugin** (`com.ivanmurzak.unity.mcp`) — live Editor connection, see below.
- Android first, iOS second. Reference device: Redmi Note 9 / Snapdragon 662.
- Tests: `com.unity.test-framework` (NUnit).

## Working with the Unity Editor

A live Unity Editor is connected over MCP. **Prefer the MCP skills over raw file I/O for anything Unity tracks.**

| Task | Use | Not |
|---|---|---|
| Read/write `.cs` files | `script-read`, `script-update-or-create` (Roslyn-validates, refreshes AssetDatabase, waits for compile) | `Write`/`Edit` on scripts |
| Assets, `.meta`, prefabs, materials | `assets-*` skills | direct file writes |
| Scene / GameObject / component structure | `scene-*`, `gameobject-*` skills | hand-editing `.unity` YAML |
| Check errors after a change | `console-get-logs` | guessing |
| Run tests | `tests-run` | — |
| Visual verification | `screenshot-game-view`, `screenshot-camera` | — |

Rules of thumb:

- **Inspect before mutating.** Call the `*-get-data` / `*-find` / `*-list` variant first so type names and diffs are correct (`gameobject-component-list-all` for valid component type names, `assets-shader-list-all` for shader names).
- **Always check compilation** after touching code — `script-update-or-create` returns the compile result; follow with `console-get-logs` filtered to errors.
- **Save scenes before `tests-run`** — it aborts on dirty scenes.
- **Never hand-edit `.meta` files or scene YAML.** It desyncs the AssetDatabase and produces broken GUIDs.
- Don't enter/exit playmode casually; scene edits made in playmode are discarded.
- For heavier Unity work, delegate to the `unity-game-dev` subagent.

## Code conventions

### Architecture

- **Data lives in ScriptableObjects, not in code.** Every number in the GDD (§4–§6) is a tunable field on an SO asset under `Assets/_Game/Data/`. Never hardcode speeds, spawn rates, heat values, scores or reach distances.
- **Systems communicate through ScriptableObject event channels** (`GameEvent`, `GameEvent<T>`). No singleton webs. **Never call `FindObjectOfType` / `GameObject.Find` at runtime.**
- **The world is static; the player moves.** `WorldRebaser` recentres everything when `player.y > 5000` to keep float precision.
- **Pool everything.** No `Instantiate` or `Destroy` during a run. Target **0 bytes allocated per frame** in `Update`.
- The road is built from 30-unit `RoadChunk` prefabs that declare spawn slots; `RunDirector` picks them by difficulty phase with an anti-repeat rule.
- Seeded `System.Random` per system (traffic / dogs / pursuit) — reproducible runs for bug repro and daily challenges.

### C# style

- Namespace root `DogSnatcher`, then the subsystem: `DogSnatcher.Gameplay`, `DogSnatcher.Pursuit`, `DogSnatcher.Data`, `DogSnatcher.UI`, `DogSnatcher.Core`.
- `[SerializeField] private` over public fields.
- Cache component references in `Awake`. **Never `GetComponent` in `Update`** — use `TryGetComponent` when the result is optional.
- `[RequireComponent]` where a component genuinely depends on another.
- Prefer `struct` + `readonly` for small per-frame value types; avoid LINQ, boxing, string concatenation and `foreach` over interfaces in hot paths.
- Renaming a serialized field breaks existing scene/prefab data — add `[FormerlySerializedAs("oldName")]`.
- Physics in `FixedUpdate`, input and visuals in `Update`, camera follow in `LateUpdate`.

### Layers

`Player`, `Traffic`, `StaticObstacle`, `Dog`, `SnareHitbox`, `Pursuer`, `Trigger`. Keep the collision matrix minimal — `Traffic` does not collide with `Traffic` (lane logic handles that), but `Pursuer` **does** collide with `Traffic` and `StaticObstacle` — that collision *is* the Karma mechanic where chasers wipe themselves out.

## Art pipeline — vector cartoon

The style is locked: bold flat vector cartoon, uniform outlines, no gradients, no pixel art. See `Docs/GameDesign.md` §8.

- **Author in vector, ship as raster.** `com.unity.vectorgraphics` is experimental on 2022.3 — do **not** ship runtime SVG. Vector masters (Figma/Illustrator) → PNG export → Sprite Atlas.
- **Project PPU = 128.** Bilinear filtering, **mipmaps off**, ASTC 6×6 on Android.
- **Outline rule:** every asset is authored at its true in-game size at PPU 128 with a 3px stroke, so outline weight is uniform in world space. A 1-unit dog is drawn 128px tall; a 4-unit truck is drawn 512px tall.
- **Skeletal animation, not frame-by-frame** — PSD Importer + 2D Animation (already in the 2D feature set). SpriteShape for kerbs, road edges and alley walls.
- **Readability colour code:** lethal hazards get a red-orange outline, dogs and collectables a warm yellow outline, background art is desaturated 40% and outline-free.
- One atlas per environment set, one for characters. Draw-call budget breaks from too many *distinct* atlases, not too many sprites.

## Project layout

Game content goes under `Assets/_Game/` (the underscore keeps it sorted above package folders):

```
Assets/
├── _Game/
│   ├── Art/          Sprites, atlases, rigs, animations
│   ├── Audio/        SFX, music stems
│   ├── Data/         ScriptableObject assets (tuning, definitions, palette)
│   ├── Prefabs/      Bike, dogs, traffic, pursuers, road chunks, UI
│   ├── Scenes/       Boot, Menu, Game
│   ├── Scripts/
│   │   ├── Core/         GameManager, event channels, save, services
│   │   ├── Gameplay/     PlayerBike, SnarePole, DogCage, Dog, Traffic
│   │   ├── Pursuit/      HeatSystem, PursuitDirector, Pursuer types
│   │   ├── Spawning/     RoadStreamer, RunDirector, spawners, pooling
│   │   ├── Data/         ScriptableObject definitions
│   │   ├── UI/           HUD, menus, shop
│   │   └── Audio/        AudioDirector
│   └── Tests/
│       ├── EditMode/
│       └── PlayMode/
├── Plugins/          Third-party DLLs (Unity MCP) — do not modify
└── Settings/         URP assets — do not modify unless asked
```

**Assembly definitions:** `DogSnatcher.Runtime`, `DogSnatcher.Editor`, `DogSnatcher.Tests.EditMode`, `DogSnatcher.Tests.PlayMode`. Keep the runtime asmdef free of editor-only references.

Vector masters live in `Docs/ArtSource/` (or a linked design file) — they are the source of truth; exported PNGs are build output.

The default `Assets/Scenes/SampleScene.unity` is scaffolding — replace it with real scenes under `Assets/_Game/Scenes/` rather than growing it.

## Performance budget

Non-negotiable targets on the reference device:

| Metric | Budget |
|---|---|
| Framerate | 60 FPS stable |
| Draw calls | < 60 |
| GC alloc during a run | **0 B/frame** |
| Load into a run | < 1.5s |
| RAM | < 400 MB |
| APK size | < 80 MB |

If a change plausibly costs frame time or allocates per frame, say so in your summary.

## Testing

- **EditMode** for anything pure: heat accumulation and decay, score/combo maths, difficulty curve sampling, chunk anti-repeat selection, save migration.
- **PlayMode** for integration: lane-change timing, snare hitbox windows, pursuer state machines, collision outcomes.
- Save all open scenes before running tests.
- Keep gameplay maths in plain C# classes that MonoBehaviours delegate to, so it is testable without a scene.

## Current state

Greenfield — no gameplay code exists yet. The immediate goal is **Milestone 1, the grey-box vertical slice** (`Docs/GameDesign.md` §10): white boxes, no art, answering one question — *is "ride the outer lane to snatch" fun?* Don't build systems beyond that milestone unless asked.

## Git

- Not yet a git repository. Offer `git init` before the first commit.
- Commit through the `git-committer` subagent. **Commit messages carry no Claude/Anthropic attribution** — no `Co-Authored-By`, no generated-with footer, no session trailer.
- Unity `Library/`, `Temp/`, `Logs/`, `UserSettings/` and `obj/` must be gitignored; `Assets/`, `ProjectSettings/`, `Packages/` and every `.meta` file must be committed.
