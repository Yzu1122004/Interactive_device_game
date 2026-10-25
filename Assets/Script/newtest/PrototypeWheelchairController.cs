using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PrototypeWheelchairController : MonoBehaviour
{
    [Header("Wheel Visuals")]
    public Transform wheel_left;
    public Transform wheel_right;

    [Header("Wheel Physics")]
    public WheelCollider wheel_left_col;
    public WheelCollider wheel_right_col;

    [Header("Motor Settings")]
    public float maxTorque = 40f;
    public float turnTorque = 30f;
    public float brakeForce = 50f;
    public float maxSpeed = 20f;
    [Tooltip("How quickly input reaches the requested value.")]
    public float accelerationRate = 4f;
    [Tooltip("How quickly input returns to zero after release.")]
    public float decelerationRate = 7f;
    [Tooltip("Small input values below this are ignored.")]
    [Range(0f, 0.5f)] public float inputDeadZone = 0.08f;
    [Tooltip("Light braking when no movement input is active.")]
    public float idleBrakeForce = 8f;
    [Tooltip("Extra braking when pushing opposite to current movement.")]
    public float reverseBrakeMultiplier = 1.5f;
    [Tooltip("Turn torque multiplier when moving close to max speed.")]
    [Range(0f, 1f)] public float highSpeedTurnFactor = 0.45f;
    [Tooltip("Safety limit for wheel RPM.")]
    public float maxWheelRpm = 800f;
    [Tooltip("Wheel torque per m/s of route speed error.")]
    [Min(1f)] public float routeSpeedGain = 60f;
    [Min(0.1f)] public float routeAcceleration = 1.5f;
    [Min(0.1f)] public float routeYawAcceleration = 2.5f;
    [Min(1f)] public float routeTorqueChangeRate = 180f;
    [Tooltip("Yaw torque per radian/second of body turn-speed error.")]
    [Min(0f)] public float routeYawSpeedGain = 40f;
    [Min(1f)] public float routeTurnTorqueLimit = 65f;
    [Min(0f)] public float routeGroundProbeTolerance = 0.15f;

    [Header("Keyboard Input")]
    public KeyCode forwardKey = KeyCode.W;
    public KeyCode backwardKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode brakeKey = KeyCode.Space;

    [Header("Prototype Input")]
    public PrototypeEncoderInput encoder;
    public bool enableKeyboardInput = true;

    [Header("Movement Audio")]
    [Tooltip("Audio source for wheelchair movement.")]
    public AudioSource movementAudioSource;
    [Range(0f, 1f)] public float movementVolume = 0.85f;
    [Tooltip("Minimum horizontal speed (m/s) that plays the rolling sound.")]
    [Min(0.001f)] public float minimumSoundSpeed = 0.05f;
    [Tooltip("Minimum yaw speed (radians/s) that plays the turning sound.")]
    [Min(0.001f)] public float minimumSoundTurnSpeed = 0.05f;
    [Tooltip("Audio fade speed.")]
    public float soundFadeSpeed = 5f;

    private float targetV = 0f;
    private float targetH = 0f;
    private float currentV = 0f;
    private float currentH = 0f;
    private Rigidbody rb;
    private float voiceV;
    private float voiceH;
    private float voiceUntil;
    private bool voiceActive;
    private float stopUntil;
    private bool wasManualInput;
    private bool wasBraking;

    public bool HasManualInput { get; private set; }
    public int ControlRevision { get; private set; }
    public bool RouteControlled { get; private set; }
    private float routeForwardTarget;
    private float routeYawTarget;
    private WheelCollider[] supportWheels;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private float groundCheckTime = float.NegativeInfinity;
    private bool groundSupported;
    public string RouteGroundSupport { get; private set; } = "none";
    public float RouteRequestedSpeed { get { return routeForwardTarget; } }
    public float RouteRequestedYawRate { get { return routeYawTarget; } }
    public bool CanDriveRoutePhysics
    {
        get { return wheel_left_col != null && wheel_right_col != null &&
            wheel_left_col.enabled && wheel_right_col.enabled; }
    }
    public bool RouteIsGrounded
    {
        get
        {
            if (!CanDriveRoutePhysics) return false;
            if (groundCheckTime == Time.fixedTime) return groundSupported;
            groundCheckTime = Time.fixedTime;
            groundSupported = false;
            RouteGroundSupport = "none";
            if (wheel_left_col.isGrounded || wheel_right_col.isGrounded)
            { RouteGroundSupport = "wheel"; return groundSupported = true; }
            if (supportWheels != null)
                foreach (var wheel in supportWheels)
                    if (wheel != null && wheel.enabled && wheel.isGrounded)
                    { RouteGroundSupport = "wheel"; return groundSupported = true; }
            // Some imported WheelCollider setups report false while the chair is
            // supported. Check the ground beneath the actual wheel geometry too.
            if (supportWheels != null && rb != null)
                foreach (var wheel in supportWheels)
                {
                    if (wheel == null || !wheel.enabled) continue;
                    Vector3 origin = wheel.transform.TransformPoint(wheel.center) + Vector3.up * 0.05f;
                    float distance = wheel.radius + wheel.suspensionDistance + routeGroundProbeTolerance + 0.05f;
                    int hits = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, distance,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < hits; i++)
                    {
                        var hit = groundHits[i];
                        if (hit.collider == null || hit.collider.attachedRigidbody == rb || hit.normal.y < 0.5f) continue;
                        RouteGroundSupport = "probe";
                        return groundSupported = true;
                    }
                }
            return false;
        }
    }

    public void SetRouteControl(bool controlled)
    {
        RouteControlled = controlled;
        CancelVoiceInput();
        targetV = targetH = currentV = currentH = 0f;
        routeForwardTarget = routeYawTarget = 0f;
        if (wheel_left_col != null && wheel_right_col != null) ApplyBrake(controlled ? 0f : brakeForce);
    }

    // Navigation sets target wheel speeds. Unity still resolves gravity, suspension,
    // traction and collisions; navigation never writes the Rigidbody pose/velocity.
    public void DriveRoute(float forwardSpeed, float yawRate, bool brake)
    {
        if (!RouteControlled || rb == null || !CanDriveRoutePhysics) return;
        ApplyBrake(brake ? brakeForce : 0f);
        if (brake) { routeForwardTarget = routeYawTarget = 0f; return; }
        routeForwardTarget = Mathf.MoveTowards(routeForwardTarget, forwardSpeed, routeAcceleration * Time.fixedDeltaTime);
        routeYawTarget = Mathf.MoveTowards(routeYawTarget, yawRate, routeYawAcceleration * Time.fixedDeltaTime);
        // Opposite wheel rotation cancels in the average. Control translation
        // from rolling speed, but yaw from the actual body: wheel RPM alone can
        // reach its turn target while the chair barely rotates against friction.
        float rollingSpeed = (WheelSpeed(wheel_left_col) + WheelSpeed(wheel_right_col)) * 0.5f;
        float forwardTorque = Mathf.Clamp((routeForwardTarget - rollingSpeed) * routeSpeedGain, -maxTorque, maxTorque);
        float yawTorque = Mathf.Clamp(routeYawTarget * turnTorque +
            (routeYawTarget - rb.angularVelocity.y) * routeYawSpeedGain,
            -routeTurnTorqueLimit, routeTurnTorqueLimit);
        DriveRouteWheel(wheel_left_col, forwardTorque, yawTorque);
        DriveRouteWheel(wheel_right_col, forwardTorque, yawTorque);
    }

    public void ReleaseRouteWheels()
    {
        if (!RouteControlled || !CanDriveRoutePhysics) return;
        routeForwardTarget = routeYawTarget = 0f;
        ApplyBrake(0f);
        wheel_left_col.motorTorque = wheel_right_col.motorTorque = 0f;
    }

    private static float WheelSpeed(WheelCollider wheel)
    { return wheel.rpm * (2f * Mathf.PI * wheel.radius / 60f); }

    private void DriveRouteWheel(WheelCollider wheel, float forwardTorque, float yawTorque)
    {
        // Use physical wheel position, not the model's left/right naming.
        float sideOffset = Vector3.Dot(wheel.transform.position - rb.worldCenterOfMass, transform.right);
        float torque = forwardTorque - Mathf.Sign(sideOffset) * yawTorque;
        // Leave the command continuous across brief contact gaps. WheelCollider
        // produces traction only while touching the ground; the navigator handles
        // sustained airborne states without pulsing the brake every other tick.
        if (Mathf.Abs(wheel.rpm) > maxWheelRpm && Mathf.Sign(torque) == Mathf.Sign(wheel.rpm))
        { wheel.motorTorque = 0f; return; }
        wheel.motorTorque = Mathf.MoveTowards(wheel.motorTorque, torque, routeTorqueChangeRate * Time.fixedDeltaTime);
    }

    public bool SetVoiceInput(float forward, float turn, float duration)
    {
        if (!isActiveAndEnabled || HasManualInput || Time.unscaledTime < stopUntil) return false;
        voiceV = Mathf.Clamp(forward, -1f, 1f);
        voiceH = Mathf.Clamp(turn, -1f, 1f);
        voiceUntil = Time.unscaledTime + Mathf.Clamp(duration, 0.1f, 3f);
        voiceActive = true;
        return true;
    }

    public void CancelVoiceInput()
    {
        voiceActive = false;
        voiceV = voiceH = 0f;
    }

    public void StopMovement()
    {
        CancelVoiceInput();
        targetV = targetH = currentV = currentH = 0f;
        stopUntil = Time.unscaledTime + 0.5f;
        ControlRevision++;
        if (wheel_left_col != null && wheel_right_col != null) ApplyBrake(brakeForce);
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        supportWheels = GetComponentsInChildren<WheelCollider>();
        if (rb != null)
        {
            rb.centerOfMass = new Vector3(0, -0.2f, 0);
        }

        if (encoder == null) encoder = FindObjectOfType<PrototypeEncoderInput>();
        if (wheel_left_col == null || wheel_right_col == null)
        {
            Debug.LogError("Assign both WheelColliders to PrototypeWheelchairController.", this);
            enabled = false;
            return;
        }

        if (movementAudioSource == null) movementAudioSource = GetComponent<AudioSource>();
        if (movementAudioSource != null)
        {
            // The player hears their own wheelchair equally in both ears.
            movementAudioSource.spatialBlend = 0f;
            movementAudioSource.panStereo = 0f;
            movementAudioSource.spatialize = false;
            movementAudioSource.dopplerLevel = 0f;
            movementAudioSource.loop = true;
            movementAudioSource.playOnAwake = false;
            movementAudioSource.volume = 0f;
            movementAudioSource.Stop();
        }
    }

    void Update()
    {
        HandleInput();

        UpdateWheelPosition(wheel_left_col, wheel_left);
        UpdateWheelPosition(wheel_right_col, wheel_right);

        HandleMovementSound();
    }

    private void HandleMovementSound()
    {
        if (movementAudioSource == null || rb == null) return;

        float planarSpeed = Vector3.ProjectOnPlane(rb.velocity, Vector3.up).magnitude;
        float turnSpeed = Mathf.Abs(Vector3.Dot(rb.angularVelocity, Vector3.up));
        bool moving = planarSpeed >= minimumSoundSpeed || turnSpeed >= minimumSoundTurnSpeed;
        float targetVolume = moving ? movementVolume : 0f;

        if (moving && !movementAudioSource.isPlaying)
        {
            movementAudioSource.UnPause();
            if (!movementAudioSource.isPlaying) movementAudioSource.Play();
        }
        movementAudioSource.volume = Mathf.MoveTowards(
            movementAudioSource.volume, targetVolume, soundFadeSpeed * Time.deltaTime);
        if (!moving && movementAudioSource.volume <= 0f && movementAudioSource.isPlaying)
            movementAudioSource.Pause();
    }

    void FixedUpdate()
    {
        if (RouteControlled) return;
        SmoothInput();
        ApplyMovement();
    }

    void HandleInput()
    {
        targetV = 0f;
        if (enableKeyboardInput && Input.GetKey(forwardKey)) targetV += 1f;
        if (enableKeyboardInput && Input.GetKey(backwardKey)) targetV -= 1f;

        targetH = 0f;
        if (enableKeyboardInput && Input.GetKey(leftKey)) targetH -= 1f;
        if (enableKeyboardInput && Input.GetKey(rightKey)) targetH += 1f;

        bool keyboardMovement = (enableKeyboardInput &&
            (Input.GetKey(forwardKey) || Input.GetKey(backwardKey) ||
             Input.GetKey(leftKey) || Input.GetKey(rightKey)));
        bool encoderMovement = false;

        if (encoder != null && encoder.isActiveAndEnabled)
        {
            encoderMovement = Mathf.Abs(encoder.VerticalInput) >= inputDeadZone ||
                Mathf.Abs(encoder.HorizontalInput) >= inputDeadZone;
            targetV += encoder.VerticalInput;
            targetH += encoder.HorizontalInput;
        }

        // Automatic movement owns the wheels until Space stops it. Encoder
        // noise or WASD must not silently hand control back to manual mode.
        HasManualInput = !RouteControlled && (keyboardMovement || encoderMovement);
        if (RouteControlled) targetV = targetH = 0f;
        bool braking = Input.GetKey(brakeKey);
        if ((HasManualInput && !wasManualInput) || (braking && !wasBraking)) ControlRevision++;
        wasManualInput = HasManualInput;
        wasBraking = braking;
        if (HasManualInput || braking) CancelVoiceInput();
        if (braking)
        {
            stopUntil = Time.unscaledTime + 0.1f;
            targetV = targetH = currentV = currentH = 0f;
        }
        if (voiceActive && Time.unscaledTime >= voiceUntil)
        {
            CancelVoiceInput();
            currentV = currentH = 0f;
            stopUntil = Time.unscaledTime + 0.3f;
        }
        if (!HasManualInput && voiceActive)
        {
            targetV = voiceV;
            targetH = voiceH;
        }
        if (Time.unscaledTime < stopUntil) targetV = targetH = 0f;

        targetV = ApplyDeadZone(Mathf.Clamp(targetV, -1f, 1f));
        targetH = ApplyDeadZone(Mathf.Clamp(targetH, -1f, 1f));
    }

    private void SmoothInput()
    {
        currentV = MoveInputTowards(currentV, targetV);
        currentH = MoveInputTowards(currentH, targetH);
    }

    private float MoveInputTowards(float current, float target)
    {
        float rate = Mathf.Abs(target) > Mathf.Abs(current) ? accelerationRate : decelerationRate;
        return Mathf.MoveTowards(current, target, rate * Time.fixedDeltaTime);
    }

    private float ApplyDeadZone(float value)
    {
        if (Mathf.Abs(value) < inputDeadZone) return 0f;
        return value;
    }

    void ApplyMovement()
    {
        if (rb == null || wheel_left_col == null || wheel_right_col == null) return;

        if (Input.GetKey(brakeKey) || Time.unscaledTime < stopUntil)
        {
            ApplyBrake(brakeForce);
            return;
        }

        float forwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        float speedRatio = maxSpeed > 0f ? Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxSpeed) : 0f;
        bool hasMoveInput = Mathf.Abs(currentV) > 0f || Mathf.Abs(currentH) > 0f;
        bool reversingDirection = Mathf.Abs(forwardSpeed) > 0.5f && Mathf.Sign(forwardSpeed) != Mathf.Sign(currentV) && Mathf.Abs(currentV) > 0f;

        if (!hasMoveInput)
        {
            ApplyBrake(idleBrakeForce);
        }
        else if (reversingDirection)
        {
            ApplyBrake(brakeForce * reverseBrakeMultiplier);
        }
        else
        {
            ApplyBrake(0f);
        }

        float move = reversingDirection ? 0f : CalculateMoveTorque(forwardSpeed);
        float turn = reversingDirection ? 0f : CalculateTurnTorque(speedRatio);

        if (Mathf.Abs(wheel_left_col.rpm) > maxWheelRpm)
        {
            wheel_left_col.motorTorque = 0f;
        }

        if (Mathf.Abs(wheel_right_col.rpm) > maxWheelRpm)
        {
            wheel_right_col.motorTorque = 0f;
        }

        if (Mathf.Abs(wheel_left_col.rpm) <= maxWheelRpm)
        {
            wheel_left_col.motorTorque = move + turn;
        }

        if (Mathf.Abs(wheel_right_col.rpm) <= maxWheelRpm)
        {
            wheel_right_col.motorTorque = move - turn;
        }
    }

    private float CalculateMoveTorque(float forwardSpeed)
    {
        if (maxSpeed <= 0f) return currentV * maxTorque;

        bool acceleratingForward = currentV > 0f && forwardSpeed >= maxSpeed;
        bool acceleratingBackward = currentV < 0f && forwardSpeed <= -maxSpeed;

        if (acceleratingForward || acceleratingBackward)
        {
            return 0f;
        }

        float speedRatio = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxSpeed);
        float torqueScale = 1f - (speedRatio * 0.35f);
        return currentV * maxTorque * torqueScale;
    }

    private float CalculateTurnTorque(float speedRatio)
    {
        float turnScale = Mathf.Lerp(1f, highSpeedTurnFactor, speedRatio);
        return currentH * turnTorque * turnScale;
    }

    void ApplyBrake(float force)
    {
        wheel_left_col.brakeTorque = force;
        wheel_right_col.brakeTorque = force;

        if (force > 0f)
        {
            wheel_left_col.motorTorque = 0f;
            wheel_right_col.motorTorque = 0f;
        }
    }

    void UpdateWheelPosition(WheelCollider col, Transform trans)
    {
        if (col == null || trans == null) return;

        Vector3 pos;
        Quaternion rot;
        col.GetWorldPose(out pos, out rot);
        trans.position = pos;
        trans.rotation = rot;
    }

    private void OnDisable()
    {
        CancelVoiceInput();
        ControlRevision++;
        HasManualInput = false;
        wasManualInput = wasBraking = false;
        targetV = targetH = currentV = currentH = 0f;
        if (wheel_left_col != null && wheel_right_col != null) ApplyBrake(brakeForce);
        if (movementAudioSource != null) movementAudioSource.Pause();
    }
}
