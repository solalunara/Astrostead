using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InitialVelocity : MonoBehaviour
{
    public Vector3 m_vInitialVelocity;
    void OnEnable()
    {
        GetComponent<Rigidbody>().velocity = m_vInitialVelocity;
    }
}
