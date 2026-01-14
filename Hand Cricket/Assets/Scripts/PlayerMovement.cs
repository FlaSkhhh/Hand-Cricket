using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    float moveSpeed;
    Rigidbody rb;
    Vector3 moveVector;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        GetInput();
        //Debug.Log(moveVector);
        rb.AddForce(moveVector * moveSpeed * Time.deltaTime,ForceMode.VelocityChange);
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
