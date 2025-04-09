using Photon.Pun;
using UnityEngine;

public class TrainMovement : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float waitTime = 5f;
    
    [Header("Position Limits")]
    [SerializeField] private float startPositionX = 155f;
    [SerializeField] private float endPositionX = -124.51f;
    
    // Private variables
    private Vector3 startPosition;
    private Vector3 endPosition;
    private Vector3 targetPosition;
    private float timer = 0f;
    private bool isWaiting = false;
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private float smoothing = 10f;
    
    void Start()
    {
        // Store the original y and z coordinates
        startPosition = transform.position;
        startPosition.x = startPositionX;
        
        endPosition = transform.position;
        endPosition.x = endPositionX;
        
        // Set initial target position
        targetPosition = endPosition;
        
        // Initialize network variables
        networkPosition = transform.position;
        networkRotation = transform.rotation;
    }
    
    void Update()
    {
        // Only the Master Client controls the train movement logic
        if (PhotonNetwork.IsMasterClient)
        {
            MoveTrain();
        }
        else
        {
            // Non-master clients interpolate to the networked position
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * smoothing);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * smoothing);
        }
    }
    
    private void MoveTrain()
    {
        // If waiting at a stop
        if (isWaiting)
        {
            timer += Time.deltaTime;
            
            // Check if wait time is over
            if (timer >= waitTime)
            {
                isWaiting = false;
                timer = 0f;
                
                // Switch target position
                targetPosition = (targetPosition == endPosition) ? startPosition : endPosition;
            }
        }
        // If moving
        else
        {
            // Move toward target position
            transform.position = Vector3.MoveTowards(
                transform.position, 
                targetPosition, 
                speed * Time.deltaTime
            );
            
            // Check if train reached the target position
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                isWaiting = true;
                timer = 0f;
            }
        }
    }
    
    // This method is called by Photon to sync data across the network
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this player: send the others our data
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(isWaiting);
            stream.SendNext(timer);
            stream.SendNext(targetPosition);
        }
        else
        {
            // Network player, receive data
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            isWaiting = (bool)stream.ReceiveNext();
            timer = (float)stream.ReceiveNext();
            targetPosition = (Vector3)stream.ReceiveNext();
        }
    }
}
