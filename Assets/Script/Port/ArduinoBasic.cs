using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class ArduinoBasic : MonoBehaviour
{
    private SerialPort arduinoStream;
    public string port = "COM4";
    private Thread readThread;

    private readonly object lockObj = new object();
    private string latestMessage = "";
    private bool isNewMessage = false;

    private float lastCountL = 0f;
    private float lastCountR = 0f;
    private bool isInitialized = false;

    [Header("目標物件")]
    public GameObject wheelchairObject;

    [Header("移動設定")]
    public float moveSpeed = 0.01f;
    public float rotateSpeed = 0.5f;
    public float deadZone = 1.0f;

    [Header("平滑設定")]
    [Range(0f, 1f)]
    public float lerpFactor = 0.1f;

    private Vector3 targetPosition;
    private float targetAngleY;
    private Vector3 currentPosition;
    private float currentAngleY;

    void Start()
    {
        if (wheelchairObject == null) wheelchairObject = GameObject.Find("WhellChair");

        if (wheelchairObject != null)
        {
            currentPosition = wheelchairObject.transform.localPosition;
            currentAngleY = wheelchairObject.transform.localEulerAngles.y;
            targetPosition = currentPosition;
            targetAngleY = currentAngleY;
        }

        if (!string.IsNullOrEmpty(port))
        {
            try
            {
                arduinoStream = new SerialPort(port, 9600);
                arduinoStream.ReadTimeout = 10;
                arduinoStream.Open();
                readThread = new Thread(new ThreadStart(ArduinoRead));
                readThread.IsBackground = true;
                readThread.Start();
                Debug.Log("Arduino 連線成功！");
            }
            catch (System.Exception e) { Debug.LogError("連線失敗: " + e.Message); }
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
            string[] parts = messageToProcess.Split(',');
            if (parts.Length == 2 && float.TryParse(parts[0], out float countL) && float.TryParse(parts[1], out float countR))
            {
                if (!isInitialized)
                {
                    lastCountL = countL; lastCountR = countR;
                    isInitialized = true; return;
                }

                // 1. 計算這一次的變化量
                float deltaL = countL - lastCountL;
                float deltaR = countR - lastCountR;

                // 2. 【核心保護機制】如果跳動太誇張（超過 50），代表是數據錯誤，直接略過
                if (Mathf.Abs(deltaL) > 50 || Mathf.Abs(deltaR) > 50)
                {
                    lastCountL = countL;
                    lastCountR = countR;
                    return;
                }

                // 3. 只有超過死區才處理
                if (Mathf.Abs(deltaL) >= deadZone || Mathf.Abs(deltaR) >= deadZone)
                {
                    float forwardDelta = (deltaL + deltaR) * 0.5f;
                    float rotateDelta = (deltaR - deltaL);

                    // 更新目標角度與位置
                    targetAngleY -= rotateDelta * rotateSpeed;
                    float angleRad = targetAngleY * Mathf.Deg2Rad;
                    Vector3 forwardDirection = new Vector3(Mathf.Sin(angleRad), 0, Mathf.Cos(angleRad));

                    targetPosition += forwardDirection * forwardDelta * moveSpeed;

                    // 存下這一次的數值作為下次的參考點
                    lastCountL = countL;
                    lastCountR = countR;
                }
            }
        }

        // 4. 平滑更新渲染
        if (wheelchairObject != null)
        {
            currentAngleY = Mathf.LerpAngle(currentAngleY, targetAngleY, lerpFactor);
            currentPosition = Vector3.Lerp(currentPosition, targetPosition, lerpFactor);

            wheelchairObject.transform.localPosition = currentPosition;
            wheelchairObject.transform.localRotation = Quaternion.Euler(0, currentAngleY, 0);
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
                    lock (lockObj) { latestMessage = line.Trim(); isNewMessage = true; }
                }
            }
            catch { }
        }
    }

    void OnApplicationQuit()
    {
        if (readThread != null) readThread.Abort();
        if (arduinoStream != null && arduinoStream.IsOpen) arduinoStream.Close();
    }
    // 補回這個函式，讓 WheelController 不會報錯
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
            Debug.LogWarning("寫入錯誤：" + e.Message);
        }
    }
}