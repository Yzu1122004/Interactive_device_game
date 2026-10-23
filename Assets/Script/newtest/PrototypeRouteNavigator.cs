using System.Collections.Generic;
using UnityEngine;

// Follow the road graph through the existing wheel motors. The Rigidbody stays
// dynamic and gravity/collision response remain owned by Unity's physics engine.
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(Rigidbody), typeof(PrototypeWheelchairController))]
public class PrototypeRouteNavigator : MonoBehaviour
{
    public PrototypeRouteNetwork network;
    public PrototypeWheelchairController wheelchair;
    [Min(0.1f)] public float moveSpeed = 2.5f;
    [Min(1f)] public float rotationSpeed = 100f;
    [Min(0.01f)] public float arrivalTolerance = 0.5f;
    [Min(0.1f)] public float maxAttachDistance = 2f;
    [Min(0.05f)] public float groundContactGraceTime = 0.25f;
    public bool recordDiagnosticsInEditor = true;
    [Header("Obstacle Recovery Nudges")]
    [Min(0.05f)] public float nudgeDistance = 0.3f;
    [Min(1f)] public float nudgeAngle = 15f;
    [Min(0.05f)] public float nudgeMoveSpeed = 0.35f;
    [Min(1f)] public float nudgeRotationSpeed = 35f;
    [Min(1f)] public float nudgeTimeout = 8f;
    [SerializeField] private string status = "等待路線指令";
    public string Status { get { return status; } }
    public bool IsActive { get { return ownsBody; } }
    public bool IsPaused { get { return paused; } }

