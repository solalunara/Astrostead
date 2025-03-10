using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
using Cursor = UnityEngine.Cursor;
using static Statics;

public class PlayerBodyController : MonoBehaviour
{
    const float EPSILON = 0.01f;


    public GameObject GroundEntity => m_pGroundEntity;
    public Vector3 Up => m_vUp;
    public Vector3 BaseVelocity => m_vBaseVelocity;
    public Quaternion m_qCameraRotation;
    public Quaternion m_qCameraPivotRotation;
    public Vector3 m_vGravity = Vector3.zero;
    public float m_fAirMoveFraction = 0.1f;
    public float m_fMaxSpeed = 3.0f;
    public float m_fMaxSprintSpeed = 6.0f;
    public int m_iGroundThreshold = 1;
    public float m_fFrictionConstant = 1.5f;
    public int m_iCoyoteFrames = 8;
    public float m_fJumpVelocity = 4.0f;
    //public bool m_bEnableABH = true;
    public bool m_bLocalMovement = true;
    public Vector3 GroundCollisionPt => m_vGroundCollisionPt;

    public GameObject m_pSOI = null;

    Rigidbody m_pRigidbody;
    Vector3 m_vUp => transform.up;
    Vector3 m_vForward => transform.forward;
    bool m_bCrouched = false;
    int m_iGroundFrames = 0;
    int m_iFramesSinceGround = 0;
    int m_iCrouchedFrames = 0;
    bool m_bWantsToCrouch = false;
    int m_iJumpTimer = 0;
    bool m_bSprinting = false;
    GameObject m_pGroundEntity;
    Vector3 m_vGroundNormal;
    Vector3 m_vGroundCollisionPt;
    GameObject m_pUncrouchedObj;
    GameObject m_pCrouchedObj;
    Vector3 m_vPlayerGroundUncrouchDelta;
    Vector3 m_vPlayerGroundCrouchDelta;
    PlayerSphere m_pPlayerSphere;
    Vector3 m_vBaseVelocity = Vector3.zero;
    KeyCode iCrouchKey = KeyCode.None;

    void OnEnable()
    {
        m_pUncrouchedObj = transform.parent.GetComponentInChildren<PlayerUncrouchedPart>().gameObject;
        m_pCrouchedObj = transform.parent.GetComponentInChildren<PlayerCrouchedPart>( true ).gameObject;
        m_pPlayerSphere = transform.parent.GetComponentInChildren<PlayerSphere>();
        m_pRigidbody = transform.GetComponent<Rigidbody>();
        Physics.ContactEvent += Physics_ContactEvent;

        m_pCrouchedObj.SetActive( true );
        Bounds bbxWorldSpaceCrouched = m_pCrouchedObj.GetComponent<Collider>().bounds;
        m_pCrouchedObj.SetActive( false );
        Bounds bbxWorldSpaceUncrouched = m_pUncrouchedObj.GetComponent<Collider>().bounds;
        Vector3 vContactPtCrouched = new( bbxWorldSpaceCrouched.center.x, bbxWorldSpaceCrouched.min.y, bbxWorldSpaceCrouched.center.z );
        Vector3 vContactPtUncrouched = new( bbxWorldSpaceUncrouched.center.x, bbxWorldSpaceUncrouched.min.y, bbxWorldSpaceUncrouched.center.z );
        Vector3 vUpperPtCrouched = new( bbxWorldSpaceCrouched.center.x, bbxWorldSpaceCrouched.max.y, bbxWorldSpaceCrouched.center.z );
        Vector3 vUpperPtUncrouched = new( bbxWorldSpaceUncrouched.center.x, bbxWorldSpaceUncrouched.max.y, bbxWorldSpaceUncrouched.center.z );
        m_vPlayerGroundCrouchDelta = vContactPtUncrouched - vContactPtCrouched; //difference from contact pt crouched to contact pt uncrouched (for crouching on ground)
        m_vPlayerGroundUncrouchDelta = -m_vPlayerGroundCrouchDelta + ( vUpperPtUncrouched - vUpperPtCrouched ); //not exactly -gndcrouch b/c top extents not neccesarily alligned
        m_pCrouchedObj.GetComponent<PlayerCrouchedPart>().PushUpperTriggerUp( m_vPlayerGroundUncrouchDelta );
    }

