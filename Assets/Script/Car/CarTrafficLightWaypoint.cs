using UnityEngine;

public class CarTrafficLightWaypoint : MonoBehaviour
{
    [Header("這個車道要看哪一根紅綠燈")]
    public TrafficLightSystem targetTrafficLight;

    // 提供給汽車 AI 檢查：現在車道能不能通行？
    public bool CanCarPass()
    {
        if (targetTrafficLight == null) return true; //如果沒綁定紅綠燈，預設可以直接通過

        
        if (targetTrafficLight.currentStatus == LightState.Green) //如果是黃燈(GoToRedYellow)、紅燈(Red)、紅轉綠黃燈(GoToGreenYellow)，車子都應該停下
        {
            return true;
        }

        return false;
    }
}