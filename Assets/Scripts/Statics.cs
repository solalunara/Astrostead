using System.Collections.Generic;
using UnityEngine;

static class Statics
{
    public static float g_fStartTime;

    // Planet mass in megatons
    public static Vector3 CalculateGravityAccel( float fPlanetMass, Vector3 ptThis, Vector3 ptPlanet )
    {
        const float G = 6.67e-02f;
        float fSqrRadius = ( ptThis - ptPlanet ).sqrMagnitude;
        Vector3 vNorm = ( ptThis - ptPlanet ).normalized;
        return G * fPlanetMass / fSqrRadius * -vNorm;
    }
}