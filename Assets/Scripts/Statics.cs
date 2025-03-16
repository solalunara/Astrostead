using System;
using System.Collections.Generic;
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