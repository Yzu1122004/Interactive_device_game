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
    [Header("--- 當前狀態 (公開供 AI 讀取) ---")]
    public LightState currentStatus = LightState.Green;

    private float timer = 0f;

    void Start()
    {
        SetLight(LightState.Green);
    }

    void Update()
    {
        timer += Time.deltaTime;

        switch (currentStatus)
        {
            case LightState.Green:
                if (timer >= greenDuration) SetLight(LightState.GoToRedYellow);
                break;

            case LightState.GoToRedYellow:
                if (timer >= yellowGoToRedDuration) SetLight(LightState.Red);
                break;

            case LightState.Red:
                if (timer >= redDuration) SetLight(LightState.GoToGreenYellow);
                break;

            case LightState.GoToGreenYellow:
                if (timer >= yellowGoToGreenDuration) SetLight(LightState.Green); 
                break;
        }
    }

    void SetLight(LightState newState)
    {
        currentStatus = newState;
        timer = 0f;

        ResetRenderers(redLightRenderers);
        ResetRenderers(yellowLightRenderers);
        ResetRenderers(greenLightRenderers);

        if (hasPedestrianLights)
        {
            ResetRenderers(pedestrianLights.redManRenderers);
            ResetRenderers(pedestrianLights.greenManRenderers);
        }

        switch (newState)
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