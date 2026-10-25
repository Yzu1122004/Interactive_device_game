using System.Collections.Generic;
using UnityEngine;

// Free voice movement and destination routes share the existing wheel motors. The Rigidbody stays
// dynamic and gravity/collision response remain owned by Unity's physics engine.
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(Rigidbody), typeof(PrototypeWheelchairController))]
public class PrototypeRouteNavigator : MonoBehaviour
{
    public PrototypeRouteNetwork network;
    public PrototypeWheelchairController wheelchair;
    public PrototypeThresholdCrossing thresholdAssist;
    [Min(0.1f)] public float moveSpeed = 2.5f;
    [Min(1f)] public float rotationSpeed = 100f;
    [Min(0.01f)] public float arrivalTolerance = 0.5f;
    [Min(0.1f)] public float maxAttachDistance = 2f;
    [Min(0.05f)] public float groundContactGraceTime = 0.25f;
    [Range(1f, 89f)] public float frontJunctionHalfAngle = 60f;
    public bool recordDiagnosticsInEditor = true;
    [Header("Obstacle Recovery Nudges")]
    [Min(0.05f)] public float nudgeDistance = 0.3f;
    [Min(1f)] public float nudgeAngle = 15f;
    [Min(0.05f)] public float nudgeMoveSpeed = 0.35f;
    [Min(1f)] public float nudgeRotationSpeed = 35f;
    [Min(1f)] public float nudgeTimeout = 8f;
    [SerializeField] private string status = "等待自由移動或目的地指令";
    public string Status { get { return status; } }
    public bool IsActive { get { return ownsBody; } }
    public bool IsPaused { get { return paused; } }

