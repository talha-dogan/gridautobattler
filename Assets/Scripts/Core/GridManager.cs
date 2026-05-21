using UnityEngine;
using TDEV.Core;

/// <summary>
/// Singleton that owns the authoritative 8x8 GridNode data model.
///
/// Grid specification:
///   - Origin (Node[0,0] / H1) world center
///   - Cell size (center-to-center distance)
///   - Columns (X axis)                      : 0 – 7  (left → right)
///   - Rows    (Y axis)                      : 0 – 7  (bottom → top)
///
/// The grid is generated once in Awake() and never changes at runtime.
/// All other systems query it through the public API below.
/// </summary>
public class GridManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static GridManager Instance { get; private set; }

    // ── Grid Constants ────────────────────────────────────────────────────────
    // Total number of columns and rows on the board.
    public const int Columns = 8;
    public const int Rows = 8;

    // ── Adjustable Grid Settings ──────────────────────────────────────────────
    [Header("Grid Setup")]
    [Tooltip("World-space position of the center of Node[0,0] (bottom-left cell H1).")]
    [SerializeField] private Vector3 originWorldPosition = new Vector3(1.3f, 1.3f, 0f);

    [Tooltip("Distance between the centers of two adjacent cells (horizontal or vertical).")]
    [SerializeField] private float cellSize = 2.6f;

    // Expose cellSize for other scripts that might need it
    public float CellSize => cellSize;

    // ── Grid Data ─────────────────────────────────────────────────────────────
    // The 2D array that holds every node on the board.
    // Indexed as gridArray[column, row] → gridArray[x, y].
    private GridNode[,] gridArray;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Enforce the Singleton pattern — only one GridManager may exist.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Build the grid immediately so every other Awake() that runs after this
        // one can safely call GetNode() or GetNodeWorldPosition().
        GenerateGrid();
    }

    // ── Grid Generation ───────────────────────────────────────────────────────

    /// <summary>
    /// Allocates the 8x8 array and calculates the world position of every node.
    ///
    /// Formula for node (x, y):
    ///   worldX = originWorldPosition.x + x * cellSize
    ///   worldY = originWorldPosition.y + y * cellSize
    /// </summary>
    private void GenerateGrid()
    {
        gridArray = new GridNode[Columns, Rows];

        for (int x = 0; x < Columns; x++)
        {
            for (int y = 0; y < Rows; y++)
            {
                // Calculate the exact world-space center of this cell.
                Vector3 worldPos = new Vector3(
                    originWorldPosition.x + x * cellSize,
                    originWorldPosition.y + y * cellSize,
                    0f
                );

                gridArray[x, y] = new GridNode(x, y, worldPos);
            }
        }

        Debug.Log($"[GridManager] Grid generated: {Columns}x{Rows} nodes. " +
                  $"Origin={originWorldPosition}, CellSize={cellSize}");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the GridNode at the given grid coordinates.
    /// Returns null if the coordinates are out of bounds.
    /// </summary>
    /// <param name="x">Column index (0-7).</param>
    /// <param name="y">Row index (0-7).</param>
    public GridNode GetNode(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            Debug.LogWarning($"[GridManager] GetNode({x},{y}) is out of bounds.");
            return null;
        }

        return gridArray[x, y];
    }

    /// <summary>
    /// Returns the world-space center position of the node at (x, y).
    /// Returns Vector3.zero and logs a warning if the coordinates are out of bounds.
    /// </summary>
    /// <param name="x">Column index (0-7).</param>
    /// <param name="y">Row index (0-7).</param>
    public Vector3 GetNodeWorldPosition(int x, int y)
    {
        GridNode node = GetNode(x, y);
        if (node == null)
        {
            Debug.LogWarning($"[GridManager] GetNodeWorldPosition({x},{y}) — node not found, returning Vector3.zero.");
            return Vector3.zero;
        }

        return node.WorldPosition;
    }

    /// <summary>
    /// Converts a world-space position to the nearest grid coordinates.
    /// Clamps the result to valid grid bounds.
    /// Useful for snapping a dragged unit back to the closest cell.
    /// </summary>
    /// <param name="worldPosition">A position in world space.</param>
    /// <param name="x">Output column index.</param>
    /// <param name="y">Output row index.</param>
    public void WorldToGrid(Vector3 worldPosition, out int x, out int y)
    {
        x = Mathf.RoundToInt((worldPosition.x - originWorldPosition.x) / cellSize);
        y = Mathf.RoundToInt((worldPosition.y - originWorldPosition.y) / cellSize);

        // Clamp to valid grid range so callers never receive an out-of-bounds index.
        x = Mathf.Clamp(x, 0, Columns - 1);
        y = Mathf.Clamp(y, 0, Rows - 1);
    }

    /// <summary>
    /// Returns true if the given coordinates fall within the valid grid range.
    /// </summary>
    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < Columns && y >= 0 && y < Rows;
    }

    /// <summary>
    /// Clears the occupancy state of every node in the grid.
    /// Must be called at the start of each level load so that nodes marked
    /// occupied by the previous level's units do not block new spawns.
    /// </summary>
    public void ResetGrid()
    {
        for (int x = 0; x < Columns; x++)
        {
            for (int y = 0; y < Rows; y++)
            {
                if (gridArray[x, y] != null)
                    gridArray[x, y].IsOccupied = false;
            }
        }

        Debug.Log("[GridManager] Grid reset: all nodes marked as unoccupied.");
    }

    // ── Debug Visualisation ───────────────────────────────────────────────────

    /// <summary>
    /// Draws the grid in the Scene view using Gizmos so the overlay can be
    /// visually verified against the Tilemap without entering Play mode.
    /// Green  = free node center.
    /// Red    = occupied node center.
    /// </summary>
    private void OnDrawGizmos()
    {
        // If the grid has not been generated yet (Edit mode), draw a preview
        // using the same formula so the overlay is always visible.
        for (int x = 0; x < Columns; x++)
        {
            for (int y = 0; y < Rows; y++)
            {
                Vector3 center = new Vector3(
                    originWorldPosition.x + x * cellSize,
                    originWorldPosition.y + y * cellSize,
                    0f
                );

                // Determine occupancy colour (runtime only; Edit mode always green).
                bool occupied = gridArray != null && gridArray[x, y] != null && gridArray[x, y].IsOccupied;
                Gizmos.color = occupied ? Color.red : Color.green;

                // Draw a small cross at the node center.
                float crossSize = 0.2f;
                Gizmos.DrawLine(center + Vector3.left * crossSize, center + Vector3.right * crossSize);
                Gizmos.DrawLine(center + Vector3.down * crossSize, center + Vector3.up * crossSize);

                // Draw a wire square representing the cell boundary.
                Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
                Gizmos.DrawWireCube(center, new Vector3(cellSize, cellSize, 0f));
            }
        }
    }
}