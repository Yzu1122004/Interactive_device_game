using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerCollision : MonoBehaviour
{
    [Header("碰撞設定")]
    public float invincibilityDuration = 1.0f;
    public float recoilForce = 10f;

    [Header("技能設定")]
    public bool isSkillActive = false;
    public float skillCostPerSec = 10f;
    public float skillRegenPerSec = 3f;

    [Header("雷射射程與推力設定")]
    [Tooltip("雷射最大射程（距離限制）")]
    public float maxLaserDistance = 20f;
    [Tooltip("雷射持續推動物件的力量大小")]
    public float laserPushForce = 20f;

    [Header("--- 標籤（Tag）獨立設定 ---")]
    [Tooltip("這些物件撞到玩家時，玩家會受到傷害扣血")]
    private Dictionary<string, float> damageValues = new Dictionary<string, float>()
    {
        { "Car", 40f },
        { "Motor", 10f },
        { "Tree", 10f },
        { "Plant", 5f },
        { "TrafficCone", 5f },
        { "TransformerBox", 10f },
        { "Shop", 15f },
        { "Otheritem", 5f }
    };

    [Tooltip("只有在這個清單內的標籤物件，才能被雷射射中並持續推動！")]
    public List<string> pushableTags = new List<string>()
    {
        "TrafficCone",
        "Motor",
        "Plant",
        "Otheritem"
    };

    [Header("特效設定")]
    public ParticleSystem skillParticle;   // 拖入雷射射中物體時噴發的火花粒子
    public LineRenderer laserLine;         // 拖入你的 Line Renderer 組件
    public Transform laserOrigin;          // 雷射發射起點（車頭前方的空物件）

    private Rigidbody rb;
    private bool isInvincible = false;
    private MeshRenderer[] renderers;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<MeshRenderer>();

        // 防呆：如果忘記在 Inspector 設定發射點，預設使用輪椅本體中心
        if (laserOrigin == null) laserOrigin = transform;

        // 確保遊戲一開始執行時，雷射主束與火花粒子百分之百處於關閉隱藏狀態
        ForceDisableLaser();
    }

    void Update()
    {
        if (GameManager.Instance.isGameOver) return;

        HandleSkillInput();
        HandleEnergyCalculations();

        // 如果玩家正在按住 Q 鍵且有能量，每幀更新雷射位置、範圍與物理推力計算
        if (isSkillActive)
        {
            UpdateLaserAndPhysics();
        }
    }

    private void HandleSkillInput()
    {
        // 偵測是否按住 Q 鍵，且 GameManager 內的能量大於 0
        if (Input.GetKey(KeyCode.Q) && GameManager.Instance.playerEnergy > 0)
        {
            if (!isSkillActive) StartSkill();
        }
        else
        {
            if (isSkillActive) StopSkill();
        }
    }

    private void HandleEnergyCalculations()
    {
        if (isSkillActive)
        {
            GameManager.Instance.playerEnergy -= skillCostPerSec * Time.deltaTime;
            if (GameManager.Instance.playerEnergy <= 0)
            {
                GameManager.Instance.playerEnergy = 0;
                StopSkill();
            }
        }
        else
        {
            if (GameManager.Instance.playerEnergy < GameManager.Instance.maxEnergy)
            {
                GameManager.Instance.playerEnergy += skillRegenPerSec * Time.deltaTime;
                GameManager.Instance.playerEnergy = Mathf.Clamp(GameManager.Instance.playerEnergy, 0f, GameManager.Instance.maxEnergy);
            }
        }
        GameManager.Instance.UpdateEnergyUI();
    }

    // --- 修改：開啟技能時顯示雷射與播放特效 ---
    private void StartSkill()
    {
        isSkillActive = true;
        Debug.Log("技能開啟！");

        if (laserLine != null)
        {
            laserLine.enabled = true;
            laserLine.positionCount = 2; // 啟用起點與終點
        }

        if (skillParticle != null && !skillParticle.isPlaying)
        {
            skillParticle.Play();
        }
    }

    // --- 修改：關閉技能時徹底隱藏雷射與停止特效 ---
    private void StopSkill()
    {
        isSkillActive = false;
        Debug.Log("技能關閉！");

        if (laserLine != null)
        {
            laserLine.positionCount = 0; // 清空點位，避免畫面殘留殘影線條
            laserLine.enabled = false;
        }

        if (skillParticle != null)
        {
            skillParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 停止並清除畫面上現存粒子
        }
    }

    // 一開始遊戲時的強制防呆關閉
    private void ForceDisableLaser()
    {
        isSkillActive = false;
        if (laserLine != null)
        {
            laserLine.positionCount = 0;
            laserLine.enabled = false;
        }
        if (skillParticle != null)
        {
            skillParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    // 動態計算雷射射線與可推動標籤物件
    private void UpdateLaserAndPhysics()
    {
        if (laserLine == null || laserOrigin == null) return;

        // 設定雷射線條的起點（Index 0）為發射口位置
        if (laserLine.positionCount < 2) laserLine.positionCount = 2;
        laserLine.SetPosition(0, laserOrigin.position);

        RaycastHit hit;
        Vector3 rayDirection = transform.forward; // 朝向輪椅車頭正前方發射

        // 進行物理射線偵測（雷射距離由 maxLaserDistance 決定）
        if (Physics.Raycast(laserOrigin.position, rayDirection, out hit, maxLaserDistance))
        {
            // 如果射線在距離內打到任何物體，雷射的終點（Index 1）就會精準停在該撞擊點上
            laserLine.SetPosition(1, hit.point);

            // 讓衝擊火花特效跟隨移動到物體撞擊表面，並面向碰撞法線
            if (skillParticle != null)
            {
                skillParticle.transform.position = hit.point;
                skillParticle.transform.rotation = Quaternion.LookRotation(hit.normal);

                if (!skillParticle.isPlaying) skillParticle.Play();
            }

            // --- 雷射觸碰與推動標籤檢查 ---
            string hitTag = hit.collider.gameObject.tag;

            // 檢查此物體的 Tag 是否在「可推動標籤清單 (pushableTags)」內
            if (pushableTags.Contains(hitTag))
            {
                Rigidbody targetRb = hit.collider.GetComponent<Rigidbody>();

                // 如果該物件有掛 Rigidbody 且未凍結物理 (isKinematic = false)
                if (targetRb != null && !targetRb.isKinematic)
                {
                    Vector3 pushDirection = transform.forward;
                    pushDirection.y = 0.1f; // 微幅往上提，推起來更順
                    pushDirection = pushDirection.normalized;

                    // 在雷射打中的精準點（hit.point）上持續施加推力
                    targetRb.AddForceAtPosition(pushDirection * laserPushForce, hit.point, ForceMode.Force);
                }
            }
        }
        else
        {
            // 如果前方沒打到任何東西，雷射光束直接延伸到最遠自訂射程
            Vector3 endPoint = laserOrigin.position + (rayDirection * maxLaserDistance);
            laserLine.SetPosition(1, endPoint);

            // 沒打到東西時，火花粒子移動到最遠端
            if (skillParticle != null)
            {
                skillParticle.transform.position = endPoint;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = laserOrigin != null ? laserOrigin.position : transform.position + Vector3.up * 0.5f;
        Gizmos.DrawWireSphere(origin, 0.2f);
        Gizmos.DrawLine(origin, origin + transform.forward * maxLaserDistance);
        Gizmos.DrawWireSphere(origin + transform.forward * maxLaserDistance, 0.2f);
    }

    // 身體硬碰撞扣血邏輯
    private void OnCollisionEnter(Collision collision)
    {
        string hitTag = collision.gameObject.tag;

        if (hitTag == "End")
        {
            GameManager.Instance.CompleteLevel();
            return;
        }

        if (damageValues.ContainsKey(hitTag))
        {
            // 按住 Q 發射雷射技能期間，身體處於無敵狀態，撞到障礙物不會扣血
            if (isSkillActive) return;
            if (isInvincible) return;

            float damage = damageValues[hitTag];
            GameManager.Instance.TakeDamage(damage, hitTag);

            ApplyRecoil(collision);
            StartCoroutine(InvincibilityRoutine());
        }
    }

    private void ApplyRecoil(Collision collision)
    {
        Vector3 recoilDir = collision.contacts[0].normal;
        Vector3 finalDir = (recoilDir + Vector3.up * 0.5f).normalized;
        rb.AddForce(finalDir * recoilForce, ForceMode.Impulse);
    }

    IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        float elapsed = 0;
        while (elapsed < invincibilityDuration)
        {
            SetRenderersEnabled(false);
            yield return new WaitForSeconds(0.1f);
            SetRenderersEnabled(true);
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