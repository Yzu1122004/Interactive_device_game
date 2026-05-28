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

    void Start()
    {
        // Initialize background thread to continuously listen for UDP data from Python
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("--- UNITY: Opening UDP port 5005, waiting for scan signals from Python... ---");
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

                Debug.LogWarning("🌐 NETWORK CLEAR! Unity received JSON string: " + message);

                lock (queueLock)
                {
                    dataQueue.Enqueue(message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("UDP port error: " + e.Message);
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
                Debug.LogError("❌ JSON decoding failed or data is empty!");
                return;
            }

            Debug.Log($"📊 Starting map synchronization. Number of zones received from Python: {scanData.zones.Length}");

            // Loop through all 11 zones for updating
            for (int i = 0; i < scanData.zones.Length; i++)
            {
                // Safety check in case incoming array exceeds configured anchors
                if (i >= zoneAnchors.Length || zoneAnchors[i] == null) continue;

                string qrContent = scanData.zones[i].Trim();

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
                GameObject selectedPrefab = null;

                foreach (var mapping in qrMappingList)
                {
                    if (mapping.qrText.Equals(qrContent, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedPrefab = mapping.prefab;
                        break;
                    }
                }

                // --- STEP 4: SPAWN NEW OBJECT AT THE CENTER OF THE ZONE ANCHOR ---
                if (selectedPrefab != null)
                {
                    Transform anchor = zoneAnchors[i];

                    // Instantiate Prefab at anchor position and rotation
                    GameObject newObj = Instantiate(selectedPrefab, anchor.position, anchor.rotation);

                    // Set as child of the zone for cleaner hierarchy management
                    newObj.transform.SetParent(anchor);

                    // Store reference for deletion during the next scan
                    spawnedObjects[i] = newObj;

                    Debug.Log($"[SYNC] Zone {i + 1}: Successfully spawned object '{qrContent}'.");
                }
                else
                {
                    Debug.LogError($"❌ ERROR: QR string '{qrContent}' in Zone {i + 1} has no configured Prefab in the Inspector!");
                }
            }

            Debug.Log("🎯 FULL MAP UPDATE COMPLETED!");
        }
        catch (Exception e)
        {
            Debug.LogError("JSON parsing error: " + e.Message);
        }
    }

    private void OnApplicationQuit()
    {
        if (receiveThread != null)
            receiveThread.Abort();

        if (udpClient != null)
            udpClient.Close();
    }
}