# ⚔️ OttomanChessAuto — Grid Auto-Battler

> **A Grid-Based Side-Scroller Auto-Battler** built in Unity 6 with URP 2D.
>
> Inspired by management-strategy titles like *Dwarves: Glory, Death and Loot* and *Totally Accurate Battle Simulator* — tactical army preparation meets fully automated, physics-driven combat.

🎮 **Play on itch.io:** [talha-dogan.itch.io/ottomanchessauto](https://talha-dogan.itch.io/ottomanchessauto)

**Studio:** TDEVGAMES · **Engine:** Unity 6000.4.6f1 · Universal Render Pipeline 2D · **Language:** C# / .NET Standard 2.1

---

## 📖 Table of Contents

1. [Game Overview](#game-overview)
2. [Core Gameplay Loop](#core-gameplay-loop)
3. [Grid & Spawning System](#grid--spawning-system)
4. [AI & Combat Logic](#ai--combat-logic)
5. [Equipment & Upgrade System](#equipment--upgrade-system)
6. [Technical Architecture](#technical-architecture)
   - [Architecture Quality Matrix](#architecture-quality-matrix)
   - [SOLID Principles](#solid-principles)
   - [Data-Driven Design](#data-driven-design-scriptable-objects)
   - [Object Pooling (5 Pools)](#object-pooling-5-pools)
   - [GameEvents Bus](#gameevents-bus-logicui-decoupling)
   - [MVP Pattern](#mvp-pattern-presenter-layer)
   - [Finite State Machine](#finite-state-machine)
   - [Save System (Binary + AES)](#save-system-binary--aes)
   - [Localization System](#localization-system)
   - [Easing Library & Animation Curves](#easing-library--animation-curves)
   - [Addressables & Async Loading](#addressables--async-loading)
   - [Input System](#input-system)
   - [Debug & QA Tools](#debug--qa-tools)
7. [Project Structure](#project-structure)
8. [System Dependency Map](#system-dependency-map)
9. [Key Constants Reference](#key-constants-reference)
10. [Designer Guides](#designer-guides)
11. [Screenshots](#screenshots)
12. [Planned Features & Roadmap](#planned-features--roadmap)

---

## 🎮 Game Overview <a name="game-overview"></a>

OttomanChessAuto is a **2D side-scrolling tactical auto-battler**. Players act as battlefield commanders: configure your army composition and equipment loadout before each engagement, then watch your Ottoman soldiers fight autonomously using a custom AI system.

The visual perspective is a **lateral side-scroller** — units face each other across a horizontal battlefield, with player forces on the left and enemy formations on the right. Combat is fully physics-driven via Unity's 2D Rigidbody system, giving units natural push-and-slide interactions as they close in on their targets.

**Three Scenes:**

| Scene | Purpose |
|---|---|
| `StartScene` | Main menu, settings panel, game entry point |
| `UpgradeScene` | Army management, equipment drag-and-drop, pawn shop, loot boxes |
| `GridScene` | Main battle arena — preparation + combat + resolution |

---

## 🔄 Core Gameplay Loop <a name="core-gameplay-loop"></a>

The game alternates between two distinct phases per level:

```
┌─────────────────────────────────────────────────────────────────┐
│                     PREPARATION PHASE                           │
│                                                                 │
│  • LevelManager loads the next LevelDataSO asset               │
│  • UnitSpawner places the enemy formation (EnemyFormationSO)   │
│    onto the right side of the grid (Columns 6–7)               │
│  • Player reviews the enemy layout and presses [WAR!]          │
│  • UnitSpawner spawns player units onto Column 0               │
│                                                                 │
└──────────────────────────────┬──────────────────────────────────┘
                               │ WAR! button pressed
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                       COMBAT PHASE                              │
│                                                                 │
│  • BattleManager.StartBattle() sets isBattleStarted = true     │
│  • All BaseUnit FSMs unlock and begin executing                 │
│  • Units autonomously seek targets via Lane Priority AI        │
│  • Melee units close distance and swing; Ranged units fire     │
│    pooled projectiles via ProjectileFactory                     │
│  • BattleManager monitors unit deaths via OnDeath events       │
│  • When one side reaches 0 units → Win or Lose broadcast       │
│                                                                 │
└──────────────────────────────┬──────────────────────────────────┘
                               │ GameEvents.LevelWin / LevelLose
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                    RESOLUTION PHASE                             │
│                                                                 │
│  • LevelManager awards gold (goldReward from LevelDataSO)      │
│  • Surviving units play Victory animation                       │
│  • Board is cleared (UnitFactory returns all units to pools)   │
│  • GridManager.ResetGrid() wipes all occupancy flags           │
│  • Next LevelDataSO is loaded after a 3-second delay           │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🗺️ Grid & Spawning System <a name="grid--spawning-system"></a>

### The 8×8 World-Space Grid

The entire battlefield is defined by a single `GridManager` singleton that owns an authoritative `GridNode[8, 8]` data model. The grid is generated **once in `Awake()`** and never changes at runtime.

| Property | Value |
|---|---|
| Dimensions | 8 columns × 8 rows |
| Origin `[0,0]` world center | `(1.3, 1.3, 0)` |
| Cell size (center-to-center) | `2.6` Unity units |
| Column axis | X → left (0) to right (7) |
| Row axis | Y → bottom (0) to top (7) |

`GridNode` is a plain C# class (not a `MonoBehaviour`) storing grid coordinates, pre-calculated world-space position, and a mutable occupancy flag. An `OnDrawGizmos()` overlay renders the grid in the Scene view at all times — green for free nodes, red for occupied ones.

### Deterministic Spawning Layout

```
Side-Scroller Lateral View (Y = row / "lane"):

  Col:  0          1  2  3  4  5       6    7
  ┌─────────────────────────────────────────────┐
  │ [P] ─────────────────────────────── [E2] [E1] │  Row 7
  │ [P] ─────────────────────────────── [E2] [E1] │  Row 6
  │ [P] ─────────────────────────────── [E2] [E1] │  Row 5
  │ [P] ─────────────────────────────── [E2] [E1] │  Row 4
  │ [P] ─────────────────────────────── [E2] [E1] │  Row 3
  │ [P] ─────────────────────────────── [E2] [E1] │  Row 2
  │ [P] ─────────────────────────────── [E2] [E1] │  Row 1
  │ [P] ─────────────────────────────── [E2] [E1] │  Row 0
  └─────────────────────────────────────────────┘
       Player                          Overflow Primary
       Col 0                           Col 6   Col 7
```

**Player Spawning** (`UnitSpawner.SpawnPlayerUnits`) — Column `X = 0`. Melee units fill rows `Y = 0..7` first (front line), then Ranged units fill remaining rows. Maximum **8 units**.

**Enemy Spawning** (`UnitSpawner.SpawnEnemyFormation`) — Two-pass, two-column strategy:

| Pass | Column | Rows | Capacity |
|---|---|---|---|
| Pass 1 — Primary | `X = 7` | `Y = 0..7` | 8 units |
| Pass 2 — Overflow | `X = 6` | `Y = 0..7` | 8 units |
| **Total** | | | **16 units max** |

---

## 🧠 AI & Combat Logic <a name="ai--combat-logic"></a>

### Finite State Machine

Every unit runs a lightweight FSM with **five states**, managed entirely inside `BaseUnit`:

```
                    ┌──────────────────────────────────────┐
                    │                                      │
          ┌─────────▼──────────┐              ┌───────────▼──────────┐
          │     IDLE STATE     │◄─────────────│   ATTACKING STATE    │
          │  Scans for targets │  target dead │  Fires on cooldown   │
          └─────────┬──────────┘              └───────────▲──────────┘
                    │ target found                         │ in range
                    ▼                                      │
          ┌─────────────────────┐                         │
          │    MOVING STATE     ├─────────────────────────┘
          │  Closes on target   │
          └─────────┬───────────┘
                    │ health = 0
                    ▼
          ┌─────────────────────┐     ┌─────────────────────┐
          │     DEAD STATE      │     │   VICTORY STATE     │
          │  Returns to pool    │     │  Plays bounce anim  │
          └─────────────────────┘     └─────────────────────┘
```

State objects (`IdleState`, `MovingState`, `AttackingState`) are **shared static instances** — one object per state type, not per unit, eliminating per-unit allocations.

### Lane Priority Targeting System

`BattleManager.GetClosestTargetFor()` implements a **three-tier Lane Priority** system:

```
┌──────────────────────────────────────────────────────────────────┐
│                  LANE PRIORITY TARGETING                         │
│                                                                  │
│  TIER 1 — Same Lane (seekerRow == candidateRow)                  │
│    → Find the closest living enemy in the exact same row.        │
│                                                                  │
│  TIER 2 — Adjacent Lanes (seekerRow ± 1, excluding same row)     │
│    → Only reached when the seeker's own lane is empty.           │
│    → Prevents units from marching across the entire board.       │
│                                                                  │
│  TIER 3 — Global Fallback (entire opposing team)                 │
│    → Only reached when same AND adjacent lanes are empty.        │
│    → Returns the absolute closest living enemy anywhere.         │
│                                                                  │
│  All distance comparisons use sqrMagnitude (no Mathf.Sqrt).      │
└──────────────────────────────────────────────────────────────────┘
```

### Local Physics Overlap Search

Before escalating to the global `BattleManager` query, each unit runs a **local `Physics2D.OverlapCircle`** scan (radius = `attackRange × 1.5`) using the non-allocating `ContactFilter2D` API. A **hysteresis factor** (`0.5625` = `0.75²`) prevents target-switching jitter — a new candidate must be significantly closer before a switch occurs.

### Crash-Safe Unit Removal (Two-Phase Queue)

Units that die during a `foreach` traversal are not removed immediately. `BattleManager` uses a `_pendingRemovals` queue: deaths are queued in `Update()` and the queue is flushed safely in `LateUpdate()` — preventing `InvalidOperationException: Collection was modified` without requiring list copies on every frame.

---

## 🛡️ Equipment & Upgrade System <a name="equipment--upgrade-system"></a>

The Upgrade Scene provides a full **drag-and-drop equipment management** interface for the player's army of up to 8 units.

**`EquipmentDataSO`** — Each piece of equipment defines identity (`equipmentName`, `EquipmentSlot`), stat bonuses (`bonusHealth`, `bonusDamage`, `bonusAttackSpeed`), and an Addressable sprite loaded on demand.

**`PlayerArmyDataSO`** — A persistent ScriptableObject shared between scenes. Stores 8 `ArmySlot` entries, each containing a base unit reference and five optional equipment slots (Helmet, Vest, Pants, Weapon, Shield).

**Pawn Shop** — Players start with 1 unlocked pawn slot (free). Additional slots cost **300 gold** each, up to a maximum of **8**. Slot count is persisted via `GameSaveService`.

**Loot Box System** — Costs **200 gold** per box. Spawns a random equipment item into the stash with an open/close animation.

| UI Component | Responsibility |
|---|---|
| `UpgradeDragItemUI` | Handles pointer drag events, creates ghost image |
| `UpgradeCharacterDropZoneUI` | Accepts drops onto character equipment slots |
| `StashDropZoneUI` | Accepts drops back into the inventory stash |
| `ShowcasePawnController` | Manages 3D pawn showcase visibility |

---

## 🏗️ Technical Architecture <a name="technical-architecture"></a>

### Architecture Quality Matrix

| Grade | System / Pattern | Status |
|---|---|---|
| **A** | SOLID Principles | ✅ Fully applied across all systems |
| **A** | Abstraction | ✅ `BaseUnit`, `BaseUnitDataSO`, `IGameView`, `IUnitState` |
| **A** | Singleton | ✅ 9 singletons — `BattleManager`, `GridManager`, `LevelManager`, `UnitFactory`, `UnitSpawner`, `ProjectileFactory`, `GameSaveService`, `SoundManager`, `VFXManager` |
| **A** | ScriptableObject Architecture | ✅ Fully data-driven: units, levels, formations, equipment, stat progressions |
| **A** | Factory Pattern | ✅ `UnitFactory` + `ProjectileFactory` with `ObjectPool<T>` |
| **A** | Polymorphism | ✅ `BaseUnit` → `MeleeUnit` / `RangedUnit` hierarchy |
| **A** | Decoupling | ✅ `GameEvents` static event bus — zero direct cross-system references |
| **B** | Projectile Pooling | ✅ `ProjectileFactory` — one pool per unique prefab |
| **B** | Floating Text Pooling | ✅ `DamageTextManager` — pooled damage numbers |
| **B** | VFX Pooling | ✅ `VFXManager` — `ObjectPool<ParticleSystem>` per VFX type |
| **B** | Enemy Pooling | ✅ `UnitFactory` — one pool per unique unit prefab |
| **B** | Sound Pooling | ✅ `SoundManager` — `AudioSource` pool (10 initial, 20 max) |
| **B** | MVP Pattern | ✅ `GamePresenter` + `IGameView` + `GameUIManager` |
| **C** | Lazy Loading | ✅ Addressables async for equipment sprites |
| **C** | Memory Unload | ✅ Addressable handle release on scene unload |
| **C** | Sprite Bundles | ✅ Addressable groups: Arena, Audio, Items, UI, Units |
| **E** | Async Scene Loading | ✅ `LoadSceneAsync` with `allowSceneActivation` control |
| **F** | Easing Library | ✅ `EasingLibrary` — 22 easing functions (Quad, Cubic, Bounce, Elastic…) |
| **F** | Stat Progression Curves | ✅ `StatProgressionSO` — `AnimationCurve` per stat |
| **F** | Movement & Spawn Pacing Curves | ✅ `movementCurve` + `spawnPacingCurve` in `StatProgressionSO` |
| **G** | Binary Save System | ✅ `SaveManager` — AES-256 encrypted binary + SHA-256 checksum |
| **G** | Save Versioning & Migration | ✅ `CurrentSaveVersion = 2`, `SaveMigrationService` v1→v2 |
| **G** | Save Meta Data | ✅ `saveVersion`, `savedAt`, `saveCount`, `totalPlayTimeSeconds` |
| **H** | New Input System | ✅ `GameInputHandler` — gamepad + keyboard unified |
| **I** | Debug & QA Tools | ✅ `DebugToolsWindow` — stage jump, inventory fill, stress test |
| **J** | Localization | ✅ `LocalizationManager` — TR/EN JSON files, fallback chain |
| **J** | Localization Key Naming | ✅ `LocalizationKeys.cs` — typed constants, no magic strings |
| **D** | Google Sheets Sync | ❌ Not implemented — planned for a future update |

---

### SOLID Principles

| Principle | Implementation |
|---|---|
| **Single Responsibility** | `UnitSpawner` only spawns. `BattleManager` only manages battle state and targeting. `GameUIManager` only updates UI. `UnitFactory` only manages pools. Each class has one reason to change. |
| **Open/Closed** | `BaseUnit` is abstract and open for extension (`MeleeUnit`, `RangedUnit`) without modifying the base. `BaseUnitDataSO` is extended by `MeleeUnitDataSO` and `RangedUnitDataSO`. |
| **Liskov Substitution** | `MeleeUnit` and `RangedUnit` are fully substitutable for `BaseUnit` anywhere in the codebase (`BattleManager.playerUnits`, `UnitFactory.CreateUnit`). |
| **Interface Segregation** | `IDamageable` and `IAttacker` are separate, minimal interfaces. `IUnitState` defines only `Enter`, `Execute`, `Exit`. `IGameView` defines only the UI surface the Presenter needs. |
| **Dependency Inversion** | `BattleManager` depends on the `BaseUnit` abstraction, not concrete types. `Projectile` depends on `IObjectPool<Projectile>`. `GamePresenter` depends on `IGameView`, not `GameUIManager`. |

<img width="574" height="62" alt="Ekran görüntüsü 2026-06-02 033545" src="https://github.com/user-attachments/assets/1a4bfdaf-423f-4eda-8ec8-59ffacf616f4" />


---

### Data-Driven Design (Scriptable Objects)

All game content is defined in ScriptableObject assets, completely decoupled from scene objects. Designers can create and tune levels, units, and equipment **without touching code**.

```
Assets/Scripts/Data/
├── BaseUnitDataSO.cs              ← Abstract base: name, prefab, health, damage, speed, range, cooldown
│   ├── MeleeUnitDataSO.cs         ← Extends base: swingAngle, swingDuration
│   └── RangedUnitDataSO.cs        ← Extends base: projectilePrefab, projectileSpeed
│
├── LevelDataSO.cs                 ← meleeLimit, rangedLimit, unit data refs, formation ref, goldReward
├── StatProgressionSO.cs           ← AnimationCurve per stat (health, damage, speed, cooldown, range…)
│
├── Units/
│   ├── EnemyFormationSO.cs        ← List<UnitPlacement> { unitData, offset }
│   └── PlayerArmyDataSO.cs        ← 8 ArmySlot entries (baseUnitData + 5 equipment refs)
│
├── Equipment/
│   ├── EquipmentDataSO.cs         ← equipmentName, slot, bonuses, Addressable spriteReference
│   └── EquipmentSlot.cs           ← Enum: Helmet, Vest, Pants, Weapon, Shield
│
└── Text/
    └── DamageTextDataSO.cs        ← Floating damage number visual configuration
```
<img width="600" alt="image" src="https://github.com/user-attachments/assets/09b60c2e-40d3-45e8-9424-f4455cb216d9" />
<img width="330" alt="image" src="https://github.com/user-attachments/assets/f1485c10-3a4f-4902-bec4-a4dba1ef8175" />


---

### Object Pooling (5 Pools)

All five pools use **Unity's `UnityEngine.Pool.ObjectPool<T>`** and follow an identical architectural pattern.

**`UnitFactory`** — One `ObjectPool<BaseUnit>` per unique unit prefab. A reverse lookup dictionary (`_unitToPool`) lets any unit release itself in O(1) without factory references.

**`ProjectileFactory`** — One pool per unique projectile prefab, shared across all units firing it. Previously each `RangedUnit` had its own pool — ProjectileFactory consolidates them into one shared pool per prefab (default capacity 10, max 60).

**`DamageTextManager`** — Pooled `DamageText` instances spawned above units on hit. Auto-returned after the float-and-fade animation completes.

**`VFXManager`** — One `ObjectPool<ParticleSystem>` per `VFXType` enum entry. All pools are pre-warmed at `Awake()`. A `WaitWhile(() => ps.isPlaying)` coroutine auto-returns each effect on completion.

**`SoundManager`** — `Queue<AudioSource>` idle pool + `List<AudioSource>` active list. 10 initial sources, 20 max. If the pool is exhausted, the oldest active source is forcibly reclaimed. Pitch variation and random clip selection are applied per-play.



<img width="168" height="382" alt="AuttoBattlerPool-ezgif com-crop" src="https://github.com/user-attachments/assets/f92e7133-502b-478d-8a94-f4ee4c3ac1df" />




---

### GameEvents Bus (Logic–UI Decoupling)

`GameEvents` is a **static C# event bus** — no `MonoBehaviour`, no scene references. Any system broadcasts by invoking a method; any system reacts by subscribing/unsubscribing in `OnEnable`/`OnDisable`.

```csharp
// BattleManager broadcasts — never touches UI directly
GameEvents.LevelWin(rewardMessage);

// SoundManager reacts independently
GameEvents.OnLevelWin += OnLevelWin;

// VFXManager reacts independently
GameEvents.OnUnitDied += OnUnitDied;

// GamePresenter routes to IGameView (UI layer)
GameEvents.OnLevelWin += HandleLevelWin;
```

`GameEvents.ClearAllEvents()` is called by `SceneCleanupPipeline` on every scene unload to prevent stale scene-local subscribers from receiving events after their scene is gone.


<img width="629" height="692" alt="image" src="https://github.com/user-attachments/assets/515242a1-f06b-43b7-8e35-3ab633206395" />


---

### MVP Pattern (Presenter Layer)

The battle UI follows a strict **Model → Presenter → View** flow:

- **Model** — `GameEvents` static bus fires raw data events (`int gold`, `string message`).
- **Presenter** — `GamePresenter` subscribes to `GameEvents`, translates events into display decisions, and calls methods on `IGameView`. Zero knowledge of Unity components.
- **View** — `IGameView` defines three methods: `ShowStatusText`, `ShowLevelIndex`, `ShowGold`. `GameUIManager` implements this interface and is the only class allowed to write to `TextMeshPro` components.

The entire UI layer can be replaced or mocked by swapping the `IGameView` implementation.

<img width="1519" height="796" alt="image" src="https://github.com/user-attachments/assets/90bc8e7c-8465-4d9a-ac90-87bc58d15606" />


---

### Finite State Machine

See [AI & Combat Logic](#ai--combat-logic) for the full FSM diagram. Key design decisions:

- State objects are **shared static instances** — no per-unit allocation.
- `Dead` and `Victory` states are handled inline in `BaseUnit.Update()` to avoid unnecessary interface dispatch.
- Visual updates (`UpdateVisuals()`) run **before** the battle-logic gate, ensuring animations play during the pre-battle placement phase.


<img width="683" height="124" alt="image" src="https://github.com/user-attachments/assets/f66101a4-fb5b-42f8-9e90-2c38253b6f26" />
<img width="318" height="87" alt="image" src="https://github.com/user-attachments/assets/792d9df2-d8f4-4540-9202-cdb6e3d777e6" />


---

### Save System (Binary + AES)

Player progression (gold, inventory, army composition, active level) is securely saved via `SaveManager`.

- **Binary + AES-256 Encryption** — Data is serialised to JSON, encrypted with AES-256-CBC, and written as raw bytes to `gamesave.dat` in `Application.persistentDataPath`.
- **SHA-256 Checksum** — Computed from the full JSON string and embedded before every write. Validated on every load to detect tampered files.
- **Automatic Backup** — Before every write, the current file is copied to `gamesave.dat.bak`. If the primary file fails on load, the backup is tried automatically.
- **Versioning & Migration** — `CurrentSaveVersion = 2`. `SaveMigrationService` applies migrations step-by-step (v1→v2: added inventory list, language code, null-safe army slots). No data is ever discarded.
- **Meta Fields** — Every save carries `saveVersion`, `savedAt` (ISO 8601 UTC), `totalPlayTimeSeconds`, and `saveCount`.

<img width="810" height="633" alt="image" src="https://github.com/user-attachments/assets/6aa4627e-4717-4009-b9ff-147bb4545157" />

---

### Localization System

All in-game text is managed through a custom JSON-based system via `LocalizationManager`. Currently supports **Turkish (TR, default)** and **English (EN)**.

- **No magic strings** — All keys are `const string` fields in `LocalizationKeys.cs` following a `domain.object` naming convention (`ui.start_button`, `battle.level_cleared`, `item.sword`).
- **Two-level fallback chain** — Current language → English → raw key string.
- **Live switching** — `LocalizationManager.SetLanguage(code)` swaps the active dictionary and fires `OnLanguageChanged` so all `LocalizedText` components refresh simultaneously without manual wiring.
- **Parameterised keys** — `{0}`, `{1}` placeholders in JSON resolved via `string.Format`.

+<img width="216" height="211" alt="image" src="https://github.com/user-attachments/assets/2658394d-370f-464a-ac27-0d6c15ad54f8" />
<img width="250" height="246" alt="image" src="https://github.com/user-attachments/assets/e9964f69-f36c-462e-9ca3-b47d88702ca3" />
<img width="231" height="283" alt="image" src="https://github.com/user-attachments/assets/5dbb6199-dc17-4969-8228-df50bd1b4f7d" />
tr.json + en.json


---

### Easing Library & Animation Curves

**`EasingLibrary`** — 22 easing functions typed by the `EaseType` enum (Quad, Cubic, Bounce, Elastic, and more).

**`StatProgressionSO`** — Replaces every flat numeric stat with an `AnimationCurve` evaluated on a normalised level value `[0, 1]`. Designers reshape curves in the Inspector with no code changes.

```
healthCurve         Linear(0→50,  1→500)   — HP scaling
damageCurve         Linear(0→10,  1→100)   — damage scaling
attackCooldownCurve Linear(0→2s,  1→0.5s)  — attack speed
goldRewardCurve     Linear(0→10g, 1→200g)  — economy curve
movementCurve       EaseInOut(0,0, 1,1)    — unit acceleration profile
spawnPacingCurve    EaseInOut(0,2s, 1,0.3s) — enemy trickle → assault
```


<img width="814" height="335" alt="image" src="https://github.com/user-attachments/assets/684fad38-ab25-44e1-862f-fe68b9e8a252" />

<img width="814" height="357" alt="image" src="https://github.com/user-attachments/assets/b44d3aeb-ab71-40ec-8314-d883055f1ac1" />

---

### Addressables & Async Loading

- **Lazy Loading** — Equipment sprites are stored as `AssetReferenceT<Sprite>` and only loaded into memory when the slot is opened. Handles are registered with `SceneCleanupPipeline` and released automatically on scene unload.
- **Sprite Bundles** — Five Addressable groups (`Units`, `Items`, `UI`, `Arena`, `Audio`), each bundled as `PackTogether`.
- **Async Scene Loading** — `SceneLoader.TransitionTo()` fades to black → cleans up → loads additively with `allowSceneActivation = false` (holds at 90% until ready) → activates → fades back in. An `OnLoadProgress` action fires at each step for accurate progress bar display.
- **Scene Cleanup Pipeline** — A deterministic 7-step `SceneCleanupPipeline` runs before every unload: release Addressable handles → return pooled units → stop VFX → log sound state → clear GameEvents → `UnloadUnusedAssets` → `GC.Collect()`.

<img width="987" height="566" alt="image" src="https://github.com/user-attachments/assets/08679c8a-b403-4594-949c-6caac3de830e" />

---

### Input System

`GameInputHandler` is the sole integration point with the **Unity New Input System**. It wraps the auto-generated `InputSystem_Actions` class and exposes clean C# events — no other class references `InputSystem_Actions` directly.

| Action | Keyboard | Gamepad |
|---|---|---|
| Confirm / Click | Space / Left Click | A / Cross |
| Cancel | Escape / Right Click | B / Circle |
| Ready (WAR!) | Enter | Start |
| Speed Cycle | Tab | RB |
| Navigate | WASD / Arrow Keys | Left Stick / D-Pad |

Two action maps are toggled: `SwitchToUI()` (Upgrade scene — drag-and-drop) and `SwitchToPlayer()` (Grid scene — battle controls). `GamepadCursor` drives a `VirtualMouseInput` component from the left stick, enabling UGUI drag-and-drop to work identically on gamepad and mouse.

<img width="422" height="169" alt="image" src="https://github.com/user-attachments/assets/67d4c12f-e3a6-4c03-924b-2379f55a7214" />
<img width="423" height="153" alt="image" src="https://github.com/user-attachments/assets/4572aba9-d1af-4bdc-a197-7a90d3e081c4" />


---

### Debug & QA Tools

`DebugToolsWindow` is a custom `EditorWindow` (Tools → TDEV → Debug Tools, `Ctrl+Shift+D`) with six collapsible sections, functional in both Edit Mode and Play Mode.

- **Quick Stage Start** — Jump to any level index instantly; Previous/Next level navigation via Reflection; Auto Start Battle toggle.
- **Quick Inventory Test** — Unlock all 8 pawn slots; fill random equipment into all army slots; clear all equipment; add/reset gold.
- **Quick Time Test** — `Time.timeScale` slider (0–10) with preset buttons (0.25×, 0.5×, 1×, 2×, 4×, 8×). Live FPS and Fixed Timestep readout.
- **Stress Testing** — Spawn 1–64 units on random grid nodes (both teams or enemy only); Clear All Units with full pool + state reset. Live unit count display per team.


<table>
  <tr>
    <td valign="top">
      <img width="591" height="832" alt="image" src="https://github.com/user-attachments/assets/0cfb6b6e-43ed-43c2-9cd2-40706d432b46" />
    </td>
    <td valign="top">
      <img width="579" height="441" alt="image" src="https://github.com/user-attachments/assets/5405812a-9eaf-4310-bc9e-19e8f5ccbc60" />
    </td>
  </tr>
</table>

---

## 📁 Project Structure <a name="project-structure"></a>

```
Assets/
├── Scripts/
│   ├── Core/          // Managers, Bootstrap, Singletons, GameEvents
│   ├── Units/         // BaseUnit, MeleeUnit, RangedUnit, FSM States
│   ├── Data/          // ScriptableObjects (LevelData, UnitData, Equipment)
│   ├── UI/            // Presenter, IGameView, Drag-Drop Systems
│   ├── Systems/       // Save, Localization, VFX, Easing, SceneLoader
│   ├── Managers/      // UnitFactory, ProjectileFactory, SoundManager
│   └── Editor/        // DebugToolsWindow, AddressablesGroupSetup
├── Prefabs/
│   ├── Units/         // Player and Enemy unit prefabs
│   ├── UI/            // Menus, popups, drag items
│   └── VFX/           // Particle systems
├── GameData/
│   ├── Levels/        // 212 LevelDataSO instances
│   ├── Units/         // EnemyFormationSO, PlayerArmyDataSO assets
│   └── Equipment/     // EquipmentDataSO assets (Gun, Sword, Shield…)
└── AddressableAssets/ // Sprites (Items, Units, UI, Arena), Audio
```

---

## 🔗 System Dependency Map <a name="system-dependency-map"></a>

Systems are designed with **strictly unidirectional** dependencies:

```
UnitSpawner       ──►  GridManager, UnitFactory, LevelManager
BattleManager     ──►  GridManager, GameEvents
GamePresenter     ──►  GameEvents, IGameView
RangedUnit        ──►  ProjectileFactory
GameUIManager     ──►  Unity UI components only (zero game logic)
VFXManager        ──►  GameEvents (subscribes), ParticleSystem pools
SoundManager      ──►  GameEvents (subscribes), AudioSource pool
SceneLoader       ──►  SceneCleanupPipeline, FaderUI
SaveManager       ──►  GameSaveService (persists), SaveMigrationService
```

---

## 🔢 Key Constants Reference <a name="key-constants-reference"></a>

| Constant | Value | Description |
|---|---|---|
| `GridXSize` | `8` | Number of columns on the battlefield |
| `GridYSize` | `8` | Number of rows (lanes) on the battlefield |
| `PlayerSpawnColumn` | `0` | Far-left column where player units spawn |
| `EnemyPrimaryColumn` | `7` | Far-right column where enemies spawn |
| `EnemyOverflowColumn` | `6` | Secondary enemy column when count exceeds 8 |
| `MaxArmySize` | `8` | Maximum units per side on the board |
| `HysteresisFactor` | `0.5625f` | AI target-switch tolerance (`0.75²` units) |
| `AudioInitialPool` | `10` | Starting AudioSource pool size |
| `AudioMaxPool` | `20` | Hard cap on simultaneous audio sources |
| `CurrentSaveVersion` | `2` | Save schema version — drives migration |

---

## 🎨 Designer Guides <a name="designer-guides"></a>

Step-by-step instructions for adding content **without touching any code**:

### Generating Campaign Levels via TDEV Level Generator

<img width="500" height="400" alt="Ekran görüntüsü 2026-06-02 042144" src="https://github.com/user-attachments/assets/5c8d8518-94ef-41e6-8301-afc2491b1c2a" />

Navigate to TDEV > Grid Level Generator from the top Unity menu to open the tool.

Expand the Enemy Unit Pool list and assign the enemy Scriptable Object files you want to include in the pool.

Under the Player Unit Setup section, drag and drop your player unit data into the Player Melee Data and Player Ranged Data fields.

In the Campaign Settings, set the total number of levels you wish to create via the Levels to Generate slider and define the Base Gold Reward.

Adjust the game's difficulty progression by modifying the Enemy Count Curve graph under Enemy Difficulty Curve (the X-axis represents normalized level progression, and the Y-axis sets the enemy count).

Once your setup is complete, click the Generate Diverse Levels button (e.g., Generate 500 Diverse Levels) at the bottom to batch-create your levels.



### Adding a New Unit Type

1. In the Project window, right-click inside `GameData/Units/`.
2. Select **Create > AutoBattler > Units > Melee Unit Data** (or Ranged).
3. Name the SO and fill in base stats (Health, Damage, Speed, Range) via the Inspector.
4. Drag the unit prefab from `Prefabs/Units/` into the `unitPrefab` field.
5. Optionally assign a `StatProgressionSO` to enable level-based scaling.

<img width="351" height="330" alt="image" src="https://github.com/user-attachments/assets/c0925707-4908-4015-805e-de8215295a55" />
<img width="298" height="296" alt="image" src="https://github.com/user-attachments/assets/26293fa5-c761-4858-bf7c-0cbbc7912b5f" />


### Adding a New Level

1. Create a new `LevelDataSO` via **Create > TDEV >  Level Data**.
2. Create a new `EnemyFormationSO` to define the enemy grid layout, and assign it to the Level Data.
3. Set `goldReward` for level completion and the melee/ranged unit limits for the player.
4. Add the Level Data to the end of the Level List inside the `LevelManager` prefab.

<img width="401" height="335" alt="image" src="https://github.com/user-attachments/assets/34c680d8-2df3-4013-997c-b6e30c43f919" />
<img width="297" height="180" alt="image" src="https://github.com/user-attachments/assets/740f70c3-a975-43e5-b539-7094547f5a28" />


### Adding a New Equipment Item

1. Create the item via **Create > AutoBattler > Equipment > Equipment Data**.
2. Select the slot from the `EquipmentSlot` enum (Helmet, Vest, Pants, Weapon, Shield).
3. Input the stat bonuses (`bonusHealth`, `bonusDamage`, `bonusAttackSpeed`).
4. Add the item's sprite to the **"Items"** Addressables group, then link it to the `spriteReference` field.

<img width="332" height="80" alt="image" src="https://github.com/user-attachments/assets/5c8ae426-4cc3-46ba-ae8e-6e3451dc50d2" />
<img width="296" height="146" alt="image" src="https://github.com/user-attachments/assets/89ac1ed8-1f90-4943-84b6-20200337131a" />
---

## 📸 Screenshots <a name="screenshots"></a>

### 🎮 Gameplay — Battle Phase
<img width="1920" height="1080" alt=" Grid Overview" src="https://github.com/user-attachments/assets/a46e8671-630a-4b65-a258-9d9edff60927" />


### 🗂️ Upgrade Scene — Equipment & Army Management
<img width="1920" height="1080" alt="Upgrade Scene" src="https://github.com/user-attachments/assets/b85288ea-4f71-4e37-820e-aa9bc20269ea" />


### 🛒 Pawn Shop & Loot Box
<img width="1920" height="1080" alt="Pawn Shop" src="https://github.com/user-attachments/assets/1f186a08-1926-4486-8713-3ed2bf2fd8ee" />


