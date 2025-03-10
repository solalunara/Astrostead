using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Statics;

public class CameraController : MonoBehaviour
{
    public bool m_bLocalMovement = true;
    public float m_fMouseSpeed = 1000.0f;
    public float m_fPlayerDegreesPerSecond = 360.0f;
    public float m_fCameraSmoothingDegreesPerSecond = 1800.0f;
    float m_fRotationX = 0.0f;
    float m_fRotationY = 0.0f;
    Camera m_pCamera;
    PlayerBodyController m_pPlayer;
    Quaternion qTargetLocalRot = Quaternion.identity;
    Quaternion qTargetParentRot = Quaternion.identity;
    bool m_bWasInSOILastFrame = false;

    void OnEnable()
    {
        m_pCamera = GetComponentInChildren<Camera>();
        m_pPlayer = transform.parent.parent.GetComponentInChildren<PlayerBodyController>();
    }

    // Update is called once per frame
    void Update()
    {
        // edit cursor lock state for editor
        if ( m_bLocalMovement )
        {
            if ( Application.isEditor )
            {
                bool mouseOverWindow = Input.mousePosition.x > 0 && Input.mousePosition.x < Screen.width && Input.mousePosition.y > 0 && Input.mousePosition.y < Screen.height;

                //check if the cursor is locked but not centred in the game viewport - i.e. like it is every time the game starts
                if ( Cursor.lockState == CursorLockMode.Locked && !mouseOverWindow )
                {
                    Cursor.lockState = CursorLockMode.Locked;
                }
                //if the player presses Escape, unlock the cursor
                if ( Input.GetKeyDown(KeyCode.Escape) && Cursor.lockState == CursorLockMode.Locked )
                {
                    Cursor.lockState = CursorLockMode.None;
                }
                //if the player releases any mouse button while the cursor is over the game viewport, then lock the cursor again
                else if ( Cursor.lockState == CursorLockMode.None )
                {
                    if ( mouseOverWindow && ( Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2) ) )
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                    }
                }
            }

            // should not be a lerp for relativistic reasons
            transform.parent.position = m_pPlayer.transform.position;
            Vector3 vUp;
            Vector3 vForward;

            float x, y;
            x = y = 0.0f;

            if ( Cursor.lockState == CursorLockMode.Locked )
            {
                y = -Input.GetAxis( "Mouse Y" );
                x = Input.GetAxis( "Mouse X" );
                m_fRotationY += x * m_fMouseSpeed * Time.deltaTime;
                m_fRotationX += y * m_fMouseSpeed * Time.deltaTime;
                m_fRotationX = Mathf.Clamp( m_fRotationX, -90.0f, 90.0f );

                if ( m_fRotationY >  360.0f )
                    m_fRotationY -= 360.0f;
                if ( m_fRotationY < -360.0f )
                    m_fRotationY += 360.0f;
            }

            if ( m_pPlayer.m_pSOI )
            {
                vUp = -CalculateGravityAccel( m_pPlayer.m_pSOI.GetComponent<Rigidbody>().mass, m_pPlayer.transform.position, m_pPlayer.m_pSOI.transform.position ).normalized;
                if ( !m_bWasInSOILastFrame )
                {
                    m_bWasInSOILastFrame = true;
                    // try to find a value for m_fRotationX using the angle from the horizon to transform.forward
                    Vector3 vCamRight = Vector3.Cross( transform.forward, vUp ).normalized;
                    Vector3 vHorizon = Vector3.Cross( vUp, vCamRight );
                    m_fRotationX = Vector3.SignedAngle( transform.forward, vHorizon, vCamRight );
                    m_fRotationX = Mathf.Clamp( m_fRotationX, -90.0f, 90.0f );
                }

                Vector3 vRight = Vector3.Cross( transform.parent.forward, vUp );
                vForward = Vector3.Cross( vUp, vRight ).normalized;
                vForward = Quaternion.AngleAxis( x * m_fMouseSpeed * Time.deltaTime, vUp ) * vForward;

                qTargetLocalRot = Quaternion.AngleAxis( m_fRotationX, Vector3.right );
            }
            else
            {
                if ( m_bWasInSOILastFrame )
                    m_bWasInSOILastFrame = false;

                Vector3 vRight = Vector3.Cross( transform.parent.forward, transform.parent.up );
                Quaternion qInputRotation = Quaternion.AngleAxis( y * m_fMouseSpeed * Time.deltaTime, -vRight ) * 
                                            Quaternion.AngleAxis( x * m_fMouseSpeed * Time.deltaTime, transform.parent.up );
                vUp = qInputRotation * transform.parent.up;
                vForward = qInputRotation * transform.parent.forward;

                // no SOI - camera pivot will deal with the x rotation
                qTargetLocalRot = Quaternion.identity;
            }

            qTargetParentRot = Quaternion.LookRotation( vUp, -vForward ) * Quaternion.Euler( 90, 0, 0 );

            transform.parent.rotation = Quaternion.RotateTowards( transform.parent.rotation, qTargetParentRot, m_fCameraSmoothingDegreesPerSecond * Time.deltaTime );
            transform.localRotation = Quaternion.RotateTowards( transform.localRotation, qTargetLocalRot, m_fCameraSmoothingDegreesPerSecond * Time.deltaTime );

            m_pPlayer.transform.rotation = Quaternion.RotateTowards( m_pPlayer.transform.rotation, transform.parent.rotation, m_fPlayerDegreesPerSecond * Time.deltaTime );
        }


        m_pPlayer.m_qCameraRotation = transform.rotation;
        m_pPlayer.m_qCameraPivotRotation = transform.parent.rotation;
    }
}
