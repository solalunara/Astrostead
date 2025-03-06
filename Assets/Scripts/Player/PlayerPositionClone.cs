using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPositionClone : MonoBehaviour
{
    public GameObject m_pPlayer;

    // Update is called once per frame
    void Update()
    {
        transform.position = m_pPlayer.GetComponent<PlayerBodyController>().transform.position;
    }
}
