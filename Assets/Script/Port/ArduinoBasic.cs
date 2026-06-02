using System.Globalization;
using System.IO.Ports;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public class ArduinoBasic : MonoBehaviour
{
    private SerialPort arduinoStream;
    public string port = "COM5";
    private Thread readThread;

    private readonly object lockObj = new object();
    private string latestMessage = "";
    private bool isNewMessage = false;
    private float attackPressedUntil = 0f;
    private bool confirmPressed = false;
    private float confirmPressedUntil = 0f;
    private bool randomSpawnPressed = false;
    private float randomSpawnPressedUntil = 0f;
    private Process scanProcess;
    private float nextAllowedScanTime = 0f;

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

    [Header("QR Scan")]
    public bool launchScannerOnConfirm = false;
    public string pythonExecutable = "python";
    public string scanScriptRelativePath = "python/Scan.py";
    public float scanLaunchCooldown = 1f;
    public int scannerTriggerUdpPort = 5006;

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

    public bool IsConfirmPressed
    {
        get { return Time.time <= confirmPressedUntil; }
    }

    public bool IsRandomSpawnPressed
    {
        get { return Time.time <= randomSpawnPressedUntil; }
    }

    public bool ConsumeConfirmPressed()
    {
        if (!confirmPressed)
        {
            return false;
        }

        confirmPressed = false;
        return true;
    }

    public bool ConsumeRandomSpawnPressed()
    {
        if (!randomSpawnPressed)
        {
            return false;
        }

        randomSpawnPressed = false;
        return true;
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
                Invoke(nameof(ResetArduinoState), 1f);
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

        if (message == "SPAWN_RANDOM")
        {
            randomSpawnPressed = true;
            randomSpawnPressedUntil = Time.time + 0.15f;
            return;
        }

        if (message == "CONFIRM_PLACEMENT")
        {
            confirmPressed = true;
            confirmPressedUntil = Time.time + 0.15f;

            if (launchScannerOnConfirm)
            {
                LaunchQrScanner();
            }

            SendScannerConfirmTrigger();

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

    private void LaunchQrScanner()
    {
        if (Time.time < nextAllowedScanTime)
        {
            return;
        }

        if (scanProcess != null && !scanProcess.HasExited)
        {
            Debug.Log("QR scanner is already running.");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string scriptPath = Path.Combine(projectRoot, scanScriptRelativePath);

        if (!File.Exists(scriptPath))
        {
            Debug.LogError("QR scan script not found: " + scriptPath);
            return;
        }

        string executable = string.IsNullOrWhiteSpace(pythonExecutable) ? "python" : pythonExecutable;

        if (executable == "python")
        {
            string localPython = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python", "Python313", "python.exe");

            if (File.Exists(localPython))
            {
                executable = localPython;
            }
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = "\"" + scriptPath + "\"",
            WorkingDirectory = Path.GetDirectoryName(scriptPath),
            UseShellExecute = false,
            CreateNoWindow = false
        };

        try
        {
            scanProcess = Process.Start(startInfo);
            nextAllowedScanTime = Time.time + scanLaunchCooldown;
            Debug.Log("QR scanner started by confirm button: " + scriptPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to start QR scanner: " + e.Message);
        }
    }

    private void SendScannerConfirmTrigger()
    {
        try
        {
            using (UdpClient client = new UdpClient())
            {
                byte[] data = Encoding.UTF8.GetBytes("CONFIRM_PLACEMENT");
                client.Send(data, data.Length, "127.0.0.1", scannerTriggerUdpPort);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Failed to send scanner trigger: " + e.Message);
        }
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
                arduinoStream.WriteLine(message);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Arduino write failed: " + e.Message);
        }
    }

    public void ResetArduinoState()
    {
        attackPressedUntil = 0f;
        confirmPressed = false;
        confirmPressedUntil = 0f;
        randomSpawnPressed = false;
        randomSpawnPressedUntil = 0f;
        VerticalInput = 0f;
        HorizontalInput = 0f;
        inputActiveUntil = 0f;
        isInitialized = false;
        lastLeftCount = 0f;
        lastRightCount = 0f;

        ArduinoWrite("GAME_RESET");
    }
}
