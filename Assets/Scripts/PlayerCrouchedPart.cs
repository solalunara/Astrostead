using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Statics;

public class PlayerCrouchedPart : PlayerJumpablePart
{
    public List<Collider> UncrouchCollisionObjectsLower = new();
    public List<Collider> UncrouchCollisionObjectsUpper = new();

    PlayerUncrouchedPart pUncrouched;
    UncrouchedTriggerObj[] pUncrouchedTrigger;
    void OnEnable()
    {
        pUncrouched = transform.parent.GetComponentInChildren<PlayerUncrouchedPart>( true );
        pUncrouchedTrigger = transform.parent.GetComponentsInChildren<UncrouchedTriggerObj>( true );

        if ( pUncrouchedTrigger.Length != 2 )
        {
            pUncrouchedTrigger = new UncrouchedTriggerObj[ 2 ];
            GameObject pUncrouchedGameObjLower = new( "__uncrouched_trigger_lower" );
            GameObject pUncrouchedGameObjUpper = new( "__uncrouched_trigger_upper" );
            GameObject[] pUncrouchedGameObjs = new[] { pUncrouchedGameObjLower, pUncrouchedGameObjUpper };
            for ( int i = 0; i < 2; ++i )
            {
                GameObject pUncrouchedGameObj = pUncrouchedGameObjs[ i ];
                pUncrouchedGameObj.transform.parent = transform.parent;
                pUncrouchedTrigger[ i ] = pUncrouchedGameObj.AddComponent<UncrouchedTriggerObj>();
                pUncrouchedTrigger[ i ].CrouchedPart = this;
                if ( pUncrouched.TryGetComponent( out CapsuleCollider pCapsule ) )
                {
                    CapsuleCollider pNewCapsule = pUncrouchedGameObj.AddComponent<CapsuleCollider>();
                    pNewCapsule.radius = pCapsule.radius;
                    pNewCapsule.height = pCapsule.height;
                    pNewCapsule.center = pCapsule.center;
                    pNewCapsule.direction = pCapsule.direction;

                    pNewCapsule.isTrigger = true;
                }
                else
                    throw new NotImplementedException( "Non-Capsule Player Collider not yet implemented" );

                pUncrouchedGameObj.transform.localPosition = pUncrouched.transform.localPosition;
            }
        }
    }

    public void PushUpperTriggerUp( Vector3 vDelta )
    {
        for ( int i = 0; i < pUncrouchedTrigger.Length; ++i )
            if ( pUncrouchedTrigger[ i ].gameObject.name == "__uncrouched_trigger_upper" )
                pUncrouchedTrigger[ i ].transform.localPosition += vDelta;
    }

    void OnDisable()
    {
        UncrouchCollisionObjectsLower.Clear();
        UncrouchCollisionObjectsUpper.Clear();
    }
}
