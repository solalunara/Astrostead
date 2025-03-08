using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Statics;

[RequireComponent(typeof(Rigidbody))]
public class Planet : MonoBehaviour
{
    public static readonly List<Planet> s_pPlanets = new();

    void OnEnable()
    {
        s_pPlanets.Add( this );
    }
    void OnDisable()
    {
        s_pPlanets.Remove( this );
    }

    void FixedUpdate()
    {
        Vector3 vTotalGravAccel = Vector3.zero;
        foreach ( Planet p in s_pPlanets )
        {
            if ( p == this )
                continue;
            Vector3 vGravAccel = CalculateGravityAccel( p.GetComponent<Rigidbody>().mass, transform.position, p.transform.position );
            vTotalGravAccel += vGravAccel;
        }
        GetComponent<Rigidbody>().AddForce( vTotalGravAccel, ForceMode.Acceleration );
    }
}
