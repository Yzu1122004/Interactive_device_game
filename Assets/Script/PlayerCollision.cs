using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerCollision : MonoBehaviour
{
    [Header("碰撞設定")]
    public float invincibilityDuration = 1.0f; // 無敵時間
    public float recoilForce = 10f;            // 反彈力道強度

    private Rigidbody rb;
    private bool isInvincible = false;
    private MeshRenderer[] renderers;          // 用於製作閃爍效果
    private Dictionary<string, float> damageValues = new Dictionary<string, float>()
    {
        { "Truck", 10f },
        { "Motor", 10f },
        { "Tree", 10f },
        { "Plant", 10f },
        { "TrafficCone", 10f },
        { "TransformerBox", 10f },
        { "Shop", 10f },
        { "Otheritem", 10f }
    };
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // 取得所有子物件的 Renderer，受傷時可以做閃爍效果
        renderers = GetComponentsInChildren<MeshRenderer>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        string hitTag = collision.gameObject.tag;

        // 判斷是否撞到終點
        if (hitTag == "End")
        {
            // 呼叫 GameManager 執行成功的結算
            GameManager.Instance.CompleteLevel();
            return;
        }

        if (damageValues.ContainsKey(hitTag))
        {
            // 1. 檢查是否在無敵冷卻中
            if (isInvincible) return;

            // 2. 執行扣血
            float damage = damageValues[hitTag];
            GameManager.Instance.TakeDamage(damage, hitTag);

            // 3. 觸發物理反彈
            ApplyRecoil(collision);

            // 4. 開啟無敵冷卻協程
            StartCoroutine(InvincibilityRoutine());
        }
    }


    private void ApplyRecoil(Collision collision)
    {
        // 取得撞擊點的法線方向 (這條線會從障礙物指向玩家)
        Vector3 recoilDir = collision.contacts[0].normal;

        // 為了防止反彈力把輪椅壓進地板，稍微給一點向上的分量
        Vector3 finalDir = (recoilDir + Vector3.up * 0.5f).normalized;

        // 使用 Impulse (瞬間衝力) 模式
        rb.AddForce(finalDir * recoilForce, ForceMode.Impulse);
    }

    // --- 冷卻時間與視覺反饋 ---
    IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;

        // 簡單的閃爍效果，讓玩家知道現在是無敵的
        float elapsed = 0;
        while (elapsed < invincibilityDuration)
        {
            SetRenderersEnabled(false); // 隱藏
            yield return new WaitForSeconds(0.1f);
            SetRenderersEnabled(true);  // 顯示
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.2f;
        }

        isInvincible = false;
    }

    void SetRenderersEnabled(bool state)
    {
        foreach (var r in renderers) r.enabled = state;
    }
}