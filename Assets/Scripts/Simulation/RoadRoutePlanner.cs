using System.Collections.Generic;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>A* pedestrian routing over explicitly registered road segments.</summary>
    public static class RoadRoutePlanner
    {
        private const float LaneSpacing = 14f;
        private const float WalkHeight = .65f;
        private static RoadNetworkGraph network;
        private struct Node { public int X, Z; public Node(int x, int z) { X=x; Z=z; } public Vector2Int Key { get { return new Vector2Int(X,Z); } } }

        public static void SetNetwork(RoadNetworkGraph graph) { network = graph; }

        public static bool TryBuildRoadPath(Vector2Int start, Vector2Int destination, out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();
            return network != null && network.TryFindPath(start, destination, out path);
        }

        public static int GetRoadDistanceKm(List<Vector2Int> path)
        {
            // Prototype map scale: one road-grid edge represents one simulation kilometre.
            return path == null || path.Count < 2 ? 0 : path.Count - 1;
        }

        public static List<Vector3> Build(Vector3 start, Vector3 destination)
        {
            List<Vector3> route = new List<Vector3>();
            if (network == null) { route.Add(start); route.Add(destination); return route; }
            Vector2Int startKey = network.NearestNode(start, LaneSpacing);
            Vector2Int goalKey = network.NearestNode(destination, LaneSpacing);
            Node startNode = new Node(startKey.x,startKey.y), goalNode = new Node(goalKey.x,goalKey.y);
            AddDistinct(route,start);
            AddOrthogonalConnector(route,start,ToWorld(startNode));
            List<Node> path = FindPath(startNode,goalNode);
            if (path == null || path.Count == 0) { AddDistinct(route,destination); return route; }
            for(int i=1;i<path.Count;i++) AddDistinct(route,ToWorld(path[i]));
            AddOrthogonalConnector(route,ToWorld(goalNode),destination);
            return SimplifyCollinear(route);
        }

        private static List<Node> FindPath(Node start, Node goal)
        {
            List<Node> open = new List<Node>{start};
            HashSet<Vector2Int> closed = new HashSet<Vector2Int>();
            Dictionary<Vector2Int,Vector2Int> came = new Dictionary<Vector2Int,Vector2Int>();
            Dictionary<Vector2Int,int> g = new Dictionary<Vector2Int,int>{{start.Key,0}};
            Dictionary<Vector2Int,int> f = new Dictionary<Vector2Int,int>{{start.Key,Heuristic(start,goal)}};
            while(open.Count>0)
            {
                int best=0;
                for(int i=1;i<open.Count;i++) if(Score(f,open[i].Key)<Score(f,open[best].Key)) best=i;
                Node current=open[best]; open.RemoveAt(best);
                if(current.Key==goal.Key) return Reconstruct(came,current.Key);
                closed.Add(current.Key);
                foreach(Vector2Int key in network.Neighbours(current.Key))
                {
                    if(closed.Contains(key)) continue;
                    int tentative=Score(g,current.Key)+Mathf.Abs(current.Key.x-key.x)+Mathf.Abs(current.Key.y-key.y);
                    if(tentative>=Score(g,key)) continue;
                    came[key]=current.Key; g[key]=tentative;
                    f[key]=tentative+Heuristic(new Node(key.x,key.y),goal);
                    bool present=false; for(int j=0;j<open.Count;j++) if(open[j].Key==key){present=true;break;}
                    if(!present) open.Add(new Node(key.x,key.y));
                }
            }
            return null;
        }
        private static List<Node> Reconstruct(Dictionary<Vector2Int,Vector2Int> came,Vector2Int current)
        {
            List<Node> result=new List<Node>{new Node(current.x,current.y)}; Vector2Int previous;
            while(came.TryGetValue(current,out previous)){current=previous;result.Add(new Node(current.x,current.y));}
            result.Reverse(); return result;
        }
        private static int Heuristic(Node a,Node b){return Mathf.Abs(a.X-b.X)+Mathf.Abs(a.Z-b.Z);}
        private static int Score(Dictionary<Vector2Int,int> values,Vector2Int key){int v;return values.TryGetValue(key,out v)?v:int.MaxValue/4;}
        private static Vector3 ToWorld(Node n){return new Vector3(n.X*LaneSpacing,WalkHeight,n.Z*LaneSpacing);}
        private static void AddOrthogonalConnector(List<Vector3> points,Vector3 a,Vector3 b){AddDistinct(points,new Vector3(b.x,WalkHeight,a.z));AddDistinct(points,b);}
        private static void AddDistinct(List<Vector3> points,Vector3 p){if(points.Count==0||Vector3.Distance(points[points.Count-1],p)>.1f)points.Add(p);}
        private static List<Vector3> SimplifyCollinear(List<Vector3> points)
        {
            if(points.Count<3)return points; List<Vector3> result=new List<Vector3>{points[0]};
            for(int i=1;i<points.Count-1;i++){Vector3 a=result[result.Count-1],b=points[i],c=points[i+1];Vector3 ab=b-a,bc=c-b;if(Mathf.Abs(ab.x*bc.z-ab.z*bc.x)>.01f)result.Add(b);}
            result.Add(points[points.Count-1]);return result;
        }
    }
}
