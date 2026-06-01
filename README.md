OttomanChessAuto — Technical Architecture README
Play on itch.io: https://talha-dogan.itch.io/ottomanchessauto

OttomanChessAuto is a tactical auto-battler where you build and equip an Ottoman army, arrange them on an 8×8 grid, and watch them clash against increasingly fierce enemy formations — all without lifting a finger once the battle begins. Progression is driven by loot, a pawn-shop economy, and a stat-curve system that ensures every level feels meaningfully harder than the last.

Table of Contents
A — Data-Driven Architecture & SOLID
Abstraction
Singleton
ScriptableObject Architecture
Factory Pattern
Polymorphism
Base Entity Definitions
Decoupling
B — Spawns, Object Pooling, UI and Logic Separation
Projectiles
Floating Damage Texts
VFX Pooling & Enemy Pooling
Sounds
Spawn Rate and Enemy Power via Data
MVP Pattern
C — Addressables
Memory Unload
Lazy Loading
Sprite Bundles
D — Google Sheets Sync
E — Level Load / Unload
Additive Scenes
Async Loading
Memory Cleanup
F — Animation Curve Mastery
Easing
Stat Progression Curves
Movement Curves
Spawn Pacing
G — Save System
Binary Format
Versioning
Migration
Meta Data
H — Input System
New Unity Input System
Gamepad Support
Keyboard Support
I — Quick Gameplay Test
Start Game in Any Stage
Start with Any Inventory & Hero
Start with Any Game Time
Stress Testing
J — Localization
A — Data-Driven Architecture & SOLID
A — Abstraction
Abstraction is enforced at two levels: interfaces that define behavioural contracts, and abstract classes that provide a shared implementation skeleton.

Interfaces — IAttacker and IDamageable (/Scripts/Core/)

// Any entity capable of dealing damage must implement this.
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
IAttacker.Attack(IDamageable target) and IDamageable.TakeDamage(float amount) keep the attack pipeline entirely type-agnostic. BattleManager, AttackingState, and the FSM never need to know whether the attacker is a MeleeUnit or a RangedUnit.

Abstract ScriptableObject — BaseUnitDataSO (/Scripts/Data/Units/BaseUnitDataSO.cs)

All unit-data assets (Turk.asset, E-Bot.asset, etc.) derive from the abstract BaseUnitDataSO. This forces a common contract (health, damage, speed, prefab reference) while allowing derived types (MeleeUnitDataSO, RangedUnitDataSO) to add specialised fields such as swing angles or projectile speed.


A — Singleton
Every manager that must persist across scenes and provide a single point of access is implemented as a Unity-style singleton — a lazy Instance property enforced in Awake():

private void Awake()
{
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;
    DontDestroyOnLoad(gameObject); // Only on persistent managers
}
Singletons in the project:

Class	File	Persists Across Scenes
UnitFactory	Managers/UnitFactory.cs	No (scene-bound)
ProjectileFactory	Managers/ProjectileFactory.cs	No
VFXManager	Systems/VFX/VFXManager.cs	No
SoundManager	Managers/SoundManager.cs	Yes
BattleManager	Managers/BattleManager.cs	No
DamageTextManager	Managers/DamageTextManager.cs	No
LevelManager	Managers/LevelManager.cs	No
GameInputHandler	Managers/InputDeviceManager/GameInputHandler.cs	Yes
SceneLoader	Systems/SceneLoader.cs	Yes
GameSaveService	Systems/Save/GameSaveService.cs	Yes
A — ScriptableObject Architecture
All game configuration lives in ScriptableObject assets, entirely separate from scene objects. This allows designers to tune values in the Inspector and ensures data survives code changes without requiring prefab re-links.

Key ScriptableObject types:

