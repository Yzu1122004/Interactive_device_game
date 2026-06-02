using UnityEngine;

public class WheelController : MonoBehaviour
{
    [Header("Wheel Visuals")]
    public Transform wheel_left;
    public Transform wheel_right;

    [Header("Wheel Physics")]
    public WheelCollider wheel_left_col;
    public WheelCollider wheel_right_col;

    [Header("Motor Settings")]
    public float maxTorque = 0f;
    public float turnTorque = 0f;
    public float brakeForce = 0f;
    public float maxSpeed = 0f;
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

    [Header("Keyboard Input")]
    public KeyCode forwardKey = KeyCode.W;
    public KeyCode backwardKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode brakeKey = KeyCode.Space;

    [Header("Special Area Settings")]
    public float slopMaxSpeed = 10f;
    private float originalMaxSpeed;
    public ArduinoBasic arduino;

    [Header("Movement Audio")]
    [Tooltip("Audio source for wheelchair movement.")]
    public AudioSource movementAudioSource;
    [Tooltip("Audio fade speed.")]
    public float soundFadeSpeed = 5f;

    private float maxMovementVolume = 1.0f;
    private float targetV = 0f;
    private float targetH = 0f;
    private float currentV = 0f;
    private float currentH = 0f;
    private Rigidbody rb;

    void Start()
    {
        originalMaxSpeed = maxSpeed;
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.centerOfMass = new Vector3(0, -0.2f, 0);
        }

        if (arduino == null) arduino = FindObjectOfType<ArduinoBasic>();

        if (movementAudioSource == null) movementAudioSource = GetComponent<AudioSource>();
        if (movementAudioSource != null)
        {
            maxMovementVolume = movementAudioSource.volume;
            if (!movementAudioSource.isPlaying) movementAudioSource.Play();
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

        float currentSpeed = rb.velocity.magnitude;

        if (currentSpeed < 1f)
        {
            movementAudioSource.volume = Mathf.MoveTowards(movementAudioSource.volume, 0f, soundFadeSpeed * Time.deltaTime);
            if (movementAudioSource.volume <= 0f && movementAudioSource.isPlaying)
            {
                movementAudioSource.Pause();
            }
        }
        else
        {
            if (!movementAudioSource.isPlaying) movementAudioSource.UnPause();
            movementAudioSource.volume = Mathf.MoveTowards(movementAudioSource.volume, maxMovementVolume, soundFadeSpeed * Time.deltaTime);
        }
    }

    void FixedUpdate()
    {
        SmoothInput();
        ApplyMovement();
    }

    void HandleInput()
    {
        targetV = 0f;
        if (Input.GetKey(forwardKey)) targetV += 1f;
        if (Input.GetKey(backwardKey)) targetV -= 1f;

        targetH = 0f;
        if (Input.GetKey(leftKey)) targetH -= 1f;
        if (Input.GetKey(rightKey)) targetH += 1f;

        if (arduino != null)
        {
            targetV += arduino.VerticalInput;
            targetH += arduino.HorizontalInput;
        }

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

        if (Input.GetKey(brakeKey))
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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("slope"))
        {
            maxSpeed = slopMaxSpeed;
            if (arduino != null)
            {
                arduino.ArduinoWrite("B_ON");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("slope"))
        {
            maxSpeed = originalMaxSpeed;
            if (arduino != null)
            {
                arduino.ArduinoWrite("B_OFF");
            }
        }
    }
}
