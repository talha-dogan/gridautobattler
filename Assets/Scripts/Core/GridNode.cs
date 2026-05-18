using UnityEngine;

namespace TDEV.Core
{
    /// <summary>
    /// Represents a single cell on the 8x8 battle grid.
    /// Stores the grid coordinates, the exact world-space center position,
    /// and whether the cell is currently occupied by a unit.
    /// </summary>
    public class GridNode
    {
        // ── Grid Coordinates ──────────────────────────────────────────────────
        // X: column index (0 = left, 7 = right)
        // Y: row index    (0 = bottom / player side, 7 = top / enemy side)
        public int X { get; private set; }
        public int Y { get; private set; }

        // ── World Position ────────────────────────────────────────────────────
        // The exact center of this cell in Unity world space.
        // Calculated once during grid generation and never changes.
        public Vector3 WorldPosition { get; private set; }

        // ── Occupancy ─────────────────────────────────────────────────────────
        // True when a unit is standing on this node.
        // Set by UnitSpawner during placement; cleared when the unit leaves.
        public bool IsOccupied { get; set; }

        // ── Constructor ───────────────────────────────────────────────────────
        /// <summary>
        /// Creates a new GridNode with its grid coordinates and pre-calculated world position.
        /// </summary>
        /// <param name="x">Column index (0-7).</param>
        /// <param name="y">Row index (0-7).</param>
        /// <param name="worldPosition">The world-space center of this cell.</param>
        public GridNode(int x, int y, Vector3 worldPosition)
        {
            X = x;
            Y = y;
            WorldPosition = worldPosition;
            IsOccupied = false;
        }

        /// <summary>
        /// Returns a human-readable description of this node for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"GridNode[{X},{Y}] @ {WorldPosition} | Occupied: {IsOccupied}";
        }
    }
}
