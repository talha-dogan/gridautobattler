# ⚔️ OttomanChessAuto

> **A Grid-Based Side-Scroller Auto-Battler** built in Unity 6 with URP 2D.
>
> Inspired by management-strategy titles like *Dwarves: Glory, Death and Loot* and *Totally Accurate Battle Simulator* — tactical army preparation meets fully automated, physics-driven combat.

**Play on itch.io:** [https://talha-dogan.itch.io/ottomanchessauto](https://talha-dogan.itch.io/ottomanchessauto)

**Studio:** TDEVGAMES  
**Engine:** Unity 6000.4.6f1 · Universal Render Pipeline 2D  
**Language:** C# · .NET Standard 2.1

OttomanChessAuto is a tactical auto-battler where you build and equip an Ottoman army, arrange them on an 8×8 grid, and watch them clash against increasingly fierce enemy formations — all without lifting a finger once the battle begins. Progression is driven by loot, a pawn-shop economy, and a stat-curve system that ensures every level feels meaningfully harder than the last.

---

## 📖 Table of Contents

1. [Game Overview](#1-game-overview)
2. [Core Gameplay Loop](#2-core-gameplay-loop)
3. [Grid & Spawning System](#3-grid--spawning-system)
4. [AI & Combat Logic](#4-ai--combat-logic)
5. [Equipment & Upgrade System](#5-equipment--upgrade-system)
6. [Technical Architecture](#6-technical-architecture)
7. [Key Constants Reference](#7-key-constants-reference)
8. [Designer Guides](#8-designer-guides)
9. [Screenshots](#9-screenshots)
10. [Planned Features & Roadmap](#10-planned-features--roadmap)

---

## 1. 🎮 Game Overview

OttomanChessAuto is a **2D side-scrolling tactical auto-battler**. The player acts as a battlefield commander: they configure their army composition and equipment loadout before each engagement, then watch their soldiers fight autonomously using a custom AI system.

The visual perspective is a **lateral side-scroller** — units face each other across a horizontal battlefield, with the player's forces on the left and enemy formations on the right. Combat is fully physics-driven via Unity's 2D Rigidbody system, giving units natural push-and-slide interactions as they close in on their targets.

**Three Scenes:**

| Scene | Purpose |
|---|---|
| `StartScene` | Main menu, settings panel, game entry point |
| `UpgradeScene` | Army management, equipment drag-drop, pawn shop, loot boxes |
| `GridScene` | Main battle arena — preparation + combat + resolution |

---

## 2. 🔄 Core Gameplay Loop

The game alternates between two distinct phases per level:

```text
┌─────────────────────────────────────────────────────────────────┐
│                     PREPARATION PHASE                           │
│                                                                 │
│  • LevelManager loads the next LevelDataSO asset                │
│  • UnitSpawner places the enemy formation (EnemyFormationSO)    │
│    onto the right side of the grid (Columns 6–7)                │
│  • Player reviews the enemy layout and presses [WAR!]           │
│  • UnitSpawner spawns player units onto Column 0                │
│                                                                 │
└──────────────────────────────┬──────────────────────────────────┘
                               │ WAR! button pressed
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                       COMBAT PHASE                              │
│                                                                 │
│  • BattleManager.StartBattle() sets isBattleStarted = true      │
│  • All BaseUnit FSMs unlock and begin executing                 │
│  • Units autonomously seek targets via Lane Priority AI         │
│  • Melee units close distance and swing; Ranged units fire      │
│    pooled projectiles via ProjectileFactory                     │
│  • BattleManager monitors unit deaths via OnDeath events        │
│  • When one side reaches 0 units → Win or Lose broadcast        │
│                                                                 │
└──────────────────────────────┬──────────────────────────────────┘
                               │ GameEvents.LevelWin / LevelLose
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                     RESOLUTION PHASE                            │
│                                                                 │
│  • LevelManager awards gold (goldReward from LevelDataSO)       │
│  • Surviving units play Victory animation                       │
│  • Board is cleared (UnitFactory returns all units to pools)    │
│  • GridManager.ResetGrid() wipes all occupancy flags            │
│  • Next LevelDataSO is loaded after a 3-second delay            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
3. 🗺️ Grid & Spawning SystemThe 8×8 World-Space GridThe entire battlefield is defined by a single GridManager singleton that owns an authoritative GridNode[8, 8] data model. The grid is generated once in Awake() and never changes at runtime.PropertyValueDimensions8 columns × 8 rowsOrigin [0,0] world center(1.3, 1.3, 0)Cell size (center-to-center)2.6 Unity unitsColumn axisX → left (0) to right (7)Row axisY → bottom (0) to top (7)Deterministic Spawning LayoutPlaintextSide-Scroller Lateral View (Y = row / "lane"):

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
       Player                           Overflow Primary
       Col 0                            Col 6   Col 7
4. 🧠 AI & Combat LogicFinite State MachineEvery unit runs a lightweight FSM with five states, managed entirely inside BaseUnit:Plaintext                    ┌──────────────────────────────────────┐
                    │                                      │
          ┌─────────▼──────────┐              ┌───────────▼──────────┐
          │     IDLE STATE     │◄─────────────│   ATTACKING STATE    │
          │  Scans for targets │  target dead │  Fires on cooldown   │
          └─────────┬──────────┘              └───────────▲──────────┘
                    │ target found                        │ in range
                    ▼                                     │
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
Lane Priority Targeting SystemBattleManager.GetClosestTargetFor() implements a three-tier Lane Priority system:TIER 1 (Same Lane): Find the closest living enemy in the exact same row.TIER 2 (Adjacent Lanes): If the lane is empty, find the closest enemy in the row immediately above or below.TIER 3 (Global Fallback): If adjacent lanes are also empty, target the absolute closest living enemy anywhere on the board.5. 🛡️ Equipment & Upgrade SystemThe Upgrade Scene provides a full drag-and-drop equipment management interface for the player's army of up to 8 units.Equipment Data (EquipmentDataSO): Defines slot (Helmet, Vest, Pants, Weapon, Shield), stat bonuses, and Addressable sprite.Army Roster (PlayerArmyDataSO): Persistent SO shared between scenes storing 8 ArmySlot entries.Pawn Shop: Players buy additional deployment slots (up to 8) using gold.Loot Box System: Players can purchase random equipment drops.6. 🏗️ Technical ArchitectureA — Data-Driven Architecture & SOLIDA-AbstractionAbstraction is enforced at two levels: interfaces that define behavioural contracts, and abstract classes that provide a shared implementation skeleton.Interfaces — IAttacker and IDamageable (/Scripts/Core/)C#// Any entity capable of dealing damage must implement this.
public interface IAttacker
{
    void Attack(IDamageable target);
}

// Interface Segregation: only entities that can receive damage implement this.
public interface IDamageable
{
    void TakeDamage(float amount);
    event Action<BaseUnit> OnDeath;
}
IAttacker.Attack(IDamageable target) and IDamageable.TakeDamage(float amount) keep the attack pipeline entirely type-agnostic. BattleManager, AttackingState, and the FSM never need to know whether the attacker is a MeleeUnit or a RangedUnit.Abstract ScriptableObject — BaseUnitDataSO (/Scripts/Data/Units/BaseUnitDataSO.cs)All unit-data assets (Turk.asset, E-Bot.asset, etc.) derive from the abstract BaseUnitDataSO. This forces a common contract (health, damage, speed, prefab reference) while allowing derived types (MeleeUnitDataSO, RangedUnitDataSO) to add specialised fields such as swing angles or projectile speed.A-SingletonEvery manager that must persist across scenes and provide a single point of access is implemented as a Unity-style singleton — a lazy Instance property enforced in Awake():C#private void Awake()
{
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;
    DontDestroyOnLoad(gameObject); // Only on persistent managers
}
Singletons in the project:ClassFilePersists Across ScenesUnitFactoryManagers/UnitFactory.csNo (scene-bound)ProjectileFactoryManagers/ProjectileFactory.csNoVFXManagerSystems/VFX/VFXManager.csNoSoundManagerManagers/SoundManager.csYesBattleManagerManagers/BattleManager.csNoDamageTextManagerManagers/DamageTextManager.csNoLevelManagerManagers/LevelManager.csNoGameInputHandlerManagers/InputDeviceManager/GameInputHandler.csYesSceneLoaderSystems/SceneLoader.csYesGameSaveServiceSystems/Save/GameSaveService.csYesA-ScriptableObject ArchitectureAll game configuration lives in ScriptableObject assets, entirely separate from scene objects. This allows designers to tune values in the Inspector and ensures data survives code changes without requiring prefab re-links.LevelDataSO — Defines per-level army limits, unit data references, enemy formation, and gold reward. 212 instances live under /Assets/GameData/Levels/.BaseUnitDataSO (abstract) / MeleeUnitDataSO / RangedUnitDataSO — Identity card for every unit: name, prefab, stats, and an optional StatProgressionSO reference.EnemyFormationSO — Encodes the exact grid positions and unit types of an enemy wave. Referenced directly by LevelDataSO so wave editing requires no code changes.StatProgressionSO — Seven AnimationCurve fields that replace flat-number balancing with designer-controlled curves.EquipmentDataSO — Stat bonuses plus an AssetReferenceT<Sprite> Addressable reference for on-demand icon loading.PlayerArmyDataSO — Runtime + save state for the player's army slots; bridges the Upgrade and Battle scenes.DamageTextDataSO — Configures floating damage number colour, float speed, and fade duration.WeaponBehaviourSO / MeleeWeaponBehaviourSO / RangedWeaponBehaviourSO — Strategy pattern for weapon attack behaviour.A-Factory PatternTwo dedicated factories centralise object creation and encapsulate all pooling logic:UnitFactory (/Scripts/Managers/UnitFactory.cs)Maintains one ObjectPool<BaseUnit> per unique unit prefab.C#// One pool per unique prefab
private readonly Dictionary<GameObject, ObjectPool<BaseUnit>> _pools = new();

public BaseUnit CreateUnit(BaseUnitDataSO unitData, Vector3 spawnPosition, Team team, int level = 1)
{
    ObjectPool<BaseUnit> pool = GetOrCreatePool(unitData.unitPrefab);
    BaseUnit spawnedUnit = pool.Get();
    spawnedUnit.Initialize(unitData, team, level);
    BattleManager.Instance.RegisterUnit(spawnedUnit);
    return spawnedUnit;
}
ProjectileFactory (/Scripts/Managers/ProjectileFactory.cs)Mirrors the same pattern for projectiles. Consolidates them into one shared pool per prefab (default capacity 10, max 60). A reverse lookup dictionary (_projectileToPool) lets Projectile.ReturnToPool() release itself without any direct reference to the factory.A-PolymorphismBaseUnit is an abstract MonoBehaviour implementing the full FSM, movement, health, and equipment logic. Concrete subclasses override only Attack() to express their specific combat behaviour:C#// MeleeUnit.cs — swings the weapon visually, plays melee audio, applies damage directly
public override void Attack(IDamageable target)
{
    weaponVisuals.SwingWeapon(meleeData.swingAngle, meleeData.swingDuration);
    SoundManager.Instance.PlaySoundAtPosition(SoundType.WeaponMeleeSwing, transform.position);
    ((BaseUnit)target).TakeDamage(attackDamage);
}

// RangedUnit.cs — requests a pooled projectile from ProjectileFactory, launches it
public override void Attack(IDamageable target)
{
    Projectile projectile = ProjectileFactory.Instance.Get(rangedData.projectilePrefab);
    SoundManager.Instance.PlaySoundAtPosition(SoundType.WeaponShoot, transform.position);
    projectile.Launch((BaseUnit)target, attackDamage, rangedData.projectileSpeed, unitTeam);
}
A-Base Entity DefinitionsBaseUnit (/Scripts/Core/BaseUnit.cs) is the single abstract root for all shared unit logic:Stats — health, damage, attack range, cooldown, move speed.Stat Scaling — if statProgression != null, each stat is sampled from the corresponding AnimationCurve.Equipment Integration — ApplyEquipmentBonus() and ResetEquipmentBonus() add/remove flat bonuses safely.FSM Tick — Update() delegates every frame to shared static IUnitState instances to avoid allocations.Physics Movement — FixedUpdate() applies Rigidbody2D.AddForce toward the target.Death & Pooling — Die() queues the unit in BattleManager._pendingRemovals and calls UnitFactory.ReleaseUnit(this).A-DecouplingSystem communication happens through two complementary mechanisms:GameEvents static event bus (/Scripts/Core/GameEvents.cs)Any system broadcasts by invoking a method; any system reacts by subscribing in OnEnable/OnDisable.C#// BattleManager broadcasts — never touches UI directly
GameEvents.LevelWin(rewardMessage);

// SoundManager reacts independently
GameEvents.OnLevelWin += OnLevelWin;

// VFXManager reacts independently
GameEvents.OnUnitDied += OnUnitDied;

// GamePresenter routes to IGameView (UI layer)
GameEvents.OnLevelWin += HandleLevelWin;
MVP — IGameView / GamePresenterGamePresenter subscribes to GameEvents, translates raw events into display logic, and pushes results through IGameView. GameUIManager implements IGameView and never contains gameplay logic.B — Spawns, Object Pooling, UI and Logic SeparationB-ProjectilesProjectileFactory maintains one ObjectPool<Projectile> per unique prefab. RangedUnit.Attack() retrieves a live projectile; Projectile.ReturnToPool() releases it at end-of-flight.B-Floating TextsDamageTextManager owns a single ObjectPool<DamageText>. DamageText runs a coroutine that floats upward and fades its alpha to zero, then calls _pool.Release(this) instead of Destroy().B-VFX Pooling & Enemy PoolingVFX Pooling: VFXManager manages one ObjectPool<ParticleSystem> per VFXType. All pools are pre-warmed at startup.Enemy Pooling: UnitFactory uses actionOnGet to activate and actionOnRelease to deactivate and re-parent under [UnitPool].B-SoundsSoundManager uses a Queue<AudioSource> pool. On PlaySound(), a source is dequeued, configured with a random clip and pitch, then added to _active. Finished sources are returned to the queue in Update().B-Spawn Rate and Enemy Power via DataEnemy difficulty is entirely driven by StatProgressionSO. The level index is normalised to [0, 1] and passed into AnimationCurve.Evaluate():C#float hp            = statProgression.EvaluateHealth(currentLevel, maxLevel);
float damage        = statProgression.EvaluateDamage(currentLevel, maxLevel);
float spawnInterval = statProgression.EvaluateSpawnPacing(waveProgress);
B-MVP PatternThe battle UI follows a strict Model → Presenter → View flow:C#// GamePresenter — zero knowledge of TextMeshPro
private void HandleLevelWin(string message) => _view.ShowStatusText(message);
C — AddressablesC-Memory UnloadSceneCleanupPipeline maintains a global List<AsyncOperationHandle> _trackedHandles. On scene unload, it executes a strict 7-step pipeline including Addressables.Release() on all tracked handles, returning units/VFX to pools, clearing events, and calling GC.Collect().C-Lazy LoadingEquipmentDataSO stores equipment icons as AssetReferenceT<Sprite>.C#[Header("Visual")]
[Tooltip("Addressable reference — sprite is only loaded into memory on demand.")]
public AssetReferenceT<Sprite> spriteReference;
C-Sprite BundlesAssets are organised into five Addressable groups (Units, Items, UI, Arena, Audio), managed by a custom menu-driven editor tool (AddressablesGroupSetup).D — Google Sheets Sync(Not present in the current codebase. Recommended approach for future milestones: Google Sheets API v4 → CSV export → Unity EditorWindow importer writing ScriptableObject assets via AssetDatabase).E — Level Load / UnloadE-Additive ScenesThe project uses a three-scene additive architecture managed by SceneLoader:CoreScene ── DontDestroyOnLoad singletons (never unloaded)UpgradeScene ── Army builder, pawn shop, equipment drag-and-dropGridScene ── 8×8 battle grid, unit AI, combatE-Async LoadingSceneLoader.TransitionTo(targetScene, sceneToUnload) executes a 5-step coroutine using LoadSceneAsync(targetScene, Additive) with allowSceneActivation = false to prevent frame spikes on activation.E-Memory CleanupSceneCleanupPipeline.RunCleanup() always runs before any UnloadSceneAsync call, covering Addressables, pools, events, and GC in a deterministic order.F — Animation Curve MasteryF-EasingA dedicated EasingLibrary provides easing functions. SceneLoader uses Time.unscaledDeltaTime-based linear fade that remains functional even when Time.timeScale = 0:C#while (elapsed < duration)
{
    elapsed += Time.unscaledDeltaTime;
    float t = Mathf.Clamp01(elapsed / duration);
    _faderImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(startAlpha, targetAlpha, t));
    yield return null;
}
F-Stat Progression CurvesStatProgressionSO replaces flat numeric stats with an AnimationCurve evaluated on a normalised level value [0, 1]:C#public AnimationCurve healthCurve         = AnimationCurve.Linear(0f, 50f,  1f, 500f);
public AnimationCurve damageCurve         = AnimationCurve.Linear(0f, 10f,  1f, 100f);
public AnimationCurve speedCurve          = AnimationCurve.Linear(0f, 2f,   1f, 5f);
F-Movement CurvesmovementCurve provides a normalised speed multiplier for smooth acceleration and deceleration during unit movement:C#public float EvaluateMovement(float normalizedTime)
    => movementCurve.Evaluate(Mathf.Clamp01(normalizedTime));
F-Spawn PacingspawnPacingCurve maps wave progress [0, 1] to spawn interval in seconds. At wave start the interval is slow; by wave end it drops to a rapid assault.G — Save SystemG-Binary FormatSaveManager serialises SaveData to JSON via JsonUtility.ToJson(), encrypts the result with AES-256-CBC, and writes the raw bytes to gamesave.dat. Loading decrypts the bytes, parses the JSON, and validates the SHA-256 checksum:C#// Write path
byte[] encrypted = Encrypt(json);
File.WriteAllBytes(SaveFilePath, encrypted);

// Read path
byte[] bytes = File.ReadAllBytes(path);
string json  = Decrypt(bytes);
SaveData data = JsonUtility.FromJson<SaveData>(json);
if (!ValidateChecksum(data)) return null;
G-Versioning & MigrationSaveManager.CurrentSaveVersion is currently 2. SaveMigrationService implements a while (data.saveVersion < CurrentSaveVersion) loop, applying one migration step at a time (e.g., filling missing fields with safe defaults) so no data is ever discarded.G-Meta DataSaveData carries meta fields like saveVersion, savedAt, totalPlayTimeSeconds, saveCount, and checksum.H — Input SystemH-New Unity Input SystemGameInputHandler wraps the auto-generated InputSystem_Actions class and exposes clean C# events (OnConfirm, OnCancel, OnSpeedCycle, MapsValue, CursorDelta). Two action maps are toggled via SwitchToUI() and SwitchToPlayer().H-Gamepad SupportGamepadCursor drives a VirtualMouseInput component from the gamepad left stick, simulating a physical mouse cursor for standard UGUI drag-and-drop.H-Keyboard SupportBindings are defined in the .inputactions asset. There are no Input.GetKey() calls anywhere in the gameplay codebase.I — Quick Gameplay TestDebugToolsWindow (Tools → TDEV → Debug Tools) is a custom EditorWindow providing independently collapsible sections that work in Edit and Play Mode.Start Game in Any Stage: Populates a level slider reading LevelManager.levels. Sets PlayerPrefs and forces Play Mode.Start with Any Inventory & Hero: Buttons to unlock all slots, fill random equipment from AssetDatabase, clear equipment, or add Gold.Start with Any Game Time: Provides a Time.timeScale slider and preset buttons (0.25× to 8×) with a live FPS counter.Stress Testing: Spawns 1–64 units on both teams simultaneously to test performance.J — LocalizationJ-Custom Localization SystemLoads JSON files from Resources/Localization/ into a Dictionary<string, string>. Fallback chain: current language → English → raw key string.J-Key NamingLocalizationKeys eliminates magic strings entirely. All keys are const string fields following a strict domain.object.field naming convention:C#// Pattern: domain.object
public const string UI_START_BUTTON      = "ui.start_button";
public const string BATTLE_LEVEL_CLEARED = "battle.level_cleared";
public const string ITEM_SWORD           = "item.sword";
LocalizedText components auto-refresh via LocalizationManager.OnLanguageChanged.7. ⚙️ Key Constants ReferenceCore parameters that should not be hard-modified in code during balancing:ConstantValueDescriptionGridXSize8Number of vertical columns on the battlefieldGridYSize8Number of horizontal rows (lanes) on the battlefieldPlayerSpawnColumn0Far-left column where player units are spawnedEnemyPrimaryColumn7Far-right column where enemy units are spawnedEnemyOverflowColumn6Secondary column used if enemy count exceeds 8MaxArmySize8Maximum number of units a single side can have on the boardHysteresisFactor0.5625fTolerance for AI target switching (square of 0.75 units distance)8. 📝 Designer GuidesStep-by-step instructions for adding content without touching the code:Adding a New Unit TypeIn the Project window, right-click inside ScriptableObjects/Units/.Select Create > AutoBattler > Units > Melee Unit Data (or Ranged).Name the SO and input base stats (Health, Damage, Speed) via the Inspector.Drag and drop the visual unit prefab (from Prefabs/Units) into the unitPrefab field.Adding a New LevelCreate a new LevelDataSO via Create > AutoBattler > Levels > Level Data.Create a new EnemyFormationSO to dictate the enemy layout, and assign it to the Level Data.Set the goldReward amount given upon level completion.Add the newly created Level Data to the end of the Level List inside the LevelManager prefab.Adding a New Equipment ItemCreate the item via Create > AutoBattler > Equipment > Equipment Data.Select the item type from the EquipmentSlot enum (Helmet, Vest, Weapon, etc.).Input the stat bonuses the item will provide.Add the item's sprite to the "Items" group in the Addressables window, then link it to the spriteReference field in the SO.9. 📸 Screenshots(Note: Replace placeholders in the media/ folder with actual captured gameplay GIFs/images)10. 🚀 Planned Features & RoadmapFeatureDescriptionBoss FightsLarge-scale units with custom multi-phase animations, AoE abilities, and unique AI behaviorsSet BonusesSynergy system granting extra passive stats when equipping multiple items from the same set (e.g., Knight Set)Online LeaderboardServer integration for comparing highest cleared stages against the global player baseAdvanced Formation SystemAllows players to place units freely across the first 3 columns instead of being restricted to Column 0
