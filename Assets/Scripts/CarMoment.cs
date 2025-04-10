using Photon.Pun;
using UnityEngine;
using System.Collections;

public class CarMoment : MonoBehaviourPun, IPunObservable
{
    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float deceleration = 8f;
    [SerializeField] private float steeringSpeed = 100f;
    [SerializeField] private float turnRadius = 5f;
    
    [Header("AI Settings")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waypointReachDistance = 3f;
    [SerializeField] private bool loopWaypoints = true;
    [SerializeField] private float lookAheadDistance = 10f;
    
    [Header("Realism Settings")]
    [SerializeField] private float tiltAngle = 5f;
    [SerializeField] private float tiltSpeed = 10f;
    [SerializeField] private AnimationCurve speedTurnCurve = AnimationCurve.EaseInOut(0, 0.5f, 1, 1);
    [SerializeField] private float wheelRotationSpeed = 200f;
    
    [Header("Wheel Transforms")]
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;
    [SerializeField] private Transform rearLeftWheel;
    [SerializeField] private Transform rearRightWheel;
    [SerializeField] private float maxWheelTurnAngle = 30f;
    
    // Debug Options
    [Header("Debug")]
    [SerializeField] private bool showDebugLines = true;
    [SerializeField] private bool moveWithoutWaypoints = false;
    
    // Private variables
    private float currentSpeed = 0f;
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private float smoothing = 10f;
    private int currentWaypointIndex = 0;
    private bool isInitialized = false;
    private Vector3 currentLookAheadPoint;
    private Vector3 lastPosition;
    private float currentTilt = 0f;
    private float currentWheelAngle = 0f;
    
    void Start()
    {
        // Initialize network variables
        networkPosition = transform.position;
        networkRotation = transform.rotation;
        lastPosition = transform.position;
        
        // Add initial speed so car doesn't start from standstill
        currentSpeed = maxSpeed * 0.3f;
        
        // If no waypoints set but debug mode enabled, create a temporary path
        if ((waypoints == null || waypoints.Length == 0) && moveWithoutWaypoints)
        {
            CreateTemporaryPath();
        }
        
        isInitialized = true;
        
        // Only control AI if we're the Master Client
        if (!PhotonNetwork.IsMasterClient && photonView.IsMine)
        {
            // We're not the master client - transfer ownership to master client
            photonView.TransferOwnership(PhotonNetwork.MasterClient);
        }
    }
    
    private void CreateTemporaryPath()
    {
        // Create a simple circular path around the starting position
        waypoints = new Transform[8];
        
        for (int i = 0; i < 8; i++)
        {
            GameObject waypointObj = new GameObject("TempWaypoint_" + i);
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 pos = transform.position + new Vector3(Mathf.Sin(angle) * 50f, 0, Mathf.Cos(angle) * 50f);
            waypointObj.transform.position = pos;
            waypoints[i] = waypointObj.transform;
        }
        
        Debug.Log("Created temporary waypoints for car movement");
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        // Only the Master Client controls AI NPCs regardless of ownership
        if (PhotonNetwork.IsMasterClient)
        {
            HandleAIMovement();
            UpdateWheels();
        }
        else if (!photonView.IsMine)
        {
            // Clients receive networked position
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * smoothing);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * smoothing);
            
            // Update wheels for clients too
            UpdateWheels();
        }
    }
    
    private void UpdateWheels()
    {
        // Skip if no wheels assigned
        if (frontLeftWheel == null || frontRightWheel == null || 
            rearLeftWheel == null || rearRightWheel == null)
            return;
            
        // Calculate wheel rotation speed based on car speed
        float wheelRotation = currentSpeed * wheelRotationSpeed * Time.deltaTime;
        
        // Rotate all wheels forward
        frontLeftWheel.Rotate(wheelRotation, 0, 0, Space.Self);
        frontRightWheel.Rotate(wheelRotation, 0, 0, Space.Self);
        rearLeftWheel.Rotate(wheelRotation, 0, 0, Space.Self);
        rearRightWheel.Rotate(wheelRotation, 0, 0, Space.Self);
        
        // Set steering angle for front wheels
        Vector3 frontLeftRot = frontLeftWheel.localEulerAngles;
        Vector3 frontRightRot = frontRightWheel.localEulerAngles;
        
        frontLeftRot.y = currentWheelAngle;
        frontRightRot.y = currentWheelAngle;
        
        frontLeftWheel.localEulerAngles = frontLeftRot;
        frontRightWheel.localEulerAngles = frontRightRot;
    }
    
    private void HandleAIMovement()
    {
        // If no waypoints, the car can't move except in debug mode
        if ((waypoints == null || waypoints.Length == 0) && !moveWithoutWaypoints)
        {
            Debug.LogWarning("No waypoints assigned to car. Please assign waypoints in inspector.");
            return;
        }
        
        // Simple movement in a straight line if debugging without waypoints
        if (moveWithoutWaypoints && (waypoints == null || waypoints.Length == 0))
        {
            currentSpeed = maxSpeed;
            transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);
            return;
        }
        
        // Calculate velocity for tilt
        Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
            
        // Get current target waypoint
        Vector3 targetPosition = waypoints[currentWaypointIndex].position;
        targetPosition.y = transform.position.y; // Keep on same vertical level
        
        // Check if we've reached the waypoint
        Vector3 directionToWaypoint = targetPosition - transform.position;
        if (directionToWaypoint.magnitude < waypointReachDistance)
        {
            // Move to next waypoint
            currentWaypointIndex++;
            
            // Loop back to first waypoint if needed
            if (currentWaypointIndex >= waypoints.Length)
            {
                if (loopWaypoints)
                    currentWaypointIndex = 0;
                else
                    currentWaypointIndex = waypoints.Length - 1;
            }
        }
        
        // Calculate look-ahead point for smoother turns
        int nextWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        if (nextWaypointIndex < waypoints.Length && loopWaypoints)
        {
            // Get positions of current and next waypoints
            Vector3 currentWP = waypoints[currentWaypointIndex].position;
            Vector3 nextWP = waypoints[nextWaypointIndex].position;
            
            // Calculate direction vector and distance
            Vector3 wpDirection = nextWP - currentWP;
            float distanceToNextWP = Vector3.Distance(currentWP, nextWP);
            
            // Calculate a look-ahead point that's ahead of the current waypoint
            float lookAheadRatio = Mathf.Min(lookAheadDistance / distanceToNextWP, 0.8f);
            currentLookAheadPoint = currentWP + wpDirection * lookAheadRatio;
            currentLookAheadPoint.y = transform.position.y;
            
            // Show debug visualization
            if (showDebugLines)
            {
                Debug.DrawLine(transform.position, targetPosition, Color.green);
                Debug.DrawLine(transform.position, currentLookAheadPoint, Color.yellow);
                Debug.DrawSphere(currentLookAheadPoint, 1f, Color.yellow);
            }
        }
        else
        {
            // If there's no next waypoint, use the current one
            currentLookAheadPoint = targetPosition;
        }
        
        // Calculate ideal target direction based on look-ahead point
        Vector3 targetDirection = (currentLookAheadPoint - transform.position).normalized;
        
        // Accelerate/decelerate based on turn sharpness
        float turnAngle = Vector3.Angle(transform.forward, targetDirection);
        float speedFactor = speedTurnCurve.Evaluate(1.0f - Mathf.Clamp01(turnAngle / 90f));
        float targetSpeed = maxSpeed * speedFactor;
        
        // Apply acceleration/deceleration
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, 
            (currentSpeed < targetSpeed ? acceleration : deceleration) * Time.deltaTime);
        
        // Calculate turn rate - slower turns at lower speeds
        float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeed);
        float effectiveTurnRate = steeringSpeed * speedRatio;
        
        // Calculate target rotation with banking
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        
        // Calculate banking angle based on turning rate
        Vector3 crossProduct = Vector3.Cross(transform.forward, targetDirection);
        float bankDirection = Mathf.Sign(crossProduct.y);
        float turnSharpness = Mathf.Clamp01(turnAngle / 45f);
        float targetTilt = -bankDirection * tiltAngle * turnSharpness;
        
        // Smoothly apply banking
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSpeed);
        
        // Calculate steering wheel angle
        float wheelTurnFactor = turnAngle * bankDirection * 0.5f;
        currentWheelAngle = Mathf.Lerp(currentWheelAngle, 
            Mathf.Clamp(wheelTurnFactor, -maxWheelTurnAngle, maxWheelTurnAngle), 
            Time.deltaTime * 5f);
        
        // Apply rotation with banking
        Quaternion bankRotation = Quaternion.Euler(0, 0, currentTilt);
        transform.rotation = Quaternion.Slerp(transform.rotation, 
            targetRotation * bankRotation, effectiveTurnRate * Time.deltaTime);
        
        // Move forward
        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime, Space.Self);
    }
    
    // This method is called by Photon to sync data across the network
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this car: send the others our data
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(currentSpeed);
            stream.SendNext(currentWaypointIndex);
            stream.SendNext(currentTilt);
            stream.SendNext(currentWheelAngle);
        }
        else
        {
            // Network car, receive data
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            currentSpeed = (float)stream.ReceiveNext();
            currentWaypointIndex = (int)stream.ReceiveNext();
            currentTilt = (float)stream.ReceiveNext();
            currentWheelAngle = (float)stream.ReceiveNext();
        }
    }
    
    private void OnDrawGizmos()
    {
        // Draw waypoints in editor for easier setup
        if (waypoints != null && waypoints.Length > 0)
        {
            Gizmos.color = Color.blue;
            
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                {
                    // Draw waypoint
                    Gizmos.DrawSphere(waypoints[i].position, 1f);
                    
                    // Draw line to next waypoint
                    if (i < waypoints.Length - 1 && waypoints[i+1] != null)
                    {
                        Gizmos.DrawLine(waypoints[i].position, waypoints[i+1].position);
                    }
                    else if (loopWaypoints && waypoints[0] != null)
                    {
                        Gizmos.DrawLine(waypoints[i].position, waypoints[0].position);
                    }
                }
            }
            
            // Highlight current waypoint
            if (Application.isPlaying && currentWaypointIndex < waypoints.Length && waypoints[currentWaypointIndex] != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(waypoints[currentWaypointIndex].position, 1.5f);
            }
        }
    }
}