    private Rigidbody body;
    private bool ownsBody, paused, backwards, uTurning;
    private CollisionDetectionMode originalCollisionDetection;
    private int controlRevision;
    private float airborneTime;
    private Vector3 driveDirection;
    private PrototypeRouteNode from, to;
    private PrototypeRouteNode destination;
    private PrototypeVoiceCommand pendingTurn;
    private readonly Queue<PrototypeRouteNode> destinationSteps = new Queue<PrototypeRouteNode>();
    private Vector3 attachment;
    private bool attaching;
    private bool aligning;
    private bool settlingAtTarget;
    private bool nudging, nudgeTurning, nudgeSettling;
    private float nudgeSign, nudgeElapsed;
    private Vector3 nudgeStart, nudgeAxis;
    private Quaternion nudgeRotation;
    private Quaternion uTurnTarget;
    private float diagnosticHeadingError;
    private float diagnosticTargetDistance;
#if UNITY_EDITOR
    private System.IO.StreamWriter physicsTrace;
    private int diagnosticTicks;
#endif

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (wheelchair == null) wheelchair = GetComponent<PrototypeWheelchairController>();
    }

    public void PauseForVoiceInput()
    {
        if (!ownsBody) return;
        paused = true;
        wheelchair.DriveRoute(0f, 0f, true);
        driveDirection = Vector3.zero;
        status = "已暫停，等待新的語音指令";
    }

    public bool TryHandleTranscript(string transcript, out string message)
    {
        if (PrototypeVoiceCommandParser.TryParse(transcript, out var command))
        {
            Execute(command);
            message = status;
            return true;
        }
        string text = PrototypeVoiceCommandParser.NormalizeText(transcript);
        if (network != null && !string.IsNullOrEmpty(text))
        {
            foreach (var node in network.Nodes)
            {
                if (!node.IsDestination) continue;
                string name = PrototypeVoiceCommandParser.NormalizeText(node.destinationName);
                if (string.IsNullOrEmpty(name)) continue;
                if (text == name || text == "去" + name || text == "到" + name || text == "前往" + name ||
                    text == "帶我去" + name || text == "带我去" + name || text == "導航到" + name || text == "导航到" + name)
                {
                    GoTo(node);
                    message = status;
                    return true;
                }
            }
        }
        message = "未辨識為路線指令，請說前進、後退、左轉、右轉、掉頭、停止或目的地";
        return false;
    }

    public string DestinationHint()
    {
        var names = new List<string>();
        if (network != null)
            foreach (var node in network.Nodes) if (node.IsDestination) names.Add(node.destinationName);
        return string.Join("、", names);
    }

    public void Execute(PrototypeVoiceCommand command)
    {
        if (command == PrototypeVoiceCommand.Stop)
        { StopNavigation("已停止路線移動"); wheelchair.StopMovement(); return; }
        if (PrototypeVoiceCommandParser.IsNudge(command))
        {
            if (Ready(false)) BeginNudge(command);
            return;
        }
        if (!Ready()) return;
        if (nudging) StopNavigation();
        if (command == PrototypeVoiceCommand.Left || command == PrototypeVoiceCommand.Right)
        {
            destination = null;
            destinationSteps.Clear();
            pendingTurn = command;
            if (!ownsBody || to == null || uTurning)
            {
                if (!BeginDirectional(false)) return;
            }
            paused = false;
            uTurning = false;
            status = "沿線前進，於下一個可" + PrototypeVoiceCommandParser.GetLabel(command) + "的路口轉向";
            return;
        }
        destination = null;
        destinationSteps.Clear();
        pendingTurn = PrototypeVoiceCommand.None;
        if (command == PrototypeVoiceCommand.UTurn)
        {
            if (!Locate(out var a, out var b, out var point) || !Acquire(point)) return;
            uTurnTarget = Quaternion.AngleAxis(180f, Vector3.up) * body.rotation;
            uTurning = true;
            attaching = false;
            paused = false;
            status = "掉頭中：原地轉半圈後沿線返回";
        }
        else if (command == PrototypeVoiceCommand.Forward || command == PrototypeVoiceCommand.Backward)
        {
            if (!BeginDirectional(command == PrototypeVoiceCommand.Backward)) return;
            status = backwards ? "保持面向，沿線後退" : "沿線前進；路口優先直行";
        }
    }

    private bool Ready(bool requireNetwork = true)
    {
        if (!isActiveAndEnabled || (requireNetwork && (network == null || !network.isActiveAndEnabled)) || body == null ||
            wheelchair == null || !wheelchair.isActiveAndEnabled)
        { status = "路網或輪椅控制器尚未接好"; return false; }
        if (wheelchair.HasManualInput || Input.GetKey(wheelchair.brakeKey))
        { status = "請先放開手推、WASD 與煞車，再發出路線指令"; return false; }
        if (body.isKinematic || !body.useGravity || !body.detectCollisions || !wheelchair.CanDriveRoutePhysics)
        { status = "請啟用輪子碰撞、Rigidbody 重力與碰撞，並關閉 Is Kinematic"; return false; }
        return true;
    }

    private void BeginNudge(PrototypeVoiceCommand command)
    {
        StopNavigation();
        Acquire(body.position);
        nudging = true;
        nudgeSettling = false;
        nudgeElapsed = 0f;
        nudgeTurning = command == PrototypeVoiceCommand.NudgeLeft || command == PrototypeVoiceCommand.NudgeRight;
        nudgeSign = command == PrototypeVoiceCommand.NudgeBackward || command == PrototypeVoiceCommand.NudgeLeft ? -1f : 1f;
        nudgeStart = body.position;
        nudgeAxis = PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward).normalized;
        nudgeRotation = nudgeTurning ? Quaternion.AngleAxis(nudgeSign * nudgeAngle, Vector3.up) * body.rotation : body.rotation;
        status = "微調中：「" + PrototypeVoiceCommandParser.GetLabel(command) + "」，完成後停住";
    }

    private void TickNudge(float dt)
    {
        nudgeElapsed += dt;
        if (nudgeElapsed > nudgeTimeout)
        { StopNavigation("微調已逾時，可能被障礙物擋住；請改用其他方向"); return; }
        float angle = Vector3.SignedAngle(PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward),
            PrototypeRouteGeometry.Flat(nudgeRotation * Vector3.forward), Vector3.up);
        float remaining = nudgeDistance - Vector3.Dot(PrototypeRouteGeometry.Flat(body.position - nudgeStart), nudgeAxis) * nudgeSign;
        diagnosticHeadingError = angle;
        diagnosticTargetDistance = nudgeTurning ? 0f : Mathf.Max(0f, remaining);
        if (nudgeTurning ? Mathf.Abs(angle) <= 1.5f : remaining <= 0.025f) nudgeSettling = true;
        if (nudgeSettling)
        {
            driveDirection = Vector3.zero;
            wheelchair.DriveRoute(0f, 0f, true);
            if (PrototypeRouteGeometry.Flat(body.velocity).magnitude < 0.08f && Mathf.Abs(body.angularVelocity.y) < 0.08f)
                StopNavigation("微調完成，已停住；可繼續微調，或說前進／目的地回到路線");
            return;
        }
        float speed = nudgeTurning ? 0f : nudgeSign * Mathf.Min(nudgeMoveSpeed, Mathf.Max(0.12f, remaining * 2f));
        driveDirection = nudgeTurning ? Vector3.zero : nudgeAxis * nudgeSign;
        float yaw = Mathf.Clamp(angle * 3f, -nudgeRotationSpeed, nudgeRotationSpeed) * Mathf.Deg2Rad;
        wheelchair.DriveRoute(speed, yaw, false);
    }

    private bool Locate(out PrototypeRouteNode a, out PrototypeRouteNode b, out Vector3 point)
    {
        a = b = null;
        point = body.position;
        float best = float.PositiveInfinity;
        foreach (var first in network.Nodes)
        {
            foreach (var second in network.GetNeighbors(first))
            {
                if (first.GetInstanceID() >= second.GetInstanceID()) continue;
                Vector3 projected = PrototypeRouteGeometry.Project(body.position, first.transform.position, second.transform.position);
                float distance = PrototypeRouteGeometry.Flat(body.position - projected).magnitude;
                if (distance >= best) continue;
                best = distance; a = first; b = second; point = projected;
            }
        }
        if (a != null && best <= maxAttachDistance) return true;
        StopNavigation("輪椅離路線太遠，請手推或用 WASD 回到藍線附近");
        return false;
    }

    private bool Acquire(Vector3 point)
    {
        if (ownsBody) return true;
        originalCollisionDetection = body.collisionDetectionMode;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        wheelchair.SetRouteControl(true);
        wheelchair.DriveRoute(0f, 0f, true);
        airborneTime = 0f;
        driveDirection = Vector3.zero;
        controlRevision = wheelchair.ControlRevision;
        ownsBody = true;
        StartPhysicsTrace();
        return true;
    }

    private bool BeginDirectional(bool reverse)
    {
        if (!Locate(out var a, out var b, out var point) || !Acquire(point)) return false;
        backwards = reverse;
        paused = false;
        uTurning = false;
        Vector3 heading = body.rotation * (reverse ? Vector3.back : Vector3.forward);
        PrototypeRouteNode atNode = NearNode(point, a) ? a : NearNode(point, b) ? b : null;
        if (atNode != null)
        {
            from = atNode;
            if (!ChooseNext(atNode, heading)) return false;
            SetAttachment(point);
            return true;
        }
        float towardA = Vector3.Dot(PrototypeRouteGeometry.Flat(a.transform.position - point).normalized, heading);
        float towardB = Vector3.Dot(PrototypeRouteGeometry.Flat(b.transform.position - point).normalized, heading);
        to = towardA > towardB ? a : b;
        from = to == a ? b : a;
        SetAttachment(point);
        return true;
    }

    private void SetAttachment(Vector3 projected)
    {
        settlingAtTarget = false;
        attaching = PrototypeRouteGeometry.Flat(body.position - projected).magnitude > arrivalTolerance;
        // Join the road ahead rather than trying to pivot 90 degrees toward a
        // projection immediately beside/behind the chair.
        attachment = to != null ? Vector3.MoveTowards(projected, to.transform.position, 2.5f) : projected;
        aligning = false;
    }

    private bool NearNode(Vector3 position, PrototypeRouteNode node)
    { return PrototypeRouteGeometry.Flat(position - node.transform.position).magnitude <= arrivalTolerance * 2f; }

    private PrototypeRouteNode PickBranch(PrototypeRouteNode node, Vector3 heading, PrototypeVoiceCommand turn)
    {
        PrototypeRouteNode result = null;
        float best = float.PositiveInfinity;
        foreach (var candidate in network.GetNeighbors(node))
        {
            Vector3 direction = PrototypeRouteGeometry.Flat(candidate.transform.position - node.transform.position);
            if (direction.sqrMagnitude < 0.0001f) continue;
            float angle = Vector3.SignedAngle(PrototypeRouteGeometry.Flat(heading), direction, Vector3.up);
            if (!PrototypeRouteGeometry.MatchesTurn(angle, turn)) continue;
            float difference = Mathf.Abs(angle - (turn == PrototypeVoiceCommand.Left ? -90f :
                turn == PrototypeVoiceCommand.Right ? 90f : 0f));
            if (difference < best) { best = difference; result = candidate; }
        }
        return result;
    }

    private bool ChooseNext(PrototypeRouteNode node, Vector3 heading)
    {
        settlingAtTarget = false;
        aligning = false;
        if (destination != null)
        {
            if (node == destination)
            { StopNavigation("已抵達「" + destination.destinationName + "」"); return false; }
            if (destinationSteps.Count == 0)
            { StopNavigation("目的地路線已中斷，請重新指定目的地"); return false; }
            to = destinationSteps.Dequeue();
            if (!network.GetNeighbors(node).Contains(to))
            { StopNavigation("路段已變更，請重新指定目的地"); return false; }
        }
        else
        {
            to = pendingTurn == PrototypeVoiceCommand.None ? null : PickBranch(node, heading, pendingTurn);
            if (to != null)
            {
                pendingTurn = PrototypeVoiceCommand.None;
                status = "已選擇路口分支，沿線繼續行駛";
            }
            else to = PickBranch(node, heading, PrototypeVoiceCommand.Forward);
            if (to == null)
            {
                bool hasOnwardRoad = false;
                foreach (var neighbor in network.GetNeighbors(node))
                {
                    if (neighbor == from) continue;
                    Vector3 branch = PrototypeRouteGeometry.Flat(neighbor.transform.position - node.transform.position);
                    if (Mathf.Abs(Vector3.SignedAngle(PrototypeRouteGeometry.Flat(heading), branch, Vector3.up)) < 145f)
                    { hasOnwardRoad = true; break; }
                }
                StopNavigation(hasOnwardRoad ?
                    "此路口沒有可直行的路段，請說左轉、右轉或目的地" :
                    "已到路線盡頭，請說掉頭、後退或目的地");
                return false;
            }
        }
        from = node;
        return true;
    }

    private void GoTo(PrototypeRouteNode goal)
    {
        if (nudging) StopNavigation();
        if (!Ready() || !Locate(out var a, out var b, out var point)) return;
        List<PrototypeRouteNode> bestPath = null;
        float bestCost = float.PositiveInfinity;
        foreach (var entry in new[] { a, b })
        {
            if (!network.TryFindPath(entry, goal, out var candidate)) continue;
            float cost = Vector3.Distance(point, entry.transform.position);
            for (int i = 1; i < candidate.Count; i++)
                cost += Vector3.Distance(candidate[i - 1].transform.position, candidate[i].transform.position);
            if (cost < bestCost) { bestCost = cost; bestPath = candidate; }
        }
        if (bestPath == null) { StopNavigation("找不到通往目的地的連通路線"); return; }
        if (!Acquire(point)) return;
        destination = goal;
        destinationSteps.Clear();
        foreach (var step in bestPath) destinationSteps.Enqueue(step);
        pendingTurn = PrototypeVoiceCommand.None;
        backwards = paused = uTurning = false;
        to = destinationSteps.Dequeue();
        from = to == a ? b : a;
        if (NearNode(point, to) && destinationSteps.Count > 0)
        { from = to; to = destinationSteps.Dequeue(); }
        SetAttachment(point);
        status = "沿路線前往「" + goal.destinationName + "」";
    }

    private void Update()
    {
        if (ownsBody && wheelchair != null && Input.GetKey(wheelchair.brakeKey))
        { StopNavigation("已停止自動移動，可使用手推或 WASD 手動操作"); return; }
        if (ownsBody && (wheelchair == null || !wheelchair.isActiveAndEnabled ||
            wheelchair.ControlRevision != controlRevision || wheelchair.HasManualInput))
            StopNavigation("已取消導航，由手推或鍵盤接手");
    }

    private void FixedUpdate()
    {
        if (!ownsBody) return;
        WritePhysicsTrace();
        if (Input.GetKey(wheelchair.brakeKey))
        { StopNavigation("已停止自動移動，可使用手推或 WASD 手動操作"); return; }
        if ((!nudging && (network == null || !network.isActiveAndEnabled)) || wheelchair.ControlRevision != controlRevision ||
            wheelchair.HasManualInput)
        { StopNavigation("已中止路線移動"); return; }
        float dt = Time.fixedDeltaTime;
        if (!wheelchair.RouteIsGrounded)
        {
            airborneTime += dt;
            if (airborneTime > 1f)
            { StopNavigation("輪椅離地，已停止導航"); return; }
            if (airborneTime > groundContactGraceTime)
            {
                // This is a sustained loss of support, rather than contact noise.
                // Do not apply brakes while freely falling.
                wheelchair.DriveRoute(0f, 0f, false);
                return;
            }
        }
        else airborneTime = 0f;
        if (paused) { wheelchair.DriveRoute(0f, 0f, true); return; }
        if (nudging) { TickNudge(dt); return; }
        if (uTurning)
        {
            float angle = Vector3.SignedAngle(PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward),
                PrototypeRouteGeometry.Flat(uTurnTarget * Vector3.forward), Vector3.up);
            diagnosticHeadingError = angle;
            diagnosticTargetDistance = 0f;
            driveDirection = Vector3.zero;
            wheelchair.DriveRoute(0f, RequestedYawRate(angle), false);
            if (Mathf.Abs(angle) <= 1f && Mathf.Abs(body.angularVelocity.y) < 0.1f)
            {
                wheelchair.DriveRoute(0f, 0f, true);
                uTurning = false;
                if (BeginDirectional(false)) status = "掉頭完成，沿原路返回";
            }
            return;
        }
        if (to == null || !network.Contains(to) || !network.Contains(from))
        { StopNavigation("路徑節點失效，已停止"); return; }
        if (!network.GetNeighbors(from).Contains(to))
        { StopNavigation("目前路段已解除連線，已停止"); return; }

        Vector3 target = attaching ? attachment : to.transform.position;
        Vector3 direction = PrototypeRouteGeometry.Flat(target - body.position);
        diagnosticTargetDistance = direction.magnitude;
        // Latch arrival before checking speed. Otherwise inertia can carry the
        // chair past the tiny arrival radius and make it turn back to chase it.
        // Crossing the end plane also counts, within a narrow road corridor.
        Vector3 incoming = PrototypeRouteGeometry.Flat(target - from.transform.position);
        Vector3 pastTarget = PrototypeRouteGeometry.Flat(body.position - target);
        bool crossedTarget = incoming.sqrMagnitude > 0.0001f &&
            Vector3.Dot(pastTarget, incoming.normalized) >= 0f &&
            (pastTarget - incoming.normalized * Vector3.Dot(pastTarget, incoming.normalized)).magnitude <=
                Mathf.Max(0.5f, arrivalTolerance * 2f);
        if (direction.magnitude <= arrivalTolerance || crossedTarget) settlingAtTarget = true;
        if (settlingAtTarget)
        {
            wheelchair.DriveRoute(0f, 0f, true);
            driveDirection = Vector3.zero;
            if (PrototypeRouteGeometry.Flat(body.velocity).magnitude >= 0.35f) return;
            settlingAtTarget = false;
            aligning = false;
            if (attaching) { attaching = false; return; }
            Vector3 heading = PrototypeRouteGeometry.Flat(to.transform.position - from.transform.position);
            if (heading.sqrMagnitude < 0.0001f) heading = body.rotation * Vector3.forward;
            ChooseNext(to, heading);
            return;
        }
        Vector3 facing = PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward);
        float headingError = Vector3.SignedAngle(facing, backwards ? -direction : direction, Vector3.up);
        diagnosticHeadingError = headingError;
        // Slow down near each node; turn with differential wheel torque before proceeding.
        if (Mathf.Abs(headingError) > 45f) aligning = true;
        else if (Mathf.Abs(headingError) < 12f) aligning = false;
        float speed = aligning ? 0f : Mathf.Min(moveSpeed, direction.magnitude * 1.5f) *
            Mathf.Clamp01(1f - Mathf.Abs(headingError) / 90f);
        driveDirection = speed > 0f ? direction.normalized : Vector3.zero;
        wheelchair.DriveRoute(backwards ? -speed : speed, RequestedYawRate(headingError), false);
    }

    private float RequestedYawRate(float angle)
    { return Mathf.Clamp(angle * 3f, -rotationSpeed, rotationSpeed) * Mathf.Deg2Rad; }

    private void OnCollisionStay(Collision collision)
    {
        if (!ownsBody) return;
        foreach (var contact in collision.contacts)
        {
            // Ground and ordinary slopes support the chair; a facing wall blocks it.
            if (Mathf.Abs(contact.normal.y) < 0.6f &&
                (uTurning || Vector3.Dot(contact.normal, driveDirection) < -0.3f))
            {
                StopNavigation("碰到前方障礙物，已停止導航");
                wheelchair.StopMovement();
                return;
            }
        }
    }

    public void StopNavigation(string message = "已停止路線移動")
    {
        status = message;
        paused = uTurning = attaching = settlingAtTarget = nudging = nudgeSettling = false;
        destination = null;
        destinationSteps.Clear();
        pendingTurn = PrototypeVoiceCommand.None;
        from = to = null;
        driveDirection = Vector3.zero;
        if (!ownsBody) return;
        ownsBody = false;
        ClosePhysicsTrace();
        if (body != null)
        {
            body.collisionDetectionMode = originalCollisionDetection;
        }
        if (wheelchair != null) wheelchair.SetRouteControl(false);
    }

    private void OnDisable() { StopNavigation("路線導航已停用"); }

    private void StartPhysicsTrace()
    {
#if UNITY_EDITOR
        ClosePhysicsTrace();
        if (!recordDiagnosticsInEditor) return;
        try
        {
            string directory = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs");
            System.IO.Directory.CreateDirectory(directory);
            physicsTrace = new System.IO.StreamWriter(System.IO.Path.Combine(directory, "prototype-route-physics.csv"), false,
                new System.Text.UTF8Encoding(false)) { AutoFlush = true };
            physicsTrace.WriteLine("time,phase,groundSource,x,y,z,speed,yawRate,grounded,leftGround,rightGround,leftRpm,rightRpm,leftTorque,rightTorque,leftBrake,rightBrake,targetSpeed,targetYaw,airborneTime,bodyYaw,headingError,targetDistance");
            diagnosticTicks = 0;
        }
        catch (System.Exception) { ClosePhysicsTrace(); }
#endif
    }

    private void WritePhysicsTrace()
    {
#if UNITY_EDITOR
        if (physicsTrace == null || diagnosticTicks++ % 5 != 0) return;
        if (diagnosticTicks > 4500) { ClosePhysicsTrace(); return; }
        try
        {
            var left = wheelchair.wheel_left_col;
            var right = wheelchair.wheel_right_col;
            string phase = paused ? "paused" : nudging ? "nudge" : uTurning ? "uturn" : settlingAtTarget ? "arrival" : attaching ? "attach" : aligning ? "align" : "drive";
            float[] values = { body.position.x, body.position.y, body.position.z,
                PrototypeRouteGeometry.Flat(body.velocity).magnitude, body.angularVelocity.y,
                wheelchair.RouteIsGrounded ? 1f : 0f, left.isGrounded ? 1f : 0f, right.isGrounded ? 1f : 0f,
                left.rpm, right.rpm, left.motorTorque, right.motorTorque, left.brakeTorque, right.brakeTorque,
                wheelchair.RouteRequestedSpeed, wheelchair.RouteRequestedYawRate, airborneTime,
                body.rotation.eulerAngles.y, diagnosticHeadingError, diagnosticTargetDistance };
            var line = new System.Text.StringBuilder(Time.realtimeSinceStartup.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            line.Append(',').Append(phase).Append(',').Append(wheelchair.RouteGroundSupport);
            foreach (float value in values) line.Append(',').Append(value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
            physicsTrace.WriteLine(line.ToString());
        }
        catch (System.Exception) { ClosePhysicsTrace(); }
#endif
    }

    private void ClosePhysicsTrace()
    {
#if UNITY_EDITOR
        try { physicsTrace?.Dispose(); } catch (System.Exception) { }
        physicsTrace = null;
#endif
    }
}
