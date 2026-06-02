using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("玩家狀態")]
    public float maxHealth = 100f;
    public float playerHealth = 100f;
    public float maxEnergy = 100f;
    public float playerEnergy = 100f;
    public bool isGameOver = false;

    [Header("倒數計時設定")]
    public float timeRemaining = 120f;
    public bool isTimerRunning = false;

    [Header("UI 元件 (已移除百分比文字)")]
    public Image healthBarFill;
    public Image energyBarFill; // 拖入能量條的 Image (Filled 模式)
    public Text timerText;

    [Header("結算 UI")]
    public GameObject resultUI;

    [Header("--- 新增：結算評級圖片設定 ---")]
    [Tooltip("包含三張評級圖片的父物件 Panel")]
    public GameObject ratingPanel;

    [Tooltip("【優秀/滿血】對應的圖片物件（例如：血量 >= 70%）")]
    public GameObject excellentImage;

    [Tooltip("【普通/中等】對應的圖片物件（例如：30% <= 血量 < 70%）")]
    public GameObject goodImage;

    [Tooltip("【慘烈/殘血】對應的圖片物件（例如：血量 < 30%）")]
    public GameObject badImage;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        Time.timeScale = 1f;
        isTimerRunning = true;
        ResetArduinoForNewRound();

        // 初始隱藏所有結算相關 UI
        if (resultUI != null) resultUI.SetActive(false);
        if (ratingPanel != null) ratingPanel.SetActive(false);
        HideAllRatingImages();

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
                GameOver();
            }
        }
    }

    void UpdateHealthUI()
    {
        if (healthBarFill != null) healthBarFill.fillAmount = playerHealth / maxHealth;
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
    }

    public void TakeDamage(float amount, string obstacleName)
    {
        if (isGameOver) return;
        playerHealth -= amount;
        playerHealth = Mathf.Clamp(playerHealth, 0, maxHealth);
        UpdateHealthUI();

        if (playerHealth <= 0)
        {
            GameOver();
        }
    }

    // 失敗結算（時間到 或 血量歸零）
    void GameOver()
    {
        isGameOver = true;
        isTimerRunning = false;
        Time.timeScale = 0f;

        if (resultUI != null) resultUI.SetActive(true);

        // 【核心修改】失敗時也要打開 ratingPanel 並亮起 badImage
        if (ratingPanel != null) ratingPanel.SetActive(true);
        HideAllRatingImages();
        if (badImage != null) badImage.SetActive(true);

        Debug.Log("Game Over: 輪椅損壞或時間結束 -> 顯示 badImage");
    }

    // 成功抵達終點結算
    public void CompleteLevel()
    {
        if (isGameOver) return;
        isGameOver = true;
        isTimerRunning = false;
        Time.timeScale = 0f;

        if (resultUI != null) resultUI.SetActive(true);

        if (ratingPanel != null) ratingPanel.SetActive(true);

        // 依據剩餘血量比例決定顯示哪張圖片
        float healthPercentage = playerHealth / maxHealth;
        ShowRatingImageByHealth(healthPercentage);
    }

    // 依據血量百分比亮起對應圖片
    private void ShowRatingImageByHealth(float healthPercent)
    {
        HideAllRatingImages(); // 先全部關閉防呆

        if (healthPercent >= 0.7f) // 血量 70% 以上 (高血量/滿血過關)
        {
            if (excellentImage != null) excellentImage.SetActive(true);
            Debug.Log("通關評級：優秀 (Excellent)");
        }
        else if (healthPercent >= 0.3f) // 血量 30% ~ 69% 之間 (中等血量過關)
        {
            if (goodImage != null) goodImage.SetActive(true);
            Debug.Log("通關評級：普通 (Good)");
        }
        else // 血量小於 30% (殘血過關)
        {
            if (badImage != null) badImage.SetActive(true);
            Debug.Log("通關評級：慘烈 (Bad)");
        }
    }

    // 重置所有圖片狀態
    private void HideAllRatingImages()
    {
        if (excellentImage != null) excellentImage.SetActive(false);
        if (goodImage != null) goodImage.SetActive(false);
        if (badImage != null) badImage.SetActive(false);
    }

    public void RestartGame()
    {
        ResetArduinoForNewRound();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ResetArduinoForNewRound()
    {
        ArduinoBasic arduino = FindObjectOfType<ArduinoBasic>();
        if (arduino != null)
        {
            arduino.ResetArduinoState();
        }
    }
}