using System;
using System.Collections;
using System.Collections.Generic;
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

    public void InstantiateGeometryData( Geometry iGeometry, Vector3 ptGeometryOrigin, Quaternion qGeometryRotation, Vector3 vSideLength )
    {
        if ( m_bInstantiated )
            throw new InvalidOperationException( "Can only instantiate geometry data once!" );
        m_iGeometry = iGeometry;
        RecalculateMesh( ptGeometryOrigin, qGeometryRotation, vSideLength );
        m_bInstantiated = true;
    }

    void RecalculateMesh( Vector3 ptGeometryOrigin, Quaternion qGeometryRotation, Vector3 vSideLength )
    {
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
                    vDirectionalSideLength = qGeometryRotation * vDirectionalSideLength;
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

        m.vertices = pVerts;
        if ( m_iGeometry != Geometry.CARTESIAN )
        {
            Destroy( GetComponent<Collider>() );
            gameObject.AddComponent<MeshCollider>().convex = true;
        }

        m.RecalculateBounds();
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateUVDistributionMetrics();

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