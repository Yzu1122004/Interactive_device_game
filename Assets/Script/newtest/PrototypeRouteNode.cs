using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PrototypeRouteNode : MonoBehaviour
{
    [Tooltip("Leave empty for a normal road point; set a name for a destination.")]
    public string destinationName = "";
    [Tooltip("Connected road points. Each connection is treated as a two-way road.")]
    public List<PrototypeRouteNode> connections = new List<PrototypeRouteNode>();

    public PrototypeRouteNetwork Network { get { return GetComponentInParent<PrototypeRouteNetwork>(); } }
    public bool IsDestination { get { return !string.IsNullOrWhiteSpace(destinationName); } }

    private void OnValidate()
    {
        if (connections == null) connections = new List<PrototypeRouteNode>();
        var seen = new HashSet<PrototypeRouteNode>();
        connections.RemoveAll(node => node == null || node == this || !seen.Add(node));
    }
}
