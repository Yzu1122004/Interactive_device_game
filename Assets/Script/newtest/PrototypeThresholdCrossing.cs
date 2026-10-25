using UnityEngine;

// A virtual attendant supplies forces while the same dynamic chair/colliders
// remain active. This is a prototype animation controller, not real-chair guidance.
public class PrototypeThresholdCrossing : MonoBehaviour
{
    public PrototypeWheelchairController wheelchair;
    public Collider threshold;
    [Min(0.1f)] public float detectionDistance = 1.5f;
    [Min(0.01f)] public float maximumHeight = 0.4f;
    [Min(0.05f)] public float maximumThickness = 1.2f;
    [Min(0.01f)] public float clearance = 0.06f;
    [Min(0.05f)] public float assistSpeed = 0.25f;
    [Range(5f, 35f)] public float maximumTilt = 30f;
    [Min(1f)] public float timeout = 30f;
    public bool IsActive { get; private set; }
    public string Status { get; private set; } = "等待協助跨越";

    private Rigidbody body;
    private Vector3[] positions;
    private Quaternion[] rotations;
    private Vector3 desiredPosition;
    private Quaternion desiredRotation;
    private Vector3 forward;
    private int stage;
    private float elapsed;
    private float extraLift;
    private WheelCollider[] frontWheels;
    private WheelCollider[] allWheels;
    private Vector3[] wheelCentres;
    private float[] wheelRadii;
    private Vector3[][] hullCorners;
    private Vector3 startPosition;
    private float floorHeight;
    private float settledTime;
    private static readonly string[] Labels = { "靠近門檻", "協助抬起前輪", "前輪向前越過門檻", "協助後輪抬起", "後輪向前越過門檻", "通過門檻後轉平", "平穩落下" };
    public Vector3 TravelDirection { get { return forward; } }
    public int Stage { get { return stage; } }
    public float PositionError { get { return IsActive ? Vector3.Distance(body.position, StageGoal()) : 0f; } }
    public float RotationError { get { return IsActive ? Quaternion.Angle(body.rotation, rotations[stage]) : 0f; } }
    public float PlannedApproach { get; private set; }
    public float PlannedTilt { get; private set; }

