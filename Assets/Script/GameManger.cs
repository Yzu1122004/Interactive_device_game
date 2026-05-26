using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("玩家狀態")]
    public float maxHealth = 100f;
    public float playerHealth = 100f;
    // --- 新增能量欄位 ---
    public float maxEnergy = 100f;
    public float playerEnergy = 100f;
    public bool isGameOver = false;

    [Header("倒數計時設定")]
    public float timeRemaining = 120f;
    public bool isTimerRunning = false;

    [Header("UI 元件")]
    public Image healthBarFill;
    public Text healthText;
    public Text timerText;
    // --- 新增能量 UI ---
    public Image energyBarFill; // 拖入能量條的 Image (Filled 模式)
    public Text energyText;     // 可選：顯示百分比的文字

    [Header("結算 UI")]
    public GameObject resultUI;
    public Text resultMessageText;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        Time.timeScale = 1f;
        isTimerRunning = true;
        if (resultUI != null) resultUI.SetActive(false);
        UpdateHealthUI();
        UpdateEnergyUI(); // 初始化能量 UI
    }

    void Update()
    {
        if (isTimerRunning && !isGameOver)
        {
            if (timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime;
                UpdateTimerUI();
            }
            else
            {
                timeRemaining = 0;
                GameOver("時間結束：你未能按時抵達目的地");
            }
        }
    }

    void UpdateHealthUI()
    {
        if (healthBarFill != null) healthBarFill.fillAmount = playerHealth / maxHealth;
        if (healthText != null) healthText.text = playerHealth.ToString() + "%";
    }

    void UpdateTimerUI()
    {
        if (timerText == null) return;

        float minutes = Mathf.FloorToInt(timeRemaining / 60);
        float seconds = Mathf.FloorToInt(timeRemaining % 60);


        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void UpdateEnergyUI()
    {
        if (energyBarFill != null) energyBarFill.fillAmount = playerEnergy / maxEnergy;
        if (energyText != null) energyText.text = Mathf.FloorToInt(playerEnergy).ToString() + "%";
    }

    public void TakeDamage(float amount, string obstacleName)
    {
        if (isGameOver) return;
        playerHealth -= amount;
        playerHealth = Mathf.Clamp(playerHealth, 0, maxHealth);
        UpdateHealthUI();

        if (playerHealth <= 0)
        {
            GameOver("輪椅損壞嚴重：無法行動");
        }
    }

    void GameOver(string message)
    {
        isGameOver = true;
        isTimerRunning = false;
        Time.timeScale = 0f;
        if (resultUI != null) resultUI.SetActive(true);
        if (resultMessageText != null) resultMessageText.text = message;
        Debug.Log("Game Over: " + message);
    }

    public void CompleteLevel()
    {
        if (isGameOver) return;
        isGameOver = true;
        isTimerRunning = false;
        Time.timeScale = 0f;
        if (resultUI != null) resultUI.SetActive(true);
        if (resultMessageText != null)
        {
            int bonus = Mathf.FloorToInt(timeRemaining * 10);
            resultMessageText.text = "抵達終點！\n剩餘時間獎勵：" + bonus;
            resultMessageText.color = Color.yellow;
        }
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}