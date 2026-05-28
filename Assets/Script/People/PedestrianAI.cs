using UnityEngine;
using System.Collections.Generic;

public class PedestrianAI : MonoBehaviour
{
    private List<Transform> waypoints = new List<Transform>();
    private int currentWaypointIndex = 0;

    [Header("移動設定")]
    public float moveSpeed = 2f;
    public float rotationSpeed = 10f;
    public float arrivalDistance = 0.5f;

    [Tooltip("拖入這個行人身上的 Audio Source 組件（播放腳步聲或環境談話聲）")]
    public AudioSource footstepAudioSource;
    [Tooltip("腳步聲音量淡入淡出的平滑速度")]
    public float soundFadeSpeed = 5f;

    private float maxFootstepVolume = 1.0f;
    private float currentSpeed;
    private Animator animator;
    private bool isWaitingForLight = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        currentSpeed = moveSpeed; // 確保初始速度有被賦值

        // 【音效防呆初始化】如果忘記在 Inspector 拖入，自動抓取同物件身上的 AudioSource
        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
        }

        if (footstepAudioSource != null)
        {
            maxFootstepVolume = footstepAudioSource.volume; // 記住你在 Inspector 給這隻行人設定的初始音量大小

            // 雙重保險：確保 3D 音效有勾、且一開始是播放狀態
            footstepAudioSource.spatialBlend = 1.0f; // 強制 3D 空間音效
            if (!footstepAudioSource.isPlaying) footstepAudioSource.Play();
        }
    }

    void Update()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        // 如果正在等紅燈，每幀持續檢查是否轉為綠燈
        if (isWaitingForLight)
        {
            CheckLightStatus();
        }
        else
        {
            MoveTowardsWaypoint();
        }

        // 【核心控制】根據目前的行進狀態與實際速度，控制走路聲音的消失與出現
        HandleFootstepSound();
    }

    // 動態控制腳步音效的開關與淡入淡出
    private void HandleFootstepSound()
    {
        if (footstepAudioSource == null) return;

        // 如果目前處於等紅燈狀態，或是實質移動速度接近 0（停下步伐）
        if (isWaitingForLight || currentSpeed < 0.05f)
        {
            // 讓音量平滑地遞減到 0 (消失)
            footstepAudioSource.volume = Mathf.MoveTowards(footstepAudioSource.volume, 0f, soundFadeSpeed * Time.deltaTime);

            // 當音量完全歸零時，暫停播放以優化音訊效能
            if (footstepAudioSource.volume <= 0f && footstepAudioSource.isPlaying)
            {
                footstepAudioSource.Pause();
            }
        }
        else // 行人正在前進移動中
        {
            // 如果剛才處於暫停狀態，重新點火恢復播放
            if (!footstepAudioSource.isPlaying)
            {
                footstepAudioSource.UnPause();
            }

            // 讓音量平滑地遞增回原本設定的最大音量 (重新播放)
            footstepAudioSource.volume = Mathf.MoveTowards(footstepAudioSource.volume, maxFootstepVolume, soundFadeSpeed * Time.deltaTime);
        }
    }

    public void SetupRoute(List<Transform> routeWaypoints, float speed)
    {
        waypoints = new List<Transform>(routeWaypoints);
        moveSpeed = speed;
        currentSpeed = speed;
        currentWaypointIndex = 0;

        if (waypoints.Count > 0)
        {
            transform.position = waypoints[0].position;
        }

        SetWalkingAnimation(true);
    }

    private void MoveTowardsWaypoint()
    {
        Transform targetWaypoint = waypoints[currentWaypointIndex];

        // 計算朝向路點的方向
        Vector3 direction = (targetWaypoint.position - transform.position).normalized;
        direction.y = 0; // 鎖定 Y 軸，防止身體前後傾斜

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 往前移動 (以當前速度為準)
        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);

        // 抵達檢查
        if (Vector3.Distance(transform.position, targetWaypoint.position) < arrivalDistance)
        {
            // 在前進到下一個點之前，先檢查這一個點是不是「紅綠燈等待點」
            TrafficLightWaypoint lightWaypoint = targetWaypoint.GetComponent<TrafficLightWaypoint>();

            // 如果踩到紅綠燈點，而且現在不安全/不能過馬路
            if (lightWaypoint != null && !lightWaypoint.CanPedestrianPassSafety(moveSpeed))
            {
                isWaitingForLight = true;
                currentSpeed = 0f; // 速度歸零
                SetWalkingAnimation(false); // 播 Idle 動畫
                // Debug.Log($"{gameObject.name} 踩到等待點，開始等紅燈。");
                Debug.Log(gameObject.name + " reached the waiting point and is now waiting for the red light.");
                return; // 卡在當前 index，直到綠燈亮起
            }

            // 如果沒紅綠燈或安全可期，前進到下一個路點
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Count)
            {
                DestroyDestination();
            }
        }
    }

    // 原地等紅燈時的每幀檢查
    private void CheckLightStatus()
    {
        Transform currentWaypoint = waypoints[currentWaypointIndex];
        TrafficLightWaypoint lightWaypoint = currentWaypoint.GetComponent<TrafficLightWaypoint>();

        // 檢查紅綠燈是否允許通行，且剩餘時間足夠
        if (lightWaypoint == null || lightWaypoint.CanPedestrianPassSafety(moveSpeed))
        {
            isWaitingForLight = false;
            currentSpeed = moveSpeed; // 【關鍵】把速度還給行人！
            currentWaypointIndex++;   // 准許前往斑馬線對面的下個點
            SetWalkingAnimation(true); // 恢復走路動畫
            Debug.Log($"{gameObject.name} 偵測到綠燈且時間充足，出發過馬路！");
        }
    }

    private void SetWalkingAnimation(bool isWalking)
    {
        if (animator != null)
        {
            animator.SetBool("IsWalking", isWalking);
        }
    }

    private void DestroyDestination()
    {
        Destroy(gameObject);
    }
}