LevelDataSO (/Scripts/Data/LevelDataSO.cs) — Defines per-level army limits, unit data references, enemy formation, and gold reward. 212 instances live under /Assets/GameData/Levels/.
BaseUnitDataSO (abstract) / MeleeUnitDataSO / RangedUnitDataSO — Identity card for every unit: name, prefab, stats, and an optional StatProgressionSO reference.
EnemyFormationSO — Encodes the exact grid positions and unit types of an enemy wave. Referenced directly by LevelDataSO so wave editing requires no code changes.
StatProgressionSO — Seven AnimationCurve fields that replace flat-number balancing with designer-controlled curves (see Section F).
EquipmentDataSO — Stat bonuses plus an AssetReferenceT<Sprite> Addressable reference for on-demand icon loading.
PlayerArmyDataSO — Runtime + save state for the player's army slots; bridges the Upgrade and Battle scenes.
DamageTextDataSO — Configures floating damage number colour, float speed, and fade duration.
WeaponBehaviourSO / MeleeWeaponBehaviourSO / RangedWeaponBehaviourSO — Strategy pattern for weapon attack behaviour, assigned to EquipmentDataSO.

A — Factory Pattern
Two dedicated factories centralise object creation and encapsulate all pooling logic:

UnitFactory (/Scripts/Managers/UnitFactory.cs)

Maintains one ObjectPool<BaseUnit> per unique unit prefab, keyed by the GameObject reference. Callers simply call UnitFactory.Instance.CreateUnit(unitData, position, team, level) — the factory handles pool lookup, activation, Initialize() injection, and BattleManager registration:

// One pool per unique prefab
private readonly Dictionary<GameObject, ObjectPool<BaseUnit>> _pools = new();

public BaseUnit CreateUnit(BaseUnitDataSO unitData, Vector3 spawnPosition, Team team, int level = 1)
{
    ObjectPool<BaseUnit> pool = GetOrCreatePool(unitData.unitPrefab);
    BaseUnit spawnedUnit = pool.Get();
    spawnedUnit.Initialize(unitData, team, level);
    BattleManager.Instance.RegisterUnit(spawnedUnit);
    return spawnedUnit;
}
ProjectileFactory (/Scripts/Managers/ProjectileFactory.cs)

Mirrors the same pattern for projectiles. Previously, each RangedUnit maintained its own private pool — meaning 20 ranged units firing the same prefab produced 20 separate pools. ProjectileFactory consolidates them into one shared pool per prefab (default capacity 10, max 60). A reverse lookup dictionary (_projectileToPool) lets Projectile.ReturnToPool() release itself without any direct reference to the factory.


A — Polymorphism
BaseUnit is an abstract MonoBehaviour implementing the full FSM, movement, health, and equipment logic. Concrete subclasses override only Attack() to express their specific combat behaviour:

// MeleeUnit.cs — swings the weapon visually, plays melee audio, applies damage directly
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
BattleManager and AttackingState call unit.Attack(target) without distinguishing between unit types — the correct behaviour is dispatched at runtime by the virtual method table.

A — Base Entity Definitions
BaseUnit (/Scripts/Core/BaseUnit.cs) is the single abstract root for all shared unit logic (614 lines):

Stats — health, damage, attack range, cooldown, move speed; populated via Initialize(BaseUnitDataSO, Team, int level).
Stat Scaling — if statProgression != null, each stat is sampled from the corresponding AnimationCurve at normalised level.
Equipment Integration — ApplyEquipmentBonus() and ResetEquipmentBonus() add/remove flat bonuses without re-initialising, enabling safe pool recycling.
FSM Tick — Update() delegates every frame to the current IUnitState (IdleState, MovingState, AttackingState) implemented as shared static instances to avoid per-frame allocations.
Physics Movement — FixedUpdate() applies Rigidbody2D.AddForce toward the target; velocity is clamped to moveSpeed; linear damping of 15 enables natural crowd-pushing.
Death & Pooling — Die() fires OnDeath, queues the unit in BattleManager._pendingRemovals (flushed in LateUpdate), and calls UnitFactory.Instance.ReleaseUnit(this).
BaseUnitDataSO is the data counterpart — an abstract ScriptableObject every unit data asset must extend.

