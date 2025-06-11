using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControl : MonoBehaviour
{
    private Camera _camera;

    private void Start()
    {
        _camera = Camera.main;
    }

    private void Update()
    {
        // Handle camera movement
        if (Input.GetKey(KeyCode.W))
        {
            _camera.transform.Translate(Vector3.forward * Time.deltaTime * 5f);
        }
        if (Input.GetKey(KeyCode.S))
        {
            _camera.transform.Translate(Vector3.back * Time.deltaTime * 5f);
        }
        if (Input.GetKey(KeyCode.A))
        {
            _camera.transform.Translate(Vector3.left * Time.deltaTime * 5f);
        }
        if (Input.GetKey(KeyCode.D))
        {
            _camera.transform.Translate(Vector3.right * Time.deltaTime * 5f);
        }

        // Handle camera rotation
        if (Input.GetMouseButton(1)) // Right mouse button
        {
            float rotX = Input.GetAxis("Mouse X") * 2f;
            float rotY = Input.GetAxis("Mouse Y") * 2f;
            _camera.transform.Rotate(-rotY, 0, 0, Space.Self);
            _camera.transform.Rotate(0, rotX, 0, Space.World);
        }
        
        // Handle zoom
        var scroll = Input.GetAxis("Mouse ScrollWheel");
         _camera.transform.Translate(0, 0, scroll * 4f, Space.Self);

        if (Input.GetMouseButton(2))
        {
            // Middle mouse button for panning
            float panX = Input.GetAxis("Mouse X") * 0.5f;
            float panY = Input.GetAxis("Mouse Y") * 0.5f;
            _camera.transform.Translate(-panX, -panY, 0, Space.Self);
            
        }
        
        
        
    }
}
