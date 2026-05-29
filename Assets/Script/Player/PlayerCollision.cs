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

    [Header("標籤（Tag）設定")]
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
        { "Iron", 5f },
        { "Otheritem", 5f }
    };

    [Tooltip("只有在這個清單內的標籤物件，才能被雷射射中並持續推動！")]
    public List<string> pushableTags = new List<string>()
    {
        "TrafficCone",
        "Motor",
        "Plant",
        "Otheritem",
        "Iron"
    };

    [Header("特效設定")]
    public ParticleSystem skillParticle;
    public LineRenderer laserLine;
    public Transform laserOrigin;
    public GameObject laserSphere;

    [Header("技能音效設定")]
    [Tooltip("拖入負責播放雷射持續嗡嗡聲的 Audio Source（記得勾選 Loop）")]
    public AudioSource laserAudioSource;

    [Header("Arduino 控制")]
    public ArduinoBasic arduino;

    private Rigidbody rb;
    private bool isInvincible = false;
    private MeshRenderer[] renderers;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<MeshRenderer>();

        if (laserOrigin == null) laserOrigin = transform;
        if (arduino == null) arduino = FindObjectOfType<ArduinoBasic>();

        ForceDisableLaser();
    }

    void Update()
    {
        if (GameManager.Instance.isGameOver) return;

        HandleSkillInput();
        HandleEnergyCalculations();

        if (isSkillActive)
        {
            UpdateLaserAndPhysics();
        }
    }

    private void HandleSkillInput()
    {
        bool keyboardSkillInput = Input.GetKey(KeyCode.Q);
        bool arduinoSkillInput = arduino != null && arduino.IsAttackPressed;
        bool wantsToUseSkill = (keyboardSkillInput || arduinoSkillInput) && GameManager.Instance.playerEnergy > 0;

        if (arduinoSkillInput && !isSkillActive)
        {
            Debug.Log("Arduino 攻擊按鈕觸發技能");
        }

        if (wantsToUseSkill)
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

    private void StartSkill()
    {
        isSkillActive = true;
        Debug.Log("技能開啟！");

        if (laserLine != null)
        {
            laserLine.enabled = true;
            laserLine.positionCount = 2;
        }

        if (skillParticle != null && !skillParticle.isPlaying)
        {
            skillParticle.Play();
        }

        if (laserSphere != null)
        {
            laserSphere.SetActive(true);
        }

        // 【播放雷射音效】
        if (laserAudioSource != null && !laserAudioSource.isPlaying)
        {
            laserAudioSource.Play();
        }
    }

    private void StopSkill()
    {
        isSkillActive = false;
        Debug.Log("技能關閉！");

        if (laserLine != null)
        {
            laserLine.positionCount = 0;
            laserLine.enabled = false;
        }

        if (skillParticle != null)
        {
            skillParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (laserSphere != null)
        {
            laserSphere.SetActive(false);
        }

        // 【停止雷射音效】
        if (laserAudioSource != null && laserAudioSource.isPlaying)
        {
            laserAudioSource.Stop();
        }
    }

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

        if (laserSphere != null)
        {
            laserSphere.SetActive(false);
        }

        // 【初始防呆關閉音效】
        if (laserAudioSource != null)
        {
            laserAudioSource.Stop();
        }
    }

    private void UpdateLaserAndPhysics()
    {
        if (laserLine == null || laserOrigin == null) return;

        if (laserLine.positionCount < 2) laserLine.positionCount = 2;
        laserLine.SetPosition(0, laserOrigin.position);

        RaycastHit hit;
        Vector3 rayDirection = transform.forward;

        if (Physics.Raycast(laserOrigin.position, rayDirection, out hit, maxLaserDistance))
        {
            laserLine.SetPosition(1, hit.point);

            if (skillParticle != null)
            {
                skillParticle.transform.position = hit.point;
                skillParticle.transform.rotation = Quaternion.LookRotation(hit.normal);

                if (!skillParticle.isPlaying) skillParticle.Play();
            }

            string hitTag = hit.collider.gameObject.tag;

            if (pushableTags.Contains(hitTag))
            {
                Rigidbody targetRb = hit.collider.GetComponent<Rigidbody>();

                if (targetRb != null && !targetRb.isKinematic)
                {
                    Vector3 pushDirection = transform.forward;
                    pushDirection.y = 0.1f;
                    pushDirection = pushDirection.normalized;

                    targetRb.AddForceAtPosition(pushDirection * laserPushForce, hit.point, ForceMode.Force);
                }
            }
        }
        else
        {
            Vector3 endPoint = laserOrigin.position + (rayDirection * maxLaserDistance);
            laserLine.SetPosition(1, endPoint);

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