    public bool TryBegin(out string message)
    {
        message = "門檻或輪椅尚未指定";
        if (wheelchair == null || threshold == null || !threshold.enabled || threshold.isTrigger || !threshold.gameObject.activeInHierarchy) return false;
        body = wheelchair.GetComponent<Rigidbody>();
        if (body == null || body.isKinematic || !body.useGravity || !body.detectCollisions) return false;
        if (!wheelchair.RouteIsGrounded)
        { message = "請先讓輪椅穩定接地，再協助跨越"; return false; }
        Vector3 start = body.position;
        forward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Bounds bounds = threshold.bounds;
        float along = Vector3.Dot(bounds.center - start, forward);
        float extent = ProjectExtent(bounds.extents, forward);
        float side = Mathf.Abs(Vector3.Dot(bounds.center - start, right));
        if (along + extent <= 0f || along - extent > detectionDistance || side > ProjectExtent(bounds.extents, right) + 0.35f)
        { message = "請先靠近並面向前方的門檻"; return false; }
        if (extent * 2f > maximumThickness)
        { message = "門檻碰撞範圍太厚，請面向較窄的一側，或調整門檻 Collider"; return false; }

        WheelCollider left = wheelchair.wheel_left_col, rear = wheelchair.wheel_right_col;
        if (left == null || rear == null) return false;
        Vector3 rearContact = (ContactPoint(left) + ContactPoint(rear)) * 0.5f;
        Vector3 frontContact = Vector3.zero;
        var front = new System.Collections.Generic.List<WheelCollider>();
        int count = 0;
        foreach (var wheel in wheelchair.GetComponentsInChildren<WheelCollider>())
            if (wheel != left && wheel != rear && wheel.enabled)
            { frontContact += ContactPoint(wheel); front.Add(wheel); count++; }
        if (count == 0) { message = "找不到輪椅前小輪，無法計算跨越姿態"; return false; }
        frontContact /= count;
        frontWheels = front.ToArray();
        CacheClearanceGeometry(start);
        float wheelbase = Vector3.Dot(frontContact - rearContact, forward);
        float height = bounds.max.y - rearContact.y;
        if (height <= 0.01f || height > maximumHeight || wheelbase < 0.2f)
        { message = "門檻高度或輪子配置不適合目前協助設定；請檢查 Collider 與 Maximum Height"; return false; }
        float tilt = Mathf.Atan2(height + clearance, wheelbase) * Mathf.Rad2Deg;
        if (tilt > maximumTilt)
        { message = "目前門檻需要的抬輪角度過大，請降低門檻或調整協助設定"; return false; }

        Quaternion level = Quaternion.LookRotation(forward, Vector3.up);
        Vector3 rearLocal = Quaternion.Inverse(body.rotation) * (rearContact - start);
        Vector3 frontLocal = Quaternion.Inverse(body.rotation) * (frontContact - start);
        Quaternion noseUp = Quaternion.AngleAxis(-tilt, right) * level;
        Quaternion rearUp = Quaternion.AngleAxis(tilt, right) * level;
        float entry = along - extent, exit = along + extent;
        // The chassis/footrest can protrude beyond the front caster centres.
        // Stop before the first solid part hits the threshold; otherwise the
        // approach never finishes and the lifting stage can never start.
        float frontExtent = Vector3.Dot(frontContact - start, forward);
        foreach (var wheel in frontWheels)
            frontExtent = Mathf.Max(frontExtent, Vector3.Dot(ContactPoint(wheel) - start, forward) + wheel.radius);
        foreach (var collider in wheelchair.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger || collider is WheelCollider || collider.attachedRigidbody != body) continue;
            frontExtent = Mathf.Max(frontExtent,
                Vector3.Dot(collider.bounds.center - start, forward) + ProjectExtent(collider.bounds.extents, forward));
        }
        float approach = Mathf.Max(0f, entry - frontExtent - clearance);
        PlannedApproach = approach;
        PlannedTilt = tilt;
        Vector3 approachPosition = start + forward * approach;
        Vector3 rearAnchor = approachPosition + level * rearLocal;
        Vector3 frontLiftPosition = rearAnchor - noseUp * rearLocal;
        float frontTravel = Mathf.Max(0f, exit + clearance - Vector3.Dot(frontLiftPosition + noseUp * frontLocal - start, forward));
        Vector3 frontAdvance = frontLiftPosition + forward * frontTravel;
        Vector3 frontAnchor = frontAdvance + noseUp * frontLocal;
        frontAnchor.y = rearContact.y;
        Vector3 rearLiftPosition = frontAnchor - rearUp * frontLocal;
        float rearTravel = Mathf.Max(0f, exit + clearance - Vector3.Dot(rearLiftPosition + rearUp * rearLocal - start, forward));
        Vector3 rearAdvance = rearLiftPosition + forward * rearTravel;
        // Clear the complete wheel/chassis envelope, including its sweep while
        // returning to level. A WheelCollider ray has no solid wheel sidewall.
        float rearExtent = Mathf.Min(RearExtent(level), RearExtent(rearUp));
        rearAdvance += forward * Mathf.Max(0f, exit + clearance + 0.04f -
            (Vector3.Dot(rearAdvance - start, forward) + rearExtent));
        Vector3 levelPosition = rearAdvance;
        levelPosition.y = Mathf.Max(frontAdvance.y, rearAdvance.y);
        Vector3 finish = levelPosition;
        floorHeight = rearContact.y;
        // The floor on the far side need not have precisely the starting height.
        foreach (var hit in Physics.RaycastAll(finish + Vector3.up * 2f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider != threshold && hit.collider.attachedRigidbody != body && hit.normal.y > 0.65f &&
                Mathf.Abs(hit.point.y - rearContact.y) < 0.5f)
                floorHeight = Mathf.Max(floorHeight, hit.point.y);
        finish.y = floorHeight - LowestWheel(level);
        positions = new[] { approachPosition, frontLiftPosition, frontAdvance, rearLiftPosition, rearAdvance, levelPosition, finish };
        rotations = new[] { level, noseUp, noseUp, rearUp, rearUp, level, level };
        desiredPosition = start;
        desiredRotation = body.rotation;
        stage = 0; elapsed = extraLift = settledTime = 0f;
        IsActive = true;
        Status = Labels[stage];
        message = Status;
        return true;
    }