A — Decoupling
System communication happens through two complementary mechanisms:

GameEvents static event bus (/Scripts/Core/GameEvents.cs)

A static class with pure C# events. Any system broadcasts by invoking a method; any system reacts by subscribing/unsubscribing in OnEnable/OnDisable. No MonoBehaviour reference is required — the bus works across all loaded scenes:

// BattleManager broadcasts — never touches UI directly
GameEvents.LevelWin(rewardMessage);

// SoundManager reacts independently
GameEvents.OnLevelWin += OnLevelWin;

// VFXManager reacts independently
GameEvents.OnUnitDied += OnUnitDied;

// GamePresenter routes to IGameView (UI layer)
GameEvents.OnLevelWin += HandleLevelWin;
GameEvents.ClearAllEvents() is called by SceneCleanupPipeline on every scene unload to prevent stale scene-local subscribers from receiving events after their scene is gone.

MVP — IGameView / GamePresenter (/Scripts/UI/)

GamePresenter subscribes to GameEvents, translates raw events into display logic, and pushes results through IGameView. GameUIManager implements IGameView — it only updates TextMeshPro components and never contains gameplay logic. GamePresenter never touches a Unity component directly.


B — Spawns, Object Pooling, UI and Logic Separation
B — Projectiles
ProjectileFactory (/Scripts/Managers/ProjectileFactory.cs) maintains one ObjectPool<Projectile> per unique prefab (default capacity 10, max 60). RangedUnit.Attack() calls ProjectileFactory.Instance.Get(prefab) to retrieve a live projectile; Projectile.ReturnToPool() releases it at end-of-flight without any direct factory reference.

A forward-reference trick using a one-element ObjectPool[] array lets the pool's createFunc lambda register the instance in _projectileToPool before the pool object is formally assigned — solving the chicken-and-egg capture problem cleanly.


B — Floating Damage Texts
DamageTextManager (/Scripts/Managers/DamageTextManager.cs) owns a single ObjectPool<DamageText> (default 10, max 30). SpawnDamageText(position, amount) retrieves an instance, applies a random horizontal and vertical offset (configured in DamageTextDataSO), and calls Initialize().

DamageText (/Scripts/Systems/DamageText.cs) runs a coroutine that floats the text upward and fades its alpha to zero over fadeDuration seconds, then calls _pool.Release(this) instead of Destroy(). The pool reference is injected at creation time via SetPool(IObjectPool<DamageText>), keeping DamageText fully decoupled from the manager.


B — VFX Pooling & Enemy Pooling
VFX — VFXManager (/Scripts/Systems/VFX/VFXManager.cs)

One ObjectPool<ParticleSystem> per VFXType enum entry (UnitDeath, BattleWin, etc.). All pools are pre-warmed at startup by WarmUpPool() to eliminate first-frame cost. PlayVFX(type, position) retrieves a ParticleSystem, positions and plays it, then a WaitWhile(() => ps.isPlaying) coroutine automatically returns it to the pool on completion.

VFXManager subscribes directly to GameEvents.OnUnitDied and GameEvents.OnLevelWin — zero wiring from unit or battle code.

Enemy / Unit Pooling — UnitFactory (/Scripts/Managers/UnitFactory.cs)

One ObjectPool<BaseUnit> per unit prefab. actionOnGet activates the GameObject; actionOnRelease deactivates it and re-parents it under [UnitPool]. Units are never instantiated during battle — only when the pool needs to grow.


B — Sounds
SoundManager (/Scripts/Managers/SoundManager.cs) uses a Queue<AudioSource> pool. At startup, initialPoolSize (default 10) AudioSource components are created under AudioSourcePool. On PlaySound(), a source is dequeued, configured with a random clip from SoundEntry.clips[] and a random pitch in [pitchMin, pitchMax], then added to _active. Every Update() tick, finished sources are returned to the queue. If the hard cap (maxPoolSize = 20) is hit, the oldest active source is force-stopped and reused.

