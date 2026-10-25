using System;

// Inputs are bounds projected into the wheelchair's original heading frame.
// No Unity physics dependency: the clearance arithmetic can be tested directly.
public static class PrototypeBypassDimensions
{
    public static void Calculate(float chairLeft, float chairRight, float chairRear,
        float obstacleLeft, float obstacleRight, float obstacleFar, int side, float clearance,
        out float offset, out float passDistance)
    {
        offset = Math.Max(0.05f, side > 0 ? obstacleRight - chairLeft + clearance :
            chairRight - obstacleLeft + clearance);
        passDistance = Math.Max(0f, obstacleFar - chairRear + clearance);
    }
}
