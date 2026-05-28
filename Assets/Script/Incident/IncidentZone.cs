using UnityEngine;

[RequireComponent(typeof(BoxCollider))] // Guarantees that a Collider always exists to prevent NullReference errors
public class IncidentZone : MonoBehaviour
{
    public enum IncidentType
    {
        PedestrianBlocker, // 1. Pedestrian blocking the path
        BrakingCarHorn,    // 2. Stalled car honking its horn
        ExhaustCar,        // 3. Car emitting exhaust gas
        FallingFlowerPot   // 4. Falling flower pot group
    }

    [Header("--- GENERAL CONFIGURATION ---")]
    [Tooltip("The specific incident type assigned to this trigger zone.")]
    public IncidentType assignedIncident;

    [Header("1. PEDESTRIAN BLOCKER CONFIG")]
    public GameObject blockerPedestrianPrefab;
    public Transform pedestrianSpawnPoint;

    [Header("2. BRAKING CAR HORN CONFIG")]
    public GameObject hornCarPrefab;
    public Transform carSpawnPoint;

    [Header("3. EXHAUST CAR CONFIG")]
    public GameObject exhaustCarPrefab;
    public Transform exhaustCarSpawnPoint;

    [Header("4. FALLING FLOWER POT CONFIG")]
    [Tooltip("[Mode A] If the pot group already exists in the scene hierarchy, drag its root Rigidbody here.")]
    public Rigidbody targetFlowerPotGroup;

    [Tooltip("[Mode B] If you want to dynamically spawn a preset group when triggered, drag the Prefab here.")]
    public GameObject flowerPotGroupPrefab;
    [Tooltip("Spawn reference transform for Mode B (Leaves blank to default to trigger center).")]
    public Transform flowerPotSpawnPoint;

    [Tooltip("The instantaneous impact force applied to tip the pots over.")]
    public float pushForce = 8f;

    private bool isActivatedByManager = false;
    private bool hasTriggered = false;
    private Collider zoneCollider;

    private void Awake()
    {
        // Cache the collider reference safely on startup
        zoneCollider = GetComponent<Collider>();
        // Ensure the collider is configured as a trigger volume
        zoneCollider.isTrigger = true; 
    }

    public void SetActivate(bool state)
    {
        isActivatedByManager = state;
        hasTriggered = false;
        if (zoneCollider != null)
        {
            zoneCollider.enabled = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check activation state and filter explicitly by Player tag
        if (isActivatedByManager && !hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true;
            // Disable collider immediately after trigger to prevent double-execution artifacts
            zoneCollider.enabled = false; 
            ExecuteIncident();
        }
    }

    private void ExecuteIncident()
    {
        Debug.Log($"<color=cyan>[Incident System]</color> Player entered zone. Triggering: {assignedIncident}");

        switch (assignedIncident)
        {
            case IncidentType.PedestrianBlocker:
                TriggerPedestrianBlocker();
                break;
            case IncidentType.BrakingCarHorn:
                TriggerBrakingCarHorn();
                break;
            case IncidentType.ExhaustCar:
                TriggerExhaustCar();
                break;
            case IncidentType.FallingFlowerPot:
                TriggerFallingFlowerPot();
                break;
        }
    }

    private void TriggerPedestrianBlocker()
    {
        if (blockerPedestrianPrefab != null && pedestrianSpawnPoint != null)
        {
            Instantiate(blockerPedestrianPrefab, pedestrianSpawnPoint.position, pedestrianSpawnPoint.rotation);
        }
    }

    private void TriggerBrakingCarHorn()
    {
        if (hornCarPrefab != null && carSpawnPoint != null)
        {
            Instantiate(hornCarPrefab, carSpawnPoint.position, carSpawnPoint.rotation);
        }
    }

    private void TriggerExhaustCar()
    {
        if (exhaustCarPrefab != null && exhaustCarSpawnPoint != null)
        {
            Instantiate(exhaustCarPrefab, exhaustCarSpawnPoint.position, exhaustCarSpawnPoint.rotation);
        }
    }

    private void TriggerFallingFlowerPot()
    {
        GameObject activePotGroupInstance = null;

        // Mode B: Dynamic Spawning instantiation
        if (flowerPotGroupPrefab != null)
        {
            Vector3 spawnPos = flowerPotSpawnPoint != null ? flowerPotSpawnPoint.position : transform.position;
            Quaternion spawnRot = flowerPotSpawnPoint != null ? flowerPotSpawnPoint.rotation : transform.rotation;
            activePotGroupInstance = Instantiate(flowerPotGroupPrefab, spawnPos, spawnRot);
        }
        // Mode A: Fallback to pre-existing scene object reference
        else if (targetFlowerPotGroup != null)
        {
            activePotGroupInstance = targetFlowerPotGroup.gameObject;
        }

        if (activePotGroupInstance != null)
        {
            // ADVANCED PHYSICS FIX: Query all child rigidbodies inside the group setup
            Rigidbody[] childPots = activePotGroupInstance.GetComponentsInChildren<Rigidbody>();

            if (childPots.Length == 0)
            {
                Debug.LogError(" [Physics Error] No Rigidbody found on the Flower Pot Group or its children!");
                return;
            }

            // Calculate vectorized direction downward and outward diagonally
            Vector3 pushDirection = (transform.right + Vector3.down * 0.3f).normalized;

            foreach (Rigidbody rb in childPots)
            {
                // Wake up rigidbodies from static placement parameters
                rb.isKinematic = false;
                
                // If it's a child element, un-parent it so they roll and spill dynamically on impact!
                if (rb.gameObject != activePotGroupInstance)
                {
                    rb.transform.SetParent(null); 
                }

                // Apply direct physical shock forces
                rb.AddForce(pushDirection * pushForce, ForceMode.Impulse);
                rb.AddTorque(transform.forward * pushForce * 0.5f, ForceMode.Impulse);
            }
        }
        else
        {
            Debug.LogWarning(" [Configuration Alert] No scene reference target or prefab bound to Flower Pot setup!");
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isActivatedByManager ? Color.green : Color.red;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.DrawWireCube(box.center, box.size);
        }
        Gizmos.matrix = oldMatrix;
    }
}