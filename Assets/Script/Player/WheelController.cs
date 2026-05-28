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
    public float maxTorque = 0f;
    public float turnTorque = 0f;
    public float brakeForce = 0f;
    public float maxSpeed = 0f;

    [Header("按鍵設定")]
    public KeyCode forwardKey = KeyCode.W;
    public KeyCode backwardKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode brakeKey = KeyCode.Space;

    [Header("特殊區域設定")]
    public float slopMaxSpeed = 10f;
    private float originalMaxSpeed;
    public ArduinoBasic arduino;

    [Header("輪椅移動音效設定")]
    [Tooltip("拖入輪椅移動持續音效的 Audio Source")]
    public AudioSource movementAudioSource;
    [Tooltip("音效淡入淡出的平滑速度")]
    public float soundFadeSpeed = 5f;

    private float maxMovementVolume = 1.0f;
    private float currentV = 0f;
    private float currentH = 0f;
    private Rigidbody rb;

    void Start()
    {
        originalMaxSpeed = maxSpeed;
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.2f, 0);

        // 【音效初始化防呆】
        if (movementAudioSource == null) movementAudioSource = GetComponent<AudioSource>();
        if (movementAudioSource != null)
        {
            maxMovementVolume = movementAudioSource.volume;
            if (!movementAudioSource.isPlaying) movementAudioSource.Play();
        }
    }

    void Update()
    {
        HandleInput();

        UpdateWheelPosition(wheel_left_col, wheel_left);
        UpdateWheelPosition(wheel_right_col, wheel_right);

        // 【核心控制】依據輪椅實際移動速度控制音效
        HandleMovementSound();
    }

    // 動態控制輪椅移動音效
    private void HandleMovementSound()
    {
        if (movementAudioSource == null || rb == null) return;

        // 使用 Rigidbody 的實體移動速度判定
        float currentSpeed = rb.velocity.magnitude;

        if (currentSpeed < 1f)
        {
            // 停下時，音量平滑歸零
            movementAudioSource.volume = Mathf.MoveTowards(movementAudioSource.volume, 0f, soundFadeSpeed * Time.deltaTime);
            if (movementAudioSource.volume <= 0f && movementAudioSource.isPlaying)
            {
                movementAudioSource.Pause();
            }
        }
        else
        {
            // 移動時，恢復播放與音量
            if (!movementAudioSource.isPlaying) movementAudioSource.UnPause();
            movementAudioSource.volume = Mathf.MoveTowards(movementAudioSource.volume, maxMovementVolume, soundFadeSpeed * Time.deltaTime);
        }
    }

    void FixedUpdate()
    {
        ApplyMovement();
    }

    void HandleInput()
    {
        currentV = 0;
        if (Input.GetKey(forwardKey)) currentV = 1;
        if (Input.GetKey(backwardKey)) currentV = -1;

        currentH = 0;
        if (Input.GetKey(leftKey)) currentH = -1;
        if (Input.GetKey(rightKey)) currentH = 1;
    }

    void ApplyMovement()
    {
        float currentSpeed = rb.velocity.magnitude;

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

        if (currentSpeed > maxSpeed)
        {
            wheel_left_col.motorTorque = 0;
            wheel_right_col.motorTorque = 0;
        }
        else
        {
            wheel_left_col.motorTorque = move + turn;
            wheel_right_col.motorTorque = move - turn;
        }

        if (Mathf.Abs(wheel_left_col.rpm) > 800) wheel_left_col.motorTorque = 0;
        if (Mathf.Abs(wheel_right_col.rpm) > 800) wheel_right_col.motorTorque = 0;
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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("slope"))
        {
            maxSpeed = slopMaxSpeed;
            if (arduino != null)
            {
                arduino.ArduinoWrite("B_ON");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("slope"))
        {
            maxSpeed = originalMaxSpeed;
            if (arduino != null)
            {
                arduino.ArduinoWrite("B_OFF");
            }
        }
    }
}