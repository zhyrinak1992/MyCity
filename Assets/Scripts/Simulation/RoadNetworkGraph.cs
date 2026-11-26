using System.Collections.Generic;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>Explicit walkable road graph. Road segments register their endpoint nodes and edges.</summary>
    public sealed class RoadNetworkGraph
    {
        private readonly HashSet<Vector2Int> nodes = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, HashSet<Vector2Int>> edges = new Dictionary<Vector2Int, HashSet<Vector2Int>>();

        public void Clear() { nodes.Clear(); edges.Clear(); }
        public void AddNode(Vector2Int node)
        {
            nodes.Add(node);
            if (!edges.ContainsKey(node)) edges[node] = new HashSet<Vector2Int>();
        }
        public void AddSegment(Vector2Int a, Vector2Int b)
        {
            if (a == b || (a.x != b.x && a.y != b.y)) return;
            AddNode(a); AddNode(b);
            edges[a].Add(b); edges[b].Add(a);
        }
        public bool Contains(Vector2Int node) { return nodes.Contains(node); }
        public bool HasSegment(Vector2Int a, Vector2Int b)
        {
            HashSet<Vector2Int> neighbours;
            return edges.TryGetValue(a, out neighbours) && neighbours.Contains(b);
        }
        public bool RemoveSegment(Vector2Int a, Vector2Int b)
        {
            HashSet<Vector2Int> fromA, fromB;
            bool removedA = edges.TryGetValue(a, out fromA) && fromA.Remove(b);
            bool removedB = edges.TryGetValue(b, out fromB) && fromB.Remove(a);
            // Keep isolated nodes: they remain valid anchors for later road extensions.
            return removedA || removedB;
        }
        public List<Vector2Int[]> GetSegments()
        {
            List<Vector2Int[]> result = new List<Vector2Int[]>();
            foreach (KeyValuePair<Vector2Int, HashSet<Vector2Int>> entry in edges)
                foreach (Vector2Int neighbour in entry.Value)
                    if (entry.Key.x < neighbour.x || (entry.Key.x == neighbour.x && entry.Key.y < neighbour.y))
                        result.Add(new Vector2Int[] { entry.Key, neighbour });
            return result;
        }

        public IEnumerable<Vector2Int> Neighbours(Vector2Int node)
        {
            HashSet<Vector2Int> result;
            return edges.TryGetValue(node, out result) ? result : Empty;
        }
        private static readonly HashSet<Vector2Int> Empty = new HashSet<Vector2Int>();


        /// <summary>Deterministic shortest path using registered road edges only.</summary>
        public bool TryFindPath(Vector2Int start, Vector2Int goal, out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();
            if (!nodes.Contains(start) || !nodes.Contains(goal)) return false;
            List<Vector2Int> open = new List<Vector2Int> { start };
            HashSet<Vector2Int> closed = new HashSet<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            Dictionary<Vector2Int, int> gScore = new Dictionary<Vector2Int, int> { { start, 0 } };
            Dictionary<Vector2Int, int> fScore = new Dictionary<Vector2Int, int> { { start, Heuristic(start, goal) } };
            while (open.Count > 0)
            {
                open.Sort(delegate(Vector2Int a, Vector2Int b)
                {
                    int fa = fScore.ContainsKey(a) ? fScore[a] : int.MaxValue;
                    int fb = fScore.ContainsKey(b) ? fScore[b] : int.MaxValue;
                    int compare = fa.CompareTo(fb);
                    if (compare != 0) return compare;
                    compare = a.x.CompareTo(b.x);
                    return compare != 0 ? compare : a.y.CompareTo(b.y);
                });
                Vector2Int current = open[0];
                open.RemoveAt(0);
                if (current == goal)
                {
                    path.Add(current);
                    Vector2Int previous;
                    while (cameFrom.TryGetValue(current, out previous))
                    { current = previous; path.Add(current); }
                    path.Reverse();
                    return true;
                }
                closed.Add(current);
                List<Vector2Int> neighbours = new List<Vector2Int>(Neighbours(current));
                neighbours.Sort(delegate(Vector2Int a, Vector2Int b)
                {
                    int compare = a.x.CompareTo(b.x);
                    return compare != 0 ? compare : a.y.CompareTo(b.y);
                });
                foreach (Vector2Int neighbour in neighbours)
                {
                    if (closed.Contains(neighbour)) continue;
                    int tentative = gScore[current] + 1;
                    int known;
                    if (gScore.TryGetValue(neighbour, out known) && tentative >= known) continue;
                    cameFrom[neighbour] = current;
                    gScore[neighbour] = tentative;
                    fScore[neighbour] = tentative + Heuristic(neighbour, goal);
                    if (!open.Contains(neighbour)) open.Add(neighbour);
                }
            }
            return false;
        }

        private static int Heuristic(Vector2Int a, Vector2Int b)
        { return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); }

        public Vector2Int NearestNode(Vector3 position, float spacing)
        {
            Vector2Int best = default(Vector2Int);
            float bestDistance = float.MaxValue;
            foreach (Vector2Int node in nodes)
            {
                float d = (new Vector3(node.x * spacing, position.y, node.y * spacing) - position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = node; }
            }
            return best;
        }
    }
}
