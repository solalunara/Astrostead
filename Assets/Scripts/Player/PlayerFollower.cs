using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFollower : MonoBehaviour
{
    public GameObject FollowPlayer;

    // Update is called once per frame
    void FixedUpdate()
    {
        transform.position = Vector3.Lerp( transform.position, FollowPlayer.transform.position, 10 * Time.fixedDeltaTime );
        transform.rotation = Quaternion.RotateTowards( transform.rotation, FollowPlayer.transform.rotation, 120 * Time.fixedDeltaTime );
    }
}
