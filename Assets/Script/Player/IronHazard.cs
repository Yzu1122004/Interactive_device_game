using UnityEngine;

public class IronHazard : MonoBehaviour
{
    [Header("靠近持續傷害設定")]
    [Tooltip("每秒造成的傷害量")]
    public float damagePerSecond = 2f;

    private bool isPlayerInside = false;
    private float damageTimer = 0f;

    void Update()
    {
        // 如果玩家在感應範圍內，且遊戲還沒結束
        if (isPlayerInside && GameManager.Instance != null && !GameManager.Instance.isGameOver)
        {
            damageTimer += Time.deltaTime;

            // 累積滿 1 秒就觸發一次持續傷害
            if (damageTimer >= 1f)
            {

                GameManager.Instance.TakeDamage(damagePerSecond, "Iron_Range");
                damageTimer = 0f;
            }
        }
    }

    // 玩家輪椅進入靠近範圍
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;
            damageTimer = 0f; // 進來時重置計時器
        }
    }

    // 玩家輪椅離開靠近範圍
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            damageTimer = 0f;
        }
    }
}