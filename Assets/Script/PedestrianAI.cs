using UnityEngine;
using System.Collections.Generic;

public class PedestrianAI : MonoBehaviour
{
    private List<Transform> waypoints = new List<Transform>();
    private int currentWaypointIndex = 0;

    [Header("移動設定")]
    public float moveSpeed = 2f;
    public float rotationSpeed = 5f;
    public float arrivalDistance = 0.5f;

    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        MoveTowardsWaypoint();
    }

    // 由 Spawner 初始化這隻行人的路線
    public void SetupRoute(List<Transform> routeWaypoints, float speed)
    {
        waypoints = new List<Transform>(routeWaypoints);
        moveSpeed = speed;
        currentWaypointIndex = 0;

        if (waypoints.Count > 0)
        {
            transform.position = waypoints[0].position; // 初始位置設為第一個路點
        }

        if (animator != null)
        {
            animator.SetBool("isWalking", true);
        }
    }

    private void MoveTowardsWaypoint()
    {
        Transform targetWaypoint = waypoints[currentWaypointIndex];
        Vector3 targetPosition = targetWaypoint.position;
        // 保持 Y 軸與行人一致，防止走一走陷進地下或飄起來
        targetPosition.y = transform.position.y;

        // 1. 計算真正朝向目標的世界方向
        Vector3 direction = targetPosition - transform.position;
        Vector3 normalizedDirection = direction.normalized; // 取得純方向向量

        // 2. 處理旋轉：讓行人平滑面朝目標點
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(normalizedDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        transform.position += normalizedDirection * moveSpeed * Time.deltaTime;

        // 4. 檢查是否到達當前路點
        if (Vector3.Distance(transform.position, targetPosition) < arrivalDistance)
        {
            currentWaypointIndex++;

            // 如果已經走完所有路點，直接刪除物件
            if (currentWaypointIndex >= waypoints.Count)
            {
                DestroyDestination();
            }
        }
    }

    private void DestroyDestination()
    {
        
        Destroy(gameObject);
    }

    // 留給未來紅綠燈系統檢測停下的 API
    public void SetSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
        if (animator != null)
        {
            animator.SetBool("isWalking", newSpeed > 0);
        }
    }
}