using System.Collections.Generic;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>Walkable sidewalk graph. Coordinates are stored in decimetres for stable keys.</summary>
    public sealed class PedestrianNetworkGraph
    {
        private const float Scale = 10f;
        private readonly HashSet<Vector2Int> nodes = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, HashSet<Vector2Int>> edges = new Dictionary<Vector2Int, HashSet<Vector2Int>>();

        public void Clear() { nodes.Clear(); edges.Clear(); }
        private static Vector2Int Key(Vector3 p) { return new Vector2Int(Mathf.RoundToInt(p.x * Scale), Mathf.RoundToInt(p.z * Scale)); }
        private static Vector3 World(Vector2Int p) { return new Vector3(p.x / Scale, .68f, p.y / Scale); }
        private void AddNode(Vector2Int p) { nodes.Add(p); if (!edges.ContainsKey(p)) edges[p] = new HashSet<Vector2Int>(); }
        public void AddWalkway(Vector3 a, Vector3 b)
        {
            Vector2Int ka = Key(a), kb = Key(b);
            if (ka == kb) return;
            AddNode(ka); AddNode(kb); edges[ka].Add(kb); edges[kb].Add(ka);
        }
        public void ConnectCrossing(Vector3 a, Vector3 b) { AddWalkway(a, b); }
        public bool Contains(Vector3 p) { return nodes.Contains(Key(p)); }

        public List<Vector3> BuildRoute(Vector3 start, Vector3 destination)
        {
            List<Vector3> result = new List<Vector3>();
            if (nodes.Count == 0) { result.Add(start); result.Add(destination); return result; }
            Vector2Int s = Nearest(Key(start)), g = Nearest(Key(destination));
            result.Add(new Vector3(start.x, .68f, start.z));
            AddOrthogonal(result, result[result.Count - 1], World(s));
            List<Vector2Int> path = FindPath(s, g);
            if (path == null) { AddDistinct(result, destination); return result; }
            for (int i = 1; i < path.Count; i++) AddDistinct(result, World(path[i]));
            AddOrthogonal(result, World(g), destination);
            return Simplify(result);
        }
        private Vector2Int Nearest(Vector2Int p)
        {
            Vector2Int best = default(Vector2Int); float bestD = float.MaxValue;
            foreach (Vector2Int n in nodes) { float d = (n - p).sqrMagnitude; if (d < bestD) { bestD = d; best = n; } }
            return best;
        }
        private List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            List<Vector2Int> open = new List<Vector2Int> { start };
            HashSet<Vector2Int> closed = new HashSet<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> came = new Dictionary<Vector2Int, Vector2Int>();
            Dictionary<Vector2Int, float> g = new Dictionary<Vector2Int, float> { { start, 0f } };
            Dictionary<Vector2Int, float> f = new Dictionary<Vector2Int, float> { { start, Heuristic(start, goal) } };
            while (open.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < open.Count; i++) if (Score(f, open[i]) < Score(f, open[best])) best = i;
                Vector2Int current = open[best]; open.RemoveAt(best);
                if (current == goal)
                {
                    List<Vector2Int> path = new List<Vector2Int> { current }; Vector2Int prev;
                    while (came.TryGetValue(current, out prev)) { current = prev; path.Add(current); }
                    path.Reverse(); return path;
                }
                closed.Add(current);
                HashSet<Vector2Int> neighbours;
                if (!edges.TryGetValue(current, out neighbours)) continue;
                foreach (Vector2Int next in neighbours)
                {
                    if (closed.Contains(next)) continue;
                    float tentative = Score(g, current) + Vector2Int.Distance(current, next);
                    if (tentative >= Score(g, next)) continue;
                    came[next] = current; g[next] = tentative; f[next] = tentative + Heuristic(next, goal);
                    if (!open.Contains(next)) open.Add(next);
                }
            }
            return null;
        }
        private static float Heuristic(Vector2Int a, Vector2Int b) { return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); }
        private static float Score(Dictionary<Vector2Int, float> map, Vector2Int key) { float value; return map.TryGetValue(key, out value) ? value : float.MaxValue / 4f; }
        private static void AddDistinct(List<Vector3> list, Vector3 p) { if (list.Count == 0 || Vector3.Distance(list[list.Count - 1], p) > .12f) list.Add(p); }
        private static void AddOrthogonal(List<Vector3> list, Vector3 a, Vector3 b) { AddDistinct(list, new Vector3(b.x, .68f, a.z)); AddDistinct(list, new Vector3(b.x, .68f, b.z)); }
        private static List<Vector3> Simplify(List<Vector3> points)
        {
            if (points.Count < 3) return points;
            List<Vector3> result = new List<Vector3> { points[0] };
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 a = result[result.Count - 1], b = points[i], c = points[i + 1];
                Vector3 ab = b - a, bc = c - b;
                if (Mathf.Abs(ab.x * bc.z - ab.z * bc.x) > .01f) result.Add(b);
            }
            result.Add(points[points.Count - 1]); return result;
        }
    }
}
