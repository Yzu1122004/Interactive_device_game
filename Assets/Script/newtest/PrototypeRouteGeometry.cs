using UnityEngine;

public static class PrototypeRouteGeometry
{
    public static Vector3 Flat(Vector3 value) { value.y = 0f; return value; }

    public static Vector3 Project(Vector3 position, Vector3 a, Vector3 b)
    {
        Vector3 road = Flat(b - a);
        float t = road.sqrMagnitude < 0.0001f ? 0f :
            Mathf.Clamp01(Vector3.Dot(Flat(position - a), road) / road.sqrMagnitude);
        return Vector3.Lerp(a, b, t);
    }

    public static bool MatchesTurn(float angle, PrototypeVoiceCommand command)
    {
        if (command == PrototypeVoiceCommand.Left) return angle < -35f && angle > -145f;
        if (command == PrototypeVoiceCommand.Right) return angle > 35f && angle < 145f;
        // Continue through ordinary bends between placed nodes. A right-angle
        // junction still requires an explicit turn; returning needs UTurn.
        return Mathf.Abs(angle) <= 60f;
    }
}
