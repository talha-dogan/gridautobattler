using UnityEngine;

using System.Collections.Generic;

using TDEV.Core;



/// <summary>

/// Responsible for all AI decision-making:

///   1. Analysing the player's army composition.

///   2. Drafting a counter-army within the level budget.

///   3. Spawning that army on the battlefield.

///   4. Broadcasting the result to the UI via GameEvents (no direct UI references).

///

/// Spawning coordinates are passed in by UnitSpawner so this class stays

/// independent of scene layout details.

/// </summary>

public class WaveDirector : MonoBehaviour

{

    public static WaveDirector Instance { get; private set; }



    private void Awake()

    {

        if (Instance == null) Instance = this;

        else Destroy(gameObject);

    }



    // -------------------------------------------------------------------------

    // Public entry point — called by UnitSpawner.AiThinkingRoutine

    // -------------------------------------------------------------------------



    /// <summary>

    /// Analyses the player's current army, drafts a counter-army, spawns it,

    /// and fires a GameEvents.SetStatusText with the AI's "thinking" result.

    /// </summary>

    public void ExecuteAiDrafting(LevelDataSO levelData, Vector3 enemyCenterPos, float spawnSpread)

    {

        // 1. Count the player's units from the BattleManager list — no scene scan needed.

        int playerMeleeCount = 0;

        int playerRangedCount = 0;



        if (BattleManager.Instance != null)

        {

            foreach (var unit in BattleManager.Instance.playerUnits)

            {

                if (unit.unitData == null) continue;

                if (unit.unitData.unitType == UnitType.Melee) playerMeleeCount++;

                else playerRangedCount++;

            }

        }



        // 2. Draft the counter-army using the budget-based algorithm.

        List<BaseUnitDataSO> aiArmy = DraftEnemyArmy(levelData, playerMeleeCount, playerRangedCount);



        int aiMeleeCount = 0;

        int aiRangedCount = 0;



        // 3. Spawn the AI army spread randomly around the enemy center position.

        for (int i = 0; i < aiArmy.Count; i++)

        {

            if (aiArmy[i].unitType == UnitType.Melee) aiMeleeCount++;

            else aiRangedCount++;



            Vector3 randomOffset = new Vector3(

                Random.Range(-spawnSpread, spawnSpread),

                Random.Range(-spawnSpread, spawnSpread),

                0f);



            UnitFactory.Instance.CreateUnit(aiArmy[i], enemyCenterPos + randomOffset, Team.Enemy);

        }



        // 4. Calculate a flavour win-probability and broadcast it to the UI.

        //    No direct reference to LevelManager.statusText — fully decoupled.

        int winProbability = Mathf.Clamp(

            50 + (aiArmy.Count - (playerMeleeCount + playerRangedCount)) * 5 + Random.Range(-10, 11),

            10, 95);



        string statusMessage =

            $"AI is thinking...\n" +

            $"I see {playerMeleeCount} melee and {playerRangedCount} ranged units.\n" +

            $"If I deploy {aiMeleeCount} melee and {aiRangedCount} ranged units,\n" +

            $"my win probability is {winProbability}%!";



        GameEvents.SetStatusText(statusMessage);



        Debug.Log($"<color=red>AI Deployed {aiArmy.Count} units to counter your " +

                  $"{playerMeleeCount + playerRangedCount} units!</color>");

    }



    // -------------------------------------------------------------------------

    // Budget-based drafting algorithm (pure logic — no side effects)

    // -------------------------------------------------------------------------



    /// <summary>

    /// Builds a list of enemy unit data within the level's dynamic budget,

    /// weighted toward units that counter the player's composition.

    /// </summary>

    public List<BaseUnitDataSO> DraftEnemyArmy(LevelDataSO levelData, int drawnMelee, int drawnRanged)

    {

        List<BaseUnitDataSO> draftedArmy = new List<BaseUnitDataSO>();



        if (levelData == null)

        {

            Debug.LogWarning("AI Director: levelData is null — cannot draft army.");

            return draftedArmy;

        }



        int dynamicBudget = levelData.goldReward + (drawnMelee * 10) + (drawnRanged * 15);

        int currentBudget = dynamicBudget;



        if (levelData.availableEnemies == null || levelData.availableEnemies.Count == 0)

        {

            Debug.LogWarning("AI Director: No enemies available in the roster!");

            return draftedArmy;

        }



        bool playerIsRangedHeavy = drawnRanged > drawnMelee;

        bool playerIsMeleeHeavy = drawnMelee > drawnRanged;



        // Allocate the candidate list ONCE outside the loop to avoid per-iteration

        // heap allocations (GC fix carried over from Task 1.2).

        List<TDEV.Core.EnemyCostData> affordableAndTacticalEnemies = new List<TDEV.Core.EnemyCostData>();



        int safetyCounter = 0;

        while (currentBudget > 0 && safetyCounter < 100)

        {

            safetyCounter++;



            // Reuse the list by clearing — zero heap allocation per iteration.

            affordableAndTacticalEnemies.Clear();



            foreach (var enemy in levelData.availableEnemies)

            {

                if (currentBudget >= enemy.spawnCost && enemy.spawnCost > 0)

                {

                    int selectionWeight = 1;



                    if (playerIsRangedHeavy && enemy.isGoodAgainstRanged) selectionWeight += 4;

                    if (playerIsMeleeHeavy && enemy.isGoodAgainstMelee) selectionWeight += 4;



                    // Add the entry multiple times to weight the random pick.

                    for (int i = 0; i < selectionWeight; i++)

                        affordableAndTacticalEnemies.Add(enemy);

                }

            }



            if (affordableAndTacticalEnemies.Count == 0) break;



            TDEV.Core.EnemyCostData selectedEnemy =

                affordableAndTacticalEnemies[Random.Range(0, affordableAndTacticalEnemies.Count)];



            draftedArmy.Add(selectedEnemy.enemyData);

            currentBudget -= selectedEnemy.spawnCost;

        }



        Debug.Log($"AI Math Complete -> Budget: {dynamicBudget}. Army Size: {draftedArmy.Count}");

        return draftedArmy;

    }

}

