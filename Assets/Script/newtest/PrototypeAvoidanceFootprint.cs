using UnityEngine;

// Optional override when a model's physical colliders do not match its footprint.
[DisallowMultipleComponent]
public class PrototypeAvoidanceFootprint : MonoBehaviour
{
    [Tooltip("勾選後使用手動長寬；未勾選時仍依實際 Collider 計算。尺寸為物件本地座標單位，會套用 Transform Scale。")]
    public bool useManualSize;
    [Min(0.05f)] public float width = 0.8f;
    [Min(0.05f)] public float length = 1.2f;
    public Vector3 localCentre;

    public Vector3[] WorldCorners(Bounds verticalBounds)
    {
        var points = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = transform.TransformPoint(localCentre + new Vector3(
                (i & 1) == 0 ? -width * 0.5f : width * 0.5f, 0f,
                (i & 2) == 0 ? -length * 0.5f : length * 0.5f));
            point.y = (i & 4) == 0 ? verticalBounds.min.y : verticalBounds.max.y;
            points[i] = point;
        }
        return points;
    }

    private void OnDrawGizmosSelected()
    {
        if (!useManualSize) return;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(localCentre, new Vector3(width, 0.05f, length));
        Gizmos.matrix = previous;
    }
}