    private void Physics_ContactEvent( PhysicsScene scene, NativeArray<ContactPairHeader>.ReadOnly pHeaderArray )
    {
        m_vGroundCollisionPt = Vector3.zero;
        m_vGroundNormal = Vector3.zero;
        m_pGroundEntity = null;
        foreach ( var Header in pHeaderArray )
        {
            Component rFirst = Header.Body;
            Component rSecond = Header.OtherBody;

            PlayerBodyController pBody = null;
            if ( rFirst  &&  rFirst.gameObject.GetComponentInChildren<PlayerBodyController>() )
                pBody =  rFirst.gameObject.GetComponentInChildren<PlayerBodyController>();
            if ( rSecond && rSecond.gameObject.GetComponentInChildren<PlayerBodyController>() )
                pBody = rSecond.gameObject.GetComponentInChildren<PlayerBodyController>();

            if ( !pBody )
                continue;

            for ( int i = Header.PairCount; --i >= 0; )
            {
                ref readonly var Pair = ref Header.GetContactPair( i );

                if ( !Pair.Collider || !Pair.OtherCollider )
                    continue;
                
                PlayerJumpablePart cFirst = Pair.Collider.gameObject.GetComponent<PlayerJumpablePart>();
                PlayerJumpablePart cSecond = Pair.OtherCollider.gameObject.GetComponent<PlayerJumpablePart>();

                if ( !cFirst && !cSecond )
                    continue;

                if ( cFirst && cSecond )
                    throw new Exception( "Two objects with PlayerJumpablePart are colliding: " + cFirst.gameObject + " and " + cSecond.gameObject );

                // 'normal' way around is 1st obj jumpable, 2nd object other
                bool bReversed = !cFirst;

                NativeArray<ContactPairPoint> contacts = new( Pair.ContactCount, Allocator.Temp );
                Pair.CopyToNativeArray( contacts );
                GameObject pOtherObject = bReversed ? Pair.Collider.gameObject : Pair.OtherCollider.gameObject;

                foreach ( ContactPairPoint contact in contacts )
                {
                    Vector3 vNorm = bReversed ? -contact.Normal : contact.Normal;
                    if ( Vector3.Dot( m_vGroundNormal, m_vUp ) < Vector3.Dot( vNorm, m_vUp ) && Vector3.Dot( vNorm, m_vUp ) > 0.7f )
                    {
                        m_vGroundCollisionPt = contact.Position;
                        m_vGroundNormal = vNorm;
                        m_pGroundEntity = pOtherObject;
                    }
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if ( Input.GetKeyDown( KeyCode.LeftControl ) && m_pGroundEntity )
        {
            m_bWantsToCrouch = true;
            iCrouchKey = KeyCode.LeftControl;
        }
        if ( Input.GetKeyDown( KeyCode.C ) && !m_pGroundEntity )
        {
            m_bWantsToCrouch = true;
            iCrouchKey = KeyCode.C;
        }
        if ( Input.GetKeyUp( iCrouchKey ) )
            m_bWantsToCrouch = false;

        if ( Input.GetKeyDown( KeyCode.LeftShift ) )
            m_bSprinting = true;
        else if ( Input.GetKeyUp( KeyCode.LeftShift ) )
            m_bSprinting = false;

        if ( Input.GetKeyDown( KeyCode.E ) )
        {
        }
        if ( Input.GetKeyDown( KeyCode.Escape ) )
        {
        }
    }

    void Friction()
    {
        if ( m_iGroundFrames > m_iGroundThreshold )
        {
            Vector3 vGroundVelocity = m_pGroundEntity.GetComponentInParent<Rigidbody>().GetPointVelocity( m_vGroundCollisionPt );
            Vector3 vVelocity = m_pRigidbody.velocity - vGroundVelocity;
            if ( vVelocity.sqrMagnitude == 0.0f )
                return;
            Vector3 vFriction = Vector3.Dot( m_vGravity, -m_vUp ) * m_fFrictionConstant * Time.fixedDeltaTime * Vector3.Dot( m_vGroundNormal, m_vUp ) / vVelocity.magnitude * -vVelocity;
            if ( vFriction.sqrMagnitude > vVelocity.sqrMagnitude )
                m_pRigidbody.velocity = vGroundVelocity;
            else
                m_pRigidbody.velocity += vFriction;
        }
    }

    bool SetCrouchedState( bool bCrouch, bool bTest = false )
    {
        bool bChangingState = bCrouch != m_bCrouched;
        if ( !bChangingState )
            return false;

        // see PlayerJumpablePart.cs -> OnCollisionExit
        //if ( m_pGroundEntity )
        //    m_pGroundEntity.GetComponent<Collider>().providesContacts = false;

        Vector3 vPos;
        if ( !bCrouch )
        {
            //have to manually test for floor as we could be uncrouching in the air but with not enough space
            IEnumerable<Collider> pHitColliders = m_pCrouchedObj.GetComponent<PlayerCrouchedPart>().UncrouchCollisionObjectsLower;
            IEnumerable<Collider> pHitCollidersUpper = m_pCrouchedObj.GetComponent<PlayerCrouchedPart>().UncrouchCollisionObjectsUpper;
            Vector3 vDelta = Vector3.zero;

            UncrouchedTriggerObj[] pUncroucheds = GetComponentsInChildren<UncrouchedTriggerObj>();
            bool bFirstLower = pUncroucheds[ 0 ].gameObject.name == "__uncrouched_trigger_lower";
            Collider pUncrouchedLower = pUncroucheds[ bFirstLower ? 0 : 1 ].GetComponent<Collider>();
            Collider pUncrouchedUpper = pUncroucheds[ bFirstLower ? 1 : 0 ].GetComponent<Collider>();

            //prevent uncrouch from ground if we can't go up
            if ( m_pGroundEntity )
            {
                foreach ( var pHitColliderUpper in pHitCollidersUpper )
                {
                    bool bhit = Physics.ComputePenetration( pHitColliderUpper, pHitColliderUpper.transform.position, pHitColliderUpper.transform.rotation,
                                                            pUncrouchedUpper, pUncrouchedUpper.transform.position, pUncrouchedUpper.transform.rotation,
                                                            out Vector3 vNorm, out float fDist );
                    vNorm = -vNorm; 
                    //normal cond - only things blocking us from above should prevent uncrouch
                    if ( bhit && fDist > 0 && Vector3.Dot( vNorm, Up ) < 0 )
                        return false;
                }
            }

            Collider pCrouchedCollider = m_pCrouchedObj.GetComponent<Collider>();
            foreach ( var pObject in m_pPlayerSphere.PlayerUniverse )
            {
                bool bhit = Physics.ComputePenetration( pObject, pObject.transform.position, pObject.transform.rotation,
                                                        pCrouchedCollider, pCrouchedCollider.transform.position, pCrouchedCollider.transform.rotation,
                                                        out Vector3 vNorm, out float fDist );
                // this code can't handle if the crouched collider having a penetration be resolved
                // just return false - the external code will try again later
                if ( bhit && fDist > 0 )
                {
                    if ( !bTest )
                        transform.position += -vNorm * fDist;
                    return false;
                }
            }

            bool bHit = pHitColliders.Any();
            if ( bHit && !m_pGroundEntity )
            {
                Vector3 ptUncrouchedColliderOrigin = m_pUncrouchedObj.transform.position;
                foreach ( var pHitCollider in pHitColliders )
                {
                    bool bhit = Physics.ComputePenetration( pHitCollider, pHitCollider.transform.position, pHitCollider.transform.rotation,
                                                            pUncrouchedLower, ptUncrouchedColliderOrigin, pUncrouchedLower.transform.rotation,
                                                            out Vector3 vNorm, out float fDist );

                    if ( !bhit ) //moved out of the way already
                        continue;

                    ptUncrouchedColliderOrigin += -vNorm * fDist;
                }
                vDelta += ptUncrouchedColliderOrigin - m_pUncrouchedObj.transform.position;
                m_pRigidbody.velocity -= Vector3.Dot( m_pRigidbody.velocity, Up ) * Up;
            }
            if ( bHit && m_pGroundEntity )
                vDelta += transform.TransformDirection( m_vPlayerGroundUncrouchDelta );

            vPos = transform.position + vDelta;
        }
        else
        {
            if ( m_iGroundFrames > 0 )
                vPos = transform.position + transform.TransformDirection( m_vPlayerGroundCrouchDelta );
            else
                vPos = transform.position;
        }

        if ( !bTest )
        {
            transform.position = vPos;

            m_pUncrouchedObj.SetActive( !bCrouch );
            m_pCrouchedObj.SetActive( bCrouch );

            m_bCrouched = bCrouch;
        }

        return true;
    }

    void WalkMove()
    {
        Vector3 vVelocity = m_pRigidbody.velocity - m_vBaseVelocity;
        //try to walk
        Vector3 WalkForce = Vector3.zero;
        if ( Input.GetKey( KeyCode.W ) )
            WalkForce += Vector3.forward;
        if ( Input.GetKey( KeyCode.S ) )
            WalkForce -= Vector3.forward;
        if ( Input.GetKey( KeyCode.D ) )
            WalkForce += Vector3.right;
        if ( Input.GetKey( KeyCode.A ) )
            WalkForce -= Vector3.right;
        WalkForce = 0.3f * WalkForce.normalized;

        if ( m_bLocalMovement )
            WalkForce = m_qCameraPivotRotation * WalkForce;

        if ( WalkForce != Vector3.zero && !m_bLocalMovement )
            m_pRigidbody.rotation = Quaternion.LookRotation( WalkForce ) * Quaternion.AngleAxis( 0.0f, m_vUp );

        if ( m_iGroundFrames == 0 )
            WalkForce *= m_fAirMoveFraction;

        float fMaxSpeed = m_bSprinting ? m_fMaxSprintSpeed : m_fMaxSpeed;

        if ( vVelocity.sqrMagnitude < fMaxSpeed * fMaxSpeed )
        {
            // clamp walk force to only add up to max speed
            if ( ( vVelocity + WalkForce ).sqrMagnitude > fMaxSpeed * fMaxSpeed )
            {
                float __A = WalkForce.sqrMagnitude;
                float __B = 2 * Vector3.Dot( vVelocity, WalkForce );
                float __C = vVelocity.sqrMagnitude - fMaxSpeed * fMaxSpeed;
                float fScaleFactor = -__B + Mathf.Sqrt( __B * __B - 4 * __A * __C );
                fScaleFactor /= 2 * __A;
                if ( fScaleFactor < 0 || fScaleFactor > 1 )
                    throw new ArithmeticException( "Invalid scale factor: " + fScaleFactor );
                WalkForce *= fScaleFactor;
            }

            m_pRigidbody.AddForce( WalkForce, ForceMode.VelocityChange );
        }
    }

    void AirMove()
    {
        Vector3 vVelocity = m_pRigidbody.velocity - m_vBaseVelocity;

        Vector3 Force = Vector3.zero;
        if ( Input.GetKey( KeyCode.W ) )
            Force += Vector3.forward;
        if ( Input.GetKey( KeyCode.S ) )
            Force -= Vector3.forward;
        if ( Input.GetKey( KeyCode.D ) )
            Force += Vector3.right;
        if ( Input.GetKey( KeyCode.A ) )
            Force -= Vector3.right;
        if ( Input.GetKey( KeyCode.LeftShift ) )
            Force += Vector3.up;
        if ( Input.GetKey( KeyCode.LeftControl ) )
            Force -= Vector3.up;
        Force = 0.3f * Force.normalized;

        if ( m_bLocalMovement )
            Force = m_qCameraPivotRotation * Force;

        if ( Force != Vector3.zero && !m_bLocalMovement )
            m_pRigidbody.rotation = Quaternion.LookRotation( Force ) * Quaternion.AngleAxis( 0.0f, m_vUp );

        if ( m_iGroundFrames == 0 )
            Force *= m_fAirMoveFraction;

        m_pRigidbody.AddForce( Force, ForceMode.VelocityChange );
    }

    void FixedUpdate()
    {
        m_iGroundFrames = GroundEntity != null ? m_iGroundFrames + 1 : 0;
        m_iCrouchedFrames = m_iCrouchedFrames > 0 ? m_iCrouchedFrames + 1 : 0;
        m_iFramesSinceGround = m_iGroundFrames >= m_iGroundThreshold ? 0 : m_iFramesSinceGround + 1;
        m_iJumpTimer = m_iJumpTimer > 0 ? m_iJumpTimer - 1 : 0;

        transform.rotation = Quaternion.LookRotation( m_vUp, -m_vForward ) * Quaternion.Euler( 90, 0, 0 );

        if ( m_pSOI )
            m_vBaseVelocity = m_pSOI.GetComponent<Rigidbody>().GetPointVelocity( m_vGroundCollisionPt );
        else
            m_vBaseVelocity = Vector3.zero;

        // crouch code has to be before move down to prevent race condition
        if ( !m_bWantsToCrouch && m_bCrouched ) //uncrouch immediately
        {
            SetCrouchedState( m_bWantsToCrouch );
            m_iCrouchedFrames = 0;
        }
        if ( m_bWantsToCrouch && !m_bCrouched ) //start crouch timer immediately
            if ( m_iCrouchedFrames == 0 )
                m_iCrouchedFrames = 1;
        if ( ( m_iCrouchedFrames > m_iCoyoteFrames && m_pUncrouchedObj.activeSelf ) || //crouch only once we're sure we're not crouch jumping
             ( m_bWantsToCrouch && !m_pGroundEntity ) ) //
            SetCrouchedState( true );



        //if ( m_bEnableABH && m_bCrouched && m_iGroundFrames == 1 && m_pRigidbody.velocity.sqrMagnitude > m_fMaxSpeed * m_fMaxSpeed )
        //    m_pRigidbody.AddForce( -transform.forward * 5.0f, ForceMode.VelocityChange );

        if ( !m_pGroundEntity )
            m_pRigidbody.velocity += Time.fixedDeltaTime * m_vGravity;
        Friction();

        if ( m_pGroundEntity )
            WalkMove();
        else
            AirMove();

        if ( Input.GetKey( KeyCode.Space ) && m_iJumpTimer == 0 && m_iFramesSinceGround < m_iCoyoteFrames )
        {
            m_iJumpTimer = m_iCoyoteFrames + 1;
            m_pRigidbody.velocity += m_fJumpVelocity * m_vUp;
            m_iFramesSinceGround += m_iCoyoteFrames;
        }



        //transform.rotation = Quaternion.Slerp( transform.rotation, BodyGoal.transform.rotation, .2f );
        //Head.transform.localRotation = Quaternion.Slerp( Head.transform.localRotation, LookGoal.transform.rotation, .2f );
    }
}
