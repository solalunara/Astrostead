using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Flash : MonoBehaviour
{
    public float m_fHalfPeriod;
    RawImage m_pRenderer;
    void OnEnable()
    {
        m_pRenderer = GetComponent<RawImage>();
        StopAllCoroutines();
        StartCoroutine(nameof(Blink));
    }


    IEnumerator Blink()
    {
        while ( true )
        {
            m_pRenderer.enabled = !m_pRenderer.enabled;
            yield return new WaitForSeconds( m_fHalfPeriod );
        }
    }
}
