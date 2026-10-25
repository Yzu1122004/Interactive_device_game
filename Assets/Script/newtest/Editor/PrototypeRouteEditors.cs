using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PrototypeRouteNetwork))]
public class PrototypeRouteNetworkEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var network = (PrototypeRouteNetwork)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Scene 視窗開啟 Gizmos 即可查看路線。黃點是起點，綠點是目的地。\n" +
            "按住 Ctrl 選取兩個節點，再使用下方按鈕連線。所有路段皆可雙向通行。", MessageType.Info);
        if (GUILayout.Button("替子物件加入路徑節點元件"))
        {
            foreach (Transform child in network.GetComponentsInChildren<Transform>(true))
            {
                if (child == network.transform || child.GetComponentInParent<PrototypeRouteNetwork>() != network) continue;
                if (child.GetComponent<PrototypeRouteNode>() == null)
                    Undo.AddComponent<PrototypeRouteNode>(child.gameObject);
            }
        }
        PrototypeRouteEditorTools.DrawConnectionButtons();
        if (network.startNode == null || !network.Contains(network.startNode))
            EditorGUILayout.HelpBox("請將起點節點指定到 Start Node。", MessageType.Warning);
        else
        {
            int edgeCount = 0, destinations = 0, unreachable = 0;
            foreach (var node in network.Nodes)
            {
                edgeCount += network.GetNeighbors(node).Count;
                if (node.IsDestination) destinations++;
                if (!network.TryFindPath(network.startNode, node, out var path)) unreachable++;
            }
            EditorGUILayout.HelpBox(network.Nodes.Length + " 個節點／" + edgeCount / 2 + " 條路段／" +
                destinations + " 個目的地\n" + (unreachable == 0 ? "所有節點皆可由起點到達" : unreachable + " 個節點尚未連通"),
                unreachable == 0 ? MessageType.Info : MessageType.Warning);
        }
    }
}

[CustomEditor(typeof(PrototypeRouteNode)), CanEditMultipleObjects]
public class PrototypeRouteNodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Connections 可拖入另一個節點來連線。新增目的地時，在 Destination Name 填名稱，例如「王阿姨家」。\n" +
            "也可以 Ctrl 選取兩個節點，用下方按鈕連線或解除連線。", MessageType.Info);
        PrototypeRouteEditorTools.DrawConnectionButtons();
        if (targets.Length != 1) return;
        var node = (PrototypeRouteNode)target;
        if (node.Network == null)
        { EditorGUILayout.HelpBox("請將節點放到 RouteNetwork 底下。", MessageType.Warning); return; }
        foreach (var connection in node.connections)
            if (connection != null && connection.Network != node.Network)
                EditorGUILayout.HelpBox("連線指向另一個路網，這條連線不會使用。", MessageType.Warning);
        EditorGUILayout.LabelField("可通往的路點（含反向連線）");
        foreach (var neighbor in node.Network.GetNeighbors(node))
            EditorGUILayout.LabelField("• " + neighbor.name);
    }
}

public static class PrototypeRouteEditorTools
{
    public static void DrawConnectionButtons()
    {
        using (new EditorGUI.DisabledScope(!GetSelectedPair(out var a, out var b) ||
            EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("連接選取的兩個節點")) SetConnection(a, b, true);
            if (GUILayout.Button("解除選取節點的連線")) SetConnection(a, b, false);
        }
    }

    private static bool GetSelectedPair(out PrototypeRouteNode a, out PrototypeRouteNode b)
    {
        a = b = null;
        if (Selection.gameObjects.Length != 2) return false;
        a = Selection.gameObjects[0].GetComponent<PrototypeRouteNode>();
        b = Selection.gameObjects[1].GetComponent<PrototypeRouteNode>();
        return a != null && b != null && a != b && a.Network != null && a.Network == b.Network;
    }

    [MenuItem("Prototype/Routes/Connect Selected Nodes")]
    private static void ConnectSelected()
    { if (GetSelectedPair(out var a, out var b)) SetConnection(a, b, true); }

    [MenuItem("Prototype/Routes/Connect Selected Nodes", true)]
    [MenuItem("Prototype/Routes/Disconnect Selected Nodes", true)]
    private static bool CanConnect() { return GetSelectedPair(out var a, out var b); }

    [MenuItem("Prototype/Routes/Disconnect Selected Nodes")]
    private static void DisconnectSelected()
    { if (GetSelectedPair(out var a, out var b)) SetConnection(a, b, false); }

    private static void SetConnection(PrototypeRouteNode a, PrototypeRouteNode b, bool connect)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Undo.RecordObjects(new Object[] { a, b }, connect ? "Connect route nodes" : "Disconnect route nodes");
        if (a.connections == null) a.connections = new List<PrototypeRouteNode>();
        if (b.connections == null) b.connections = new List<PrototypeRouteNode>();
        if (connect)
        {
            if (!a.connections.Contains(b)) a.connections.Add(b);
            if (!b.connections.Contains(a)) b.connections.Add(a);
        }
        else
        {
            a.connections.RemoveAll(node => node == b);
            b.connections.RemoveAll(node => node == a);
        }
        EditorUtility.SetDirty(a);
        EditorUtility.SetDirty(b);
        PrefabUtility.RecordPrefabInstancePropertyModifications(a);
        PrefabUtility.RecordPrefabInstancePropertyModifications(b);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(a.gameObject.scene);
        SceneView.RepaintAll();
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawRouteLabels(PrototypeRouteNetwork network, GizmoType gizmoType)
    {
        if (!network.showRoutes) return;
        foreach (var node in network.Nodes)
        {
            string label = node.name + (node == network.startNode ? "（起點）" : node.IsDestination ? "（目的地）" : "");
            Handles.Label(node.transform.position + Vector3.up * (network.nodeRadius + 0.25f), label);
        }
    }
}