Background music runs on a dedicated AudioSource with a FadeBGM() coroutine for smooth crossfades. SetMasterSFXVolume() and SetMasterBGMVolume() expose a clean API consumed by SettingsManager.


B — Spawn Rate and Enemy Power via Data
Enemy difficulty is entirely driven by StatProgressionSO assets (/Scripts/Data/StatProgressionSO.cs). EnemyFormationSO references unit data which in turn references a StatProgressionSO. The level index is normalised to [0, 1] and passed into AnimationCurve.Evaluate():

// BaseUnit.Initialize reads from StatProgressionSO if assigned
float hp            = statProgression.EvaluateHealth(currentLevel, maxLevel);
float damage        = statProgression.EvaluateDamage(currentLevel, maxLevel);
float spawnInterval = statProgression.EvaluateSpawnPacing(waveProgress);
spawnPacingCurve defaults to EaseInOut(0, 2f, 1, 0.3f) — enemies trickle in at wave start, then accelerate to a rapid assault by wave end — entirely tunable from the Inspector with no code changes.


B — MVP Pattern
The battle UI follows a strict Model → Presenter → View flow:

Model: GameEvents static bus fires events with raw data (int gold, string message).
Presenter: GamePresenter (/Scripts/UI/GamePresenter.cs) subscribes to GameEvents, translates events into display decisions, and calls methods on IGameView.
View: IGameView (/Scripts/UI/IGameView.cs) defines three methods: ShowStatusText, ShowLevelIndex, ShowGold. GameUIManager implements this interface and is the only class allowed to write to TextMeshPro components.
// GamePresenter — zero knowledge of TextMeshPro
private void HandleLevelWin(string message) => _view.ShowStatusText(message);
The entire UI layer can be replaced or mocked by swapping the IGameView implementation.


C — Addressables
C — Memory Unload
SceneCleanupPipeline (/Scripts/Systems/SceneCleanupPipeline.cs) maintains a global List<AsyncOperationHandle> _trackedHandles. Any system that loads an Addressable calls SceneCleanupPipeline.TrackHandle(handle). On scene unload, SceneLoader invokes RunCleanup(sceneName) as a coroutine executing a strict 7-step pipeline:

Step 1 — Addressables.Release() all tracked handles → _trackedHandles cleared
Step 2 — UnitFactory: return all active BaseUnits to their pools
Step 3 — VFXManager: stop all active ParticleSystems
Step 4 — SoundManager: log state (BGM is DontDestroyOnLoad, preserved)
Step 5 — GameEvents.ClearAllEvents() — evict stale scene-local subscribers
Step 6 — Resources.UnloadUnusedAssets() — async, awaited frame-by-frame
Step 7 — GC.Collect() × 2 + GC.WaitForPendingFinalizers()

C — Lazy Loading
EquipmentDataSO (/Scripts/Data/Equipment/EquipmentDataSO.cs) stores equipment icons as AssetReferenceT<Sprite> rather than a hard Sprite reference:

[Header("Visual")]
[Tooltip("Addressable reference — sprite is only loaded into memory on demand.")]
public AssetReferenceT<Sprite> spriteReference;
The sprite texture is never loaded unless the equipment slot is actually opened in the Upgrade scene. The resulting AsyncOperationHandle is registered with SceneCleanupPipeline.TrackHandle() so it is released automatically on scene unload.


C — Sprite Bundles
Assets are organised into five Addressable groups, each bundled as PackTogether (one content bundle per group):

