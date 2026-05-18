using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TDEV.Core;

/// <summary>
/// Single Responsibility: UnitSpawner ONLY handles spawning mechanics.
/// Spawns enemy formations and player units in a random clump.
/// </summary>
public class UnitSpawner : MonoBehaviour
{
    public static UnitSpawner Instance { get; private set; }

    [Header("Enemy Positioning")]
    // The center point where the enemy formation will be centered (e.g., X=5, Y=0)
    public Vector3 enemyCenterPos = new Vector3(5f, 0f, 0f);

    [Header("Player Positioning")]
    // The center point where the player units will be spawned
    public Vector3 playerCenterPos = new Vector3(-5f, 0f, 0f);
    public float playerSpawnSpread = 2f;

    [Header("Campaign Data")]
    public LevelDataSO currentLevelData;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // -------------------------------------------------------------------------
    // Campaign
    // -------------------------------------------------------------------------

    // Called by LevelManager to set up the Campaign board
    public void PrepareLevel()
    {
        if (currentLevelData != null && currentLevelData.enemyFormation != null)
        {
            SpawnEnemyFormation(currentLevelData.enemyFormation);
            Debug.Log($"Campaign Level: {currentLevelData.name} deployed.");
        }
    }

    private void SpawnEnemyFormation(EnemyFormationSO formation)
    {
        foreach (var placement in formation.units)
        {
            Vector3 spawnPos = enemyCenterPos + new Vector3(placement.offset.x, placement.offset.y, 0);
            UnitFactory.Instance.CreateUnit(placement.unitData, spawnPos, Team.Enemy);
        }
    }

    // -------------------------------------------------------------------------
    // Battle start — entry point for the "WAR!" button
    // -------------------------------------------------------------------------

    // Called by the "WAR!" button in Campaign & Wave mode
    public void StartBattle()
    {
        // Spawn player units in a random clump before starting the battle
        if (currentLevelData != null)
        {
            SpawnPlayerUnits(currentLevelData);
        }

        if (BattleManager.Instance != null) 
        {
            BattleManager.Instance.StartBattle();
        }
    }

    // -------------------------------------------------------------------------
    // Player clump spawning
    // -------------------------------------------------------------------------

    private void SpawnPlayerUnits(LevelDataSO levelData)
    {
        // Spawn Melee Units
        for (int i = 0; i < levelData.meleeLimit; i++)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-playerSpawnSpread, playerSpawnSpread),
                Random.Range(-playerSpawnSpread, playerSpawnSpread),
                0f);
            UnitFactory.Instance.CreateUnit(levelData.meleeData, playerCenterPos + randomOffset, Team.Player);
        }

        // Spawn Ranged Units
        for (int i = 0; i < levelData.rangedLimit; i++)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-playerSpawnSpread, playerSpawnSpread),
                Random.Range(-playerSpawnSpread, playerSpawnSpread),
                0f);
            UnitFactory.Instance.CreateUnit(levelData.rangedData, playerCenterPos + randomOffset, Team.Player);
        }
    }
}
