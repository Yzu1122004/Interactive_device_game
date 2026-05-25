using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class MapObjectSpawner : MonoBehaviour
{
    private UdpClient udpClient;
    private Thread receiveThread;
    private const int port = 5005;

    private Queue<string> dataQueue = new Queue<string>();
    private readonly object queueLock = new object();

    void Start()
    {
        // Start the background network thread to listen for data
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log("--- UNITY: Opening UDP port 5005, waiting for Python... ---");
    }

    void Update()
    {
        // Process all received network data packages on the main thread
        lock (queueLock)
        {
            while (dataQueue.Count > 0)
            {
                string data = dataQueue.Dequeue();
                ProcessData(data);
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
                
                // Connection confirmation: Prints out whenever any data is received
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

    private void ProcessData(string data)
    {
        try
        {
            string[] splitData = data.Split(',');
            if (splitData.Length != 3) return;

            string type = splitData[0].Trim(); // Remove any accidental leading/trailing whitespaces
            float x = float.Parse(splitData[1]);
            float z = float.Parse(splitData[2]);

            SpawnObject(type, x, z);
        }
        catch (Exception e)
        {
            Debug.LogError("String parsing error: " + e.Message);
        }
    }

    private void SpawnObject(string type, float x, float z)
    {
        Vector3 targetPosition = new Vector3(x, 0f, z);
        
        // Try to load the Prefab from the Resources folder
        GameObject prefab = Resources.Load<GameObject>(type);

        if (prefab != null)
        {
            GameObject newObj = Instantiate(prefab, targetPosition, Quaternion.identity);
            newObj.name = "QR_Object_" + type;
            Debug.Log($"SUCCESS: Spawned object: {type} at position ({x}, {z})");
        }
        else
        {
            // Error log triggered if data is received but the file name does not match
            Debug.LogError($"❌ FILE ERROR: Unity received command to spawn '{type}', but no Prefab named '{type}' was found in Assets/Resources folder!");
        }
    }

    private void OnApplicationQuit()
    {
        // Clean up the thread and socket connections when closing the application
        if (receiveThread != null) receiveThread.Abort();
        if (udpClient != null) udpClient.Close();
    }
}