Group	Content Path Prefixes
Units	Assets/Art/Unit/, Assets/Prefabs/Units/
Items	Assets/Art/Items/, Assets/Prefabs/Items/
UI	Assets/Art/BackgraundMenu/
Arena	Assets/Art/Arena/
Audio	Assets/Art/Saund/
AddressablesGroupSetup (/Scripts/Editor/AddressablesGroupSetup.cs) is a menu-driven editor tool (Tools → TDEV → Setup Addressables Groups) that creates missing groups with BundledAssetGroupSchema + ContentUpdateGroupSchema and moves matching assets out of the Default Local Group — making the entire Addressables layout reproducible in one click.


D — Google Sheets Sync
Not present in the current codebase. A complete scan of all .cs files, .asset files, and editor scripts found no Google Sheets API integration, no CSV import/export tooling, and no auto-refresh pipeline. Level data and enemy formation data are authored directly as LevelDataSO (212 instances) and EnemyFormationSO ScriptableObject assets in the Unity Editor.

If this pipeline is planned for a future milestone, the recommended approach is: Google Sheets API v4 → CSV export → Unity EditorWindow importer writing ScriptableObject assets via AssetDatabase → [InitializeOnLoad] or an AssetPostprocessor triggering auto-refresh in the Editor.

E — Level Load / Unload
E — Additive Scenes
The project uses a three-scene additive architecture managed by SceneLoader (/Scripts/Systems/SceneLoader.cs):

CoreScene       ─── DontDestroyOnLoad singletons (never unloaded)
UpgradeScene    ─── Army builder, pawn shop, equipment drag-and-drop
GridScene       ─── 8×8 battle grid, unit AI, combat
UpgradeScene and GridScene are loaded and unloaded additively. CoreScene is always set as the Unity active scene after every transition, ensuring UnloadSceneAsync never targets the only loaded scene (which would fail silently in Unity).

E — Async Loading
SceneLoader.TransitionTo(targetScene, sceneToUnload) executes a 5-step coroutine:

Step 1 — FadeTo(1f, 0.3s)  — screen fades to black (unscaledDeltaTime — works at timeScale 0)
Step 2 — SceneCleanupPipeline.RunCleanup() + UnloadSceneAsync (hidden behind black)
Step 3 — LoadSceneAsync(targetScene, Additive), allowSceneActivation = false
         (holds at exactly 90% until ready — prevents frame spikes on activation)
Step 4 — allowSceneActivation = true → while (!isDone) → SetActiveScene(targetScene)
Step 5 — FadeTo(0f, 0.4s)  — screen fades back in
OnLoadProgress (Action<float>) is fired at every step with a remapped [0, 1] value, enabling accurate progress bar display. The full-screen fader lives on its own DontDestroyOnLoad root GameObject (separate from SceneLoader itself — DontDestroyOnLoad only works on root-level objects).


E — Memory Cleanup
See C — Memory Unload. SceneCleanupPipeline.RunCleanup() always runs before any UnloadSceneAsync call, covering Addressables, pools, events, and GC in a deterministic order.

F — Animation Curve Mastery
F — Easing
A dedicated EasingLibrary (/Scripts/Systems/Easing/EasingLibrary.cs) provides easing functions typed by the EaseType enum. Additionally, SceneLoader uses Time.unscaledDeltaTime-based linear fade that remains functional even when Time.timeScale = 0:

// SceneLoader.FadeTo — game-time-scale independent
while (elapsed < duration)
{
    elapsed += Time.unscaledDeltaTime;
    float t = Mathf.Clamp01(elapsed / duration);
    _faderImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(startAlpha, targetAlpha, t));
    yield return null;
}

F — Stat Progression Curves
StatProgressionSO (/Scripts/Data/StatProgressionSO.cs) replaces every flat numeric stat with an AnimationCurve evaluated on a normalised level value [0, 1]:

