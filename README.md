# ⚔️ Grid Auto-Battler

> **A Grid-Based Side-Scroller Auto-Battler** built in Unity 6 with URP 2D.
>
> Inspired by management-strategy titles like *Dwarves: Glory, Death and Loot* and *Totally Accurate Battle Simulator* — tactical army preparation meets fully automated, physics-driven combat.

**Studio:** TDEVGAMES
**Engine:** Unity 6000.4.6f1 · Universal Render Pipeline 2D
**Language:** C# · .NET Standard 2.1

---

## 📖 Table of Contents

1. [Game Overview](#-game-overview)
2. [Core Gameplay Loop](#-core-gameplay-loop)
3. [Grid & Spawning System](#-grid--spawning-system)
4. [AI & Combat Logic](#-ai--combat-logic)
5. [Equipment & Upgrade System](#-equipment--upgrade-system)
6. [Technical Architecture](#-technical-architecture)
   - [Architecture Quality Matrix](#architecture-quality-matrix)
   - [SOLID Principles](#solid-principles)
   - [Data-Driven Design (Scriptable Objects)](#data-driven-design-scriptable-objects)
   - [Object Pooling (5 Pools)](#object-pooling-5-pools)
   - [GameEvents Bus (Logic–UI Decoupling)](#gameevents-bus-logicui-decoupling)
   - [MVP Pattern (Presenter Layer)](#mvp-pattern-presenter-layer)
   - [Finite State Machine](#finite-state-machine)
   - [Save System (Binary + AES)](#save-system-binary--aes)
   - [Localization System](#localization-system)
   - [Easing Library](#easing-library)
   - [VFX Manager](#vfx-manager)
   - [Sound Manager](#sound-manager)
   - [Addressables & Async Loading](#addressables--async-loading)
7. [Project Structure](#-project-structure)
8. [System Dependency Map](#-system-dependency-map)
9. [Key Constants Reference](#-key-constants-reference)
10. [Designer Guides](#-designer-guides)
    - [Adding a New Unit Type](#adding-a-new-unit-type)
    - [Adding a New Level](#adding-a-new-level)
    - [Adding a New Equipment Item](#adding-a-new-equipment-item)
11. [Screenshots](#-screenshots)
12. [Planned Features & Roadmap](#-planned-features--roadmap)

---

## 🎮 Game Overview

Grid Auto-Battler is a **2D side-scrolling tactical auto-battler**. The player acts as a battlefield commander: they configure their army composition and equipment loadout before each engagement, then watch their soldiers fight autonomously using a custom AI system.

The visual perspective is a **lateral side-scroller** — units face each other across a horizontal battlefield, with the player's forces on the left and enemy formations on the right. Combat is fully physics-driven via Unity's 2D Rigidbody system, giving units natural push-and-slide interactions as they close in on their targets.

The game draws inspiration from the management-strategy genre (*Dwarves: Glory, Death and Loot*, *Northgard*, *Totally Accurate Battle Simulator*) — the player's strategic decisions during the preparation phase are the primary skill expression, while the battle phase is a satisfying, hands-off spectacle.

**Three Scenes:**

| Scene | Purpose |
|---|---|
| `StartScene` | Main menu, settings panel, game entry point |
| `UpgradeScene` | Army management, equipment drag-drop, pawn shop, loot boxes |
| `GridScene` | Main battle arena — preparation + combat + resolution |

---

## 🔄 Core Gameplay Loop

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

## 🗺️ Grid & Spawning System

### The 8×8 World-Space Grid

The entire battlefield is defined by a single `GridManager` singleton that owns an authoritative `GridNode[8, 8]` data model. The grid is generated **once in `Awake()`** and never changes at runtime.

| Property | Value |
|---|---|
| Dimensions | 8 columns × 8 rows |
| Origin `[0,0]` world center | `(1.3, 1.3, 0)` |
| Cell size (center-to-center) | `2.6` Unity units |
| Column axis | X → left (0) to right (7) |
| Row axis | Y → bottom (0) to top (7) |

**`GridNode`** is a plain C# class (not a `MonoBehaviour`) that stores:
- `int X, Y` — grid coordinates
- `Vector3 WorldPosition` — pre-calculated world-space center (immutable after generation)
- `bool IsOccupied` — mutable occupancy flag set by `UnitSpawner`

The grid provides a `WorldToGrid()` utility for converting any world-space position to the nearest grid coordinates (used by the Lane Priority AI), and an `OnDrawGizmos()` overlay that renders the grid in the Scene view at all times — green for free nodes, red for occupied ones.

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

#### Player Spawning (`UnitSpawner.SpawnPlayerUnits`)

- **Column:** `X = 0` (far left) — `PlayerSpawnColumn = 0`
- **Order:** Melee units fill rows `Y = 0..7` first (front line), then Ranged units fill remaining rows above them
- **Capacity:** Maximum **8 units** (one per row)
- **Source data:** `LevelDataSO.meleeLimit`, `LevelDataSO.rangedLimit`, `LevelDataSO.meleeData`, `LevelDataSO.rangedData`

#### Enemy Spawning (`UnitSpawner.SpawnEnemyFormation`)

Enemy placement uses a **two-pass, two-column strategy** driven by `EnemyFormationSO`:

| Pass | Column | Rows | Capacity |
|---|---|---|---|
| Pass 1 — Primary | `X = 7` (`EnemyPrimaryColumn`) | `Y = 0..7` | 8 units |
| Pass 2 — Overflow | `X = 6` (`EnemyOverflowColumn`) | `Y = 0..7` | 8 units |
| **Total** | | | **16 units max** |

Formations exceeding 16 units log a designer warning and skip the excess — the two-column capacity is a hard limit enforced by the grid geometry.

---

## 🧠 AI & Combat Logic

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

**Key FSM design decisions:**
- State objects (`IdleState`, `MovingState`, `AttackingState`) are **shared static instances** — one object per state type, not per unit. This eliminates per-unit allocations for units that share the same state logic.
- `Dead` and `Victory` states have no `IUnitState` object — they are handled inline in `BaseUnit.Update()` to avoid unnecessary interface dispatch.
- Visual updates (`UpdateVisuals()`) run **before** the battle-logic gate, ensuring breathing and sway animations play during the pre-battle placement phase.

### Lane Priority Targeting System

`BattleManager.GetClosestTargetFor()` implements a **three-tier Lane Priority** system. A "lane" is a horizontal strip defined by a shared grid row (`Y` value) — units in the same lane face each other directly across the X axis.

```
┌──────────────────────────────────────────────────────────────────┐
│                  LANE PRIORITY TARGETING                         │
│                                                                  │
│  TIER 1 — Same Lane (seekerRow == candidateRow)                  │
│    → Find the closest living enemy in the exact same row.        │
│    → Most common case; units fight their direct counterpart.     │
│                                                                  │
│  TIER 2 — Adjacent Lanes (seekerRow ± 1, excluding same row)     │
│    → Only reached when the seeker's own lane is completely       │
│      empty (all enemies in that row are dead).                   │
│    → Finds the closest enemy in the row immediately above        │
│      or below, preventing units from marching all the way        │
│      across the board to find a target.                          │
│                                                                  │
│  TIER 3 — Global Fallback (entire opposing team)                 │
│    → Only reached when both the same lane AND adjacent lanes     │
│      are empty, or when GridManager is unavailable.              │
│    → Returns the absolute closest living enemy anywhere.         │
│                                                                  │
│  All distance comparisons use sqrMagnitude (no Mathf.Sqrt).      │
└──────────────────────────────────────────────────────────────────┘
```

### Local Physics Overlap Search

Before escalating to the global `BattleManager` query, each unit runs a **local `Physics2D.OverlapCircle`** scan (radius = `attackRange × 1.5`) using the modern non-allocating `ContactFilter2D` API. This catches nearby targets without iterating the full unit list, and uses a **hysteresis factor** (`0.5625` = `0.75²`) to prevent target-switching jitter — a new candidate must be significantly closer than the current target before a switch occurs.

### Crash-Safe Unit Removal (Two-Phase Queue)

Units that die during a `foreach` traversal are **not removed immediately**. `BattleManager` uses a `_pendingRemovals` queue:

1. **Phase 1 (`QueueUnitForRemoval`):** Called immediately on `OnDeath`. Unsubscribes the delegate, fires `GameEvents.UnitDied`, and adds the unit to `_pendingRemovals`.
2. **Phase 2 (`LateUpdate` flush):** After all `Update()` loops complete, the queue is flushed, lists are mutated safely, and `EvaluateBattleOutcome()` runs on clean data.

This prevents `InvalidOperationException: Collection was modified` without requiring list copies on every frame.

---

## 🛡️ Equipment & Upgrade System

The Upgrade Scene provides a full **drag-and-drop equipment management** interface for the player's army of up to 8 units.

### Equipment Data (`EquipmentDataSO`)

Each piece of equipment is a ScriptableObject with:
- **Identity:** `equipmentName`, `EquipmentSlot` (Helmet / Vest / Pants / Weapon / Shield)
- **Stat Bonuses:** `bonusHealth`, `bonusDamage`, `bonusAttackSpeed`
- **Visual:** `AssetReferenceT<Sprite>` — Addressable sprite loaded on demand

### Army Roster (`PlayerArmyDataSO`)

A persistent ScriptableObject shared between `UpgradeScene` and `GridScene`. Stores 8 `ArmySlot` entries, each containing:
- `BaseUnitDataSO baseUnitData` — the unit's base stats and prefab
- Five optional `EquipmentDataSO` references (helmet, vest, pants, weapon, shield)

### Pawn Shop (`PawnShopManager`)

- Players start with **1 unlocked pawn slot** (free)
- Additional slots cost **300 gold** each, up to a maximum of **8**
- Pawn count is persisted via `GameSaveService`
- `GameEvents.OnPawnCountChanged` notifies UI without direct coupling

### Loot Box System (`LootBoxManager`)

- Costs **200 gold** per box
- Spawns a random equipment UI prefab into the stash
- Visual open/close animation with configurable duration

### Drag-and-Drop UI

| Component | Responsibility |
|---|---|
| `UpgradeDragItemUI` | Handles pointer drag events, creates ghost image |
| `UpgradeCharacterDropZoneUI` | Accepts drops onto character equipment slots |
| `StashDropZoneUI` | Accepts drops back into the inventory stash |
| `ShowcasePawnController` | Manages 3D pawn showcase visibility |

---

## 🏗️ Technical Architecture

### Architecture Quality Matrix

| Grade | System / Pattern | Status |
|---|---|---|
| **A** | SOLID Principles | ✅ Fully applied across all systems |
| **A** | Abstraction | ✅ `BaseUnit`, `BaseUnitDataSO`, `IGameView`, `IUnitState` |
| **A** | Singleton | ✅ 9 singletons: `BattleManager`, `GridManager`, `LevelManager`, `UnitFactory`, `UnitSpawner`, `ProjectileFactory`, `GameSaveService`, `SoundManager`, `VFXManager` |
| **A** | ScriptableObject Architecture | ✅ Fully data-driven: units, levels, formations, equipment, stat progressions |
| **A** | Factory Pattern | ✅ `UnitFactory` + `ProjectileFactory` with `ObjectPool<T>` |
| **A** | Polymorphism | ✅ `BaseUnit` → `MeleeUnit` / `RangedUnit` hierarchy |
| **A** | Base Entity Definitions | ✅ `BaseUnit` + `BaseUnitDataSO` abstract hierarchy |
| **A** | Decoupling | ✅ `GameEvents` static event bus — zero direct cross-system references |
| **B** | Projectile Pooling | ✅ `ProjectileFactory` — one pool per unique prefab |
| **B** | Floating Text Pooling | ✅ `DamageTextManager` — pooled damage numbers |
| **B** | VFX Pooling | ✅ `VFXManager` — `ObjectPool<ParticleSystem>` per VFX type |
| **B** | Enemy Pooling | ✅ `UnitFactory` — one pool per unique unit prefab |
| **B** | Sound Pooling | ✅ `SoundManager` — `AudioSource` pool (10 initial, 20 max) |
| **B** | Spawn Rate / Data Control | ✅ `EnemyFormationSO` + `StatProgressionSO` curves |
| **B** | MVP Pattern | ✅ `GamePresenter` + `IGameView` + `GameUIManager` |
| **C** | Lazy Loading | ✅ Addressables async for equipment sprites |
| **C** | Memory Unload | ✅ Addressable handle release on unload |
| **C** | Sprite Bundles | ✅ Addressable groups: Arena, Audio, Items, UI, Units |
| **E** | Async Scene Loading | ✅ `LoadSceneAsync` with `allowSceneActivation` control |
| **G** | Binary Save System | ✅ `SaveManager` — AES-256 encrypted binary + SHA-256 checksum |
| **G** | Save Meta Data | ✅ `saveVersion`, `savedAt`, `saveCount`, `totalPlayTimeSeconds` |
| **G** | Save Versioning | ✅ `SaveManager.CurrentSaveVersion = 2` |
| **G** | Save Migration | ✅ `SaveMigrationService` — step-by-step v1→v2 migration |
| **J** | Localization | ✅ `LocalizationManager` — TR/EN JSON files, fallback chain |
| **J** | Localization Key Naming | ✅ `LocalizationKeys.cs` — typed constants, no magic strings |
| **F** | Easing Library | ✅ `EasingLibrary` — 22 easing functions (Quad, Cubic, Bounce, Elastic…) |
| **F** | Stat Progression Curves | ✅ `StatProgressionSO` — `AnimationCurve` per stat |
| **F** | Spawn Pacing Curve | ✅ `spawnPacingCurve` in `StatProgressionSO` |
| **F** | Movement Curve | ✅ `movementCurve` in `StatProgressionSO` |

---

### SOLID Principles

| Principle | Implementation |
|---|---|
| **Single Responsibility** | `UnitSpawner` only spawns. `BattleManager` only manages battle state and targeting. `GameUIManager` only updates UI. `UnitFactory` only manages pools. `GamePresenter` only translates events to view calls. Each class has one reason to change. |
| **Open/Closed** | `BaseUnit` is abstract and open for extension (`MeleeUnit`, `RangedUnit`) without modifying the base. `BaseUnitDataSO` is extended by `MeleeUnitDataSO` and `RangedUnitDataSO`. |
| **Liskov Substitution** | `MeleeUnit` and `RangedUnit` are fully substitutable for `BaseUnit` anywhere in the codebase (`BattleManager.playerUnits`, `BattleManager.enemyUnits`, `UnitFactory.CreateUnit`). |
| **Interface Segregation** | `IDamageable` and `IAttacker` are separate, minimal interfaces. `IUnitState` defines only `Enter`, `Execute`, `Exit`. `IGameView` defines only the UI surface the Presenter needs. |
| **Dependency Inversion** | `BattleManager` depends on the `BaseUnit` abstraction, not concrete unit types. `Projectile` depends on `IObjectPool<Projectile>`. `GamePresenter` depends on `IGameView`, not `GameUIManager`. |

---

### Data-Driven Design (Scriptable Objects)

All game content is defined in **ScriptableObject assets**, completely decoupled from scene objects. Designers can create and tune levels, units, and equipment without touching code.

```
Assets/Scripts/Data/
├── BaseUnitDataSO.cs              ← Abstract base: name, prefab, health, damage, speed, range, cooldown
│   ├── MeleeUnitDataSO.cs         ← Extends base: swingAngle, swingDuration
│   └── RangedUnitDataSO.cs        ← Extends base: projectilePrefab, projectileSpeed
│
├── LevelDataSO.cs                 ← meleeLimit, rangedLimit, meleeData ref, rangedData ref,
│                                     enemyFormation ref, goldReward
│
├── StatProgressionSO.cs           ← AnimationCurve per stat (health, damage, speed, cooldown,
│                                     range, gold reward, spawn pacing, movement)
│
├── Units/
│   ├── EnemyFormationSO.cs        ← List<UnitPlacement> { unitData, offset }
│   ├── PlayerArmyDataSO.cs        ← 8 ArmySlot entries (baseUnitData + 5 equipment refs)
│   └── ScriptableObject/          ← E-Bot, E-Bot-Ranged (×4 variants), P-Bot, P-Bot-Ranged
│
├── Equipment/
│   ├── EquipmentDataSO.cs         ← equipmentName, slot, bonusHealth, bonusDamage,
│   │                                 bonusAttackSpeed, Addressable spriteReference
│   ├── EquipmentSlot.cs           ← Enum: Helmet, Vest, Pants, Weapon, Shield
│   └── ScriptableObject/          ← Gun, Halmet, Pants, Shield (×3), Sword, Vest
│
└── Text/
    └── DamageTextDataSO.cs        ← Floating damage number visual configuration
```

---

### Object Pooling (5 Pools)

The project uses **Unity's `UnityEngine.Pool.ObjectPool<T>`** for all runtime-spawned objects, following an identical architectural pattern across all factories.

#### `UnitFactory` — Unit Pooling

```
UnitFactory
├── Dictionary<GameObject, ObjectPool<BaseUnit>>  _pools
│     └── One pool per unique unit prefab
└── Dictionary<BaseUnit, ObjectPool<BaseUnit>>    _unitToPool
      └── Reverse lookup: instance → owning pool (for O(1) release)

Flow:
  UnitSpawner.CreateUnit(data, pos, team)
    → UnitFactory.CreateUnit()
      → GetOrCreatePool(data.unitPrefab).Get()   ← activates from pool
      → unit.Initialize(data, team)
      → BattleManager.RegisterUnit(unit)

  BaseUnit.Die()
    → UnitFactory.ReleaseUnit(unit)
      → _unitToPool[unit].Release(unit)          ← returns to pool (deactivates)
```

#### `ProjectileFactory` — Projectile Pooling

One pool per unique projectile prefab, shared across all units that fire it (previously each `RangedUnit` had its own pool — 20 separate pools with no sharing).

```
ProjectileFactory
├── Dictionary<GameObject, ObjectPool<Projectile>>  _pools
└── Dictionary<Projectile, ObjectPool<Projectile>>  _projectileToPool

Flow:
  RangedUnit.Attack()
    → ProjectileFactory.Get(rangedData.projectilePrefab)  ← activates from pool
    → projectile.Launch(target, damage, speed, team)

  Projectile.ReturnToPool()  (on hit or lifetime expiry)
    → _pool.Release(this)                                 ← returns to pool
    → Projectile.ResetState()                             ← clears velocity, trail
```

#### `DamageTextManager` — Floating Text Pooling

Pooled `DamageText` instances spawned above units on hit. Automatically returned after the float-and-fade animation completes.

#### `VFXManager` — Particle Effect Pooling

```
VFXManager
├── Dictionary<VFXType, ObjectPool<ParticleSystem>>  _pools
│     └── One pool per VFXType (UnitDeath, BattleWin, …)
└── Dictionary<ParticleSystem, ObjectPool<…>>        _psToPool

Flow:
  VFXManager.PlayVFX(VFXType.UnitDeath, position)
    → pool.Get()                    ← activates ParticleSystem from pool
    → ps.Play()
    → StartCoroutine(ReturnWhenFinished)  ← auto-returns when particle stops
```

Pool warm-up runs at `Awake()` to avoid first-frame spikes.

#### `SoundManager` — AudioSource Pooling

```
SoundManager
├── Queue<AudioSource>  _pool      ← idle sources
└── List<AudioSource>   _active    ← currently playing sources

Flow:
  SoundManager.PlaySound(SoundType.WeaponMeleeSwing)
    → GetFromPool()                ← dequeues idle AudioSource
    → src.clip = randomClip        ← random clip from SoundEntry
    → src.pitch = Random(min, max) ← pitch variation
    → src.Play()

  Update() scan:
    → if (!src.isPlaying) ReturnToPool(src)
```

**Pool configuration:** `initialPoolSize = 10`, `maxPoolSize = 20`. If the pool is exhausted, the oldest active source is forcibly reclaimed.

---

### GameEvents Bus (Logic–UI Decoupling)

`GameEvents` is a **static C# event bus** — no `MonoBehaviour`, no scene 

...no MonoBehaviour, no scene references. It enables loose coupling by allowing different systems to communicate without direct references to each other.C#public static class GameEvents
{
    // Fired when a unit dies.
    public static Action<BaseUnit> OnUnitDied;

    // Fired when the battle phase starts.
    public static Action OnBattleStarted;

    // Fired when the player wins the level.
    public static Action<int> OnLevelWin; // int: goldReward
}
MVP Pattern (Presenter Layer)The user interface (UI) and game logic are strictly separated using the MVP (Model-View-Presenter) pattern. This ensures that UI modifications never break the core game logic.Model: Systems like BattleManager, LevelManager, and SaveManager that process data and enforce game rules.View: GameUIManager, which implements the IGameView interface. It only updates visual components on the screen (buttons, texts, health bars) and makes zero logical decisions.Presenter: GamePresenter. It listens to game events (GameEvents) and calls IGameView methods to update the screen based on the data it receives.C#public class GamePresenter : MonoBehaviour
{
    private IGameView _view;

    private void Awake()
    {
        // Inject the view component.
        _view = GetComponent<IGameView>();
    }

    private void OnEnable()
    {
        // Subscribe to global game events.
        GameEvents.OnLevelWin += HandleLevelWin;
    }

    private void HandleLevelWin(int reward)
    {
        // Update UI through the view interface.
        _view.ShowVictoryScreen(reward);
    }
}
Save System (Binary + AES)Player progression (gold, inventory, army composition, active level) is securely saved to the local device via the SaveManager.Binary Serialization: Data is written in a binary format instead of standard JSON for reduced file size and faster read/write speeds.AES-256 Encryption: Save files are heavily encrypted to prevent external tampering or cheating.SHA-256 Checksum: An added verification layer to check if the save file has been corrupted.Versioning & Migration: The system currently uses CurrentSaveVersion = 2. If an older save is detected, the SaveMigrationService automatically upgrades the data step-by-step to the latest version.Localization SystemAll in-game text (currently EN/TR) is managed through JSON-based files via the LocalizationManager."Magic strings" are strictly prohibited in the codebase.Text calls are made using type-safe constants from LocalizationKeys.cs.On startup, the system automatically assigns the language based on the device's OS. If unsupported, it falls back to English.Easing LibraryInstead of relying on Unity's AnimationCurve for UI animations and floating effects, a custom mathematical EasingLibrary is used. It contains 22 different easing functions (Quad, Cubic, Bounce, Elastic, etc.). This approach reduces memory allocation and ensures highly performant tweening operations.Addressables & Async LoadingTo keep RAM usage low, heavy assets—especially equipment sprites—are managed using the Unity Addressables system.Async Loading: When a sword appears in the inventory, its sprite is loaded into memory on-the-fly asynchronously.Memory Cleanup: When equipment is deleted or sold, Addressables.Release() is called to immediately free up memory.All scenes (StartScene, UpgradeScene, GridScene) are loaded using LoadSceneAsync to prevent the main thread from freezing during transitions.📁 7. Project StructureThe project's codebase and assets are organized to maximize modularity:PlaintextAssets/
├── Scripts/
│   ├── Core/          // Managers, Bootstrap, Singletons
│   ├── Units/         // BaseUnit, MeleeUnit, RangedUnit, AI Logic
│   ├── Data/          // ScriptableObjects (LevelData, UnitData)
│   ├── UI/            // Presenter, View, Drag-Drop Systems
│   ├── Pooling/       // Factory classes, ObjectPool implementations
│   └── Utils/         // EasingLibrary, Constants, Extensions
├── Prefabs/
│   ├── Units/         // Player and Enemy unit prefabs
│   ├── UI/            // Menus, popups, drag items
│   └── VFX/           // Particle systems
├── ScriptableObjects/ // Created instances of data (Levels, Equipments)
└── AddressableAssets/ // Sprites, audio clips, localized JSONs
🔗 8. System Dependency MapSystems are designed with strictly unidirectional dependencies:UnitSpawner ➔ GridManager, UnitFactory, LevelManagerBattleManager ➔ GridManager, GameEventsGamePresenter ➔ GameEvents, IGameViewRangedUnit ➔ ProjectileFactoryGameUIManager ➔ Knows only Unity UI components. Contains zero game logic.🔑 9. Key Constants ReferenceCore parameters that should not be hard-modified in code during balancing:ConstantDescriptionGridXSize = 8Number of vertical columns on the battlefield.GridYSize = 8Number of horizontal rows (lanes) on the battlefield.PlayerSpawnColumn = 0The far-left column where player units are spawned.EnemyPrimaryColumn = 7The far-right column where enemy units are spawned.EnemyOverflowColumn = 6The secondary column used if enemy count exceeds 8.MaxArmySize = 8Maximum number of units a single side can have on the board at once.HysteresisFactor = 0.5625fTolerance for AI target switching (square of 0.75 units distance).🛠️ 10. Designer GuidesStep-by-step instructions for adding content without touching the code:Adding a New Unit TypeIn the Project window, right-click inside ScriptableObjects/Units/.Select Create > AutoBattler > Units > Melee Unit Data (or Ranged).Name the SO and input base stats (Health, Damage, Speed) via the Inspector.Drag and drop the visual unit prefab (from Prefabs/Units) into the unitPrefab field.Adding a New LevelCreate a new LevelDataSO via Create > AutoBattler > Levels > Level Data.Create a new EnemyFormationSO to dictate the enemy layout for this stage, and assign it to the Level Data.Set the goldReward amount given upon level completion.Add the newly created Level Data to the end of the Level List inside the LevelManager prefab.Adding a New Equipment ItemCreate the item via Create > AutoBattler > Equipment > Equipment Data.Select the item type from the EquipmentSlot enum (Helmet, Vest, Weapon, etc.).Input the stat bonuses the item will provide.Add the item's Sprite to the "Items" group in the Addressables window, then link it to the spriteReference field in the SO.🖼️ 11. Screenshots(In-development in-game screenshots, inventory UI, and battlefield visuals will be added here.)🚀 12. Planned Features & RoadmapUpcoming features and architectural expansions:Boss Fights: Large-scale units with custom multi-phase animations, AoE (Area of Effect) abilities, and unique AI behaviors.Set Bonuses: Synergy system that grants extra passive stats when equipping multiple items from the same set (e.g., Knight Set).Online Leaderboard: Server integration allowing players to compare their highest cleared stage against the global player base.Advanced Formation System: Unlocking the ability for the player to place their units freely across the first 3 columns, rather than being restricted to Column 0.
