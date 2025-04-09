using UnityEngine;
using System.Collections;

public class FlightMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float rotationSpeed = 1f;
    [SerializeField] private float minAltitude; // This should be set to the original Z position
    [SerializeField] private float maxAltitude = 30f;
    [SerializeField] private float changeDirectionTime = 5f;
    [SerializeField] private float flyAreaRadius = 100f;
    
    [Header("Animation")]
    [SerializeField] private string propellerAnimationName = "Animation"; // Default animation name in the GLB
    
    // Private variables
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Animator animator;
    private float nextDirectionChange;
    
    void Start()
    {
        // Store the initial position
        startPosition = transform.position;
        
        // Set minimum altitude to the initial Z position if not specified
        if (minAltitude == 0)
        {
            minAltitude = startPosition.z;
        }
        
        // Find the Animator component on this object or its children
        animator = GetComponentInChildren<Animator>();
        
        // Play the propeller animation if it exists
        if (animator != null)
        {
            // Check if the animation exists
            if (HasAnimation(propellerAnimationName))
            {
                animator.Play(propellerAnimationName);
            }
            else
            {
                Debug.LogWarning($"Animation '{propellerAnimationName}' not found in the model.");
                
                // List available animations for debugging
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                if (clips.Length > 0)
                {
                    Debug.Log("Available animations:");
                    foreach (AnimationClip clip in clips)
                    {
                        Debug.Log("- " + clip.name);
                    }
                    
                    // Try to play the first animation if it exists
                    animator.Play(clips[0].name);
                }
            }
        }
        
        // Set initial target
        PickNewDestination();
    }
    
    void Update()
    {
        // Check if it's time to change direction
        if (Time.time > nextDirectionChange)
        {
            PickNewDestination();
        }
        
        // Move toward the target
        MoveAircraft();
    }
    
    private void PickNewDestination()
    {
        // Pick a random point within the fly area
        Vector3 randomOffset = Random.insideUnitSphere * flyAreaRadius;
        
        // Ensure minimum altitude (Z position)
        randomOffset.z = Mathf.Abs(randomOffset.z);
        float newZ = startPosition.z + randomOffset.z;
        newZ = Mathf.Clamp(newZ, minAltitude, maxAltitude);
        randomOffset.z = newZ - startPosition.z;
        
        // Set the new target position
        targetPosition = startPosition + randomOffset;
        
        // Calculate the direction to the target
        Vector3 direction = (targetPosition - transform.position).normalized;
        
        // Create a rotation that looks in the direction of movement
        targetRotation = Quaternion.LookRotation(direction);
        
        // Add some banking/tilting for more natural flight
        float bankAngle = Random.Range(-30f, 30f);
        targetRotation *= Quaternion.Euler(0, 0, bankAngle);
        
        // Set the next direction change time
        nextDirectionChange = Time.time + changeDirectionTime;
    }
    
    private void MoveAircraft()
    {
        // Move toward the target position
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            speed * Time.deltaTime
        );
        
        // Smoothly rotate toward the target rotation
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
        
        // Ensure we never go below minimum altitude
        if (transform.position.z < minAltitude)
        {
            Vector3 pos = transform.position;
            pos.z = minAltitude;
            transform.position = pos;
        }
        
        // If we're close to the target, pick a new destination
        if (Vector3.Distance(transform.position, targetPosition) < 5f)
        {
            PickNewDestination();
        }
    }
    
    // Helper method to check if an animation exists
    private bool HasAnimation(string animationName)
    {
        if (animator == null) return false;
        
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        foreach (AnimationClip clip in clips)
        {
            if (clip.name == animationName)
            {
                return true;
            }
        }
        
        return false;
    }
    
    // Helper method to visualize the flight area in the editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Application.isPlaying ? startPosition : transform.position, flyAreaRadius);
        
        // Show minimum altitude plane
        Gizmos.color = Color.red;
        Vector3 center = Application.isPlaying ? startPosition : transform.position;
        center.z = minAltitude;
        Gizmos.DrawWireCube(center, new Vector3(flyAreaRadius * 2, flyAreaRadius * 2, 0.1f));
    }
}