// X axis: normalised level (0 = level 1, 1 = maxLevel)
// Y axis: stat value at that level
public AnimationCurve healthCurve         = AnimationCurve.Linear(0f, 50f,  1f, 500f);
public AnimationCurve damageCurve         = AnimationCurve.Linear(0f, 10f,  1f, 100f);
public AnimationCurve speedCurve          = AnimationCurve.Linear(0f, 2f,   1f, 5f);
public AnimationCurve attackCooldownCurve = AnimationCurve.Linear(0f, 2f,   1f, 0.5f);
public AnimationCurve attackRangeCurve    = AnimationCurve.Linear(0f, 1f,   1f, 3f);
public AnimationCurve goldRewardCurve     = AnimationCurve.Linear(0f, 10f,  1f, 200f);
If BaseUnitDataSO.statProgression != null, BaseUnit.Initialize() calls EvaluateHealth(currentLevel, maxLevel) etc. instead of reading static fields. If none is assigned, the flat BaseUnitDataSO values are used — the two modes are completely interchangeable at the asset level.


F — Movement Curves
StatProgressionSO.movementCurve (default EaseInOut(0, 0, 1, 1)) provides a normalised speed multiplier for smooth acceleration and deceleration during unit movement. Its Y value is sampled against elapsed normalised movement time:

// StatProgressionSO.cs
public float EvaluateMovement(float normalizedTime)
    => movementCurve.Evaluate(Mathf.Clamp01(normalizedTime));
A unit can start slow, accelerate, and decelerate as it approaches its target — all without any code changes, purely by reshaping the curve in the Inspector.


F — Spawn Pacing
StatProgressionSO.spawnPacingCurve (default EaseInOut(0, 2f, 1, 0.3f)) maps wave progress [0, 1] to spawn interval in seconds — a low Y means faster spawning:

// UnitSpawner reads this to throttle spawn timing
float spawnInterval = statProgression.EvaluateSpawnPacing(waveProgress);
At wave start the interval is 2 seconds (slow trickle); by wave end it drops to 0.3 seconds (rapid assault). The entire pacing feel is a single curve in the Inspector.


G — Save System
G — Binary Format
SaveManager (/Scripts/Systems/Save/SaveManager.cs) serialises SaveData to JSON via JsonUtility.ToJson(), encrypts the result with AES-256-CBC, and writes the raw bytes to gamesave.dat in Application.persistentDataPath. Loading decrypts the bytes, parses the JSON, and validates the SHA-256 checksum before returning data:

// Write path
byte[] encrypted = Encrypt(json);          // AES-CBC + PKCS7 padding
File.WriteAllBytes(SaveFilePath, encrypted);

// Read path
byte[] bytes = File.ReadAllBytes(path);
string json  = Decrypt(bytes);
SaveData data = JsonUtility.FromJson<SaveData>(json);
if (!ValidateChecksum(data)) return null;  // Reject tampered files
Before every write, the current file is copied to gamesave.dat.bak. On load, if the primary file fails (corrupt or tampered), the backup is tried automatically.


G — Versioning
SaveManager.CurrentSaveVersion is a public constant (currently = 2). Every SaveData instance stores a saveVersion field. Each breaking schema change increments this constant; SaveMigrationService handles all older versions step-by-step.

G — Migration
SaveMigrationService (/Scripts/Systems/Save/SaveMigrationService.cs) implements a while (data.saveVersion < CurrentSaveVersion) loop, applying one migration step at a time:

public static SaveData Migrate(SaveData data)
{
    while (data.saveVersion < SaveManager.CurrentSaveVersion)
    {
        switch (data.saveVersion)
        {
            case 1: data = MigrateV1ToV2(data); break;
            default:
                data.saveVersion = SaveManager.CurrentSaveVersion;
                break;
        }
    }
    return data;
}
v1 → v2: Added inventoryAssetNames (stash equipment list), languageCode string, and null-checked armySlots — all missing fields are filled with safe defaults. No data is ever discarded. Migrate() is called automatically by SaveManager.Load() before data is returned.

