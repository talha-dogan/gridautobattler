using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Handles player input for drawing unit placement paths and spawning units along them.
/// Single Responsibility: path recording, unit-count projection, and spawn delegation.
///
/// UI DECOUPLING: This class no longer holds any TextMeshProUGUI references.
/// Whenever the melee or ranged limits change, GameEvents.SetDrawLimits() is broadcast
/// and GameUIManager updates the button labels — DrawingManager never touches UI directly.
/// </summary>
public class DrawingManager : MonoBehaviour
{
    public static DrawingManager Instance { get; private set; }

    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float minDistance = 0.2f;
    [SerializeField] private float _unitSpacing = 1.2f;

    private List<Vector3> _pathPoints = new List<Vector3>();
    private bool _isDrawing = false;
    private bool _isMeleeSelected = true;

    [Header("Level Progress")]
    public int meleeLimit;
    public int rangedLimit;

    [Header("Selection & Limits")]
    public BaseUnitDataSO currentSelectedUnit;
    public bool canDraw = true;

    // Hidden variables to store the current level's specific unit data.
    [HideInInspector] public BaseUnitDataSO meleeUnitData;
    [HideInInspector] public BaseUnitDataSO rangedUnitData;

    // Cached camera reference — Camera.main uses FindObjectsByType internally,
    // so calling it every frame in UpdateDrawing is expensive.
    private Camera _mainCamera;

    public int currentLimit
    {
        get { return _isMeleeSelected ? meleeLimit : rangedLimit; }
        set
        {
            if (_isMeleeSelected) meleeLimit = value;
            else rangedLimit = value;
        }
    }

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Standard Singleton guard: destroy any duplicate that arrives after
        // the first instance has already been registered.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Cache once at startup; avoids a scene search on every drawn frame.
        _mainCamera = Camera.main;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sets the placement limits for the current level and broadcasts the new
    /// values to the UI via GameEvents so button labels stay in sync.
    /// </summary>
    public void SetupLimits(int melee, int ranged)
    {
        meleeLimit  = melee;
        rangedLimit = ranged;
        BroadcastLimits();
    }

    public void SetMeleeSelection()
    {
        // Assigns internally stored data to prevent NullReferenceException.
        currentSelectedUnit = meleeUnitData;
        _isMeleeSelected    = true;
    }

    public void SetRangedSelection()
    {
        // Assigns internally stored data to prevent NullReferenceException.
        currentSelectedUnit = rangedUnitData;
        _isMeleeSelected    = false;
    }

    // -------------------------------------------------------------------------
    // Input loop
    // -------------------------------------------------------------------------

    private void Update()
    {
        if (!canDraw || currentSelectedUnit == null || currentLimit <= 0) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)                    StartDrawing();
        if (mouse.leftButton.isPressed && _isDrawing)                UpdateDrawing(mouse.position.ReadValue());
        if (mouse.leftButton.wasReleasedThisFrame && _isDrawing)     EndDrawing();
    }

    // -------------------------------------------------------------------------
    // Drawing logic
    // -------------------------------------------------------------------------

    private void StartDrawing()
    {
        _pathPoints.Clear();
        lineRenderer.positionCount = 0;
        _isDrawing = true;
    }

    private void UpdateDrawing(Vector2 screenPosition)
    {
        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, 10f));
        worldPos.z = 0f;

        // Backtrack detection: if the cursor moves back toward the penultimate
        // point, remove the last point to allow the player to "erase" strokes.
        if (_pathPoints.Count > 1)
        {
            float distToLast        = Vector3.Distance(worldPos, _pathPoints[_pathPoints.Count - 1]);
            float distToPenultimate = Vector3.Distance(worldPos, _pathPoints[_pathPoints.Count - 2]);

            if (distToPenultimate < distToLast && distToPenultimate < (minDistance * 3.0f))
            {
                _pathPoints.RemoveAt(_pathPoints.Count - 1);
                UpdateLineRenderer();
                return;
            }
        }

        // Enforce the unit-count cap: only extend the path if the projected
        // unit count stays within the current limit.
        float currentPathLength   = PathUtils.GetTotalLength(_pathPoints);
        int   projectedUnitCount  = Mathf.FloorToInt(currentPathLength / _unitSpacing) + 1;

        if (projectedUnitCount <= currentLimit)
        {
            if (_pathPoints.Count == 0 ||
                Vector3.Distance(_pathPoints[_pathPoints.Count - 1], worldPos) > minDistance)
            {
                _pathPoints.Add(worldPos);
                UpdateLineRenderer();
            }
        }
    }

    private void UpdateLineRenderer()
    {
        lineRenderer.positionCount = _pathPoints.Count;
        lineRenderer.SetPositions(_pathPoints.ToArray());
    }

    private void EndDrawing()
    {
        _isDrawing = false;

        if (UnitSpawner.Instance != null && _pathPoints.Count >= 2)
        {
            float length        = PathUtils.GetTotalLength(_pathPoints);
            int   unitsToSpawn  = Mathf.FloorToInt(length / _unitSpacing) + 1;

            unitsToSpawn = Mathf.Min(unitsToSpawn, currentLimit);

            UnitSpawner.Instance.SpawnUnitsOnPath(
                currentSelectedUnit, new List<Vector3>(_pathPoints), unitsToSpawn);

            currentLimit -= unitsToSpawn;
            if (currentLimit < 0) currentLimit = 0;

            // Broadcast updated limits — GameUIManager will refresh the button labels.
            BroadcastLimits();
        }

        _pathPoints.Clear();
        lineRenderer.positionCount = 0;
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fires GameEvents.SetDrawLimits so any UI subscriber can update its labels.
    /// This is the ONLY place that should call SetDrawLimits — keeps the broadcast
    /// logic centralised and easy to find.
    /// </summary>
    private void BroadcastLimits()
    {
        GameEvents.SetDrawLimits(meleeLimit, rangedLimit);
    }
}
