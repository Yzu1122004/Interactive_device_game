using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelController : MonoBehaviour
{
    [Header("輪子模型 (Visuals)")]
    public Transform wheel_left;
    public Transform wheel_right;

    [Header("輪子碰撞器 (Physics)")]
    public WheelCollider wheel_left_col;
    public WheelCollider wheel_right_col;

    [Header("動力參數")]
    public float maxTorque = 150f;   
    public float turnTorque = 100f;  
    public float brakeForce = 500f;
    public float maxSpeed = 30f;

    [Header("按鍵設定")]
    public KeyCode forwardKey = KeyCode.W;
    public KeyCode backwardKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode brakeKey = KeyCode.Space;

    private float currentV = 0f;
    private float currentH = 0f;

    void Update()
    {
        
        HandleInput();

        
        UpdateWheelPosition(wheel_left_col, wheel_left);
        UpdateWheelPosition(wheel_right_col, wheel_right);
    }

    void FixedUpdate()
    {
        ApplyMovement();
    }

    void HandleInput()
    {
        // 垂直輸入 (前後)
        currentV = 0;
        if (Input.GetKey(forwardKey)) currentV = 1;
        if (Input.GetKey(backwardKey)) currentV = -1;

        // 水平輸入 (左右)
        currentH = 0;
        if (Input.GetKey(leftKey)) currentH = -1;
        if (Input.GetKey(rightKey)) currentH = 1;
    }

    void ApplyMovement()
    {
        if (Input.GetKey(brakeKey))
        {
            ApplyBrake(brakeForce);
            return;
        }
        else
        {
            ApplyBrake(0);
        }
        
        float move = currentV * maxTorque;
        float turn = currentH * turnTorque;

        float leftMotor = move + turn;
        float rightMotor = move - turn;

        wheel_left_col.motorTorque = leftMotor;
        wheel_right_col.motorTorque = rightMotor;

        if (Mathf.Abs(wheel_left_col.rpm) > 500) wheel_left_col.motorTorque = 0;
        if (Mathf.Abs(wheel_right_col.rpm) > 500) wheel_right_col.motorTorque = 0;
    }

    void ApplyBrake(float force)
    {
        wheel_left_col.brakeTorque = force;
        wheel_right_col.brakeTorque = force;

        if (force > 0)
        {
            wheel_left_col.motorTorque = 0;
            wheel_right_col.motorTorque = 0;
        }
    }

    void UpdateWheelPosition(WheelCollider col, Transform trans)
    {
        Vector3 pos;
        Quaternion rot;
        col.GetWorldPose(out pos, out rot);
        trans.position = pos;
        trans.rotation = rot;
    }
}