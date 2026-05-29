using System.Globalization;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class ArduinoBasic : MonoBehaviour
{
    private SerialPort arduinoStream;
    public string port = "COM5";
    private Thread readThread;

    private readonly object lockObj = new object();
    private string latestMessage = "";
    private bool isNewMessage = false;
    private float attackPressedUntil = 0f;

    private float lastLeftCount = 0f;
    private float lastRightCount = 0f;
    private bool isInitialized = false;

    [Header("Target")]
    public GameObject wheelchairObject;

    [Header("Debug")]
    public bool logArduinoMessages = true;

    [Header("Movement")]
    public float moveSpeed = 0.01f;
    public float rotateSpeed = 0.5f;
    public float deadZone = 1.0f;
    public float oneWheelMoveRatio = 0.35f;
    public float oneWheelTurnRatio = 0.65f;
    public bool invertLeftEncoder = false;
    public bool invertRightEncoder = false;
    public float inputHoldTime = 0.12f;

    [Header("Smoothing")]
    [Range(0f, 1f)]
    public float lerpFactor = 0.1f;

    private float inputActiveUntil = 0f;

    public float VerticalInput { get; private set; }
    public float HorizontalInput { get; private set; }

    public bool IsAttackPressed
    {
        get { return Time.time <= attackPressedUntil; }
    }

    void Start()
    {
        if (wheelchairObject == null)
        {
            wheelchairObject = GameObject.Find("WhellChair");
        }

        if (!string.IsNullOrEmpty(port))
        {
            try
            {
                arduinoStream = new SerialPort(port, 9600);
                arduinoStream.ReadTimeout = 10;
                arduinoStream.Open();

                readThread = new Thread(ArduinoRead);
                readThread.IsBackground = true;
                readThread.Start();

                Debug.Log("Arduino connected: " + port);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Arduino connection failed: " + e.Message);
            }
        }
    }

    void Update()
    {
        string messageToProcess = "";
        bool hasNewMsg = false;

        lock (lockObj)
        {
            if (isNewMessage)
            {
                messageToProcess = latestMessage;
                hasNewMsg = true;
                isNewMessage = false;
            }
        }

        if (hasNewMsg)
        {
            ProcessArduinoMessage(messageToProcess);
        }

        if (Time.time > inputActiveUntil)
        {
            VerticalInput = 0f;
            HorizontalInput = 0f;
        }
    }

    private void ProcessArduinoMessage(string message)
    {
        if (logArduinoMessages)
        {
            Debug.Log("Arduino received: " + message);
        }

        if (message == "ATTACK_ON")
        {
            attackPressedUntil = Time.time + 0.15f;
            return;
        }

        if (!message.StartsWith("MOVE:"))
        {
            return;
        }

        string moveData = message.Substring(5);
        string[] parts = moveData.Split(',');

        if (parts.Length != 2)
        {
            return;
        }

        bool parsedLeft = float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float leftCount);
        bool parsedRight = float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float rightCount);

        if (!parsedLeft || !parsedRight)
        {
            return;
        }

        if (!isInitialized)
        {
            lastLeftCount = leftCount;
            lastRightCount = rightCount;
            isInitialized = true;
            return;
        }

        float deltaLeft = leftCount - lastLeftCount;
        float deltaRight = rightCount - lastRightCount;

        lastLeftCount = leftCount;
        lastRightCount = rightCount;

        if (invertLeftEncoder) deltaLeft *= -1f;
        if (invertRightEncoder) deltaRight *= -1f;

        if (Mathf.Abs(deltaLeft) > 50 || Mathf.Abs(deltaRight) > 50)
        {
            return;
        }

        ApplyEncoderMovement(deltaLeft, deltaRight);
    }

    private void ApplyEncoderMovement(float deltaLeft, float deltaRight)
    {
        bool leftMoved = Mathf.Abs(deltaLeft) >= deadZone;
        bool rightMoved = Mathf.Abs(deltaRight) >= deadZone;

        if (!leftMoved && !rightMoved)
        {
            return;
        }

        float forwardInput = 0f;
        float turnInput = 0f;

        if (leftMoved && rightMoved)
        {
            bool sameDirection = Mathf.Sign(deltaLeft) == Mathf.Sign(deltaRight);

            if (sameDirection)
            {
                forwardInput = (deltaLeft + deltaRight) * 0.5f;
                turnInput = (deltaRight - deltaLeft) * 0.35f;
            }
            else
            {
                turnInput = (deltaRight - deltaLeft) * 0.5f;
            }
        }
        else if (leftMoved)
        {
            forwardInput = deltaLeft * oneWheelMoveRatio;
            turnInput = -deltaLeft * oneWheelTurnRatio;
        }
        else if (rightMoved)
        {
            forwardInput = deltaRight * oneWheelMoveRatio;
            turnInput = deltaRight * oneWheelTurnRatio;
        }

        VerticalInput = Mathf.Clamp(forwardInput, -1f, 1f);
        HorizontalInput = Mathf.Clamp(turnInput, -1f, 1f);
        inputActiveUntil = Time.time + inputHoldTime;
    }

    private void ArduinoRead()
    {
        while (arduinoStream != null && arduinoStream.IsOpen)
        {
            try
            {
                string line = arduinoStream.ReadLine();

                if (!string.IsNullOrEmpty(line))
                {
                    lock (lockObj)
                    {
                        latestMessage = line.Trim();
                        isNewMessage = true;
                    }
                }
            }
            catch
            {
            }
        }
    }

    void OnApplicationQuit()
    {
        if (readThread != null)
        {
            readThread.Abort();
        }

        if (arduinoStream != null && arduinoStream.IsOpen)
        {
            arduinoStream.Close();
        }
    }

    public void ArduinoWrite(string message)
    {
        try
        {
            if (arduinoStream != null && arduinoStream.IsOpen)
            {
                arduinoStream.Write(message);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Arduino write failed: " + e.Message);
        }
    }
}
