using UnityEngine;
using System.Collections.Generic;

public class CarAI : MonoBehaviour
{
    private List<Transform> waypoints = new List<Transform>();
    private int currentWaypointIndex = 0;

    [Header("移動設定")]
    public float maxMoveSpeed = 8f;      // 汽車最高時速
    public float rotationSpeed = 8f;     // 轉向平滑度
    public float arrivalDistance = 1.0f;  // 抵達路點的判定距離

    [Header("安全防撞與偵測設定")]
    public float detectionDistance = 5f; // 射線長度
    public Transform raycastOrigin;      // 射線發射點
    public float safeVehicleDistance = 3f; // 與前車的絕對安全距離 (公尺)

    [Header("--- 新增：車輛音效設定 ---")]
    [Tooltip("拖入這台車身上的 Audio Source 組件")]
    public AudioSource engineAudioSource;
    [Tooltip("引擎音效淡入淡出的平滑速度（數值越高，聲音開關越即時）")]
    public float soundFadeSpeed = 5f;

    private float maxEngineVolume = 1.0f; // 紀錄一開始設定的最大音量
    private float currentSpeed;
    private bool isBraking = false;
    private Collider myCollider;

    // 讓 Spawner 幫我們動態綁定「在我們前面的那輛車」
    [HideInInspector] public GameObject frontCar = null;

    void Awake()
    {
        myCollider = GetComponent<Collider>();
    }

    void Start()
    {
        currentSpeed = maxMoveSpeed;
        if (raycastOrigin == null) raycastOrigin = transform;

        // 【音效防呆初始化】
        if (engineAudioSource == null)
        {
            engineAudioSource = GetComponent<AudioSource>();
        }

        if (engineAudioSource != null)
        {
            maxEngineVolume = engineAudioSource.volume; // 記住你在 Inspector 給這台車設定的初始音量大小

            // 雙重保險：確保 3D 音效有勾、且一開始是播放狀態
            engineAudioSource.spatialBlend = 1.0f; // 強制 3D
            if (!engineAudioSource.isPlaying) engineAudioSource.Play();
        }
    }

    void Update()
    {
        if (GameManager.Instance.isGameOver) return;

        // 1. 重設煞車狀態，並進行物理射線/前車偵測
        isBraking = false;
        ObstacleDetection();

        // 2. 依據偵測結果，計算此幀應該要有的移動速度
        if (isBraking)
        {
            // 逐漸減速到 0
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, maxMoveSpeed * 2f * Time.deltaTime);
        }
        else
        {
            // 逐漸恢復到最高速
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxMoveSpeed, maxMoveSpeed * Time.deltaTime);
        }

        // 3. 執行車輛實體移動與轉向
        MoveVehicle();

        // 4. 【核心控制】根據目前的行駛速度，平滑控制引擎聲音的消失與出現
        HandleEngineSound();
    }

    // 動態控制引擎音效的開關與淡入淡出
    private void HandleEngineSound()
    {
        if (engineAudioSource == null) return;

        // 如果目前速度極低（小於 0.05，代表因為紅綠燈或前車停下來了）
        if (currentSpeed < 0.05f)
        {
            // 讓音量平滑地遞減到 0 (消失)
            engineAudioSource.volume = Mathf.MoveTowards(engineAudioSource.volume, 0f, soundFadeSpeed * Time.deltaTime);

            // 當音量完全歸零時，可以選擇暫停播放以節省效能
            if (engineAudioSource.volume <= 0f && engineAudioSource.isPlaying)
            {
                engineAudioSource.Pause();
            }
        }
        else // 車子開始移動了
        {
            // 如果剛才處於暫停狀態，先重新點火播放
            if (!engineAudioSource.isPlaying)
            {
                engineAudioSource.UnPause();
            }

            // 讓音量平滑地遞增回原本設定的最大音量 (重新播放)
            engineAudioSource.volume = Mathf.MoveTowards(engineAudioSource.volume, maxEngineVolume, soundFadeSpeed * Time.deltaTime);
        }
    }

    private void MoveVehicle()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        // 如果真的完全停下來了，就不執行位移，防止小幅抖動
        if (currentSpeed <= 0f) return;

        Transform targetWaypoint = waypoints[currentWaypointIndex];

        // 計算朝向路點的方向
        Vector3 direction = (targetWaypoint.position - transform.position).normalized;
        direction.y = 0; // 鎖定 Y 軸，防止車頭朝上下看

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 往前移動
        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);

        // 檢查是否抵達當前路點
        if (Vector3.Distance(transform.position, targetWaypoint.position) < arrivalDistance)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Count)
            {
                Destroy(gameObject);
            }
        }
    }

    private void ObstacleDetection()
    {
        // --- 保險機制：直接檢查與前車的距離 ---\r
        if (frontCar != null)
        {
            float distToFrontCar = Vector3.Distance(transform.position, frontCar.transform.position);
            if (distToFrontCar < safeVehicleDistance)
            {
                isBraking = true; // 離前車太近了，觸發強制煞車
                return;
            }
        }

        // --- 射線機制：偵測紅綠燈檢查哨與突然切入的車 ---
        Debug.DrawRay(raycastOrigin.position, transform.forward * detectionDistance, Color.green);

        RaycastHit[] hits = Physics.RaycastAll(raycastOrigin.position, transform.forward, detectionDistance);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == myCollider || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            // 偵測到前方的其他汽車
            if (hit.collider.CompareTag("Car"))
            {
                isBraking = true;
                return;
            }

            // 偵測到車道紅綠燈檢查哨
            CarTrafficLightWaypoint carWaypoint = hit.collider.GetComponent<CarTrafficLightWaypoint>();
            if (carWaypoint != null)
            {
                if (!carWaypoint.CanCarPass())
                {
                    isBraking = true; // 紅燈，煞車停下！
                    return;
                }
            }
        }
    }

    public void SetupRoute(List<Transform> routeWaypoints, float speed, GameObject leadingCar)
    {
        waypoints = new List<Transform>(routeWaypoints);
        maxMoveSpeed = speed;
        currentSpeed = speed;
        currentWaypointIndex = 0;
        frontCar = leadingCar; // 記住前車是誰

        if (waypoints.Count > 0)
        {
            transform.position = waypoints[0].position;
            if (waypoints.Count > 1)
            {
                transform.LookAt(new Vector3(waypoints[1].position.x, transform.position.y, waypoints[1].position.z));
            }
        }
    }
}