    public void Tick(float dt, bool paused)
    {
        if (!IsActive) return;
        if (body == null || threshold == null || !threshold.enabled || !isActiveAndEnabled)
        { Cancel("協助已中止：輪椅或門檻狀態改變"); return; }
        if (!paused)
        {
            elapsed += dt;
            if (elapsed > timeout) { Cancel("協助跨越逾時，已停止；請檢查門檻尺寸與碰撞範圍"); return; }
            // The front suspension may extend when unloaded. Judge clearance
            // from actual wheel poses and add limited attendant support.
            if ((stage == 1 || stage == 3) && Vector3.Distance(body.position, StageGoal()) < 0.035f &&
                Quaternion.Angle(desiredRotation, rotations[stage]) < 2f && !RaisedWheelsClear())
            {
                float lift = assistSpeed * dt;
                extraLift += lift;
                if (extraLift > 0.35f) { Cancel("抬輪仍無法取得足夠間距，已停止協助，請檢查門檻尺寸"); return; }
                positions[stage] = StageGoal() + Vector3.up * lift;
                positions[stage + 1] += Vector3.up * lift;
            }
            desiredPosition = Vector3.MoveTowards(desiredPosition, StageGoal(), (stage == 6 ? assistSpeed * 0.4f : assistSpeed) * dt);
            desiredRotation = Quaternion.RotateTowards(desiredRotation, rotations[stage], 30f * dt);
            // Check intermediate poses too: safe endpoints alone do not stop
            // the wheels/chassis sweeping through the step while pitching.
            if (stage > 0 && stage < 6)
            {
                float safeY = ClearanceHeight(desiredPosition, desiredRotation);
                // Gain height before moving horizontally into a new envelope.
                if (body.position.y < safeY - 0.025f)
                {
                    desiredPosition.x = body.position.x;
                    desiredPosition.z = body.position.z;
                }
                desiredPosition.y = Mathf.Max(desiredPosition.y, safeY);
            }
        }
        wheelchair.ReleaseRouteWheels();
        // The external attendant supplies support against gravity and a damped
        // push. Neither pose/velocity writes nor collision disabling is used.
        Vector3 acceleration = (desiredPosition - body.position) * 35f - body.velocity * 12f - Physics.gravity;
        acceleration.x = Mathf.Clamp(acceleration.x, -4f, 4f);
        acceleration.z = Mathf.Clamp(acceleration.z, -4f, 4f);
        acceleration.y = Mathf.Clamp(acceleration.y, stage == 6 ? 0f : -5f, 24f);
        body.AddForce(acceleration, ForceMode.Acceleration);
        Quaternion error = desiredRotation * Quaternion.Inverse(body.rotation);
        error.ToAngleAxis(out float degrees, out Vector3 axis);
        if (degrees > 180f) degrees -= 360f;
        Vector3 angular = axis.normalized * (degrees * Mathf.Deg2Rad * 20f) - body.angularVelocity * 8f;
        body.AddTorque(Vector3.ClampMagnitude(angular, 10f), ForceMode.Acceleration);
        Status = (paused ? "協助暫停，保持支撐：" : "協助跨越：") + Labels[stage];
        if (paused) return;
        Vector3 goal = StageGoal();
        if (Vector3.Distance(desiredPosition, goal) < 0.01f &&
            Vector3.Distance(body.position, goal) < 0.03f &&
            Quaternion.Angle(body.rotation, rotations[stage]) < 1.5f && body.velocity.magnitude < 0.15f)
        {
            if ((stage == 1 || stage == 3) && !RaisedWheelsClear()) return;
            if (stage == 6)
            {
                // WheelCollider.isGrounded can flicker on this chair even at
                // rest. Combine the existing support probe with wheel height.
                bool landed = wheelchair.RouteIsGrounded &&
                    Mathf.Abs(ContactPoint(wheelchair.wheel_left_col).y - floorHeight) < 0.04f &&
                    Mathf.Abs(ContactPoint(wheelchair.wheel_right_col).y - floorHeight) < 0.04f &&
                    Quaternion.Angle(body.rotation, rotations[stage]) < 2f && Mathf.Abs(body.velocity.y) < 0.08f;
                settledTime = landed ? settledTime + dt : 0f;
                if (settledTime < 0.3f) return;
            }
            stage++;
            extraLift = 0f;
            if (stage == positions.Length) Cancel("協助跨越完成，已停住");
        }
        else if (stage == 6) settledTime = 0f;
    }

    public bool IsThresholdContact(Collider collider)
    { return IsActive && collider == threshold; }

