using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UncrouchedTriggerObj : MonoBehaviour
{
    public PlayerCrouchedPart CrouchedPart
    { set => m_pCrouchedPart = value; }

    PlayerCrouchedPart m_pCrouchedPart;

    void OnEnable()
    {
    }

    void OnTriggerEnter( Collider c )
    {
        if ( gameObject.name == "__uncrouched_trigger_lower" )
            m_pCrouchedPart.UncrouchCollisionObjectsLower.Add( c );
        else if ( gameObject.name == "__uncrouched_trigger_upper" )
            m_pCrouchedPart.UncrouchCollisionObjectsUpper.Add( c );
        else throw new NotImplementedException( "Uncrouched trigger objects should only be instantiated by code" );
    }

    void OnTriggerStay( Collider c )
    {
        // needed for enabled while touching
        if ( gameObject.name == "__uncrouched_trigger_lower" )
        {
            if ( !m_pCrouchedPart.UncrouchCollisionObjectsLower.Contains( c ) )
                m_pCrouchedPart.UncrouchCollisionObjectsLower.Add( c );
        }
        else if ( gameObject.name == "__uncrouched_trigger_upper" )
        {
            if ( !m_pCrouchedPart.UncrouchCollisionObjectsUpper.Contains( c ) )
                m_pCrouchedPart.UncrouchCollisionObjectsUpper.Add( c );
        }
        else throw new NotImplementedException( "Uncrouched trigger objects should only be instantiated by code" );
    }

    void OnTriggerExit( Collider c )
    {
        if ( gameObject.name == "__uncrouched_trigger_lower" )
            m_pCrouchedPart.UncrouchCollisionObjectsLower.Remove( c );
        else if ( gameObject.name == "__uncrouched_trigger_upper" )
            m_pCrouchedPart.UncrouchCollisionObjectsUpper.Remove( c );
        else throw new NotImplementedException( "Uncrouched trigger objects should only be instantiated by code" );
    }
}
