using UnityEngine;
using System.Collections.Generic;

public class CarSpawner : MonoBehaviour
{
    [System.Serializable]
    public class CarRoute
    {
        public string routeName;
        public List<Transform> waypoints;
    }

    [Header("汽車 Prefabs 陣列 (你的汽車模型)")]
    public GameObject[] carPrefabs;

    [Header("車道線設定")]
    public List<CarRoute> carRoutes;

    [Header("車流限制數量")]
    public int maxCars = 10;

    [Header("生成時間間隔 (秒)")]
    public float minSpawnInterval = 3f;
    public float maxSpawnInterval = 7f;

    [Header("車速設定")]
    public float minCarSpeed = 6f;
    public float maxCarSpeed = 10f;

    private List<GameObject> activeCars = new List<GameObject>();
    private Dictionary<string, GameObject> lastSpawnedCarOnRoute = new Dictionary<string, GameObject>();
    private float timer = 0f;
    private float currentInterval;

    void Start()
    {
        UpdateRandomInterval();
        if (activeCars.Count < maxCars) SpawnCar();
    }

    void Update()
    {
        // 移除列表中已經走到底被 Destory 的空物件
        activeCars.RemoveAll(item => item == null);

        timer += Time.deltaTime;
        if (timer >= currentInterval)
        {
            if (activeCars.Count < maxCars)
            {
                SpawnCar();
            }
            timer = 0f;
            UpdateRandomInterval();
        }
    }

    void UpdateRandomInterval()
    {
        currentInterval = Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    void SpawnCar()
    {
        if (carPrefabs.Length == 0 || carRoutes.Count == 0) return;

        GameObject randomPrefab = carPrefabs[Random.Range(0, carPrefabs.Length)];
        CarRoute randomRoute = carRoutes[Random.Range(0, carRoutes.Count)];
        if (randomRoute.waypoints.Count < 2) return;

        Transform startPoint = randomRoute.waypoints[0];
        GameObject carGo = Instantiate(randomPrefab, startPoint.position, startPoint.rotation);
        activeCars.Add(carGo);

        // 獲取這條路線上，目前在我們前面的那一輛車
        GameObject leadingCar = null;
        if (lastSpawnedCarOnRoute.ContainsKey(randomRoute.routeName))
        {
            leadingCar = lastSpawnedCarOnRoute[randomRoute.routeName];
        }

        // 初始化汽車 AI
        CarAI carAI = carGo.GetComponent<CarAI>();
        if (carAI == null) carAI = carGo.AddComponent<CarAI>();

        // 給予車子隨機速度、綁定路線、並指定前車
        float randomSpeed = Random.Range(minCarSpeed, maxCarSpeed);
        carAI.SetupRoute(randomRoute.waypoints, randomSpeed, leadingCar);

        // 更新這條路線的「最新一輛車」紀錄，讓下一輛生成的車可以當作防撞目標
        lastSpawnedCarOnRoute[randomRoute.routeName] = carGo;
    }
}