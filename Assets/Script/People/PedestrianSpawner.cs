using UnityEngine;
using System.Collections.Generic;

public class PedestrianSpawner : MonoBehaviour
{
    [System.Serializable]
    public class PedestrianRoute
    {
        public string routeName;
        public List<Transform> waypoints;
    }

    [Header("行人 Prefabs 陣列")]
    public GameObject[] pedestrianPrefabs;

    [Header("路線設定")]
    public List<PedestrianRoute> routes;

    [Header("生成數量限制設定")]
    public int maxPedestrians = 15;  // 場景同時存在的最多人數

    [Header("生成時間區間設定 (秒)")]
    public float minSpawnInterval = 2f; // 最快幾秒生一隻
    public float maxSpawnInterval = 5f; // 最慢幾秒生一隻

    [Header("行人移動速度設定")]
    public float minMoveSpeed = 1.5f;
    public float maxMoveSpeed = 2.5f;

    private List<GameObject> activePedestrians = new List<GameObject>();
    private float timer = 0f;
    private float currentRequiredInterval; // 當前這波需要等待的時間

    void Start()
    {
        // 遊戲開始時，先隨機決定第一次生成的等待時間
        UpdateRandomInterval();

        // 初始先生一隻
        SpawnPedestrian();
    }

    void Update()
    {
        // 清理列表中已經消失的空物件
        activePedestrians.RemoveAll(item => item == null);

        timer += Time.deltaTime;

        // 當時間達到這一次隨機決定的區間值時
        if (timer >= currentRequiredInterval)
        {
            // 檢查當前人數是否已經達到上限
            if (activePedestrians.Count < maxPedestrians)
            {
                SpawnPedestrian();
            }

            timer = 0f;
            // 重要：生成完後，重新亂數抽下一隻的等待時間！
            UpdateRandomInterval();
        }
    }

    // 計算隨機區間值的函式
    void UpdateRandomInterval()
    {
        currentRequiredInterval = Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    void SpawnPedestrian()
    {
        if (pedestrianPrefabs.Length == 0 || routes.Count == 0) return;

        // 1. 隨機外觀與路線
        GameObject randomPrefab = pedestrianPrefabs[Random.Range(0, pedestrianPrefabs.Length)];
        PedestrianRoute randomRoute = routes[Random.Range(0, routes.Count)];
        if (randomRoute.waypoints.Count < 2) return;

        // 2. 生成
        Transform startPoint = randomRoute.waypoints[0];
        GameObject go = Instantiate(randomPrefab, startPoint.position, startPoint.rotation);
        activePedestrians.Add(go);

        // 3. 初始化 AI
        PedestrianAI ai = go.GetComponent<PedestrianAI>();
        if (ai == null)
        {
            ai = go.AddComponent<PedestrianAI>();
        }

        float randomSpeed = Random.Range(minMoveSpeed, maxMoveSpeed);
        ai.SetupRoute(randomRoute.waypoints, randomSpeed);
    }
}