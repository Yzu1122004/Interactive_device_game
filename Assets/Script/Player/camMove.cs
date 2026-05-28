using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class camMove : MonoBehaviour
{
    public Transform Target;
    public Vector3 Distance;
    public float TransformSpeed,rotationSpeed;

    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        Translation();
        Rotation();
    }

    public void Translation()
    {
        Vector3 tragetPosision = Target.TransformPoint(Distance);
        transform.position = Vector3.Lerp(transform.position,tragetPosision, TransformSpeed*Time.deltaTime);
    }
    public void Rotation()
    {
        Vector3 direciotn = Target.position-transform.position;
        Quaternion rotation = Quaternion.LookRotation(direciotn,Vector3.up);
        transform.rotation = Quaternion.Lerp(transform.rotation,rotation,rotationSpeed*Time.deltaTime);
    }
}