G — Meta Data
SaveData (/Scripts/Systems/Save/SaveData.cs) carries the following meta fields alongside all gameplay state:

public int    saveVersion;          // Schema version — drives migration
public string savedAt;              // ISO 8601 UTC timestamp of last write
public float  totalPlayTimeSeconds; // Accumulated play time in seconds
public int    saveCount;            // Number of times the file has been saved
public string checksum;             // SHA-256 of all other serialised fields
saveCount and savedAt are updated automatically on every SaveManager.Save(). The checksum is computed from the full JSON string (with checksum = string.Empty) and embedded before the final write — validated on every load.


H — Input System
H — New Unity Input System
GameInputHandler (/Scripts/Managers/InputDeviceManager/GameInputHandler.cs) is the sole integration point with the Unity Input System. It wraps the auto-generated InputSystem_Actions class and exposes clean C# events to the rest of the game:

public event Action OnConfirm;    // A button / Space / Left Click
public event Action OnCancel;     // B button / Escape / Right Click
public event Action OnSpeedCycle; // RB / Tab — cycles battle speed
public event Action OnReady;      // Start / Enter — triggers WAR! button
public Vector2 NavigateValue;     // WASD / Left Stick (polled every frame)
public Vector2 CursorDelta;       // Mouse position / Right Stick
No other class in the project references InputSystem_Actions directly. Two action maps are toggled via SwitchToUI() (Upgrade scene — drag-and-drop) and SwitchToPlayer() (Grid scene — battle controls).

H — Gamepad Support
GamepadCursor (/Scripts/Managers/InputDeviceManager/GamepadCursor.cs) drives a VirtualMouseInput component from the gamepad left stick, simulating a physical mouse cursor. This allows standard UGUI drag-and-drop (used in the equipment system) to work identically on gamepad as on mouse. buttonSouth (A / Cross) is mapped to simulated left click. GamepadCursor reads its input from GameInputHandler.Instance.CursorDelta every frame — keeping all input routing centralised.


H — Keyboard Support
Action	Keyboard Binding	Gamepad Binding
Confirm / Click	Space / Left Click	A / Cross
Cancel	Escape / Right Click	B / Circle
Ready (WAR!)	Enter	Start
Speed Cycle	Tab	RB
Navigate	WASD / Arrow Keys	Left Stick / D-Pad
All bindings are defined in the .inputactions asset and parsed through the auto-generated wrapper. There are no Input.GetKey(KeyCode.X) calls anywhere in the gameplay codebase.

I — Quick Gameplay Test
DebugToolsWindow (/Scripts/Editor/DebugToolsWindow.cs) is a custom EditorWindow opened via Tools → TDEV → Debug Tools (Ctrl+Shift+D). It provides six independently collapsible sections that work in both Edit Mode and Play Mode.


I — Start Game in Any Stage
The Quick Stage Start section populates a level slider (1 to N) by reading LevelManager.levels. ▶ Play + Load Level writes the target index to PlayerPrefs and enters Play Mode:

PlayerPrefs.SetInt("DebugTools_StartLevelIndex", levelIndex);
EditorApplication.isPlaying = true;
In Play Mode, ⏮ Previous / ⏭ Next use C# Reflection to call the private LevelManager.LoadLevel(int) and set _currentLevelIndex — bypassing the normal win/loss flow. 🔄 Reload Level reloads the current level instantly. The Auto Start Battle toggle removes the need to press the WAR! button each iteration.

I — Start with Any Inventory & Hero
The Quick Inventory Test section reads PlayerArmyDataSO (discovered via UnitSpawner.playerArmyData or AssetDatabase.FindAssets):

