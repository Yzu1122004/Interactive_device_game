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

    [Header("推動物件設定")]
    public float pushRange = 3f;
    public float pushRadius = 1.5f;
    public float pushForce = 15f;

    // --- 新增：粒子特效欄位 ---
    [Header("特效設定")]
    public ParticleSystem skillParticle;   // 拖入你的粒子特效組件

    private Rigidbody rb;
    private bool isInvincible = false;
    private MeshRenderer[] renderers;
    private Dictionary<string, float> damageValues = new Dictionary<string, float>()
    {
        { "Truck", 10f }, { "Motor", 10f }, { "Tree", 10f }, { "Plant", 10f },
        { "TrafficCone", 10f }, { "TransformerBox", 10f }, { "Shop", 10f }, { "Otheritem", 10f }
    };

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<MeshRenderer>();

        // 安全檢查：如果一開始特效在播放，先讓它停止
        if (skillParticle != null && skillParticle.isPlaying)
        {
            skillParticle.Stop();
        }
    }

    void Update()
    {
        if (GameManager.Instance.isGameOver) return;

        HandleSkillInput();
        HandleEnergyCalculations();

        if (isSkillActive)
        {
            PushObstaclesForward();
        }
    }

    private void HandleSkillInput()
    {
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

    // --- 修改：開啟技能時播放特效 ---
    private void StartSkill()
    {
        isSkillActive = true;
        Debug.Log("技能開啟！");

        if (skillParticle != null)
        {
            skillParticle.Play(); // 開始噴射粒子
        }
    }

    // --- 修改：關閉技能時停止特效 ---
    private void StopSkill()
    {
        isSkillActive = false;
        Debug.Log("技能關閉！");

        if (skillParticle != null)
        {
            skillParticle.Stop(); // 停止噴射粒子
        }
    }

    private void PushObstaclesForward()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 direction = transform.forward;

        RaycastHit[] hits = Physics.SphereCastAll(origin, pushRadius, direction, pushRange);

        foreach (RaycastHit hit in hits)
        {
            if (damageValues.ContainsKey(hit.collider.tag))
            {
                Rigidbody targetRb = hit.collider.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    Vector3 pushDir = (hit.collider.transform.position - transform.position).normalized;
                    pushDir.y = 0.2f;
                    pushDir = pushDir.normalized;

                    targetRb.AddForce(pushDir * pushForce, ForceMode.Impulse);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Gizmos.DrawWireSphere(origin, pushRadius);
        Gizmos.DrawLine(origin, origin + transform.forward * pushRange);
        Gizmos.DrawWireSphere(origin + transform.forward * pushRange, pushRadius);
    }

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