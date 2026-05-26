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

    // Khai báo biến đếm số thứ tự quét
    private int scanCounter = 0;

    void Start()
    {
        // Khởi động luồng mạng nền để lắng nghe dữ liệu
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log("--- UNITY: Opening UDP port 5005, waiting for Python... ---");
    }

    void Update()
    {
        // Xử lý tất cả các gói dữ liệu nhận được trên Main Thread của Unity
        lock (queueLock)
        {
            if (dataQueue.Count > 0)
            {
                // Reset bộ đếm về 0 trước khi xử lý loạt gói tin của đợt quét này
                scanCounter = 0;

                while (dataQueue.Count > 0)
                {
                    scanCounter++; // Tự tăng ID lên thành 1, 2, 3, 4, 5, 6...
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

                // Nhật ký xác nhận kết nối mạng
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

    // Đã thêm tham số 'autoId' vào hàm xử lý dữ liệu
    private void ProcessData(string data, int autoId)
    {
        try
        {
            string[] splitData = data.Split(',');
            if (splitData.Length != 3) return;

            string type = splitData[0].Trim(); // Loại bỏ khoảng trắng thừa nếu có
            float x = float.Parse(splitData[1]);
            float z = float.Parse(splitData[2]);

            // Chuyển tiếp ID tự động xuống hàm sinh vật thể
            SpawnObjectWithAutoID(type, x, z, autoId);
        }
        catch (Exception e)
        {
            Debug.LogError("String parsing error: " + e.Message);
        }
    }

    private void SpawnObjectWithAutoID(string type, float x, float z, int autoId)
    {
        Vector3 targetPosition = new Vector3(x, 0f, z);

        // Tạo tên duy nhất kết hợp Loại và ID tự sinh để tránh trùng lặp
        // Ví dụ: QR_Object_1_ID_1, QR_Object_1_ID_2
        string uniqueName = $"QR_Object_{type}_ID_{autoId}";

        // TÌM KIẾM xem trên Scene đã từng tạo vật thể có tên định danh này chưa
        GameObject existingObj = GameObject.Find(uniqueName);

        if (existingObj != null)
        {
            // Nếu ĐÃ TỒN TẠI, chỉ cần cập nhật tọa độ mới cho nó
            existingObj.transform.position = targetPosition;
            Debug.Log($"[UPDATE] Moved existing object: {uniqueName} to position ({x}, {z})");
        }
        else
        {
            // Nếu CHƯA TỒN TẠI, tiến hành tải Prefab từ Resources lên và sinh mới
            GameObject prefab = Resources.Load<GameObject>(type);

            if (prefab != null)
            {
                GameObject newObj = Instantiate(prefab, targetPosition, Quaternion.identity);
                newObj.name = uniqueName; // Gán tên duy nhất cho bản sao vừa tạo
                Debug.Log($"[SUCCESS] Spawned NEW object: {uniqueName} at position ({x}, {z})");
            }
            else
            {
                // Ghi lỗi nếu nhận được loại mà trong folder Assets/Resources không có file trùng tên
                Debug.LogError($"❌ FILE ERROR: Unity received command to spawn '{type}', but no Prefab named '{type}' was found in Assets/Resources folder!");
            }
        }
    }

    private void OnApplicationQuit()
    {
        // Giải phóng tài nguyên và đóng luồng khi tắt ứng dụng
        if (receiveThread != null) receiveThread.Abort();
        if (udpClient != null) udpClient.Close();
    }
}