🔓 Unlock All Slots — sets unlockedPawnCount = 8, persists via GameSaveService, and broadcasts GameEvents.SetPawnCount(8) to refresh the Upgrade scene UI live.
🎲 Fill Random Equipment — queries all EquipmentDataSO assets by EquipmentSlot and assigns a random item to every slot of every unlocked unit.
🗑 Clear All Equipment — nulls all equipment references across every slot.
💰 +1000 Gold / 💸 Reset Gold — calls LevelManager.AddGold() in Play Mode; modifies PlayerPrefs in Edit Mode.
I — Start with Any Game Time
The Quick Time Test section provides a Time.timeScale slider (0–10) and six preset buttons (0.25×, 0.5×, 1×, 2×, 4×, 8×). Active preset is highlighted in green. A live Fixed Timestep readout (Time.fixedDeltaTime × 1000 ms) and a real-time FPS counter (1f / Time.unscaledDeltaTime) are displayed to immediately gauge simulation load.

I — Stress Testing
The Stress Testing section spawns 1–64 units using UnitFactory.Instance.CreateUnit() placed at random unoccupied GridNode positions. Options:

Both Teams — alternates player/enemy spawns for symmetric testing.
Enemy Only — floods the board with enemy units.
🗑 Clear All Units — calls UnitFactory.Instance.ReleaseUnit() on every active BaseUnit, then resets BattleManager state and GridManager occupancy.
Live BattleManager.playerUnits.Count and BattleManager.enemyUnits.Count are shown in real time with colour-coded status labels.

J — Localization
The project ships a custom, hand-rolled localization system rather than Unity's built-in Localization package. It loads JSON files from Resources/Localization/ and resolves string keys at runtime with a two-level fallback chain.

LocalizationManager (/Scripts/Systems/Localization/LocalizationManager.cs) — Static manager. Initialize() reads the saved language code from GameSaveService and loads the matching JSON file into a Dictionary<string, string>. SetLanguage(code) swaps the active dictionary, persists the selection via GameSaveService.SetLanguageCode(), and fires OnLanguageChanged so all live LocalizedText components refresh simultaneously. Fallback chain: current language → English (FallbackLanguage) → raw key string.

LocalizationKeys (/Scripts/Systems/Localization/LocalizationKeys.cs) — Eliminates magic strings entirely. All keys are const string fields following a strict domain.object or domain.object.field naming convention:

// Pattern: domain.object
public const string UI_START_BUTTON      = "ui.start_button";
public const string BATTLE_LEVEL_CLEARED = "battle.level_cleared";
public const string SETTINGS_LANGUAGE    = "settings.language";
public const string PAWNSHOP_NOT_ENOUGH  = "pawnshop.not_enough";
public const string DEFEAT_0             = "defeat.0";
public const string ITEM_SWORD           = "item.sword";
Domain prefixes in use: ui, battle, defeat, settings, pawnshop, upgrade, item, common.

LocalizedText (/Scripts/Systems/Localization/LocalizedText.cs) — A MonoBehaviour on any TextMeshProUGUI. It subscribes to LocalizationManager.OnLanguageChanged in OnEnable and re-calls LocalizationManager.Get(key) on every language switch — no manual refresh code needed anywhere.

Parameterised keys use {0}, {1} placeholders in JSON, resolved via string.Format:

// "battle.level_cleared" → "LEVEL CLEARED!\nBase Reward: {0} Gold"
string text = LocalizationManager.Get(LocalizationKeys.BATTLE_LEVEL_CLEARED, goldAmount);
Languages currently supported: Turkish (tr, default) and English (en).


A few notes on the README as written:

Section D (Google Sheets) — No such system exists in the codebase. I've written an honest note plus a forward-looking recommendation rather than fabricating implementations that aren't there.
Section J (Localization) — The project uses a fully custom JSON-based localization system, not Unity's built-in Localization package. The key naming scheme (domain.object) is faithfully documented from LocalizationKeys.cs.
GIF/PNG placeholders — All media/ paths follow the ![Feature - Demo](media/feature_name.gif) format as requested. Replace them with your actual screen captures and recordings.
All code blocks use only constructs and class names that directly appear in the source files read during analysis.


