<div align="center">

# 🎮 Modular Auto-Battler Core

[![Play on itch.io](https://img.shields.io/badge/Play_in_Browser-itch.io-FA5C5C?style=for-the-badge&logo=itch.io)](https://talha-dogan.itch.io/modular-auto-battler-core)

<img width="800" src="https://github.com/user-attachments/assets/d4e90e8e-f003-4ac5-be17-31eef263a105" alt="Gameplay" />

A tactical 2D core featuring tactical drawing mechanics and an autonomous combat system. <br>
**[👉 Click here to play the WebGL build!](https://talha-dogan.itch.io/modular-auto-battler-core)**

</div>

## 🗺️ So, how is it wired up?

The loop here is dead simple: you sketch a formation on the ground, hit "WAR!", and let the AI take the wheel. I was aiming for a totally hands-off experience—once your units hit the field, they’re on their own. They handle their own targeting and movement logic without any babysitting from the player.

<div align="center">

### 🎬 Menus & Level Flow
<img src="https://github.com/user-attachments/assets/d9fe5a79-8723-4055-a1f8-a99555aceefc" width="395" /> <img src="https://github.com/user-attachments/assets/2d8dc707-3e81-4054-ac42-b57334c1813a" width="395" />

*Quick look at the UI and level selection.*

---

### ✏️ Positioning Units
<img src="https://github.com/user-attachments/assets/b949c49c-1af6-45b6-ba0c-c020fd47ccf1" width="800" />

*The drawing-to-deployment mechanic in action.*

</div>

## 🏗️ Architecture & Decisions

I'm not a fan of "spaghetti code," so I tried to keep this project as modular as possible using **SOLID** principles. It makes adding new units or mechanics way less of a headache:

* **Event-Driven Chaos:** I used a **GameEvents** bus so the UI and gameplay don't even need to know each other exist. They just trigger events, and whoever needs to listen, listens.
* **Unit AI (FSM):** Dealing with unit states can get messy fast. I stuck with a **Finite State Machine** so each unit always knows if it should be moving, attacking, or just standing there.
* **Data-Driven (SO):** All unit stats—health, damage, speed—are tucked away in **ScriptableObjects**. This means I can balance the game in the Inspector without waiting for the code to recompile every time.
* **Optimization:** Constant `Instantiate` and `Destroy` calls are a performance killer. I built a custom **Object Pooling** system to recycle units and projectiles, keeping the frame rate steady.

## 🎨 Game Feel & Visual Feedback

A core mechanic isn't complete without the right "juice." The visual systems are completely decoupled from the logic:

* **Breathing & Locomotion:** Units have idle breathing animations and walking sways (`UnitBreathingVisuals`).
* **Dynamic Aiming:** Ranged units track targets with their weapons, and melee units have dedicated swing animations.
* **Damage Readability:** A pooled `DamageTextManager` handles rising combat text so the player can always read the battle flow.

<details>
<summary><b>🛠️ A Bit More Technical (Expand)</b></summary>
<br>

**1. ⚔️ Smart Targeting & Zero-Allocation Physics**
The FSM runs 4 main states. Target detection relies on zero-allocation physics (`Physics2D.OverlapCircle` techniques) to prevent GC spikes. To make combat look natural, I implemented a **Hysteresis** system (units won't switch targets unless the new target is significantly closer, e.g., 12%) and a **Focus Timer** (minimum 0.5s lock-on) to prevent jittery targeting.

**2. 🎯 Battle Cleanup**
A tricky part was units dying while the game was still looping through the active list. To fix this, I added a `_pendingRemovals` queue to safely clean up dead units at the end of each frame, avoiding `InvalidOperationException` errors.

**3. 🤖 Dynamic WaveDirector**
The AI doesn't just pick random enemies. It calculates a dynamic budget based on the base reward and weights your current army composition. If you spam ranged units, the AI dynamically assigns a +4 weight bias to anti-ranged counter units to give you a real challenge.

**4. ✏️ Drawing UX (Backtracking)**
The line drawing system calculates path length limits dynamically (`PathUtils.GetTotalLength`). I also implemented path backtracking—if the player moves the cursor backward, the line safely undoes itself, creating a much smoother UX before deploying `UnitSpawner.SpawnUnitsOnPath()`.

**5. 📊 Level Economy**
The `LevelManager` rewards you for being efficient. If you win with unused units still in your hand, you get a gold bonus. 
</details>

---

## 🚀 Performance: The Pooling System

I’m pretty strict about not wasting CPU cycles. By using `UnitFactory` and `ProjectileFactory`, the game barely has to create anything new during a fight. Everything is recycled from the pool.

<div align="center">
<img width="800" height="399" alt="AutoBattlerPool" src="https://github.com/user-attachments/assets/94aa247c-ebf5-4e6a-9c89-7cb0dc689804" />
  <p><i>The Hierarchy stays clean because objects are reused instead of destroyed.</i></p>
</div>

## 🛠️ Custom Editor Tools

I think a good project should be easy to balance for people who don't want to touch code. I built these two tools to speed things up:

<table>
  <tr>
    <th width="50%">⚙️ Level Generator</th>
    <th width="50%">📦 Data Management</th>
  </tr>
  <tr>
    <td align="center">
      <img src="https://github.com/user-attachments/assets/5966b033-3333-48a2-ac8f-4cf7bf2a6eef" width="100%" alt="TDEVLevelGenerator" />
    </td>
    <td align="center">
      <img src="https://github.com/user-attachments/assets/7f8db120-cc21-4fde-ade9-743d494d3d8c" width="100%" alt="AutoBattler-scriptable-object" />
    </td>
  </tr>
  <tr>
    <td>
      A quick tool to generate 100 levels. It uses templates like Grid or Wedge so I don't have to manually place every unit.
    </td>
    <td>
      All unit stats are Inspector-ready. You can tweak attack speed or HP and see the changes live without a recompile.
    </td>
  </tr>
</table>

## 🎬 Core Scene Flow

```mermaid
graph TD
    StartScene[Start Scene] --> ModeSelection{Pick Mode}
    ModeSelection -->|Campaign| CampaignScene[Load 100-Level Campaign]
    ModeSelection -->|AI Wave| AiWaveScene[Dynamic AI Drafting]
    
    CampaignScene --> Drawing[Player draws formation lines]
    AiWaveScene --> Drawing
    
    Drawing --> War["WAR!" starts the FSM simulation]
    War --> BattleEnd[Battle ends & Rewards calculated]
