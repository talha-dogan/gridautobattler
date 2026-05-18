using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using TDEV.Core;

public class AiEnemyWaveGenerator : EditorWindow
{
    // List for multiple enemy types as seen in the image
    [SerializeField] private List<BaseUnitDataSO> enemyPool = new List<BaseUnitDataSO>();

    private BaseUnitDataSO playerMeleePrefab;
    private BaseUnitDataSO playerRangedPrefab;

    // Boundary settings for future scalability
    private float maxSpawnWidth = 16f;
    private float maxSpawnHeight = 8f;

    [MenuItem("TDEV/AiEnemyWaveGenerator")]
    public static void ShowWindow()
    {
        GetWindow<AiEnemyWaveGenerator>("AI Wave Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("AI Enemy Wave Generator (Dynamic Mode)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Use SerializedObject to render the List properly with +/- buttons
        SerializedObject so = new SerializedObject(this);
        so.Update();

        // 1. Enemy Configuration (Pool)
        EditorGUILayout.PropertyField(so.FindProperty("enemyPool"), new GUIContent("Enemy Unit Pool"), true);

        EditorGUILayout.Space(10);

        // 2. Player Configuration
        GUILayout.Label("Player Unit Data", EditorStyles.boldLabel);
        playerMeleePrefab = (BaseUnitDataSO)EditorGUILayout.ObjectField("Player Melee Data", playerMeleePrefab, typeof(BaseUnitDataSO), false);
        playerRangedPrefab = (BaseUnitDataSO)EditorGUILayout.ObjectField("Player Ranged Data", playerRangedPrefab, typeof(BaseUnitDataSO), false);

        EditorGUILayout.Space(10);

        // 3. Spawn Area Configuration
        GUILayout.Label("Spawn Area Bounds", EditorStyles.boldLabel);
        maxSpawnWidth = EditorGUILayout.FloatField("Max Width", maxSpawnWidth);
        maxSpawnHeight = EditorGUILayout.FloatField("Max Height", maxSpawnHeight);

        so.ApplyModifiedProperties();

        EditorGUILayout.Space(20);

        if (GUILayout.Button("GENERATE 20 AI WAVES", GUILayout.Height(50)))
        {
            if (enemyPool.Count == 0 || playerMeleePrefab == null || playerRangedPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign Enemy Pool and Player unit data!", "OK");
                return;
            }
            GenerateLevels();
        }
    }

    private void GenerateLevels()
    {
        string waveFolder = "Assets/GameData/WaveAi";

        // Folder safety check
        if (!AssetDatabase.IsValidFolder("Assets/GameData")) AssetDatabase.CreateFolder("Assets", "GameData");
        if (!AssetDatabase.IsValidFolder(waveFolder)) AssetDatabase.CreateFolder("Assets/GameData", "WaveAi");

        // Hardcoded progression for 20 levels
        int[] meleeLimits = { 3, 5, 4, 5, 6, 6, 7, 8, 8, 10, 6, 10, 10, 12, 12, 14, 15, 15, 18, 20 };
        int[] rangedLimits = { 0, 0, 2, 3, 3, 4, 4, 4, 5, 5, 8, 2, 5, 4, 6, 6, 8, 10, 10, 12 };
        int[] capacities = { 3, 5, 5, 7, 8, 9, 10, 10, 11, 13, 12, 12, 14, 14, 15, 18, 20, 22, 25, 30 };
        int[] rewards = { 100, 120, 150, 180, 200, 220, 250, 280, 300, 500, 320, 350, 380, 400, 450, 500, 550, 600, 700, 1500 };

        for (int i = 0; i < 20; i++)
        {
            int levelNum = i + 1;
            string levelName = $"Level_{levelNum:00}";

            // 1. Level Data SO creation
            LevelDataSO levelData = CreateInstance<LevelDataSO>();
            levelData.meleeData = playerMeleePrefab;
            levelData.rangedData = playerRangedPrefab;
            levelData.meleeLimit = meleeLimits[i];
            levelData.rangedLimit = rangedLimits[i];
            levelData.totalUnitCapacity = capacities[i];
            levelData.goldReward = rewards[i];

            // 2. Empty formation for AI to fill later
            EnemyFormationSO formation = CreateInstance<EnemyFormationSO>();
            AssetDatabase.CreateAsset(formation, $"{waveFolder}/{levelName}_Formation.asset");
            levelData.enemyFormation = formation;

            AssetDatabase.CreateAsset(levelData, $"{waveFolder}/{levelName}_Data.asset");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Success", "20 AI Waves generated with scalable Enemy Pool!", "Awesome");
    }
}