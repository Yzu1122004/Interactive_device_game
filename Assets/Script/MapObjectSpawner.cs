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
    // 1. ĐỊNH NGHĨA CẤU TRÚC ĐỂ HIỂN THỊ Ô KÉO THẢ TRÊN INSPECTOR
    // =========================================================================
    [Serializable]
    public struct QRMapping
    {
        [Tooltip("Mã số Type trong QR Code (Ví dụ: 1)")]
        public string qrType;

        [Tooltip("Kéo file Prefab 3D tương ứng vào đây")]
        public GameObject prefab;
    }

    [Header("--- THIẾT LẬP CÁC KHỐI QR VÀ PREFABS ---")]
    [SerializeField] private List<QRMapping> qrMappingList;

    [Header("--- ĐỒNG BỘ TỶ LỆ BẢN ĐỒ ---")]
    [Tooltip("Điều chỉnh số này (Thử từ 0.05 đến 0.2) để vùng quét camera khớp với diện tích map Unity")]
    [SerializeField] private float coordinateScale = 0.1f;

    // Các biến phục vụ mạng UDP
    private UdpClient udpClient;
    private Thread receiveThread;
    private const int port = 5005;

    private Queue<string> dataQueue = new Queue<string>();
    private readonly object queueLock = new object();
    private int scanCounter = 0;

    void Start()
    {
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log("--- UNITY: Opening UDP port 5005, waiting for Python... ---");
    }

    void Update()
    {
        lock (queueLock)
        {
            if (dataQueue.Count > 0)
            {
                scanCounter = 0;
                while (dataQueue.Count > 0)
                {
                    scanCounter++;
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

                Debug.LogWarning("🌐 NETWORK CLEAR! Unity received string: " + message);

                lock (queueLock)
                {
                    dataQueue.Enqueue(message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("UDP Port Error: " + e.Message);
        }
    }

    private void ProcessData(string data, int autoId)
    {
        try
        {
            string[] splitData = data.Split(',');
            if (splitData.Length != 3) return;

            string qrType = splitData[0].Trim();
            float pixelX = float.Parse(splitData[1].Trim());
            float pixelY = float.Parse(splitData[2].Trim());

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
                // Gọi hàm tính toán nâng cao theo điểm neo Zone_1
                FindZoneAndSpawnWithAnchor(selectedPrefab, pixelX, pixelY, autoId);
            }
            else
            {
                Debug.LogError($"❌ ERROR: QR Type '{qrType}' chưa được cấu hình prefab!");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("String parsing error: " + e.Message);
        }
    }

    private void FindZoneAndSpawnWithAnchor(GameObject prefab, float pixelX, float pixelY, int autoId)
    {
        // 1. LẤY TỌA ĐỘ CỦA ZONE_1 LÀM ĐIỂM GỐC CỦA BẢN ĐỒ
        GameObject zone1 = GameObject.Find("Zone_1");
        if (zone1 == null)
        {
            Debug.LogError("❌ KHÔNG TÌM THẤY ZONE_1 TRÊN HIERARCHY!");
            return;
        }
        Vector3 zone1Pos = zone1.transform.position; // Tọa độ mốc thực tế của bạn

        // 2. CÔNG THỨC ĐỘ LỆCH CHUẨN XÁC (Sửa lỗi luôn khóa vào Zone_1):
        // Thay vì cộng trực tiếp vào Zone_1, chúng ta dùng pixelX và pixelY để tính toán độ dịch chuyển.
        // Cần đảm bảo hướng di chuyển: Khi pixelX tăng (vật thể sang phải), trục X Unity tăng.
        // Khi pixelY tăng (vật thể đi xuống dưới trên camera), trục Z Unity phải GIẢM ĐI (hoặc TĂNG LÊN tùy theo hướng map).
        // Dưới đây là công thức chuẩn hóa ma trận khoảng cách:
        Vector3 estimated3DPos = new Vector3(
            zone1Pos.x + (pixelX * coordinateScale),
            zone1Pos.y,
            zone1Pos.z + (pixelY * coordinateScale) // Thử đổi dấu trừ (-) thành dấu cộng (+) ở đây để xem vật thể dịch chuyển đúng hướng không
        );

        // [DEBUG] Hãy nhìn vào ô Console để xem tọa độ ước tính có thay đổi khi bạn dịch chuyển vật thể ngoài đời không
        Debug.Log($"[TỌA ĐỘ CAMERA] Pixel: ({pixelX}, {pixelY}) -> Suy ra tọa độ 3D tạm thời: {estimated3DPos}");

        // 3. THUẬT TOÁN QUÉT TÌM ZONE GẦN NHẤT
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        GameObject closestZone = null;
        float minDistance = float.PositiveInfinity;
        int zoneCount = 0;

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.StartsWith("Zone_"))
            {
                zoneCount++;
                float dist = Vector3.Distance(estimated3DPos, obj.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestZone = obj;
                }
            }
        }

        // 4. HÚT VẬT THỂ VÀO TÂM BLOCK
        if (closestZone != null)
        {
            // Nếu khoảng cách quá xa (ví dụ lệch > 50 mét), có thể do Scale đang quá lớn/nhỏ
            if (minDistance > 30f)
            {
                Debug.LogWarning($"[CẢNH BÁO] Ô gần nhất tìm được là {closestZone.name} nhưng khoảng cách lệch tới {minDistance:F1} mét. Vui lòng tinh chỉnh lại ô Coordinate Scale!");
            }

            Vector3 finalTargetPosition = closestZone.transform.position;
            string uniqueName = $"{prefab.name}_{autoId}";
            GameObject existingObj = GameObject.Find(uniqueName);

            if (existingObj != null)
            {
                existingObj.transform.position = finalTargetPosition;
                existingObj.transform.SetParent(closestZone.transform);
                Debug.Log($"[HÚT TỰ ĐỘNG] Đã di chuyển {uniqueName} vào tâm của {closestZone.name} (Lệch toán học: {minDistance:F2}m)");
            }
            else
            {
                GameObject newObj = Instantiate(prefab, finalTargetPosition, Quaternion.identity);
                newObj.name = uniqueName;
                newObj.transform.SetParent(closestZone.transform);
                Debug.Log($"[HÚT TỰ ĐỘNG] Đã sinh mới {uniqueName} chính xác tại tâm của {closestZone.name}!");
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (receiveThread != null) receiveThread.Abort();
        if (udpClient != null) udpClient.Close();
    }
}