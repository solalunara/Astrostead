using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFollower : MonoBehaviour
{
    public PlayerBodyController FollowPlayer;

    // Update is called once per frame
    void Update()
    {
        transform.position = FollowPlayer.transform.position; //for relativistic reasons this can't be a lerp
        Quaternion qTargetCamRot = Quaternion.LookRotation( FollowPlayer.transform.up, -FollowPlayer.transform.forward ) * Quaternion.Euler( 90, 0, 0 );
        transform.rotation = Quaternion.RotateTowards( transform.rotation, FollowPlayer.transform.rotation, 120 * Time.deltaTime );
    }
}
