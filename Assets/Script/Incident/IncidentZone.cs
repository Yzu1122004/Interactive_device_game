using UnityEngine;

public class IncidentZone : MonoBehaviour
{
    public enum IncidentType
    {
        PedestrianBlocker,
        BrakingCarHorn,
        ExhaustCar,
        FallingFlowerPot
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
    [Tooltip("模式 A：如果整組花盆是原本就擺在場景上的，請把它的最外層父物件拖到這裡")]
    public Rigidbody targetFlowerPotGroup;

    [Tooltip("模式 B：如果你希望踩到格子才把多花盆預製物生成出來倒下，請把 Prefab 拖到這裡")]
    public GameObject flowerPotGroupPrefab;
    [Tooltip("配合模式 B 的生成位置，留空則預設在觸發區中心")]
    public Transform flowerPotSpawnPoint;

    [Tooltip("讓花盆倒下的推力大小")]
    public float pushForce = 8f;

    private bool isActivatedByManager = false;
    private bool hasTriggered = false;
    private bool playerInside = false;
    private ArduinoBasic arduino;

    private void Start()
    {
        arduino = FindObjectOfType<ArduinoBasic>();
    }

    public void SetActivate(bool state)
    {
        isActivatedByManager = state;
        hasTriggered = false;
        playerInside = false;
        GetComponent<Collider>().enabled = true;
    }

    private void Update()
    {
        if (isActivatedByManager && playerInside && !hasTriggered && arduino != null && arduino.ConsumeRandomSpawnPressed())
        {
            hasTriggered = true;
            arduino.ArduinoWrite("LIGHT_RANDOM_OFF");
            ExecuteIncident();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isActivatedByManager && !hasTriggered && other.CompareTag("Player"))
        {
            playerInside = true;

            if (arduino != null)
            {
                arduino.ArduinoWrite("LIGHT_RANDOM_ON");
                Debug.Log("【Arduino】玩家進入事件區，已開啟隨機事件按鈕燈。");
            }
            else
            {
                Debug.LogWarning("【Arduino】找不到 ArduinoBasic，無法開啟隨機事件按鈕燈。");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;

            if (!hasTriggered && arduino != null)
            {
                arduino.ArduinoWrite("LIGHT_RANDOM_OFF");
                Debug.Log("【Arduino】玩家離開事件區，已關閉隨機事件按鈕燈。");
            }
        }
    }

    private void ExecuteIncident()
    {
        Debug.Log($"【干擾事件】玩家按下隨機事件按鈕，觸發：{assignedIncident}");

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
        else
        {
            Debug.LogWarning("【干擾事件】擋路行人事件缺少 Prefab 或 SpawnPoint。");
        }
    }

    private void TriggerBrakingCarHorn()
    {
        if (hornCarPrefab != null && carSpawnPoint != null)
        {
            Instantiate(hornCarPrefab, carSpawnPoint.position, carSpawnPoint.rotation);
        }
        else
        {
            Debug.LogWarning("【干擾事件】喇叭車事件缺少 Prefab 或 SpawnPoint。");
        }
    }

    private void TriggerExhaustCar()
    {
        if (exhaustCarPrefab != null && exhaustCarSpawnPoint != null)
        {
            Instantiate(exhaustCarPrefab, exhaustCarSpawnPoint.position, exhaustCarSpawnPoint.rotation);
        }
        else
        {
            Debug.LogWarning("【干擾事件】廢氣車事件缺少 Prefab 或 SpawnPoint。");
        }
    }

    private void TriggerFallingFlowerPot()
    {
        Rigidbody potRbToPush = null;

        if (flowerPotGroupPrefab != null)
        {
            Vector3 spawnPos = flowerPotSpawnPoint != null ? flowerPotSpawnPoint.position : transform.position;
            Quaternion spawnRot = flowerPotSpawnPoint != null ? flowerPotSpawnPoint.rotation : transform.rotation;

            GameObject spawnedPot = Instantiate(flowerPotGroupPrefab, spawnPos, spawnRot);
            potRbToPush = spawnedPot.GetComponent<Rigidbody>();

            if (potRbToPush == null)
            {
                Debug.LogError("【干擾事件】生成的花盆 Prefab 最外層父物件沒有 Rigidbody。");
                return;
            }
        }
        else if (targetFlowerPotGroup != null)
        {
            potRbToPush = targetFlowerPotGroup;
        }

        if (potRbToPush != null)
        {
            potRbToPush.isKinematic = false;
            Vector3 pushDirection = (transform.right + Vector3.down * 0.3f).normalized;
            potRbToPush.AddForce(pushDirection * pushForce, ForceMode.Impulse);
            potRbToPush.AddTorque(transform.forward * pushForce * 0.5f, ForceMode.Impulse);
        }
        else
        {
            Debug.LogWarning("【干擾事件】未綁定任何場景花盆物件或花盆 Prefab。");
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
