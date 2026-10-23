using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

// Transport only: encoder interpretation belongs to PrototypeEncoderInput.
[DefaultExecutionOrder(-200)]
public class PrototypeArduinoConnection : MonoBehaviour
{
    public string port = "COM4";
    public int baudRate = 9600;
    public bool connectOnEnable = true;
    [Min(0f)] public float startupDelay = 2f;
    public bool logArduinoMessages;

    public bool IsConnected { get; private set; }
    public bool IsReady { get; private set; }
    [Header("Runtime Status")]
    [SerializeField] private string status = "Disconnected";
    [SerializeField] private string lastMessage = "";
    public string Status { get { return status; } }
    public string LastMessage { get { return lastMessage; } }
    public event Action<string> MessageReceived;
    public event Action StateReset;

    private readonly Queue<string> messages = new Queue<string>();
    private readonly object messageLock = new object();
    private SerialPort stream;
    private Thread reader;
    private CancellationTokenSource cancellation;
    private volatile string readError;
    private float readyAt;

    private void OnEnable()
    {
        if (connectOnEnable) Connect();
    }

    [ContextMenu("Connect Arduino")]
    public void Connect()
    {
        Disconnect();
        try
        {
            stream = new SerialPort(port, baudRate) { ReadTimeout = 100, WriteTimeout = 200 };
            stream.Open();
            cancellation = new CancellationTokenSource();
            SerialPort source = stream;
            CancellationToken token = cancellation.Token;
            reader = new Thread(() => ReadMessages(source, token)) { IsBackground = true };
            IsConnected = true;
            status = "Connected: " + port + " (starting Arduino)";
            readyAt = Time.unscaledTime + startupDelay;
            reader.Start();
            Debug.Log(Status, this);
        }
        catch (Exception exception)
        {
            Disconnect();
            status = "Connection failed: " + exception.Message;
            Debug.LogWarning(Status, this);
        }
    }

    private void ReadMessages(SerialPort source, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                string line = source.ReadLine().Trim();
                if (line.Length == 0) continue;
                lock (messageLock)
                {
                    // Bound memory if the editor pauses while Arduino keeps sending.
                    if (messages.Count >= 512) messages.Dequeue();
                    messages.Enqueue(line);
                }
            }
            catch (TimeoutException) { }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested) readError = exception.Message;
                break;
            }
        }
    }

    private void Update()
    {
        if (readError != null)
        {
            string error = readError;
            Disconnect();
            status = "Connection lost: " + error;
            Debug.LogWarning(Status, this);
            return;
        }
        if (!IsConnected) return;
        if (!IsReady)
        {
            lock (messageLock) messages.Clear();
            if (Time.unscaledTime < readyAt) return;
            ResetArduinoState();
            if (!IsConnected) return;
            IsReady = true;
            status = "Ready: " + port;
        }

        // Events run on Unity's main thread, never the serial reader thread.
        for (int i = 0; i < 512; i++)
        {
            string message;
            lock (messageLock)
            {
                if (messages.Count == 0) break;
                message = messages.Dequeue();
            }
            lastMessage = message;
            if (logArduinoMessages) Debug.Log("Arduino: " + message, this);
            MessageReceived?.Invoke(message);
        }
    }

    [ContextMenu("Reset Arduino Counters")]
    public void ResetArduinoState()
    {
        lock (messageLock) messages.Clear();
        StateReset?.Invoke();
        Send("GAME_RESET");
    }

    public void Send(string message)
    {
        if (!IsConnected) return;
        try { stream.WriteLine(message); }
        catch (Exception exception)
        {
            Disconnect();
            status = "Write failed: " + exception.Message;
            Debug.LogWarning(Status, this);
        }
    }

    [ContextMenu("Disconnect Arduino")]
    public void Disconnect()
    {
        IsConnected = false;
        IsReady = false;
        cancellation?.Cancel();
        // ReadTimeout lets the reader finish without Thread.Abort.
        if (reader != null && reader.IsAlive) reader.Join(500);
        try { stream?.Close(); }
        catch (Exception exception) { Debug.LogWarning(exception.Message, this); }
        stream?.Dispose();
        stream = null;
        reader = null;
        cancellation?.Dispose();
        cancellation = null;
        readError = null;
        lock (messageLock) messages.Clear();
        status = "Disconnected";
        StateReset?.Invoke();
    }

    private void OnDisable() { Disconnect(); }
    private void OnApplicationQuit() { Disconnect(); }
}
