<<<<<<< HEAD
# ⚔️ Grid Auto-Battler

> **A Grid-Based Side-Scroller Auto-Battler** built in Unity 6 with URP 2D.
> Inspired by management-strategy titles like *Dwarves: Glory, Death and Loot* — tactical army preparation meets fully automated, physics-driven combat.

**Studio:** TDEVGAMES
**Engine:** Unity 6000.4.6f1 · Universal Render Pipeline 2D
**Language:** C# · .NET Standard 2.1

---

## 📖 Table of Contents

1. [Game Overview](#-game-overview)
2. [Core Gameplay Loop](#-core-gameplay-loop)
3. [Grid & Spawning System](#-grid--spawning-system)
4. [AI & Combat Logic](#-ai--combat-logic)
5. [Technical Architecture](#-technical-architecture)
   - [SOLID Principles](#solid-principles)
   - [Data-Driven Design (Scriptable Objects)](#data-driven-design-scriptable-objects)
   - [Object Pooling](#object-pooling)
   - [GameEvents Bus (Logic–UI Decoupling)](#gameevents-bus-logicui-decoupling)
   - [Finite State Machine](#finite-state-machine)
6. [Project Structure](#-project-structure)
7. [System Dependency Map](#-system-dependency-map)
8. [Resuming Development](#-resuming-development)

---

## 🎮 Game Overview

Grid Auto-Battler is a **2D side-scrolling tactical auto-battler**. The player acts as a battlefield commander: they configure their army composition before each engagement, then watch their soldiers fight autonomously using a custom AI system.

The visual perspective is a **lateral side-scroller** — units face each other across a horizontal battlefield, with the player's forces on the left and enemy formations on the right. Combat is fully physics-driven via Unity's 2D Rigidbody system, giving units natural push-and-slide interactions as they close in on their targets.

The game draws inspiration from the management-strategy genre (*Dwarves: Glory, Death and Loot*, *Northgard*, *Totally Accurate Battle Simulator*) — the player's strategic decisions during the preparation phase are the primary skill expression, while the battle phase is a satisfying, hands-off spectacle.

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

Each spawned unit's `GridNode.IsOccupied` flag is set to `true` immediately after placement, keeping the grid's logical state in sync with the visual state.

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

## 🏗️ Technical Architecture

### SOLID Principles

| Principle | Implementation |
|---|---|
| **Single Responsibility** | `UnitSpawner` only spawns. `BattleManager` only manages battle state and targeting. `GameUIManager` only updates UI. `UnitFactory` only manages pools. Each class has one reason to change. |
| **Open/Closed** | `BaseUnit` is abstract and open for extension (`MeleeUnit`, `RangedUnit`) without modifying the base. `BaseUnitDataSO` is extended by `MeleeUnitDataSO` and `RangedUnitDataSO`. |
| **Liskov Substitution** | `MeleeUnit` and `RangedUnit` are fully substitutable for `BaseUnit` anywhere in the codebase (`BattleManager.playerUnits`, `BattleManager.enemyUnits`, `UnitFactory.CreateUnit`). |
| **Interface Segregation** | `IDamageable` and `IAttacker` are separate, minimal interfaces. `IUnitState` defines only `Enter`, `Execute`, `Exit`. |
| **Dependency Inversion** | `BattleManager` depends on the `BaseUnit` abstraction, not concrete unit types. `Projectile` depends on `IObjectPool<Projectile>`, not `ProjectileFactory` directly. |

---

### Data-Driven Design (Scriptable Objects)

All game content is defined in **ScriptableObject assets**, completely decoupled from scene objects. Designers can create and tune levels without touching code.

```
Assets/Scripts/Data/
├── BaseUnitDataSO.cs          ← Abstract base: name, prefab, health, damage, speed, range, cooldown
│   ├── MeleeUnitDataSO.cs     ← Extends base: swingAngle, swingDuration
│   └── RangedUnitDataSO.cs    ← Extends base: projectilePrefab, projectileSpeed
│
├── LevelDataSO.cs             ← meleeLimit, rangedLimit, meleeData ref, rangedData ref,
│                                 enemyFormation ref, goldReward
│
├── EnemyFormationSO.cs        ← List<UnitPlacement> { unitData, offset }
│
└── DamageTextDataSO.cs        ← Floating damage number visual configuration

Assets/Scripts/Data/Units/
├── P-Bot.asset                ← Player melee unit data
├── P-Bot-Ranged.asset         ← Player ranged unit data
├── E-Bot.asset                ← Enemy melee unit data
└── E-Bot-Ranged.asset         ← Enemy ranged unit data

Assets/GameData/Levels/
├── NewLevelData.asset         ← Level 1 configuration
├── NewLevelData 1.asset       ← Level 2 configuration
├── Test.asset                 ← Test level
└── Test 1.asset               ← Test level variant
```

**Adding a new level** requires only:
1. Create a new `EnemyFormationSO` asset and populate its unit list.
2. Create a new `LevelDataSO` asset, assign the formation, set limits and reward.
3. Add the asset to `LevelManager.levels` list in the Inspector.
4. Zero code changes required.

---

### Object Pooling

The project uses **Unity's `UnityEngine.Pool.ObjectPool<T>`** for both units and projectiles, following an identical architectural pattern in both factories.

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

Mirrors `UnitFactory` exactly. Previously, each `RangedUnit` instance maintained its own `ObjectPool<Projectile>` — with 20 ranged units that meant 20 separate pools with no sharing. The refactor consolidates to **one pool per unique projectile prefab**, shared across all units that fire it.

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

**Pool configuration:** `defaultCapacity = 10`, `maxSize = 50` (units) / `60` (projectiles). The `collectionCheck: true` flag catches double-release bugs in the Editor.

---

### GameEvents Bus (Logic–UI Decoupling)

`GameEvents` is a **static C# event bus** — no `MonoBehaviour`, no scene dependency, no `FindObjectOfType`. It enforces a strict one-way data flow:

```
  Gameplay Systems                GameEvents Bus              UI / Reaction Systems
  ─────────────────               ──────────────              ─────────────────────
  BattleManager         ──────►  OnBattleStarted    ──────►  (any subscriber)
  BattleManager         ──────►  OnLevelWin         ──────►  LevelManager.HandleLevelWin()
  BattleManager         ──────►  OnLevelLose        ──────►  LevelManager.HandleLevelLose()
  BattleManager         ──────►  OnUnitSpawned      ──────►  (analytics, tutorial)
  BattleManager         ──────►  OnUnitDied         ──────►  (kill tracking, VFX)
  LevelManager          ──────►  OnLevelIndexChanged──────►  GameUIManager → levelText
  LevelManager          ──────►  OnGoldChanged      ──────►  GameUIManager → goldText
  (any system)          ──────►  OnStatusTextChanged──────►  GameUIManager → statusText
```

**Rules enforced by convention:**
- Gameplay code **never** holds a `TextMeshProUGUI` reference.
- `GameUIManager` **never** calls gameplay methods directly.
- All subscribers manage their own lifecycle (`Awake` subscribe / `OnDestroy` unsubscribe).
- `GameEvents.ClearAllEvents()` is available for scene-transition cleanup to prevent stale subscriber leaks.

---

### Finite State Machine

The FSM is implemented as a classic **State pattern** with a twist: state objects are **shared static instances** on `BaseUnit`, not allocated per unit.

```csharp
// One instance shared by ALL units — stateless by design
private static readonly IdleState      _sharedIdleState      = new IdleState();
private static readonly MovingState    _sharedMovingState    = new MovingState();
private static readonly AttackingState _sharedAttackingState = new AttackingState();
```

Each state implements `IUnitState`:

```csharp
public interface IUnitState
{
    void Enter(BaseUnit unit);
    void Execute(BaseUnit unit);
    void Exit(BaseUnit unit);
}
```

State logic is **stateless** — all mutable data lives on `BaseUnit` (health, target, timers). States only call back into `BaseUnit`'s public helper methods (`FindClosestTarget`, `MoveTowardsTarget`, `HandleAttackCooldown`), keeping the state classes thin and testable.

---

## 📁 Project Structure

```
Assets/
├── Art/                        ← Sprites, textures, materials
├── GameData/
│   └── Levels/                 ← LevelDataSO assets (campaign sequence)
├── Prefabs/
│   ├── P-Bot.prefab            ← Player melee unit
│   ├── Ranged_P-Bot.prefab     ← Player ranged unit
│   ├── E-Bot.prefab            ← Enemy melee unit
│   ├── Ranged_E-Bot.prefab     ← Enemy ranged unit
│   ├── Projectile.prefab       ← Shared projectile
│   └── DamageText.prefab       ← Floating damage number
├── Scenes/
│   ├── GridScene.unity         ← Main battle scene
│   └── StartScene.unity        ← Main menu
└── Scripts/
    ├── Core/
    │   ├── BaseUnit.cs         ← Abstract unit base (FSM, physics, targeting)
    │   ├── GameEvents.cs       ← Static event bus
    │   ├── GridManager.cs      ← 8×8 grid data model (Singleton)
    │   ├── GridNode.cs         ← Single cell data (coords, world pos, occupancy)
    │   ├── IAttacker.cs        ← Attack interface
    │   ├── IDamageable.cs      ← Damage interface
    │   ├── Projectile.cs       ← Self-pooling projectile
    │   └── FSM/
    │       ├── IUnitState.cs   ← State interface
    │       ├── IdleState.cs    ← Scans for targets
    │       ├── MovingState.cs  ← Closes on target
    │       └── AttackingState.cs ← Fires on cooldown
    ├── Data/
    │   ├── BaseUnitDataSO.cs   ← Abstract unit stats SO
    │   ├── MeleeUnitDataSO.cs  ← Melee-specific stats
    │   ├── RangedUnitDataSO.cs ← Ranged-specific stats
    │   ├── LevelDataSO.cs      ← Level configuration SO
    │   ├── EnemyFormationSO.cs ← Enemy formation SO
    │   └── DamageTextDataSO.cs ← Damage text visual config
    ├── Managers/
    │   ├── BattleManager.cs    ← Battle state, unit lists, targeting, win/lose
    │   ├── LevelManager.cs     ← Level progression, economy, board reset
    │   ├── UnitFactory.cs      ← Unit object pool factory (Singleton)
    │   ├── UnitSpawner.cs      ← Grid placement logic (Singleton)
    │   ├── ProjectileFactory.cs← Projectile object pool factory (Singleton)
    │   ├── GameUIManager.cs    ← UI event subscriber (no gameplay refs)
    │   └── DamageTextManager.cs← Floating damage number spawner
    ├── Systems/
    │   ├── DamageText.cs       ← Floating text animation
    │   └── MenuManager.cs      ← Start scene logic
    └── Units/
        ├── MeleeUnit.cs        ← Concrete melee unit (swing attack)
        ├── RangedUnit.cs       ← Concrete ranged unit (projectile attack)
        ├── MeleeWeaponVisuals.cs   ← Weapon swing animation
        ├── WeaponAimVisuals.cs     ← Ranged weapon rotation
        └── Behaviors/
            └── UnitBreathingVisuals.cs ← Idle/walk/victory animations
```

---

## 🔗 System Dependency Map

```
                        ┌─────────────────┐
                        │   LevelDataSO   │ (ScriptableObject)
                        │  EnemyFormationSO│
                        └────────┬────────┘
                                 │ data reference
                                 ▼
┌──────────────┐        ┌─────────────────┐        ┌──────────────────┐
│ LevelManager │───────►│  UnitSpawner    │───────►│   UnitFactory    │
│  (Singleton) │        │  (Singleton)    │        │   (Singleton)    │
└──────┬───────┘        └─────────────────┘        └────────┬─────────┘
       │                                                     │ creates
       │ GameEvents                                          ▼
       │ (OnLevelWin/Lose)                         ┌─────────────────┐
       │                                           │    BaseUnit     │
       ▼                                           │  (MeleeUnit /   │
┌──────────────┐        ┌─────────────────┐        │   RangedUnit)   │
│BattleManager │◄───────│   GameEvents    │◄───────│                 │
│  (Singleton) │        │  (Static Bus)   │        └────────┬────────┘
└──────┬───────┘        └────────┬────────┘                 │ fires
       │ targeting               │ subscribes               │ OnDeath
       │ GetClosestTargetFor()   ▼                          │
       │                ┌─────────────────┐                 │
       │                │  GameUIManager  │                 │
       │                │  (levelText,    │                 │
       │                │   goldText,     │                 │
       │                │   statusText)   │                 │
       │                └─────────────────┘                 │
       │                                                     │
       └─────────────────────────────────────────────────────┘
                    BattleManager.RegisterUnit()
                    BattleManager.QueueUnitForRemoval()
```

---

## 🚀 Resuming Development

### Key Constants to Know

| Constant | Location | Value |
|---|---|---|
| Grid columns | `GridManager.Columns` | `8` |
| Grid rows | `GridManager.Rows` | `8` |
| Cell size | `GridManager.CellSize` | `2.6f` |
| Grid origin | `GridManager.OriginWorldPosition` | `(1.3, 1.3, 0)` |
| Player spawn column | `UnitSpawner.PlayerSpawnColumn` | `0` |
| Enemy primary column | `UnitSpawner.EnemyPrimaryColumn` | `7` |
| Enemy overflow column | `UnitSpawner.EnemyOverflowColumn` | `6` |
| Max player units | — | `8` (one per row) |
| Max enemy units | — | `16` (two columns) |

### Adding a New Unit Type

1. Create a new `ScriptableObject` that extends `BaseUnitDataSO` (or `MeleeUnitDataSO` / `RangedUnitDataSO`).
2. Create a new `MonoBehaviour` that extends `BaseUnit` and override `Attack()`.
3. Build a prefab with the new `MonoBehaviour` and assign it to the SO's `unitPrefab` field.
4. Reference the new SO in a `LevelDataSO` or `EnemyFormationSO` asset.

### Adding a New Level

1. Create an `EnemyFormationSO` asset (`Assets > Create > TDEV > Enemy Formation`).
2. Populate its `units` list with `UnitPlacement` entries (unit data + offset).
3. Create a `LevelDataSO` asset (`Assets > Create > TDEV > Level Data`).
4. Set `meleeLimit`, `rangedLimit`, `meleeData`, `rangedData`, `enemyFormation`, `goldReward`.
5. Drag the new `LevelDataSO` into `LevelManager.levels` in the Inspector.

### Active Scene: `GridScene`

The main battle scene hierarchy is organized into tagged groups:

| Group | Contents |
|---|---|
| `--- SETUP ---` | Main Camera, Global Light 2D |
| `--- MANAGERS ---` | GridMaster, GlobalManagers (LevelManager, BattleManager, GameUIManager) |
| `--- SYSTEMS ---` | UnitSpawner |
| `--- POOLS ---` | UnitPool, ProjectilePool, DamageTextPool |
| `--- ENVIRONMENT ---` | Grid / Tilemap |
| `--- UI ---` | Canvas (HUD labels, WAR! button, Back to Menu button, Status text) |

---

*Built with ❤️ by TDEVGAMES*
=======

>>>>>>> 6cd9287b41716bf7324e21b53fe3b780d3323405
