using UnityEngine;

public class IncidentZone : MonoBehaviour
{
    public enum IncidentType
    {
        PedestrianBlocker, // 1. 擋路的行人
        BrakingCarHorn,    // 2. 停滯車子按喇叭
        ExhaustCar,        // 3. 廢氣排放車
        FallingFlowerPot   // 4. 倒下的花盆
    }

    [Header("事件設定")]
    [Tooltip("這個區域固定執行的事件類型")]
    public IncidentType assignedIncident;

    [Header("1. 擋路行人設定")]
    public GameObject blockerPedestrianPrefab;
    public Transform pedestrianSpawnPoint;

    [Header("2. 停滯喇叭車設定")]
    public GameObject hornCarPrefab;
    public Transform carSpawnPoint;

    [Header("3. 廢氣車設定")]
    public GameObject exhaustCarPrefab;
    public Transform exhaustCarSpawnPoint;

    [Header("4. 倒下花盆設定")]
    [Tooltip("【模式 A】如果整組花盆是原本就擺在場景上的，請把它的最外層父物件拖到這裡")]
    public Rigidbody targetFlowerPotGroup;

    [Tooltip("【模式 B】如果你希望踩到格子才把『多花盆預製物』生成出來倒下，請把 Prefab 拖到這裡")]
    public GameObject flowerPotGroupPrefab;
    [Tooltip("配合模式 B 的生成位置（留空則預設在觸發區中心）")]
    public Transform flowerPotSpawnPoint;

    [Tooltip("讓花盆倒下的推力大小")]
    public float pushForce = 8f;

    private bool isActivatedByManager = false;
    private bool hasTriggered = false;

    public void SetActivate(bool state)
    {
        isActivatedByManager = state;
        hasTriggered = false;
        GetComponent<Collider>().enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isActivatedByManager && !hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true;
            ExecuteIncident();
        }
    }

    private void ExecuteIncident()
    {
        Debug.Log($"【干擾事件】玩家進入區域，觸發：{assignedIncident}");

        switch (assignedIncident)
        {
            case IncidentType.PedestrianBlocker:
                TriggerPedestrianBlocker();
                break;
            case IncidentType.BrakingCarHorn:
                TriggerBrakingCarHorn();
                break;
            case IncidentType.ExhaustCar:
                TriggerExhaustCar();
                break;
            case IncidentType.FallingFlowerPot:
                TriggerFallingFlowerPot();
                break;
        }
    }

    private void TriggerPedestrianBlocker()
    {
        if (blockerPedestrianPrefab != null && pedestrianSpawnPoint != null)
        {
            Instantiate(blockerPedestrianPrefab, pedestrianSpawnPoint.position, pedestrianSpawnPoint.rotation);
        }
    }

    private void TriggerBrakingCarHorn()
    {
        if (hornCarPrefab != null && carSpawnPoint != null)
        {
            Instantiate(hornCarPrefab, carSpawnPoint.position, carSpawnPoint.rotation);
        }
    }

    private void TriggerExhaustCar()
    {
        if (exhaustCarPrefab != null && exhaustCarSpawnPoint != null)
        {
            Instantiate(exhaustCarPrefab, exhaustCarSpawnPoint.position, exhaustCarSpawnPoint.rotation);
        }
    }

    // --- 4. 倒下花盆（多花盆一體化處理） ---
    private void TriggerFallingFlowerPot()
    {
        Rigidbody potRbToPush = null;

        // 優先檢查模式 B：動態生成
        if (flowerPotGroupPrefab != null)
        {
            Vector3 spawnPos = flowerPotSpawnPoint != null ? flowerPotSpawnPoint.position : transform.position;
            Quaternion spawnRot = flowerPotSpawnPoint != null ? flowerPotSpawnPoint.rotation : transform.rotation;

            GameObject spawnedPot = Instantiate(flowerPotGroupPrefab, spawnPos, spawnRot);
            potRbToPush = spawnedPot.GetComponent<Rigidbody>();

            if (potRbToPush == null)
            {
                Debug.LogError($"【錯誤】生成的花盆 Prefab 『最外層父物件』身上沒有掛 Rigidbody！");
                return;
            }
        }
        // 否則使用模式 A：場景上原本擺好的現成花盆
        else if (targetFlowerPotGroup != null)
        {
            potRbToPush = targetFlowerPotGroup;
        }

        // 開始執行推倒物理邏輯
        if (potRbToPush != null)
        {
            // 1. 喚醒物理：解除 Kinematic 讓重力恢復運作
            potRbToPush.isKinematic = false;

            // 2. 核心修正：計算斜下方的物理推力方向（使用大寫 Vector3.down 修正先前 transform.down 的報錯）
            Vector3 pushDirection = (transform.right + Vector3.down * 0.3f).normalized;

            // 3. 一巴掌推在最外層父物件的物理身體上，裡面的所有子花盆就會跟著完美的一起滾動倒下！
            potRbToPush.AddForce(pushDirection * pushForce, ForceMode.Impulse);

            // 額外加點微幅扭矩旋轉，讓花盆群組倒得更自然、有滾動感
            potRbToPush.AddTorque(transform.forward * pushForce * 0.5f, ForceMode.Impulse);
        }
        else
        {
            Debug.LogWarning("【警告】未綁定任何場景花盆物件 (Target Flower Pot Group) 或花盆 Prefab！");
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isActivatedByManager ? Color.green : Color.red;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.DrawWireCube(box.center, box.size);
        }
        Gizmos.matrix = oldMatrix;
    }
}