using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFollower : MonoBehaviour
{
    public GameObject FollowPlayer;

    // Update is called once per frame
    void Update()
    {
        transform.position = FollowPlayer.transform.position; //for relativistic reasons this can't be a lerp
        transform.rotation = Quaternion.RotateTowards( transform.rotation, FollowPlayer.transform.rotation, 120 * Time.deltaTime );
    }
}
