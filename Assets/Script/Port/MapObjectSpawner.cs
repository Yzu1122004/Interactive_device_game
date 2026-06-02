using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class MapObjectSpawner : MonoBehaviour
{
    // =========================================================================
    // 1. DATA STRUCTURE DEFINITIONS
    // =========================================================================
    [Serializable]
    public struct QRMapping
    {
        [Tooltip("QR Code string content (e.g., Tree, House, Car)")]
        public string qrText;

        [Tooltip("Drag and drop the corresponding 3D Prefab here")]
        public GameObject prefab;
    }

    // Class structure so Unity can decode JSON strings sent from Python
    // Expected format: {"zones": ["Tree", "None", "House", ...]}
    [Serializable]
    public class PythonScanData
    {
        public string[] zones;
    }

    [Header("--- QR BLOCKS AND PREFAB SETTINGS ---")]
    [SerializeField] private List<QRMapping> qrMappingList;

    [Header("--- POSITIONING 11 ZONES ON THE UNITY MAP ---")]
    [Tooltip("Drag and drop 11 GameObjects as anchor points (Zone_1 to Zone_11) in the correct order")]
    [SerializeField] private Transform[] zoneAnchors = new Transform[11];

    // Array storing currently displayed objects on the map
    // This allows removing them when the zone is empty ("None")
    private GameObject[] spawnedObjects = new GameObject[11];

    // UDP networking variables
    private UdpClient udpClient;
    private Thread receiveThread;
    private const int port = 5005; // Synchronized with Python port 5005

    private Queue<string> dataQueue = new Queue<string>();
    private readonly object queueLock = new object();
    private bool isQuitting = false;

    void Awake()
    {
        ResolveMissingZoneAnchors();
        spawnedObjects = new GameObject[zoneAnchors.Length];
    }

    void Start()
    {
        // Initialize background thread to continuously listen for UDP data from Python
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("--- UNITY: Opening UDP port 5005, waiting for scan signals from Python... ---");
    }

    private void ResolveMissingZoneAnchors()
    {
        if (zoneAnchors == null || zoneAnchors.Length != 11)
        {
            zoneAnchors = new Transform[11];
        }

        for (int i = 0; i < zoneAnchors.Length; i++)
        {
            if (zoneAnchors[i] != null) continue;

            string zoneName = $"Zone_{i + 1}";
            GameObject zoneObject = GameObject.Find(zoneName);

            if (zoneObject != null)
            {
                zoneAnchors[i] = zoneObject.transform;
                Debug.Log($"[MapObjectSpawner] Auto-assigned {zoneName} as zone anchor {i + 1}.");
            }
            else
            {
                Debug.LogWarning($"[MapObjectSpawner] Missing zone anchor {i + 1}. Create a GameObject named {zoneName} or assign it in the Inspector.");
            }
        }
    }

    void Update()
    {
        // Process received data in Unity's Main Thread
        lock (queueLock)
        {
            if (dataQueue.Count > 0)
            {
                // For single-trigger scan mode, only the latest packet is needed
                string latestJsonData = "";

                while (dataQueue.Count > 0)
                {
                    latestJsonData = dataQueue.Dequeue();
                }

                if (!string.IsNullOrEmpty(latestJsonData))
                {
                    Debug.LogWarning(latestJsonData);
                    ProcessJsonData(latestJsonData);
                }
            }
        }
    }

    // Background thread for receiving network packets
    private void ReceiveData()
    {
        try
        {
            udpClient = new UdpClient(port);

            while (true)
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] dataByte = udpClient.Receive(ref anyIP);
                string message = Encoding.UTF8.GetString(dataByte);

                Debug.LogWarning("???? NETWORK CLEAR! Unity received JSON string: " + message);

                lock (queueLock)
                {
                    dataQueue.Enqueue(message);
                }
            }
        }
        catch (ThreadAbortException)
        {
            if (!isQuitting)
            {
                Debug.LogError("UDP receive thread was aborted unexpectedly.");
            }
        }
        catch (Exception e)
        {
            if (!isQuitting)
            {
                Debug.LogError("UDP port error: " + e.Message);
            }
        }
    }

    // Process and decode JSON data to synchronize the map
    private void ProcessJsonData(string jsonString)
    {
        try
        {
            // Decode JSON string into C# data array
            PythonScanData scanData = JsonUtility.FromJson<PythonScanData>(jsonString);

            if (scanData == null || scanData.zones == null)
            {
                Debug.LogError("??? JSON decoding failed or data is empty!");
                return;
            }

            Debug.Log($"???? Starting map synchronization. Number of zones received from Python: {scanData.zones.Length}");

            // Loop through all 11 zones for updating
            for (int i = 0; i < scanData.zones.Length; i++)
            {
                // Safety check in case incoming array exceeds configured anchors
                if (i >= zoneAnchors.Length || zoneAnchors[i] == null)
                {
                    Debug.LogWarning($"[SYNC] Zone {i + 1}: skipped because no zone anchor is assigned.");
                    continue;
                }

                string qrContent = scanData.zones[i].Trim();
                Debug.Log($"[SYNC] Zone {i + 1}: received QR value '{qrContent}'.");

                // --- STEP 1: REMOVE EXISTING OBJECT IN THIS ZONE (IF ANY) ---
                if (spawnedObjects[i] != null)
                {
                    Destroy(spawnedObjects[i]);
                    spawnedObjects[i] = null;
                }

                // --- STEP 2: CHECK IF THIS ZONE IS EMPTY OR "None" ---
                if (qrContent == "None" || string.IsNullOrEmpty(qrContent))
                {
                    // Empty real-world zone -> Unity keeps it empty
                    continue;
                }

                // --- STEP 3: FIND MATCHING PREFAB FOR THE QR STRING ---
                GameObject selectedPrefab = FindPrefabForQrContent(qrContent, out string matchedQrText);

                // --- STEP 4: SPAWN NEW OBJECT AT THE CENTER OF THE ZONE ANCHOR ---
                if (selectedPrefab != null)
                {
                    Transform anchor = zoneAnchors[i];

                    // Instantiate Prefab at anchor position and rotation
                    GameObject newObj = Instantiate(selectedPrefab, anchor.position, anchor.rotation);

                    Debug.Log($"? SPAWNED: {selectedPrefab.name} at {anchor.position}");
                    newObj.transform.localScale = Vector3.one * 1f;
                    newObj.SetActive(true);

                    // Set as child of the zone for cleaner hierarchy management
                    newObj.transform.SetParent(anchor);

                    // Store reference for deletion during the next scan
                    spawnedObjects[i] = newObj;

                    Debug.Log($"[SYNC] Zone {i + 1}: Successfully spawned object '{qrContent}' using mapping '{matchedQrText}'.");
                }
                else
                {
                    Debug.LogError($"??? ERROR: QR string '{qrContent}' in Zone {i + 1} has no configured Prefab in the Inspector!");
                }
            }

            Debug.Log("???? FULL MAP UPDATE COMPLETED!");
        }
        catch (Exception e)
        {
            Debug.LogError("JSON parsing error: " + e.Message);
        }
    }

    private GameObject FindPrefabForQrContent(string qrContent, out string matchedQrText)
    {
        matchedQrText = "";

        if (qrMappingList == null)
        {
            return null;
        }

        GameObject exactMatch = FindPrefabByQrText(qrContent, out matchedQrText);
        if (exactMatch != null)
        {
            return exactMatch;
        }

        string leadingNumber = GetLeadingNumber(qrContent);
        if (!string.IsNullOrEmpty(leadingNumber))
        {
            GameObject tokenMatch = FindPrefabByQrText(leadingNumber, out matchedQrText);
            if (tokenMatch != null)
            {
                Debug.LogWarning($"[SYNC] QR string '{qrContent}' matched prefab by object ID '{leadingNumber}'.");
                return tokenMatch;
            }
        }

        return null;
    }

    private string GetLeadingNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        text = text.Trim();
        int length = 0;

        while (length < text.Length && char.IsDigit(text[length]))
        {
            length++;
        }

        if (length == 0)
        {
            return "";
        }

        return text.Substring(0, length);
    }

    private GameObject FindPrefabByQrText(string qrText, out string matchedQrText)
    {
        matchedQrText = "";

        foreach (var mapping in qrMappingList)
        {
            if (string.IsNullOrWhiteSpace(mapping.qrText))
            {
                continue;
            }

            if (mapping.qrText.Trim().Equals(qrText, StringComparison.OrdinalIgnoreCase))
            {
                matchedQrText = mapping.qrText.Trim();
                return mapping.prefab;
            }
        }

        return null;
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;

        if (receiveThread != null)
            receiveThread.Abort();

        if (udpClient != null)
            udpClient.Close();
    }
}
