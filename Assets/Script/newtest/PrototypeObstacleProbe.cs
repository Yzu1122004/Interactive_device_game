using System.Collections.Generic;
using UnityEngine;

// Conservative wheelchair-sized sweeps; sensors never move the Rigidbody.
public static class PrototypeObstacleProbe
{
    private struct Shape { public Vector3 centre, half; public Quaternion rotation; }

    private static Shape ChairShape(Rigidbody body, float margin)
    {
        Quaternion yaw = Quaternion.LookRotation(PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward).normalized, Vector3.up);
        Quaternion inverse = Quaternion.Inverse(yaw);
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        Bounds worldBounds = new Bounds(body.position, Vector3.zero);
        bool found = false;
        foreach (var collider in body.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody != body) continue;
            Bounds part;
            if (collider is WheelCollider wheel)
            {
                wheel.GetWorldPose(out Vector3 centre, out Quaternion rotation);
                // A wheel is a thin disc, not a sphere as wide as its diameter.
                Vector3 axle = inverse * wheel.transform.right.normalized;
                Vector3 scale = wheel.transform.lossyScale;
                float radius = wheel.radius * Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                float halfWidth = 0.05f * Mathf.Abs(scale.x);
                Vector3 extents = new Vector3(
                    radius * Mathf.Sqrt(Mathf.Max(0f, 1f - axle.x * axle.x)) + halfWidth * Mathf.Abs(axle.x),
                    radius * Mathf.Sqrt(Mathf.Max(0f, 1f - axle.y * axle.y)) + halfWidth * Mathf.Abs(axle.y),
                    radius * Mathf.Sqrt(Mathf.Max(0f, 1f - axle.z * axle.z)) + halfWidth * Mathf.Abs(axle.z));
                part = new Bounds(inverse * (centre - body.position), extents * 2f);
            }
            else part = InFrame(ColliderCorners(collider), body.position, inverse);
            if (!found) { bounds = part; found = true; } else bounds.Encapsulate(part);
            worldBounds.Encapsulate(collider.bounds);
        }
        if (!found) bounds = new Bounds(Vector3.up * 0.55f, new Vector3(0.8f, 1f, 1.2f));
        var manual = body.GetComponent<PrototypeAvoidanceFootprint>();
        if (manual != null && manual.isActiveAndEnabled && manual.useManualSize)
        {
            Bounds footprint = InFrame(manual.WorldCorners(worldBounds), body.position, inverse);
            // Keep the physical height; only the horizontal footprint is overridden.
            Vector3 minPoint = bounds.min, maxPoint = bounds.max;
            minPoint.x = footprint.min.x; minPoint.z = footprint.min.z;
            maxPoint.x = footprint.max.x; maxPoint.z = footprint.max.z;
            bounds.SetMinMax(minPoint, maxPoint);
        }
        // Trim only the ground-contact strip so ordinary ground is not a wall.
        Vector3 min = bounds.min + Vector3.up * 0.08f;
        Vector3 max = bounds.max;
        Vector3 size = max - min;
        return new Shape {
            centre = (min + max) * 0.5f, rotation = yaw,
            half = new Vector3(size.x * 0.5f + margin,
                Mathf.Max(0.05f, size.y * 0.5f),
                size.z * 0.5f + margin)
        };
    }

    private static Vector3[] ColliderCorners(Collider collider)
    {
        Bounds b = collider.bounds;
        bool local = false;
        if (collider is BoxCollider box) { b = new Bounds(box.center, box.size); local = true; }
        else if (collider is MeshCollider mesh && mesh.sharedMesh != null) { b = mesh.sharedMesh.bounds; local = true; }
        var points = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = b.center + Vector3.Scale(b.extents, new Vector3(
                (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            points[i] = local ? collider.transform.TransformPoint(point) : point;
        }
        return points;
    }

    private static Bounds InFrame(Vector3[] points, Vector3 origin, Quaternion inverse)
    {
        Bounds bounds = new Bounds(inverse * (points[0] - origin), Vector3.zero);
        for (int i = 1; i < points.Length; i++) bounds.Encapsulate(inverse * (points[i] - origin));
        return bounds;
    }

    private static Bounds ObstacleBounds(Collider obstacle, Vector3 origin, Quaternion inverse)
    {
        var manual = obstacle.GetComponentInParent<PrototypeAvoidanceFootprint>();
        if (manual != null && manual.isActiveAndEnabled && manual.useManualSize)
            return InFrame(manual.WorldCorners(obstacle.bounds), origin, inverse);
        Bounds result = InFrame(ColliderCorners(obstacle), origin, inverse);
        // A compound Rigidbody obstacle (e.g. a car) may have several colliders.
        if (obstacle.attachedRigidbody != null)
            foreach (var part in obstacle.attachedRigidbody.GetComponentsInChildren<Collider>())
                if (part.enabled && !part.isTrigger && part.attachedRigidbody == obstacle.attachedRigidbody)
                    result.Encapsulate(InFrame(ColliderCorners(part), origin, inverse));
        return result;
    }

    public static Collider Blocked(Rigidbody body, Vector3 direction, float distance, float margin)
    {
        if (direction.sqrMagnitude < 0.001f) return null;
        Shape shape = ChairShape(body, margin);
        Vector3 centre = body.position + shape.rotation * shape.centre;
        Collider closest = null;
        float best = float.PositiveInfinity;
        foreach (var hit in Physics.BoxCastAll(centre, shape.half, direction.normalized, shape.rotation,
            distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.attachedRigidbody == body || hit.normal.y > 0.6f) continue;
            if (hit.distance < best) { best = hit.distance; closest = hit.collider; }
        }
        return closest;
    }

    public static bool TryPlan(Rigidbody body, int side, float offset, float length, float margin,
        Collider obstacle, bool returnToRoute, out List<Vector3> points)
    {
        return TryPlan(body, side, offset, length, margin, obstacle, returnToRoute, out points, out string description);
    }

    public static bool TryPlan(Rigidbody body, int side, float offset, float length, float margin,
        Collider obstacle, bool returnToRoute, out List<Vector3> points, out string description)
    {
        points = new List<Vector3>();
        description = "";
        Vector3 start = body.position;
        Shape shape = ChairShape(body, margin);
        Vector3 forward = shape.rotation * Vector3.forward, right = shape.rotation * Vector3.right;
        float pass = length;
        if (obstacle == null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy)
            obstacle = Blocked(body, forward, length + 1f, margin);
        if (obstacle != null)
        {
            Bounds b = ObstacleBounds(obstacle, start, Quaternion.Inverse(shape.rotation));
            PrototypeBypassDimensions.Calculate(shape.centre.x - shape.half.x, shape.centre.x + shape.half.x,
                shape.centre.z - shape.half.z, b.min.x, b.max.x, b.max.z, side, margin,
                out offset, out float requiredPass);
            // A wider obstacle needs a gentler, longer turn, not the same fixed S.
            length = Mathf.Max(length, offset * 2.5f);
            pass = Mathf.Max(length, requiredPass);
            description = "輪椅寬 " + (shape.half.x * 2f - margin * 2f).ToString("F2") + "／長 " +
                (shape.half.z * 2f - margin * 2f).ToString("F2") + " 公尺；障礙寬 " + b.size.x.ToString("F2") +
                "／長 " + b.size.z.ToString("F2") + " 公尺；偏移 " + offset.ToString("F2") + " 公尺";
        }
        else description = "前方未偵測到障礙，使用一般靠邊距離 " + offset.ToString("F2") + " 公尺";
        if (pass > 6f) return false;
        // S-shaped lane change; the parallel section clears the rear of the chair
        // before any return towards the original route is allowed.
        int samples = Mathf.CeilToInt((pass + (returnToRoute ? length : 0f)) / 0.08f);
        for (int i = 0; i <= samples; i++)
        {
            float along = (pass + (returnToRoute ? length : 0f)) * i / samples;
            float t = Mathf.Clamp01(along / length);
            float lateral = offset * t * t * (3f - 2f * t);
            if (along > pass)
            {
                t = Mathf.Clamp01((along - pass) / length);
                lateral = offset * (1f - t * t * (3f - 2f * t));
            }
            points.Add(start + forward * along + right * (side * lateral));
        }
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 direction = i + 1 < points.Count ? (points[i + 1] - points[i]).normalized : forward;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 centre = points[i] + rotation * shape.centre;
            foreach (var collider in Physics.OverlapBox(centre, shape.half, rotation, ~0, QueryTriggerInteraction.Ignore))
                if (collider.attachedRigidbody != body) return false;
            if (i + 1 < points.Count)
                foreach (var hit in Physics.BoxCastAll(centre, shape.half, direction, rotation,
                    Vector3.Distance(points[i], points[i + 1]), ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.attachedRigidbody != body && hit.normal.y < 0.6f) return false;
            // Do not approve a bypass whose centre/side wheels would cross a gap.
            for (int edge = -1; edge <= 1; edge++)
            {
                Vector3 probe = points[i] + rotation * Vector3.right * (shape.half.x * 0.75f * edge);
                bool supported = false;
                foreach (var hit in Physics.RaycastAll(probe + Vector3.up * 0.5f, Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.attachedRigidbody != body && hit.normal.y > 0.6f) { supported = true; break; }
                if (!supported) return false;
            }
        }
        return true;
    }
}
