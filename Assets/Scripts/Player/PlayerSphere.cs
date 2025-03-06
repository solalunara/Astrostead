using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Unity.VisualScripting;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class PlayerSphere : MonoBehaviour
{
    public List<Collider> PlayerUniverse;
    void OnTriggerEnter( Collider c )
    {
        // providesContacts must be turned on for Physics.ContactEvent to be called
        c.providesContacts = true;
        PlayerUniverse.Add( c );
    }
    void OnTriggerStay( Collider c )
    {
        // providesContacts must be turned on for Physics.ContactEvent to be called
        if ( !PlayerUniverse.Contains( c ) )
        {
            c.providesContacts = true;
            throw new NotImplementedException( "How did I get here? (letting the days go by)" );
        }
    }
    void OnTriggerExit( Collider c )
    {
        // this won't be called sometimes if we only leave by crouching/uncrouching
        // so that code will handle this itself
        c.providesContacts = false;
        PlayerUniverse.Remove( c );
    }
}
