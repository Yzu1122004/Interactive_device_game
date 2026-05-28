using UnityEngine;
using System.Collections.Generic;

public enum LightState { Green, GoToRedYellow, Red, GoToGreenYellow }

public class TrafficLightSystem : MonoBehaviour
{
    [System.Serializable]
    public class PedestrianLightGroup
    {
        public List<MeshRenderer> redManRenderers;
        public List<MeshRenderer> greenManRenderers;
    }

    [Header("--- 車道用燈組 ---")]
    public List<MeshRenderer> redLightRenderers;
    public List<MeshRenderer> yellowLightRenderers;
    public List<MeshRenderer> greenLightRenderers;

    [Header("--- 行人專用燈組 ---")]
    public bool hasPedestrianLights = true;
    public PedestrianLightGroup pedestrianLights;

    [Header("--- 公用材質球設定 ---")]
    public Material lightOffMaterial;
    public Material redOnMaterial;
    public Material yellowOnMaterial;
    public Material greenOnMaterial;

    [Header("--- 紅綠燈秒數設定 ---")]
    public float greenDuration = 5f;
    public float yellowGoToRedDuration = 2f;
    public float redDuration = 5f;
    public float yellowGoToGreenDuration = 1.5f;

    [Header("--- 當前/起始狀態設定 ---")]
    [Tooltip("你可以在這裡直接設定這根紅綠燈模擬開始時是什麼燈號")]
    public LightState currentStatus = LightState.Green;

    private float timer = 0f;

    void Start()
    {
        // 【核心修改一】不再強制指定為 Green，而是保留你在 Inspector 設好的狀態
        // 根據你選的初始燈號，立刻更新一次材質球亮燈狀態
        UpdateLightsMesh();
    }

    void Update()
    {
        timer += Time.deltaTime;

        // 【核心修改二】大改切換燈號邏輯，讓它無論從哪個燈號起始，都能完美對接它的下一階段秒數
        switch (currentStatus)
        {
            case LightState.Green:
                if (timer >= greenDuration)
                {
                    currentStatus = LightState.GoToRedYellow; // 綠燈完變黃燈
                    timer = 0f;
                    UpdateLightsMesh();
                }
                break;

            case LightState.GoToRedYellow:
                if (timer >= yellowGoToRedDuration)
                {
                    currentStatus = LightState.Red; // 黃燈完變紅燈
                    timer = 0f;
                    UpdateLightsMesh();
                }
                break;

            case LightState.Red:
                if (timer >= redDuration)
                {
                    currentStatus = LightState.GoToGreenYellow; // 紅燈完變準備轉綠的黃燈
                    timer = 0f;
                    UpdateLightsMesh();
                }
                break;

            case LightState.GoToGreenYellow:
                if (timer >= yellowGoToGreenDuration)
                {
                    currentStatus = LightState.Green; // 黃燈完回綠燈
                    timer = 0f;
                    UpdateLightsMesh();
                }
                break;
        }
    }

    // --- 提供給路人和汽車 AI 獲取當前倒數時間的接口 ---
    public float GetTimerValue()
    {
        return timer;
    }

    // 封裝原本的亮燈材質切換邏輯，方便在 Start 和狀態改變時呼叫
    private void UpdateLightsMesh()
    {
        // 先把所有燈熄滅
        ResetRenderers(redLightRenderers);
        ResetRenderers(yellowLightRenderers);
        ResetRenderers(greenLightRenderers);

        if (hasPedestrianLights)
        {
            ResetRenderers(pedestrianLights.redManRenderers);
            ResetRenderers(pedestrianLights.greenManRenderers);
        }

        // 根據目前的狀態點亮對應的燈
        switch (currentStatus)
        {
            case LightState.Green:
                SetRenderersMaterial(greenLightRenderers, greenOnMaterial);
                if (hasPedestrianLights)
                    SetRenderersMaterial(pedestrianLights.redManRenderers, redOnMaterial);
                break;

            case LightState.GoToRedYellow:
                SetRenderersMaterial(yellowLightRenderers, yellowOnMaterial);
                if (hasPedestrianLights)
                    SetRenderersMaterial(pedestrianLights.redManRenderers, redOnMaterial);
                break;

            case LightState.Red:
                SetRenderersMaterial(redLightRenderers, redOnMaterial);
                if (hasPedestrianLights)
                    SetRenderersMaterial(pedestrianLights.greenManRenderers, greenOnMaterial);
                break;

            case LightState.GoToGreenYellow:
                SetRenderersMaterial(yellowLightRenderers, yellowOnMaterial);
                if (hasPedestrianLights)
                    SetRenderersMaterial(pedestrianLights.redManRenderers, redOnMaterial);
                break;
        }
    }

    private void ResetRenderers(List<MeshRenderer> renderers)
    {
        if (renderers == null) return;
        foreach (var renderer in renderers)
        {
            if (renderer != null) renderer.material = lightOffMaterial;
        }
    }

    private void SetRenderersMaterial(List<MeshRenderer> renderers, Material mat)
    {
        if (renderers == null) return;
        foreach (var renderer in renderers)
        {
            if (renderer != null) renderer.material = mat;
        }
    }
}