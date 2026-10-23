using System.Globalization;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PrototypeArduinoConnection))]
public class PrototypeEncoderInput : MonoBehaviour
{
    public PrototypeArduinoConnection arduino;
    public bool invertLeftEncoder = true;
    public bool invertRightEncoder;
    [Min(0.01f)] public float deadZone = 1f;
    public float oneWheelMoveRatio = 1f;
    public float oneWheelTurnRatio = 1f;
    [Min(0f)] public float inputHoldTime = 0.12f;
    [Min(1)] public int maxCountDelta = 50;

    public float VerticalInput { get; private set; }
    public float HorizontalInput { get; private set; }
    public long LeftCount { get; private set; }
    public long RightCount { get; private set; }
    private bool initialized;
    private float activeUntil;

    private void OnEnable()
    {
        if (arduino == null) arduino = GetComponent<PrototypeArduinoConnection>();
        arduino.MessageReceived += ProcessMessage;
        arduino.StateReset += ResetInput;
        ResetInput();
    }

    private void OnDisable()
    {
        if (arduino != null)
        {
            arduino.MessageReceived -= ProcessMessage;
            arduino.StateReset -= ResetInput;
        }
        ResetInput();
    }

    private void Update()
    {
        if (Time.unscaledTime > activeUntil || arduino == null || !arduino.IsReady)
        {
            VerticalInput = 0f;
            HorizontalInput = 0f;
        }
    }

    public void ResetInput()
    {
        initialized = false;
        LeftCount = RightCount = 0;
        VerticalInput = HorizontalInput = 0f;
        activeUntil = 0f;
    }

    // Existing FINAL.ino sends cumulative counts, not per-frame movement.
    public void ProcessMessage(string message)
    {
        if (string.IsNullOrEmpty(message) || !message.StartsWith("MOVE:")) return;
        string[] parts = message.Substring(5).Split(',');
        if (parts.Length != 2 ||
            !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long left) ||
            !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long right)) return;

        double deltaLeft = (double)left - LeftCount;
        double deltaRight = (double)right - RightCount;
        LeftCount = left;
        RightCount = right;
        if (!initialized)
        {
            initialized = true;
            return;
        }
        // A board reset or discontinuity establishes a new baseline.
        if (System.Math.Abs(deltaLeft) > maxCountDelta || System.Math.Abs(deltaRight) > maxCountDelta)
        {
            VerticalInput = HorizontalInput = 0f;
            return;
        }
        ApplyMovement((float)deltaLeft * (invertLeftEncoder ? -1f : 1f),
            (float)deltaRight * (invertRightEncoder ? -1f : 1f));
    }

    private void ApplyMovement(float left, float right)
    {
        bool leftMoved = Mathf.Abs(left) >= deadZone;
        bool rightMoved = Mathf.Abs(right) >= deadZone;
        if (!leftMoved && !rightMoved) return;

        float forward = 0f;
        float turn = 0f;
        if (leftMoved && rightMoved)
        {
            if (Mathf.Sign(left) == Mathf.Sign(right))
            {
                forward = (left + right) * 0.5f;
                turn = (right - left) * 0.35f;
            }
            else turn = (right - left) * 0.5f;
        }
        else if (leftMoved)
        {
            forward = left * oneWheelMoveRatio;
            turn = -left * oneWheelTurnRatio;
        }
        else
        {
            forward = right * oneWheelMoveRatio;
            turn = right * oneWheelTurnRatio;
        }
        VerticalInput = Mathf.Clamp(forward, -1f, 1f);
        HorizontalInput = Mathf.Clamp(turn, -1f, 1f);
        activeUntil = Time.unscaledTime + inputHoldTime;
    }
}
