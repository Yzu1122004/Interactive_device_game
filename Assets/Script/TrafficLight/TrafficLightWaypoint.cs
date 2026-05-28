using UnityEngine;

public class TrafficLightWaypoint : MonoBehaviour
{
    [Header("這個路點要偵測的紅綠燈系統")]
    public TrafficLightSystem targetTrafficLight;

    [Header("斑馬線的對岸路點 (用來計算斑馬線長度)")]
    public Transform oppositeWaypoint;

    // 提供給路人 AI 檢查：現在行人能不能安全通過？
    public bool CanPedestrianPassSafety(float pedestrianSpeed)
    {
        if (targetTrafficLight == null) return true; // 防呆

        // 只有車道是 Red 時，行人專用燈才是綠燈
        if (targetTrafficLight.currentStatus == LightState.Red)
        {
            // 透過紅綠燈系統算出「行人綠燈還剩下幾秒」
            float currentElapsed = targetTrafficLight.GetTimerValue();
            float timeLeft = targetTrafficLight.redDuration - currentElapsed;

            // 【新增優化】如果行人紅綠燈才剛變綠燈（例如剛過了不到 1.5 秒），不管時間夠不夠，絕對放行！
            if (currentElapsed <= 1.5f)
            {
                return true;
            }

            if (oppositeWaypoint == null) return true;

            // 計算行人走完這段斑馬線需要花幾秒 = 距離 / 速度
            float distance = Vector3.Distance(transform.position, oppositeWaypoint.position);
            float timeNeeded = distance / pedestrianSpeed;

            // 如果綠燈剩餘時間還夠，准許通行；如果不夠，才留下來等下一輪
            if (timeLeft >= timeNeeded)
            {
                return true;
            }
            else
            {
                Debug.Log($"{gameObject.name}: 綠燈剩餘 {timeLeft:F1}秒，走過去要 {timeNeeded:F1}秒，時間不夠不放行。");
                return false;
            }
        }

        return false; // 車道是綠/黃燈，行人不能走
    }
}