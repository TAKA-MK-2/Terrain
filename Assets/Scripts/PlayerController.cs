using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour {

    Rigidbody rb;

	// Use this for initialization
	void Start () {
        rb = GetComponent<Rigidbody>();
	}
	
	// Update is called once per frame
	void Update ()
    {
        Vector3 velocity = Vector3.zero;
        if (Input.GetKey(KeyCode.UpArrow))
        {
            velocity += transform.forward;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            velocity += -transform.forward;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            velocity += -transform.right;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            velocity += transform.right;
        }
        rb.velocity = velocity;
    }
}
