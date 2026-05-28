using UnityEngine;
using System.Collections.Generic;

public class IncidentManager : MonoBehaviour
{
    [Header("隨機事件配置")]
    [Tooltip("這一場遊戲你想隨機啟動幾個干擾區域？")]
    public int maxActiveZones = 3;

    private List<IncidentZone> allZones = new List<IncidentZone>();

    void Start()
    {
        // 1. 自動抓取場景中所有掛有 IncidentZone 腳本的物件
        IncidentZone[] zones = FindObjectsOfType<IncidentZone>();
        allZones.AddRange(zones);

        // 2. 先把所有區域預設關閉
        foreach (var zone in allZones)
        {
            zone.SetActivate(false);
        }

        // 防呆：如果設定的啟動數量大於場景總數，就全開
        if (maxActiveZones > allZones.Count)
        {
            maxActiveZones = allZones.Count;
        }

        // 3. 隨機挑選指定數量的區域並開啟
        List<IncidentZone> tempPool = new List<IncidentZone>(allZones);
        int activatedCount = 0;

        while (activatedCount < maxActiveZones && tempPool.Count > 0)
        {
            int randomIndex = Random.Range(0, tempPool.Count);
            IncidentZone selectedZone = tempPool[randomIndex];

            // 啟動它！
            selectedZone.SetActivate(true);

            // 從臨時池移除，防止重複抽取
            tempPool.RemoveAt(randomIndex);
            activatedCount++;
        }

        Debug.Log($"【事件總管】場景內共有 {allZones.Count} 個干擾區，已隨機抽籤啟用其中的 {activatedCount} 個！");
    }
}
