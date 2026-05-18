using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using TDEV.Core;

public class CampaignLevelGenerator : EditorWindow
{
    [SerializeField] private List<BaseUnitDataSO> enemyPool = new List<BaseUnitDataSO>();

    private BaseUnitDataSO playerMelee;
    private BaseUnitDataSO playerRanged;

    private string folderPath = "Assets/GameData/Levels";

    // Camera bounds
    private float maxSpawnWidth = 16f;
    private float maxSpawnHeight = 8f;

    // ACTUAL UNIT PHYSICAL DIMENSIONS
    private float unitWidth = 1.5f;
    private float unitHeight = 2.5f;

    [MenuItem("TDEV/Unity Shape Generator")]
    public static void ShowWindow() => GetWindow<CampaignLevelGenerator>("Level Generator");

    private void OnGUI()
    {
        GUILayout.Label("Epic 100-Level Tactics Generator", EditorStyles.boldLabel);

        SerializedObject so = new SerializedObject(this);
        so.Update();

        SerializedProperty enemyPoolProp = so.FindProperty("enemyPool");
        EditorGUILayout.PropertyField(enemyPoolProp, new GUIContent("Enemy Unit Pool"), true);

        GUILayout.Space(10);
        playerMelee = (BaseUnitDataSO)EditorGUILayout.ObjectField("Player Melee Data", playerMelee, typeof(BaseUnitDataSO), false);
        playerRanged = (BaseUnitDataSO)EditorGUILayout.ObjectField("Player Ranged Data", playerRanged, typeof(BaseUnitDataSO), false);

        so.ApplyModifiedProperties();

        GUILayout.Space(20);

        GUILayout.Label("Spawn Area Bounds", EditorStyles.boldLabel);
        maxSpawnWidth = EditorGUILayout.FloatField("Max Width", maxSpawnWidth);
        maxSpawnHeight = EditorGUILayout.FloatField("Max Height", maxSpawnHeight);

        GUILayout.Space(20);

        if (GUILayout.Button("Generate 100 Tactical Levels", GUILayout.Height(40)))
        {
            if (enemyPool.Count == 0 || playerMelee == null || playerRanged == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign Enemy Pool and Player Unit Data!", "OK");
                return;
            }
            GenerateLevels();
        }
    }