    private Rigidbody body;
    [Header("Free Voice Movement")]
    [Min(0.1f)] public float freeMoveSpeed = 1f;
    [Min(1f)] public float freeTurnAngle = 45f;
    [Min(1f)] public float freeTurnSpeed = 45f;
    [Min(0.05f)] public float freeTurnMoveSpeed = 0.35f;
    [Min(1f)] public float freeTurnTimeout = 12f;
    [Header("Side Bypass")]
    [Min(0.2f)] public float sideOffset = 0.6f;
    [Min(0.8f)] public float sideForwardLength = 1.6f;
    [Min(0.05f)] public float sideMoveSpeed = 0.3f;
    [Min(0.02f)] public float obstacleMargin = 0.06f;
    [Tooltip("靠邊移動途中向前偵測的基本距離，另加目前速度的 0.3 秒距離。")]
    [Min(0.02f)] public float sideObstacleLookAhead = 0.08f;
    private bool sidePassing, sideResumeRoute;
    private List<Vector3> sidePath;
    private int sideIndex;
    private float sideElapsed;
    private float sideTimeLimit;
    private Quaternion sideHeading;
    private Collider blockingObstacle;
    private bool ownsBody, paused;
    private bool crossingThreshold;
    private bool freeMotion, freeTurning, freePivot;
    private float freeSpeed, freeTurnElapsed;
    private Quaternion freeHeading;
    private CollisionDetectionMode originalCollisionDetection;
    private int controlRevision;
    private float airborneTime;
    private Vector3 driveDirection;
    private PrototypeRouteNode from, to;
    private PrototypeRouteNode destination;
    private PrototypeVoiceCommand turnAtArrival;
    private bool followTurnedRoad;
    private readonly Queue<PrototypeRouteNode> destinationSteps = new Queue<PrototypeRouteNode>();
    private Vector3 attachment;
    private bool attaching;
    private bool aligning;
    private bool settlingAtTarget;
    private bool nudging, nudgeTurning, nudgeSettling;
    private float nudgeSign, nudgeElapsed;
    private Vector3 nudgeStart, nudgeAxis;
    private Quaternion nudgeRotation;
    private float diagnosticHeadingError;
    private float diagnosticTargetDistance;
#if UNITY_EDITOR
    private System.IO.StreamWriter physicsTrace;
    private bool physicsTraceHadThreshold;
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
        string text = PrototypeVoiceCommandParser.NormalizeText(transcript, true);
        bool addressed = false;
        foreach (string prefix in new[] { "帶我走到", "带我走到", "帶我去", "带我去", "導航到", "导航到",
            "移動到", "移动到", "走到", "走去", "前往", "去", "到" })
            if (text.StartsWith(prefix, System.StringComparison.Ordinal))
            { text = text.Substring(prefix.Length); addressed = true; break; }
        if (addressed && (text == "路口" || text == "前面的路口" || text == "前方的路口" ||
            text == "前面路口" || text == "前方路口" || text == "前面那個路口" || text == "前面那个路口" ||
            text == "下一個路口" || text == "下個路口" || text == "下一个路口" || text == "下个路口"))
        { GoToFrontJunction(); message = status; return true; }
        var matches = new List<PrototypeRouteNode>();
        if (network != null && !string.IsNullOrEmpty(text))
            foreach (var node in network.Nodes)
            {
                bool named = NormalizePlace(NodeLabel(node)) == NormalizePlace(text) ||
                    NormalizePlace(node.name) == NormalizePlace(text);
                if ((addressed || node.IsDestination) && named) matches.Add(node);
            }
        if (matches.Count == 1)
        { GoTo(matches[0]); message = status; return true; }
        if (matches.Count > 1)
        {
            PauseForVoiceInput();
            var labels = new List<string>();
            foreach (var node in matches) labels.Add(NodeLabel(node));
            status = "有多個符合的地點，請指定：" + string.Join("、", labels);
            message = status;
            return true;
        }
        message = "請說前進、後退、左右轉、微調、回頭、停下來，或去指定地點";
        return false;
    }

    private static string NodeLabel(PrototypeRouteNode node)
    { return node.IsDestination ? node.destinationName.Trim() : node.name; }

    private static string NormalizePlace(string name)
    {
        string text = PrototypeVoiceCommandParser.NormalizeText(name, true);
        string[] digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
        for (int i = 0; i < digits.Length; i++) text = text.Replace(digits[i], i.ToString());
        return text;
    }

    public string DestinationHint()
    {
        var names = new List<string>();
        if (network != null)
            foreach (var node in network.Nodes) names.Add(NodeLabel(node));
        return string.Join("、", names);
    }

    public string IntentContext
    {
        get { return status + (freeMotion ? (freeSpeed > 0f ? "；先前自由前進" : freeSpeed < 0f ? "；先前自由後退" : "；先前停著") :
            destination != null ? "；導航目的地：" + NodeLabel(destination) : ""); }
    }

    public bool TryGoToNamedPlace(string name, PrototypeVoiceCommand turn, out string message)
    {
        if (turn != PrototypeVoiceCommand.None && turn != PrototypeVoiceCommand.Left && turn != PrototypeVoiceCommand.Right)
        { message = "路口轉向只能是左轉或右轉"; return false; }
        var matches = new List<PrototypeRouteNode>();
        if (network != null && !string.IsNullOrWhiteSpace(name))
            foreach (var node in network.Nodes)
                if (NormalizePlace(name) == NormalizePlace(NodeLabel(node)) || NormalizePlace(name) == NormalizePlace(node.name)) matches.Add(node);
        if (matches.Count != 1 || (turn != PrototypeVoiceCommand.None && !IsJunction(matches[0])))
        { message = "地點不存在、名稱重複或不是路口，請重新指定"; StopNavigation(message); return false; }
        if (!GoTo(matches[0])) { message = status; return false; }
        if (ownsBody && (turn == PrototypeVoiceCommand.Left || turn == PrototypeVoiceCommand.Right))
        {
            turnAtArrival = turn;
            status += "，到達路口後" + PrototypeVoiceCommandParser.GetLabel(turn) + "並沿線繼續";
        }
        message = status;
        return ownsBody;
    }

    private bool IsJunction(PrototypeRouteNode node)
    { return NodeLabel(node).StartsWith("路口") || node.name.StartsWith("路口") || network.GetNeighbors(node).Count >= 3; }

    public void GoToFrontJunctionTurn(PrototypeVoiceCommand turn)
    {
        if (turn != PrototypeVoiceCommand.Left && turn != PrototypeVoiceCommand.Right) return;
        GoToFrontJunction();
        if (!ownsBody || destination == null || freeMotion || nudging) return;
        turnAtArrival = turn;
        status += "，到達後" + PrototypeVoiceCommandParser.GetLabel(turn) + "並沿線繼續";
    }

    public void Execute(PrototypeVoiceCommand command)
    {
        if (command == PrototypeVoiceCommand.Stop)
        { StopNavigation("已停止語音移動，可手動操作"); if (wheelchair != null) wheelchair.StopMovement(); return; }
        if (!Ready(false)) return;
        if (command == PrototypeVoiceCommand.KeepLeft || command == PrototypeVoiceCommand.KeepRight)
        { BeginSidePass(command); return; }
        if (command == PrototypeVoiceCommand.CrossThreshold)
        {
            StopNavigation();
            if (thresholdAssist == null || !thresholdAssist.isActiveAndEnabled)
            { status = "尚未接好門檻協助元件"; return; }
            if (!thresholdAssist.TryBegin(out string message)) { status = message; return; }
            Acquire(body.position);
            crossingThreshold = true;
            status = message;
            return;
        }
        if (PrototypeVoiceCommandParser.IsNudge(command)) { BeginNudge(command); return; }
        if (command != PrototypeVoiceCommand.Forward && command != PrototypeVoiceCommand.Backward &&
            command != PrototypeVoiceCommand.Left && command != PrototypeVoiceCommand.Right &&
            command != PrototypeVoiceCommand.UTurn) return;
        // Recording pauses the body but retains the previous cruising intent.
        float resumeSpeed = freeMotion ? freeSpeed : ownsBody && !nudging ? freeMoveSpeed : 0f;
        StopNavigation();
        Acquire(body.position);
        freeMotion = true;
        freeHeading = body.rotation;
        if (command == PrototypeVoiceCommand.Forward || command == PrototypeVoiceCommand.Backward)
        {
            freeSpeed = command == PrototypeVoiceCommand.Backward ? -freeMoveSpeed : freeMoveSpeed;
            status = "自由移動：持續" + PrototypeVoiceCommandParser.GetLabel(command) + "，可說停下來或按空白鍵";
        }
        else
        {
            freeSpeed = resumeSpeed;
            freeTurning = true;
            freePivot = command == PrototypeVoiceCommand.UTurn;
            float angle = freePivot ? 180f : command == PrototypeVoiceCommand.Left ? -freeTurnAngle : freeTurnAngle;
            freeHeading = Quaternion.AngleAxis(angle, Vector3.up) * body.rotation;
            status = "自由轉向：「" + PrototypeVoiceCommandParser.GetLabel(command) + "」，完成後" +
                (Mathf.Abs(freeSpeed) > 0f ? "繼續原本的前後移動" : "停住");
        }
    }

    private void TickFreeMotion(float dt)
    {
        float angle = Vector3.SignedAngle(PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward),
            PrototypeRouteGeometry.Flat(freeHeading * Vector3.forward), Vector3.up);
        diagnosticHeadingError = angle;
        diagnosticTargetDistance = 0f;
        if (freeTurning)
        {
            freeTurnElapsed += dt;
            if (freeTurnElapsed > freeTurnTimeout)
            { StopNavigation("轉向逾時，已停止；可用微調脫困"); return; }
            if (Mathf.Abs(angle) <= 2f && Mathf.Abs(body.angularVelocity.y) < 0.15f)
            {
                freeTurning = freePivot = false;
                if (Mathf.Abs(freeSpeed) < 0.001f)
                { StopNavigation("轉向完成，已停住"); return; }
                status = "轉向完成，繼續自由" + (freeSpeed > 0f ? "前進" : "後退");
            }
        }
        float speed = freeTurning ? (freePivot ? 0f : Mathf.Sign(freeSpeed) * Mathf.Min(Mathf.Abs(freeSpeed), freeTurnMoveSpeed)) : freeSpeed;
        driveDirection = Mathf.Abs(speed) > 0.001f ? PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward) * Mathf.Sign(speed) : Vector3.zero;
        float yaw = Mathf.Clamp(angle * 3f, -freeTurnSpeed, freeTurnSpeed) * Mathf.Deg2Rad;
        DriveChecked(speed, yaw);
    }

    private void BeginSidePass(PrototypeVoiceCommand command)
    {
        if (crossingThreshold || !wheelchair.RouteIsGrounded)
        { status = "請先完成跨越並穩定接地，再靠邊移動"; return; }
        bool resumeRoute = ownsBody && !freeMotion && !nudging && to != null;
        int side = command == PrototypeVoiceCommand.KeepLeft ? -1 : 1;
        if (!PrototypeObstacleProbe.TryPlan(body, side, sideOffset, sideForwardLength, obstacleMargin,
            blockingObstacle, resumeRoute, out var path, out string dimensions))
        {
            PauseForVoiceInput();
            if (ownsBody) wheelchair.DriveRoute(0f, 0f, true);
            else wheelchair.StopMovement();
            status = dimensions + "；這一側的通道或轉彎空間不足，保持停止；可換另一側，或先後退一點";
            return;
        }
        Acquire(body.position);
        sideResumeRoute = resumeRoute;
        sidePassing = true;
        sidePath = path; sideIndex = 0; sideElapsed = 0f;
        float pathLength = 0f;
        for (int i = 1; i < path.Count; i++) pathLength += Vector3.Distance(path[i - 1], path[i]);
        sideTimeLimit = Mathf.Max(45f, pathLength / Mathf.Max(0.05f, sideMoveSpeed) * 2f + 10f);
        sideHeading = Quaternion.LookRotation(PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward), Vector3.up);
        paused = nudging = nudgeSettling = freeTurning = freePivot = false;
        status = "低速" + PrototypeVoiceCommandParser.GetLabel(command) + (resumeRoute ? "，通過障礙後回到路線；" : "，完成後繼續前進；") + dimensions;
    }

    private void TickSidePass(float dt)
    {
        sideElapsed += dt;
        if (sideElapsed > sideTimeLimit) { HoldForObstacle(null, "靠邊移動逾時，已停住；可先後退一點"); return; }
        Vector3 position = PrototypeRouteGeometry.Flat(body.position);
        while (sideIndex + 1 < sidePath.Count &&
            Vector3.Distance(position, PrototypeRouteGeometry.Flat(sidePath[sideIndex + 1])) <=
            Vector3.Distance(position, PrototypeRouteGeometry.Flat(sidePath[sideIndex]))) sideIndex++;
        Vector3 end = PrototypeRouteGeometry.Flat(sidePath[sidePath.Count - 1]);
        float remaining = Vector3.Distance(position, end);
        if (remaining < 0.12f && sideIndex >= sidePath.Count - 3)
        {
            float angle = Vector3.SignedAngle(PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward),
                sideHeading * Vector3.forward, Vector3.up);
            wheelchair.DriveRoute(0f, Mathf.Clamp(angle * 3f, -30f, 30f) * Mathf.Deg2Rad, false);
            driveDirection = Vector3.zero;
            if (Mathf.Abs(angle) > 2f || body.velocity.magnitude > 0.1f || Mathf.Abs(body.angularVelocity.y) > 0.1f) return;
            sidePassing = false; sidePath = null; blockingObstacle = null;
            if (sideResumeRoute)
            { attaching = settlingAtTarget = aligning = false; status = "已通過障礙，繼續原目的地導航"; }
            else
            { freeMotion = true; freeSpeed = freeMoveSpeed; freeHeading = sideHeading; status = "靠邊完成，沿原方向繼續前進"; }
            return;
        }
        int look = Mathf.Min(sideIndex + 3, sidePath.Count - 1);
        Vector3 direction = PrototypeRouteGeometry.Flat(sidePath[look] - body.position);
        float error = Vector3.SignedAngle(PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward), direction, Vector3.up);
        float speed = Mathf.Abs(error) > 40f ? 0f : Mathf.Min(sideMoveSpeed, Mathf.Max(0.1f, remaining));
        diagnosticHeadingError = error; diagnosticTargetDistance = remaining;
        DriveChecked(speed, Mathf.Clamp(error * 3f, -35f, 35f) * Mathf.Deg2Rad);
    }

    private void DriveChecked(float speed, float yaw)
    {
        driveDirection = Mathf.Abs(speed) > 0.001f ? PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward).normalized * Mathf.Sign(speed) : Vector3.zero;
        // Ordinary driving reaches physical contact; only a planned bypass
        // checks ahead for newly introduced obstacles along its approved path.
        Collider obstacle = sidePassing ? PrototypeObstacleProbe.Blocked(body, driveDirection,
            sideObstacleLookAhead + Mathf.Abs(speed) * 0.3f, obstacleMargin) : null;
        if (obstacle != null)
        { HoldForObstacle(obstacle, "前方有障礙物，已停住；可說靠左一點、靠右一點，或後退一點"); return; }
        wheelchair.DriveRoute(speed, yaw, false);
    }

    private void HoldForObstacle(Collider obstacle, string message)
    {
        blockingObstacle = obstacle;
        sidePassing = false; sidePath = null;
        paused = true;
        driveDirection = Vector3.zero;
        wheelchair.DriveRoute(0f, 0f, true);
        status = message;
    }

    private bool Ready(bool requireNetwork = true)
    {
        if (!isActiveAndEnabled || (requireNetwork && (network == null || !network.isActiveAndEnabled)) || body == null ||
            wheelchair == null || !wheelchair.isActiveAndEnabled)
        { status = "路網或輪椅控制器尚未接好"; return false; }
        if (wheelchair.HasManualInput || Input.GetKey(wheelchair.brakeKey))
        { status = "請先放開手推、WASD 與煞車，再發出語音指令"; return false; }
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
                StopNavigation("微調完成，已停住；可說前進自由移動，或去指定地點導航");
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

    private bool ChooseNext(PrototypeRouteNode node)
    {
        settlingAtTarget = aligning = false;
        if (node == destination)
        {
            if (turnAtArrival == PrototypeVoiceCommand.None)
            { StopNavigation("已抵達「" + NodeLabel(node) + "」"); return false; }
            var turn = turnAtArrival;
            var branch = SelectRoadAfter(node, turn);
            if (branch == null)
            { StopNavigation("已到路口，但沒有可" + PrototypeVoiceCommandParser.GetLabel(turn) + "的連線，已停住"); return false; }
            destination = null;
            destinationSteps.Clear();
            turnAtArrival = PrototypeVoiceCommand.None;
            followTurnedRoad = true;
            from = node; to = branch;
            status = "已選擇" + PrototypeVoiceCommandParser.GetLabel(turn) + "路段，沿線繼續前進";
            return true;
        }
        if (followTurnedRoad)
        {
            var branch = SelectRoadAfter(node, PrototypeVoiceCommand.Forward);
            if (branch == null)
            { StopNavigation("已到路線盡頭或需要選擇新方向，已停住"); return false; }
            from = node; to = branch;
            return true;
        }
        if (destination == null || destinationSteps.Count == 0)
        { StopNavigation("目的地路線已中斷，請重新指定目的地"); return false; }
        to = destinationSteps.Dequeue();
        if (!network.GetNeighbors(node).Contains(to))
        { StopNavigation("路段已變更，請重新指定目的地"); return false; }
        from = node;
        return true;
    }

    private PrototypeRouteNode SelectRoadAfter(PrototypeRouteNode node, PrototypeVoiceCommand turn)
    {
        Vector3 incoming = PrototypeRouteGeometry.Flat(node.transform.position - from.transform.position);
        PrototypeRouteNode best = null, onlyOnward = null;
        int onwardCount = 0;
        float bestDifference = float.PositiveInfinity;
        foreach (var neighbor in network.GetNeighbors(node))
        {
            if (neighbor == from) continue;
            Vector3 direction = PrototypeRouteGeometry.Flat(neighbor.transform.position - node.transform.position);
            if (direction.sqrMagnitude < 0.0001f) continue;
            float angle = Vector3.SignedAngle(incoming, direction, Vector3.up);
            if (Mathf.Abs(angle) < 145f) { onwardCount++; onlyOnward = neighbor; }
            if (!PrototypeRouteGeometry.MatchesTurn(angle, turn)) continue;
            float difference = Mathf.Abs(angle - (turn == PrototypeVoiceCommand.Left ? -90f : turn == PrototypeVoiceCommand.Right ? 90f : 0f));
            if (difference < bestDifference) { best = neighbor; bestDifference = difference; }
        }
        // A sole continuing road may bend; do not invent a choice at a fork.
        return best != null ? best : turn == PrototypeVoiceCommand.Forward && onwardCount == 1 ? onlyOnward : null;
    }

    private bool GoTo(PrototypeRouteNode goal)
    {
        if (!Ready()) return false;
        StopNavigation();
        if (!Locate(out var a, out var b, out var point)) return false;
        if (!TryPlanRoute(a, b, point, goal, out var bestPath, out var cost))
        { StopNavigation("找不到通往目的地的連通路線"); return false; }
        StartPlannedRoute(goal, a, b, point, bestPath);
        return ownsBody;
    }

    private void GoToFrontJunction()
    {
        if (!Ready()) return;
        StopNavigation();
        if (!Locate(out var a, out var b, out var point)) return;
        Vector3 heading = PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward);
        PrototypeRouteNode nearest = null;
        List<PrototypeRouteNode> nearestPath = null;
        float nearestCost = float.PositiveInfinity, nearestAngle = float.PositiveInfinity;
        foreach (var node in network.Nodes)
        {
            // Named route points remain usable in a simple two-road prototype;
            // three or more neighbors also identify an unnamed intersection.
            if (!IsJunction(node)) continue;
            Vector3 offset = PrototypeRouteGeometry.Flat(node.transform.position - body.position);
            if (offset.magnitude <= Mathf.Max(arrivalTolerance, 0.3f)) continue;
            float angle = Mathf.Abs(Vector3.SignedAngle(heading, offset, Vector3.up));
            if (angle > frontJunctionHalfAngle) continue;
            if (!TryPlanRoute(a, b, point, node, out var path, out var cost)) continue;
            if (cost > nearestCost || (cost == nearestCost && angle >= nearestAngle)) continue;
            nearest = node; nearestPath = path; nearestCost = cost; nearestAngle = angle;
        }
        if (nearest == null)
        { status = "前方沒有可到達的路口，請調整朝向或指定地點名稱"; return; }
        StartPlannedRoute(nearest, a, b, point, nearestPath);
        status = "前往前方最近的路口：「" + NodeLabel(nearest) + "」";
    }

    private bool TryPlanRoute(PrototypeRouteNode a, PrototypeRouteNode b, Vector3 point,
        PrototypeRouteNode goal, out List<PrototypeRouteNode> bestPath, out float bestCost)
    {
        bestPath = null;
        bestCost = float.PositiveInfinity;
        foreach (var entry in new[] { a, b })
        {
            if (!network.TryFindPath(entry, goal, out var candidate)) continue;
            float cost = Vector3.Distance(point, entry.transform.position);
            for (int i = 1; i < candidate.Count; i++)
                cost += Vector3.Distance(candidate[i - 1].transform.position, candidate[i].transform.position);
            if (cost < bestCost) { bestCost = cost; bestPath = candidate; }
        }
        return bestPath != null;
    }

    private void StartPlannedRoute(PrototypeRouteNode goal, PrototypeRouteNode a,
        PrototypeRouteNode b, Vector3 point, List<PrototypeRouteNode> bestPath)
    {
        if (!Acquire(point)) return;
        destination = goal;
        destinationSteps.Clear();
        foreach (var step in bestPath) destinationSteps.Enqueue(step);
        paused = false;
        to = destinationSteps.Dequeue();
        from = to == a ? b : a;
        if (NearNode(point, to) && destinationSteps.Count > 0)
        { from = to; to = destinationSteps.Dequeue(); }
        SetAttachment(point);
        status = "沿路線前往「" + NodeLabel(goal) + "」";
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
        if ((!nudging && !freeMotion && !crossingThreshold && !sidePassing && (network == null || !network.isActiveAndEnabled)) || wheelchair.ControlRevision != controlRevision ||
            wheelchair.HasManualInput)
        { StopNavigation("已中止路線移動"); return; }
        float dt = Time.fixedDeltaTime;
        if (crossingThreshold)
        {
            if (thresholdAssist == null) { StopNavigation("門檻協助元件失效，已停止"); return; }
            thresholdAssist.Tick(dt, paused);
            driveDirection = thresholdAssist.TravelDirection;
            status = thresholdAssist.Status;
            if (!thresholdAssist.IsActive) StopNavigation(status);
            return;
        }
        if (!wheelchair.RouteIsGrounded)
        {
            airborneTime += dt;
            if (airborneTime > 1f)
            { StopNavigation("輪椅離地，已停止自動移動"); return; }
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
        if (sidePassing) { TickSidePass(dt); return; }
        if (nudging) { TickNudge(dt); return; }
        if (freeMotion) { TickFreeMotion(dt); return; }
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
            ChooseNext(to);
            return;
        }
        Vector3 facing = PrototypeRouteGeometry.Flat(body.rotation * Vector3.forward);
        float headingError = Vector3.SignedAngle(facing, direction, Vector3.up);
        diagnosticHeadingError = headingError;
        // Slow down near each node; turn with differential wheel torque before proceeding.
        if (Mathf.Abs(headingError) > 45f) aligning = true;
        else if (Mathf.Abs(headingError) < 12f) aligning = false;
        float speed = aligning ? 0f : Mathf.Min(moveSpeed, direction.magnitude * 1.5f) *
            Mathf.Clamp01(1f - Mathf.Abs(headingError) / 90f);
        driveDirection = speed > 0f ? direction.normalized : Vector3.zero;
        DriveChecked(speed, RequestedYawRate(headingError));
    }

    private float RequestedYawRate(float angle)
    { return Mathf.Clamp(angle * 3f, -rotationSpeed, rotationSpeed) * Mathf.Deg2Rad; }

    private void OnCollisionStay(Collision collision)
    {
        if (!ownsBody) return;
        if (crossingThreshold && thresholdAssist != null && thresholdAssist.IsThresholdContact(collision.collider)) return;
        foreach (var contact in collision.contacts)
        {
            // Ground and ordinary slopes support the chair; a facing wall blocks it.
            if (Mathf.Abs(contact.normal.y) < 0.6f &&
                (freePivot || Vector3.Dot(contact.normal, driveDirection) < -0.3f))
            {
                if (!nudging && !crossingThreshold)
                    HoldForObstacle(collision.collider, "碰到障礙物，已停住；可說靠左一點、靠右一點，或後退一點");
                else { StopNavigation("碰到前方障礙物，已停止自動移動"); wheelchair.StopMovement(); }
                return;
            }
        }
    }

    public void StopNavigation(string message = "已停止路線移動")
    {
        status = message;
        if (crossingThreshold && thresholdAssist != null) thresholdAssist.Cancel(message);
        crossingThreshold = false;
        sidePassing = sideResumeRoute = false;
        sidePath = null; blockingObstacle = null;
        paused = attaching = settlingAtTarget = nudging = nudgeSettling = freeMotion = freeTurning = freePivot = false;
        freeSpeed = freeTurnElapsed = 0f;
        turnAtArrival = PrototypeVoiceCommand.None;
        followTurnedRoad = false;
        destination = null;
        destinationSteps.Clear();
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
            physicsTrace.WriteLine("time,phase,groundSource,x,y,z,speed,yawRate,grounded,leftGround,rightGround,leftRpm,rightRpm,leftTorque,rightTorque,leftBrake,rightBrake,targetSpeed,targetYaw,airborneTime,bodyYaw,headingError,targetDistance,bodyPitch,thresholdPositionError,thresholdRotationError,thresholdApproach,thresholdTilt");
            diagnosticTicks = 0;
            physicsTraceHadThreshold = false;
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
            bool assist = crossingThreshold && thresholdAssist != null;
            physicsTraceHadThreshold |= assist;
            string phase = assist ? "threshold-" + thresholdAssist.Stage : paused ? "paused" : sidePassing ? "side-pass" : nudging ? "nudge" : freeMotion ? (freeTurning ? "free-turn" : "free-drive") : settlingAtTarget ? "arrival" : attaching ? "attach" : aligning ? "align" : "drive";
            float[] values = { body.position.x, body.position.y, body.position.z,
                PrototypeRouteGeometry.Flat(body.velocity).magnitude, body.angularVelocity.y,
                wheelchair.RouteIsGrounded ? 1f : 0f, left.isGrounded ? 1f : 0f, right.isGrounded ? 1f : 0f,
                left.rpm, right.rpm, left.motorTorque, right.motorTorque, left.brakeTorque, right.brakeTorque,
                wheelchair.RouteRequestedSpeed, wheelchair.RouteRequestedYawRate, airborneTime,
                body.rotation.eulerAngles.y, diagnosticHeadingError, diagnosticTargetDistance,
                Mathf.DeltaAngle(0f, body.rotation.eulerAngles.x),
                assist ? thresholdAssist.PositionError : 0f, assist ? thresholdAssist.RotationError : 0f,
                assist ? thresholdAssist.PlannedApproach : 0f, assist ? thresholdAssist.PlannedTilt : 0f };
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
        if (physicsTrace != null && physicsTraceHadThreshold)
        {
            try
            {
                string directory = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs");
                System.IO.File.Copy(System.IO.Path.Combine(directory, "prototype-route-physics.csv"),
                    System.IO.Path.Combine(directory, "prototype-threshold-physics.csv"), true);
            }
            catch (System.Exception) { }
        }
        physicsTrace = null;
        physicsTraceHadThreshold = false;
#endif
    }
}
