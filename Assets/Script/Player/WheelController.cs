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
    private float currentV = 0f;
    private float currentH = 0f;
    private Rigidbody rb;

    void Start()
    {
        originalMaxSpeed = maxSpeed;
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.2f, 0);

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
        ApplyMovement();
    }

    void HandleInput()
    {
        currentV = 0f;
        if (Input.GetKey(forwardKey)) currentV = 1f;
        if (Input.GetKey(backwardKey)) currentV = -1f;

        currentH = 0f;
        if (Input.GetKey(leftKey)) currentH = -1f;
        if (Input.GetKey(rightKey)) currentH = 1f;

        if (arduino != null)
        {
            currentV += arduino.VerticalInput;
            currentH += arduino.HorizontalInput;

            currentV = Mathf.Clamp(currentV, -1f, 1f);
            currentH = Mathf.Clamp(currentH, -1f, 1f);
        }
    }

    void ApplyMovement()
    {
        float currentSpeed = rb.velocity.magnitude;

        if (Input.GetKey(brakeKey))
        {
            ApplyBrake(brakeForce);
            return;
        }

        ApplyBrake(0f);

        float move = currentV * maxTorque;
        float turn = currentH * turnTorque;

        if (currentSpeed > maxSpeed)
        {
            wheel_left_col.motorTorque = 0f;
            wheel_right_col.motorTorque = 0f;
        }
        else
        {
            wheel_left_col.motorTorque = move + turn;
            wheel_right_col.motorTorque = move - turn;
        }

        if (Mathf.Abs(wheel_left_col.rpm) > 800) wheel_left_col.motorTorque = 0f;
        if (Mathf.Abs(wheel_right_col.rpm) > 800) wheel_right_col.motorTorque = 0f;
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