    public void Cancel(string message = "協助跨越已取消")
    { IsActive = false; Status = message; }

    private static Vector3 ContactPoint(WheelCollider wheel)
    { wheel.GetWorldPose(out Vector3 center, out Quaternion rotation); return center - Vector3.up * wheel.radius; }

    private static float ProjectExtent(Vector3 extent, Vector3 axis)
    { return Mathf.Abs(axis.x) * extent.x + Mathf.Abs(axis.z) * extent.z; }

    private Vector3 StageGoal()
    {
        Vector3 goal = positions[stage];
        if (stage > 0 && stage < 6) goal.y = Mathf.Max(goal.y, ClearanceHeight(goal, rotations[stage]));
        return goal;
    }

    private void CacheClearanceGeometry(Vector3 start)
    {
        startPosition = start;
        allWheels = wheelchair.GetComponentsInChildren<WheelCollider>();
        wheelCentres = new Vector3[allWheels.Length];
        wheelRadii = new float[allWheels.Length];
        Quaternion inverse = Quaternion.Inverse(body.rotation);
        for (int i = 0; i < allWheels.Length; i++)
        {
            allWheels[i].GetWorldPose(out Vector3 centre, out Quaternion rotation);
            wheelCentres[i] = inverse * (centre - start);
            wheelRadii[i] = allWheels[i].radius;
        }
        var hull = new System.Collections.Generic.List<Vector3[]>();
        foreach (var collider in wheelchair.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger || collider is WheelCollider || collider.attachedRigidbody != body) continue;
            Bounds b = collider.bounds;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = inverse * (b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)) - start);
            hull.Add(corners);
        }
        hullCorners = hull.ToArray();
    }

    private float RearExtent(Quaternion rotation)
    {
        float minimum = float.PositiveInfinity;
        for (int i = 0; i < wheelCentres.Length; i++)
            minimum = Mathf.Min(minimum, Vector3.Dot(rotation * wheelCentres[i], forward) - wheelRadii[i]);
        foreach (var corners in hullCorners)
            foreach (var corner in corners) minimum = Mathf.Min(minimum, Vector3.Dot(rotation * corner, forward));
        return minimum;
    }

    private float LowestWheel(Quaternion rotation)
    {
        float minimum = float.PositiveInfinity;
        for (int i = 0; i < wheelCentres.Length; i++)
            minimum = Mathf.Min(minimum, (rotation * wheelCentres[i]).y - wheelRadii[i]);
        return minimum;
    }

    private float ClearanceHeight(Vector3 position, Quaternion rotation)
    {
        float minimumY = floorHeight - LowestWheel(rotation);
        Bounds step = threshold.bounds;
        float centre = Vector3.Dot(step.center - startPosition, forward);
        float extent = ProjectExtent(step.extents, forward);
        float along = Vector3.Dot(position - startPosition, forward);
        for (int i = 0; i < wheelCentres.Length; i++)
        {
            Vector3 offset = rotation * wheelCentres[i];
            float wheelAlong = along + Vector3.Dot(offset, forward);
            if (wheelAlong + wheelRadii[i] >= centre - extent - clearance &&
                wheelAlong - wheelRadii[i] <= centre + extent + clearance)
                minimumY = Mathf.Max(minimumY, step.max.y + clearance - offset.y + wheelRadii[i]);
        }
        foreach (var corners in hullCorners)
        {
            float low = float.PositiveInfinity, high = float.NegativeInfinity, bottom = float.PositiveInfinity;
            foreach (var corner in corners)
            {
                Vector3 offset = rotation * corner;
                float distance = along + Vector3.Dot(offset, forward);
                low = Mathf.Min(low, distance); high = Mathf.Max(high, distance); bottom = Mathf.Min(bottom, offset.y);
            }
            if (high >= centre - extent - clearance && low <= centre + extent + clearance)
                minimumY = Mathf.Max(minimumY, step.max.y + clearance - bottom);
        }
        return minimumY;
    }

    private bool RaisedWheelsClear()
    {
        float minimum = threshold.bounds.max.y + 0.02f;
        if (stage == 3)
            return ContactPoint(wheelchair.wheel_left_col).y >= minimum && ContactPoint(wheelchair.wheel_right_col).y >= minimum;
        foreach (var wheel in frontWheels) if (ContactPoint(wheel).y < minimum) return false;
        return true;
    }

    private void OnDisable() { Cancel(); }
}
