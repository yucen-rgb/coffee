using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 5f;
    public float mouseSensitivity = 2f;

    Rigidbody body;
    Camera cam;

    bool isGrounded;

    void Start()
    {
        body = GetComponent<Rigidbody>();
        cam = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        // 前後左右移動
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 movement =
            transform.right * moveX +
            transform.forward * moveZ;

        body.velocity = new Vector3(
            movement.x * moveSpeed,
            body.velocity.y,
            movement.z * moveSpeed
        );

        // 跳躍
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            body.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse
            );
        }

        // 滑鼠控制左右視角
        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(
            0f,
            mouseX * mouseSensitivity,
            0f
        );

        // 滑鼠控制上下視角
        float mouseY = Input.GetAxis("Mouse Y");
        cam.transform.Rotate(
            -mouseY * mouseSensitivity,
            0f,
            0f
        );
    }

    void OnCollisionEnter(Collision collision)
    {
        isGrounded = true;
    }

    void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}

