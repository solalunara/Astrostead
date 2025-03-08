using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InitialVelocity : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<Rigidbody>().velocity += 4.0f * Vector3.right;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
