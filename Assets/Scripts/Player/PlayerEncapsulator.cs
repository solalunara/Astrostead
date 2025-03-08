using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Statics;

public class PlayerEncapsulator : MonoBehaviour
{
    public Planet SOI => m_pSOI;
    PlayerBodyController m_pPlayerController;
    Planet m_pSOI;
    Vector3 m_vSOIUp;
    void OnEnable()
    {
        m_pSOI = null;
        m_pPlayerController = GetComponentInChildren<PlayerBodyController>();
    }

    bool SetSOI( out Vector3 vTotalGravity )
    {
        vTotalGravity = Vector3.zero;
        Planet pSOI = null;
        Vector3 vSOIGravity = Vector3.zero;
        foreach ( Planet p in Planet.s_pPlanets )
        {
            Vector3 vGravAccel = CalculateGravityAccel( p.GetComponent<Rigidbody>().mass, m_pPlayerController.transform.position, p.transform.position );
            vTotalGravity += vGravAccel;
            if ( vGravAccel.sqrMagnitude > 0.01f && vGravAccel.sqrMagnitude > vSOIGravity.sqrMagnitude )
            {
                pSOI = p;
                vSOIGravity = vGravAccel;
            }
        }
        m_vSOIUp = (-vSOIGravity).normalized;
        if ( m_pSOI != pSOI )
        {
            m_pSOI = pSOI;
            return true;
        }
        return false;
    }

    void FixedUpdate()
    {
        SetSOI( out Vector3 vTotalGravity );
        m_pPlayerController.m_pSOI = m_pSOI ? m_pSOI.gameObject : null;
        m_pPlayerController.m_vGravity = vTotalGravity;
    }
}
