using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random; // avoids clash with System.Random

namespace AvoiderPlugin
{
    /// <summary>
    /// Generates points inside a width x height rectangle (origin at 0,0) where no two
    /// points are closer than "minDistance". Based on Bridson's "Fast Poisson Disk
    /// Sampling in Arbitrary Dimensions" (SIGGRAPH 2007).
    /// </summary>
    public class PoissonDiscSampler
    {
        private const int AttemptsPerPoint = 30; // "k" in Bridson's paper

        private readonly float width;
        private readonly float height;
        private readonly float minDistance;
        private readonly float minDistanceSqr;
        private readonly float cellSize;

        private readonly Vector2[,] grid;
        private readonly bool[,] occupied;
        private readonly int gridWidth;
        private readonly int gridHeight;
        private readonly List<Vector2> activeList = new List<Vector2>();

        public PoissonDiscSampler(float width, float height, float minDistance)
        {
            this.width = width;
            this.height = height;
            this.minDistance = Mathf.Max(0.01f, minDistance);
            minDistanceSqr = this.minDistance * this.minDistance;

            // Each cell can hold at most one point if its diagonal == minDistance.
            cellSize = this.minDistance / Mathf.Sqrt(2f);

            gridWidth = Mathf.Max(1, Mathf.CeilToInt(width / cellSize));
            gridHeight = Mathf.Max(1, Mathf.CeilToInt(height / cellSize));
            grid = new Vector2[gridWidth, gridHeight];
            occupied = new bool[gridWidth, gridHeight];
        }

        public IEnumerable<Vector2> Samples()
        {
            // First point is completely random.
            Vector2 first = new Vector2(Random.value * width, Random.value * height);
            yield return Accept(first);

            while (activeList.Count > 0)
            {
                int index = Random.Range(0, activeList.Count);
                Vector2 current = activeList[index];
                bool found = false;

                for (int i = 0; i < AttemptsPerPoint; i++)
                {
                    // Random point in the annulus between minDistance and 2 * minDistance.
                    float angle = Random.value * Mathf.PI * 2f;
                    float radius = Mathf.Sqrt(Random.value * 3f * minDistanceSqr + minDistanceSqr);
                    Vector2 candidate = current + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                    if (InBounds(candidate) && IsFarEnough(candidate))
                    {
                        found = true;
                        yield return Accept(candidate);
                        break;
                    }
                }

                if (!found)
                {
                    // Swap-remove this point from the active list.
                    activeList[index] = activeList[activeList.Count - 1];
                    activeList.RemoveAt(activeList.Count - 1);
                }
            }
        }

        private Vector2 Accept(Vector2 point)
        {
            activeList.Add(point);
            int gx = (int)(point.x / cellSize);
            int gy = (int)(point.y / cellSize);
            grid[gx, gy] = point;
            occupied[gx, gy] = true;
            return point;
        }

        private bool InBounds(Vector2 p)
        {
            return p.x >= 0f && p.x < width && p.y >= 0f && p.y < height;
        }

        private bool IsFarEnough(Vector2 p)
        {
            int gx = (int)(p.x / cellSize);
            int gy = (int)(p.y / cellSize);

            int xMin = Mathf.Max(gx - 2, 0);
            int xMax = Mathf.Min(gx + 2, gridWidth - 1);
            int yMin = Mathf.Max(gy - 2, 0);
            int yMax = Mathf.Min(gy + 2, gridHeight - 1);

            for (int x = xMin; x <= xMax; x++)
            {
                for (int y = yMin; y <= yMax; y++)
                {
                    if (occupied[x, y] && (grid[x, y] - p).sqrMagnitude < minDistanceSqr)
                        return false;
                }
            }
            return true;
        }
    }
}