    private void GenerateLevels()
    {
        CreateFolderStructure();

        int totalLevels = 100;

        try
        {
            for (int i = 1; i <= totalLevels; i++)
            {
                // Update Progress Bar so Unity doesn't freeze
                EditorUtility.DisplayProgressBar("Generating Campaign", $"Forging Level {i} of {totalLevels}...", (float)i / totalLevels);

                LevelDataSO levelData = CreateInstance<LevelDataSO>();
                levelData.meleeData = playerMelee;
                levelData.rangedData = playerRanged;

                // Difficulty Scaling for 100 Levels
                levelData.meleeLimit = 5 + (int)(i * 1.5f);
                levelData.rangedLimit = 2 + (int)(i * 0.75f);
                levelData.totalUnitCapacity = levelData.meleeLimit + levelData.rangedLimit;
                levelData.goldReward = 100 * i;

                EnemyFormationSO formation = CreateInstance<EnemyFormationSO>();
                int enemyUnitCount = levelData.totalUnitCapacity * 2;

                // Loop the 20 core tactics dynamically (1 to 20)
                int tacticCycle = ((i - 1) % 20) + 1;

                switch (tacticCycle)
                {
                    case 1: formation.units = GetGrid(enemyUnitCount, false, 1); break;      // Single Line
                    case 2: formation.units = GetGrid(enemyUnitCount, false, 0); break;      // Standard Block
                    case 3: formation.units = GetTriangle(enemyUnitCount, false); break;     // Wedge
                    case 4: formation.units = GetTriangle(enemyUnitCount, true); break;      // V-Shape
                    case 5: formation.units = GetArc(enemyUnitCount, false, false); break;   // Crescent
                    case 6: formation.units = GetArc(enemyUnitCount, true, false); break;    // Convex Shield
                    case 7: formation.units = GetColumns(enemyUnitCount, 2); break;          // Two Flanks
                    case 8: formation.units = GetColumns(enemyUnitCount, 3); break;          // Three Columns
                    case 9: formation.units = GetDiamond(enemyUnitCount); break;             // Diamond
                    case 10: formation.units = GetCross(enemyUnitCount); break;              // Plus Sign
                    case 11: formation.units = GetXShape(enemyUnitCount); break;             // X-Shape
                    case 12: formation.units = GetCircle(enemyUnitCount); break;             // Full Circle
                    case 13: formation.units = GetGrid(enemyUnitCount, true, 0); break;      // Checkerboard
                    case 14: formation.units = GetDiagonal(enemyUnitCount, true); break;     // Echelon Right
                    case 15: formation.units = GetDiagonal(enemyUnitCount, false); break;    // Echelon Left
                    case 16: formation.units = GetHollowBox(enemyUnitCount); break;          // Perimeter Box
                    case 17: formation.units = GetUShape(enemyUnitCount); break;             // U-Shape
                    case 18: formation.units = GetArc(enemyUnitCount, false, true); break;   // Double Crescent
                    case 19: formation.units = GetZigZag(enemyUnitCount); break;             // Zig-Zag Line
                    case 20: formation.units = GetGrid(enemyUnitCount, false, 0); break;     // Massive Horde Block
                }

                FitToBounds(formation.units);

                string formationName = $"L{i}_Formation";
                AssetDatabase.CreateAsset(formation, $"{folderPath}/{formationName}.asset");

                levelData.enemyFormation = formation;
                string levelName = $"Level_{i}_DATA";
                AssetDatabase.CreateAsset(levelData, $"{folderPath}/{levelName}.asset");
            }
        }
        finally
        {
            // Ensure the progress bar is always cleared, even if an error occurs
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Success", "100 Epic Tactical Levels Generated!", "Awesome");
    }

    private void CreateFolderStructure()
    {
        if (!AssetDatabase.IsValidFolder("Assets/GameData")) AssetDatabase.CreateFolder("Assets", "GameData");
        if (!AssetDatabase.IsValidFolder("Assets/GameData/Levels")) AssetDatabase.CreateFolder("Assets/GameData", "Levels");
    }

    // --- FORMATION ALGORITHMS ---

    private List<UnitPlacement> GetGrid(int count, bool checkerboard, int forceRows)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();
        int cols = forceRows == 1 ? count : Mathf.Min(count, Mathf.FloorToInt(maxSpawnWidth / unitWidth));
        int rows = Mathf.CeilToInt((float)count / cols);
        int placed = 0;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (placed >= count) break;
                float x = (c - (cols - 1) * 0.5f) * unitWidth;
                if (checkerboard && r % 2 != 0) x += unitWidth * 0.5f;
                float y = -r * unitHeight;
                placements.Add(CreatePlacement(new Vector2(x, y)));
                placed++;
            }
        }
        return placements;
    }

    private List<UnitPlacement> GetTriangle(int count, bool invert)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();
        int placed = 0, row = 0;

        while (placed < count)
        {
            int unitsInRow = row + 1;
            for (int col = 0; col < unitsInRow; col++)
            {
                if (placed >= count) break;
                float x = (col - (unitsInRow - 1) * 0.5f) * unitWidth;
                float y = invert ? row * unitHeight : -row * unitHeight;
                placements.Add(CreatePlacement(new Vector2(x, y)));
                placed++;
            }
            row++;
        }
        return placements;
    }

    private List<UnitPlacement> GetArc(int count, bool convex, bool doubleLayer)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();
        int placed = 0, row = 0;

        while (placed < count)
        {
            float radius = 3f + (row * unitHeight);
            if (doubleLayer && row % 2 != 0) radius += unitHeight;

            float arcLength = (120f * Mathf.Deg2Rad) * radius;
            int maxUnitsInRow = Mathf.FloorToInt(arcLength / unitWidth) + 1;
            int unitsToPlace = Mathf.Min(maxUnitsInRow, count - placed);
            float step = unitsToPlace > 1 ? 120f / (unitsToPlace - 1) : 0;

            for (int i = 0; i < unitsToPlace; i++)
            {
                float rad = (-60f + (i * step)) * Mathf.Deg2Rad;
                float x = -Mathf.Sin(rad) * radius;
                float y = convex ? Mathf.Cos(rad) * radius - radius : -Mathf.Cos(rad) * radius + radius;
                placements.Add(CreatePlacement(new Vector2(x, y - (row * unitHeight))));
                placed++;
            }
            row++;
        }
        return placements;
    }

    private List<UnitPlacement> GetColumns(int count, int numCols)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();
        int unitsPerCol = Mathf.CeilToInt((float)count / numCols);
        float spacing = maxSpawnWidth / (numCols + 1);

        int placed = 0;
        for (int r = 0; r < unitsPerCol; r++)
        {
            for (int c = 0; c < numCols; c++)
            {
                if (placed >= count) break;
                float x = (c * spacing) - (maxSpawnWidth * 0.5f) + spacing;
                float y = -r * unitHeight;
                placements.Add(CreatePlacement(new Vector2(x, y)));
                placed++;
            }
        }
        return placements;
    }

    private List<UnitPlacement> GetDiamond(int count)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();
        int side = Mathf.CeilToInt(Mathf.Sqrt(count / 2f));
        int placed = 0;

        for (int x = -side; x <= side; x++)
        {
            for (int y = -side; y <= side; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) <= side && placed < count)
                {
                    placements.Add(CreatePlacement(new Vector2(x * unitWidth, y * unitHeight)));
                    placed++;
                }
            }
        }
        return placements;
    }

    private List<UnitPlacement> GetCross(int count)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();
        int placed = 0;

        placements.Add(CreatePlacement(Vector2.zero));
        placed++;

        int step = 1;
        while (placed < count)
        {
            if (placed < count) { placements.Add(CreatePlacement(new Vector2(step * unitWidth, 0))); placed++; }
            if (placed < count) { placements.Add(CreatePlacement(new Vector2(-step * unitWidth, 0))); placed++; }
            if (placed < count) { placements.Add(CreatePlacement(new Vector2(0, step * unitHeight))); placed++; }
            if (placed < count) { placements.Add(CreatePlacement(new Vector2(0, -step * unitHeight))); placed++; }
            step++;
        }
        return placements;
    }

    private List<UnitPlacement> GetXShape(int count)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();
        int placed = 0;
        int step = 0;

        while (placed < count)
        {
            if (step == 0) { placements.Add(CreatePlacement(Vector2.zero)); placed++; }
            else
            {
                if (placed < count) { placements.Add(CreatePlacement(new Vector2(step * unitWidth, step * unitHeight))); placed++; }
                if (placed < count) { placements.Add(CreatePlacement(new Vector2(-step * unitWidth, step * unitHeight))); placed++; }
                if (placed < count) { placements.Add(CreatePlacement(new Vector2(step * unitWidth, -step * unitHeight))); placed++; }
                if (placed < count) { placements.Add(CreatePlacement(new Vector2(-step * unitWidth, -step * unitHeight))); placed++; }
            }
            step++;
        }
        return placements;
    }

    private List<UnitPlacement> GetCircle(int count)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();
        float radius = Mathf.Max(3f, (count * unitWidth) / (2 * Mathf.PI));

        for (int i = 0; i < count; i++)
        {
            float rad = (i * 360f / count) * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad) * radius;
            float y = Mathf.Sin(rad) * radius;
            placements.Add(CreatePlacement(new Vector2(x, y)));
        }
        return placements;
    }

    private List<UnitPlacement> GetDiagonal(int count, bool rightLeaning)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();
        for (int i = 0; i < count; i++)
        {
            float x = (i % 8) * unitWidth * (rightLeaning ? 1 : -1);
            float y = -(i % 8) * unitHeight - (Mathf.Floor(i / 8f) * unitHeight * 2);
            placements.Add(CreatePlacement(new Vector2(x, y)));
        }
        return placements;
    }

    private List<UnitPlacement> GetHollowBox(int count)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();
        int perimeterSize = Mathf.Max(2, Mathf.CeilToInt(count / 4f));
        int placed = 0;

        for (int i = -perimeterSize; i <= perimeterSize && placed < count; i++)
        {
            placements.Add(CreatePlacement(new Vector2(i * unitWidth, perimeterSize * unitHeight))); placed++;
            if (placed < count) { placements.Add(CreatePlacement(new Vector2(i * unitWidth, -perimeterSize * unitHeight))); placed++; }

            if (i != -perimeterSize && i != perimeterSize)
            {
                if (placed < count) { placements.Add(CreatePlacement(new Vector2(perimeterSize * unitWidth, i * unitHeight))); placed++; }
                if (placed < count) { placements.Add(CreatePlacement(new Vector2(-perimeterSize * unitWidth, i * unitHeight))); placed++; }
            }
        }
        return placements;
    }

    private List<UnitPlacement> GetUShape(int count)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();
        for (int i = 0; i < count; i++)
        {
            float sideDir = i % 2 == 0 ? 1 : -1;
            float x = sideDir * (2f + (i % 4) * unitWidth);
            float y = (i % 4) * unitHeight;
            placements.Add(CreatePlacement(new Vector2(x, y)));
        }
        return placements;
    }

    private List<UnitPlacement> GetZigZag(int count)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();
        for (int i = 0; i < count; i++)
        {
            float x = (i % 5) * unitWidth - (2.5f * unitWidth);
            float y = (i % 2 == 0 ? 0 : unitHeight) - (Mathf.Floor(i / 5f) * unitHeight * 2);
            placements.Add(CreatePlacement(new Vector2(x, y)));
        }
        return placements;
    }

    private UnitPlacement CreatePlacement(Vector2 offset)
    {
        return new UnitPlacement
        {
            unitData = enemyPool[UnityEngine.Random.Range(0, enemyPool.Count)],
            offset = offset
        };
    }

    private void FitToBounds(List<UnitPlacement> placements)
    {
        if (placements.Count == 0) return;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var p in placements)
        {
            if (p.offset.x < minX) minX = p.offset.x;
            if (p.offset.x > maxX) maxX = p.offset.x;
            if (p.offset.y < minY) minY = p.offset.y;
            if (p.offset.y > maxY) maxY = p.offset.y;
        }

        float currentWidth = maxX - minX;
        float currentHeight = maxY - minY;

        float scaleX = currentWidth > maxSpawnWidth ? maxSpawnWidth / currentWidth : 1f;
        float scaleY = currentHeight > maxSpawnHeight ? maxSpawnHeight / currentHeight : 1f;

        // Decreased minimum scale from 0.75f to 0.25f to accommodate massive Level 100 armies
        float finalScale = Mathf.Max(Mathf.Min(scaleX, scaleY), 0.25f);

        for (int i = 0; i < placements.Count; i++)
        {
            Vector2 scaledOffset = placements[i].offset * finalScale;

            scaledOffset.x += UnityEngine.Random.Range(-0.05f, 0.05f);
            scaledOffset.y += UnityEngine.Random.Range(-0.05f, 0.05f);

            UnitPlacement tempPlacement = placements[i];
            tempPlacement.offset = scaledOffset;
            placements[i] = tempPlacement;
        }
    }
}