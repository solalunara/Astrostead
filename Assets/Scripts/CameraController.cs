using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public bool m_bLocalMovement = true;
    public float m_fMouseSpeed = 1000.0f;
    float m_fRotationX = 0.0f;
    Camera m_pCamera;
    GameObject m_pPlayer;

    void OnEnable()
    {
        m_pCamera = GetComponentInChildren<Camera>();
        m_pPlayer = GetComponentInParent<PlayerFollower>().FollowPlayer;
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

            if ( Cursor.lockState == CursorLockMode.Locked )
            {
                float y = -Input.GetAxis( "Mouse Y" );
                float x = Input.GetAxis( "Mouse X" );
                m_fRotationX += y * m_fMouseSpeed * Time.deltaTime;
                m_fRotationX = Mathf.Clamp( m_fRotationX, -90.0f, 90.0f );
                transform.localRotation = Quaternion.AngleAxis( m_fRotationX, Vector3.right );
                transform.parent.localRotation *= Quaternion.AngleAxis( x * m_fMouseSpeed * Time.deltaTime, new Vector3( 0, 1, 0 ) );
            }
        }

        m_pPlayer.GetComponent<PlayerBodyController>().CameraRotation = transform.parent.rotation;
    }
}
