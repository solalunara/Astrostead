using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

static class Statics
{
    public static float g_fStartTime;
    public static Vector3[] GetCubeVertices()
    {
        if ( s_pCubeVertices == null )
            InstantiateCubeData();
        Vector3[] ret = new Vector3[ s_pCubeVertices.Length ];
        Array.Copy( s_pCubeVertices, ret, s_pCubeVertices.Length );
        return ret;
    }
    public static Vector2[] GetCubeUV( int i )
    {
        if ( s_pCubeVertices == null )
            InstantiateCubeData();
        Vector2[] ret = new Vector2[ s_pCubeUV[ i ].Count ];
        if ( ret.Length > 0 )
            s_pCubeUV[ i ].CopyTo( ret );
        return ret;
    }
    public static int[] GetCubeTriangles()
    {
        if ( s_pCubeVertices == null )
            InstantiateCubeData();
        int[] ret = new int[ s_pCubeTriangles.Length ];
        Array.Copy( s_pCubeTriangles, ret, s_pCubeTriangles.Length );
        return ret;
    }

    private static void InstantiateCubeData()
    {
        GameObject pTempCube = GameObject.CreatePrimitive( PrimitiveType.Cube );
        Mesh m = pTempCube.GetComponent<MeshFilter>().sharedMesh;
        s_pCubeVertices = m.vertices;
        for ( int i = 0; i < 8; ++i )
        {
            s_pCubeUV[ i ] = new();
            m.GetUVs( i, s_pCubeUV[ i ] );
        }
        s_pCubeTriangles = m.triangles;
        MonoBehaviour.DestroyImmediate( pTempCube );
    }
    private static Vector3[] s_pCubeVertices = null;
    private static readonly List<Vector2>[] s_pCubeUV = new List<Vector2>[ 8 ];
    private static int[] s_pCubeTriangles = null;

    public static Vector3 GetCentrePositionFromCorners( IEnumerable<Vector3> pCorners )
    {
        Vector3 vCentre = Vector3.zero;
        foreach ( Vector3 vCorner in pCorners )
            vCentre += vCorner;
        vCentre /= 8;
        return vCentre;
    }

    /// <summary>
    /// Converts a standard array of 8 cube corners into the format needed for the opengl vertices backend (len 24)
    /// </summary>
    public static IEnumerable<Vector3> VerticesFromCorners( Vector3[] pCorners )
    {
        foreach ( Vector3 vCubeVert in s_pCubeVertices )
        {
            bool x = vCubeVert.x > 0;
            bool y = vCubeVert.y > 0;
            bool z = vCubeVert.z > 0;

            switch ( (x, y, z) )
            {
                case (false, false, false):
                    yield return pCorners[ 0 ];
                    break;
                case (false, false, true):
                    yield return pCorners[ 1 ];
                    break;
                case (true, false, false):
                    yield return pCorners[ 2 ];
                    break;
                case (false, true, false):
                    yield return pCorners[ 3 ];
                    break;
                case (true, false, true):
                    yield return pCorners[ 4 ];
                    break;
                case (false, true, true):
                    yield return pCorners[ 5 ];
                    break;
                case (true, true, false):
                    yield return pCorners[ 6 ];
                    break;
                case (true, true, true):
                    yield return pCorners[ 7 ];
                    break;
            }
        }
    }

    // Planet mass in megatons
    public static Vector3 CalculateGravityAccel( float fPlanetMass, Vector3 ptThis, Vector3 ptPlanet )
    {
        const float G = 6.67e-02f;
        float fSqrRadius = ( ptThis - ptPlanet ).sqrMagnitude;
        Vector3 vNorm = ( ptThis - ptPlanet ).normalized;
        return G * fPlanetMass / fSqrRadius * -vNorm;
    }

    public static (Vector2, Vector2) GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures t )
    {
        TextureAtlas ta = TextureAtlas.GetAtlas( AtlasType.STANDARD );
        uint iTextureIndex = (uint)Array.IndexOf( Enum.GetValues( typeof(StandardAtlasTextures) ), t );
        float fSizeX = 1.0f / ta.TexturesCountX;
        float fSizeY = 1.0f / ta.TexturesCountY;
        uint iTexIndexY = iTextureIndex / ta.TexturesCountX;
        uint iTexIndexX = iTextureIndex % ta.TexturesCountX;
        Vector2 vMins = new( iTexIndexX * fSizeX, 1.0f - ( iTexIndexY + 1 ) * fSizeY );
        Vector2 vMaxs = new( ( iTexIndexX + 1 ) * fSizeX, 1.0f - iTexIndexY * fSizeY );
        return (vMins, vMaxs);
    }
}