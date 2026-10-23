using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PrototypeRouteNetwork : MonoBehaviour
{
    public PrototypeRouteNode startNode;
    public bool showRoutes = true;
    [Min(0.05f)] public float nodeRadius = 0.35f;
    public Color roadColor = new Color(0.1f, 0.85f, 1f, 1f);
    public Color destinationColor = new Color(0.2f, 1f, 0.35f, 1f);

    public PrototypeRouteNode[] Nodes
    {
        get
        {
            var nodes = new List<PrototypeRouteNode>();
            foreach (var node in GetComponentsInChildren<PrototypeRouteNode>())
                if (Contains(node)) nodes.Add(node);
            return nodes.ToArray();
        }
    }

    public bool Contains(PrototypeRouteNode node)
    {
        return node != null && node.isActiveAndEnabled && node.Network == this;
    }

    // Interpret even a single Inspector reference as a two-way connection.
    public List<PrototypeRouteNode> GetNeighbors(PrototypeRouteNode node)
    {
        var result = new List<PrototypeRouteNode>();
        if (!Contains(node)) return result;
        foreach (var candidate in Nodes)
        {
            if (candidate == node) continue;
            bool outgoing = node.connections != null && node.connections.Contains(candidate);
            bool incoming = candidate.connections != null && candidate.connections.Contains(node);
            if (outgoing || incoming) result.Add(candidate);
        }
        return result;
    }

    public PrototypeRouteNode FindDestination(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination)) return null;
        foreach (var node in Nodes)
            if (node.IsDestination && string.Equals(node.destinationName.Trim(), destination.Trim(),
                StringComparison.OrdinalIgnoreCase)) return node;
        return null;
    }

    // Dijkstra chooses the shortest connected route by world-space road length.
    // This only plans a path; it does not move or rotate the wheelchair.
    public bool TryFindPath(PrototypeRouteNode from, PrototypeRouteNode to, out List<PrototypeRouteNode> path)
    {
        path = new List<PrototypeRouteNode>();
        if (!Contains(from) || !Contains(to)) return false;
        var open = new List<PrototypeRouteNode> { from };
        var visited = new HashSet<PrototypeRouteNode>();
        var cost = new Dictionary<PrototypeRouteNode, float> { { from, 0f } };
        var previous = new Dictionary<PrototypeRouteNode, PrototypeRouteNode>();
        while (open.Count > 0)
        {
            var current = open[0];
            foreach (var candidate in open)
                if (cost[candidate] < cost[current]) current = candidate;
            open.Remove(current);
            if (current == to)
            {
                while (true)
                {
                    path.Add(current);
                    if (current == from) break;
                    current = previous[current];
                }
                path.Reverse();
                return true;
            }
            visited.Add(current);
            foreach (var neighbor in GetNeighbors(current))
            {
                if (visited.Contains(neighbor)) continue;
                float nextCost = cost[current] + Vector3.Distance(current.transform.position, neighbor.transform.position);
                if (cost.TryGetValue(neighbor, out float oldCost) && oldCost <= nextCost) continue;
                cost[neighbor] = nextCost;
                previous[neighbor] = current;
                if (!open.Contains(neighbor)) open.Add(neighbor);
            }
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        if (!showRoutes) return;
        foreach (var node in Nodes)
        {
            Gizmos.color = node == startNode ? Color.yellow : node.IsDestination ? destinationColor : roadColor;
            Gizmos.DrawSphere(node.transform.position, nodeRadius);
            foreach (var neighbor in GetNeighbors(node))
            {
                if (node.GetInstanceID() >= neighbor.GetInstanceID()) continue;
                Gizmos.color = roadColor;
                Gizmos.DrawLine(node.transform.position, neighbor.transform.position);
            }
        }
    }
}
