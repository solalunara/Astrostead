using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum Geometry
{
    CARTESIAN,
    CYLINDRICAL,
    SPHERICAL,
}

public class Voxel : MonoBehaviour
{
    // we assume for all voxels that the transform.position will be in the centre of the object
    Geometry m_iGeometry;
    bool m_bInstantiated = false;
    Vector3[] m_pvVertices;
    readonly List<Vector2>[] m_ppvUV = new List<Vector2>[ 8 ];
    int[] m_piTriangles;
    Vector3 m_vSideLength;

    public void InstantiateVoxel( Geometry iGeometry, Vector3 ptGeometryOrigin, Quaternion qGeometryRotation, Vector3 vSideLength )
    {
        if ( m_bInstantiated )
            throw new InvalidOperationException( "Can only instantiate voxel once!" );
        m_iGeometry = iGeometry;
        m_vSideLength = vSideLength;

        Mesh m = GetComponent<MeshFilter>().mesh;

        Vector3[] pVerts = m.vertices;
        switch ( m_iGeometry )
        {
            case Geometry.CARTESIAN:
                Vector3 vDirectionalSideLength = Vector3.zero;
                for ( int i = 0; i < pVerts.Length; ++i )
                {
                    for ( int j = 0; j < 3; ++j )
                        vDirectionalSideLength[ j ] = pVerts[ i ][ j ] > 0 ? vSideLength[ j ] : -vSideLength[ j ];
                    //vDirectionalSideLength = qGeometryRotation * vDirectionalSideLength;
                    pVerts[ i ] = vDirectionalSideLength / 2;
                }
                Vector3 vBoxSize = Vector3.zero;
                for ( int i = 0; i < 3; ++i )
                    vBoxSize[ i ] = vSideLength[ i ] / transform.lossyScale[ i ];
                GetComponent<BoxCollider>().size = vBoxSize;
                break;

            case Geometry.CYLINDRICAL:
                Vector3 ptLocalOrigin = Quaternion.Inverse( qGeometryRotation ) * ( transform.position - ptGeometryOrigin );
                Vector3 ptLocalOriginProjected = Vector3.ProjectOnPlane( ptLocalOrigin, Vector3.up );
                float fRadius = ptLocalOriginProjected.magnitude;
                float fInnerRadius = fRadius - vSideLength[ 0 ] / 2.0f;
                float fOuterRadius = fRadius + vSideLength[ 0 ] / 2.0f;

                float fTheta = Vector3.SignedAngle( new Vector3( 1, 0, 0 ), ptLocalOriginProjected, -Vector3.up ) * Mathf.Deg2Rad;

                // fOuterRadius * delta theta <= vSideLength[ 1 ]
                // delta theta should start at 90 deg and half until it gets to the neccesary value
                float fDeltaTheta = Mathf.PI / 2.0f;
                float fDeltaThetaInner = Mathf.PI / 2.0f;
                while ( fOuterRadius * fDeltaTheta > vSideLength[ 2 ] )
                    fDeltaTheta /= 2.0f;
                while ( fInnerRadius * fDeltaThetaInner > vSideLength[ 2 ] )
                    fDeltaThetaInner /= 2.0f;

                // for calculating upper and lower theta, we use the fDeltaTheta using the outer radius
                // the only thing fDeltaThetaInner being different will change is how far the bisector has to extend
                // to get to the layer before it
                // see https://i.imgur.com/ZrEV23Y.png
                float fMinTheta = Mathf.Floor( fTheta / fDeltaTheta ) * fDeltaTheta;
                float fMaxTheta = Mathf.Ceil( fTheta / fDeltaTheta ) * fDeltaTheta;

                for ( int i = 0; i < pVerts.Length; ++i )
                {
                    // TODO: make r decrease to appropriate value for innerradius when fDeltaThetaInner != fDeltaThetaOuter
                    float r = pVerts[ i ][ 0 ] > 0 ? fOuterRadius : fInnerRadius;
                    float t = pVerts[ i ][ 2 ] > 0 ? fMaxTheta : fMinTheta;

                    pVerts[ i ][ 0 ] = r * Mathf.Cos( t ) - ptLocalOrigin[ 0 ];
                    pVerts[ i ][ 2 ] = r * Mathf.Sin( t ) - ptLocalOrigin[ 2 ];

                    pVerts[ i ][ 1 ] = ( pVerts[ i ][ 1 ] > 0 ? vSideLength[ 1 ] : -vSideLength[ 1 ] ) / 2.0f;

                    //pVerts[ i ] = qGeometryRotation * pVerts[ i ];
                }

                break;

            case Geometry.SPHERICAL:
                throw new NotImplementedException( "Spherical Geometry not yet implemented" );
        }

        m_pvVertices = pVerts;
        for ( int i = 0; i < 8; ++i )
        {
            m_ppvUV[ i ] = new();
            m.GetUVs( i, m_ppvUV[ i ] );
        }
        m_piTriangles = m.triangles;
        m.Clear();
        if ( m_iGeometry != Geometry.CARTESIAN )
        {
            DestroyImmediate( GetComponent<BoxCollider>() );
            gameObject.AddComponent<MeshCollider>().convex = true;
        }
        m_bInstantiated = true;
    }

    Vector3 GetFaceNormal( int i )
    {
        if ( i % 6 != 0 )
            throw new ArgumentException( nameof(i) + " = " + i + ": not a valid face index - must be multiple of 6" );
        Vector3[] vPoints = new Vector3[] {
            m_pvVertices[ m_piTriangles[ i ] ],
            m_pvVertices[ m_piTriangles[ i + 1 ] ],
            m_pvVertices[ m_piTriangles[ i + 2 ] ],
        };
        Vector3 A = vPoints[ 1 ] - vPoints[ 0 ];
        Vector3 B = vPoints[ 2 ] - vPoints[ 0 ];

        Vector3 __ret_val = Vector3.Cross( A, B ).normalized;

        return Vector3.Cross( A, B ).normalized;
    }

    public void RecalculateVoxel( IEnumerable<Vector3> vNewExposedNormals )
    {
        Mesh m = GetComponent<MeshFilter>().mesh;
        m.Clear();

        List<int> piTriangles = new();
        foreach ( Vector3 vNorm in vNewExposedNormals )
            for ( int i = 0; i < m_piTriangles.Length; i += 6 )
                if ( Vector3.Dot( GetFaceNormal( i ), vNorm ) > 0.99f )
                    for ( int u = 0; u < 6; ++u )
                        piTriangles.Add( m_piTriangles[ i + u ] );


        if ( piTriangles.Any() )
        {
            m.vertices = m_pvVertices;
            for ( int i = 0; i < 8; ++i )
                m.SetUVs( i, m_ppvUV[ i ] );
            m_piTriangles = piTriangles.ToArray();
            m.triangles = m_piTriangles;
        }
        else
            m.Clear();

        m.RecalculateBounds();
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateUVDistributionMetrics();

        // modify colliders based on what triangles are rendering
        if ( piTriangles.Any() )
        {
            GetComponent<Collider>().enabled = true;
            if ( TryGetComponent( out MeshCollider mc ) )
                mc.sharedMesh = m;
        }
        else
            GetComponent<Collider>().enabled = false;
    }

    void OnDisable()
    {
        m_bInstantiated = false;
        // Unity Docs:
        //    It is your responsibility to destroy the automatically instantiated mesh when the game object is being destroyed.
        //    Resources.UnloadUnusedAssets also destroys the mesh but it is usually only called when loading a new level.
        Destroy( GetComponent<MeshFilter>().mesh );
    }
}