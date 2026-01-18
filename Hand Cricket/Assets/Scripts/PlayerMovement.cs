using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField]
    float moveSpeed;
    Rigidbody rb;
    Vector3 moveVector;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if(NetworkManager.Singleton.IsHost)transform.position = new Vector3(-2,0,0);
        else transform.position = new Vector3(2,0,0);
    }

    void FixedUpdate()
    {
        GetInput();
        //Debug.Log(moveVector);
        if(IsOwner)rb.AddForce(moveVector * moveSpeed * Time.deltaTime,ForceMode.VelocityChange);
    }

    void GetInput()
    {
        if (Input.GetKey(KeyCode.W))
        {
            moveVector.z = 1;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            moveVector.z = -1;
        }
        else moveVector.z = 0;

        if (Input.GetKey(KeyCode.A))
        {
            moveVector.x = -1;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            moveVector.x = 1;
        }
        else moveVector.x = 0;

    }
}
