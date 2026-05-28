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
    // 1. DEFINE STRUCTURE TO DISPLAY DRAG-AND-DROP SLOTS IN THE INSPECTOR
    // =========================================================================
    [Serializable]
    public struct QRMapping
    {
        [Tooltip("The Type ID in the QR Code (e.g., 1)")]
        public string qrType; 
        
        [Tooltip("Drag and drop the corresponding 3D Prefab here")]
        public GameObject prefab; 
    }

    [Header("--- QR BLOCKS AND PREFABS CONFIGURATION ---")]
    [SerializeField] 
    private List<QRMapping> qrMappingList; // List of slots that appear in the Unity Inspector

    // Variables for UDP Networking
    private UdpClient udpClient;
    private Thread receiveThread;
    private const int port = 5005;

    private Queue<string> dataQueue = new Queue<string>();
    private readonly object queueLock = new object();
    private int scanCounter = 0;

    void Start()
    {
        // Start background network thread to listen for data
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log("--- UNITY: Opening UDP port 5005, waiting for Python... ---");
    }

    void Update()
    {
        // Process all received data packets on the Unity Main Thread
        lock (queueLock)
        {
            if (dataQueue.Count > 0)
            {
                // Reset scan counter to 0 before processing this batch of scan packets
                scanCounter = 0;
                while (dataQueue.Count > 0)
                {
                    scanCounter++; // Auto-increment ID: 1, 2, 3, 4...
                    string data = dataQueue.Dequeue();
                    ProcessData(data, scanCounter);
                }
            }
        }
    }

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

                // Network connection confirmation log
                Debug.LogWarning("🌐 NETWORK CLEAR! Unity received string: " + message);

                lock (queueLock)
                {
                    dataQueue.Enqueue(message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("UDP Port Error (Port 5005 might be occupied by another app): " + e.Message);
        }
    }

    private void ProcessData(string data, int autoId)
    {
        try
        {
            string[] splitData = data.Split(',');
            if (splitData.Length != 3) return;

            string qrType = splitData[0].Trim(); // E.g., "1"
            float x = float.Parse(splitData[1]);
            float z = float.Parse(splitData[2]);

            // =========================================================================
            // 2. FIND THE ASSIGNED PREFAB BASED ON THE QR TYPE ID
            // =========================================================================
            GameObject selectedPrefab = null;

            foreach (var mapping in qrMappingList)
            {
                if (mapping.qrType == qrType)
                {
                    selectedPrefab = mapping.prefab;
                    break;
                }
            }

            if (selectedPrefab != null)
            {
                // If the Prefab is found in the configuration list, proceed to spawn the object
                SpawnObjectWithAutoID(selectedPrefab, qrType, x, z, autoId);
            }
            else
            {
                Debug.LogError($"❌ ERROR: QR Type '{qrType}' received from Python is not configured or the Prefab has not been dragged into QR_Manager yet!");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("String parsing error: " + e.Message);
        }
    }

    private void SpawnObjectWithAutoID(GameObject prefab, string qrType, float x, float z, int autoId)
    {
        Vector3 targetPosition = new Vector3(x, 0f, z);

        // Name the object in the Hierarchy using the actual name of the dragged Prefab file + ID
        // For example: If you drag a file named "Plant", the displayed name will be Plant_1
        string uniqueName = $"{prefab.name}_{autoId}"; 

        // SEARCH the Scene to check if an object with this unique identifier has already been created
        GameObject existingObj = GameObject.Find(uniqueName);

        if (existingObj != null)
        {
            // If ALREADY EXISTS, simply update its coordinates (tracks the block moving)
            existingObj.transform.position = targetPosition;
            Debug.Log($"[UPDATE] Moved existing object: {uniqueName} to position ({x}, {z})");
        }
        else
        {
            // If DOES NOT EXIST, instantiate the object directly from the assigned Prefab variable
            GameObject newObj = Instantiate(prefab, targetPosition, Quaternion.identity);
            newObj.name = uniqueName; // Assign the unique name to the newly created instance
            
            Debug.Log($"[SUCCESS] Spawned NEW object from drag-and-drop: {uniqueName} at ({x}, {z})");
        }
    }

    private void OnApplicationQuit()
    {
        // Release network resources and close the thread when quitting the application
        if (receiveThread != null) receiveThread.Abort();
        if (udpClient != null) udpClient.Close();
    }
}