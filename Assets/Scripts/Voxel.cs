using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static Statics;

public enum Geometry
{
    NONE,
    CARTESIAN,
    CYLINDRICAL,
    SPHERICAL,
}

public enum BlockType
{
    NONE,
    GRASS,
    DIRT
}

public struct GeometryData
{
    public Geometry m_iGeometry;
    public Vector3 m_ptGeometryOrigin;
    public Quaternion m_qGeometryRotation;
    public Vector3 m_vSideLength;
}

public class Voxel : MonoBehaviour
{
    // we assume for all voxels that the transform.position will be in the centre of the object
    public GeometryData GeoData
    {
        get => m_gGeometry;
        set
        {
            m_gGeometry = value;
            RefreshVertices();
        }
    }
    public BlockType Block
    {
        get => m_iType;
        set
        {
            m_iType = value;
            RefreshUVs();
        }
    }
    public List<Vector3> ExposedNormals
    {
        get
        {
            Vector3[] ret = new Vector3[ m_vNewExposedNormals.Count() ];
            m_vNewExposedNormals.CopyTo( ret );
            return ret.ToList();
        }
        set
        {
            if ( m_vNewExposedNormals.SequenceEqual( value ) )
                return;

            m_vNewExposedNormals.Clear();
            m_vNewExposedNormals.AddRange( value );
            RefreshTriangles();
        }
    }

    GeometryData m_gGeometry;
    Vector3[] m_pvVertices;
    readonly Vector2[][] m_ppvUV = new Vector2[ 8 ][];
    int[] m_piTriangles;
    BlockType m_iType;
    readonly List<Vector3> m_vNewExposedNormals = new();

    void Awake()
    {
        m_pvVertices = GetCubeVertices();
        for ( int i = 0; i < 8; ++i )
            m_ppvUV[ i ] = GetCubeUV( i );
        m_piTriangles = GetCubeTriangles();
    }

    Mesh GetOrAddMesh()
    {
        if ( !TryGetComponent( out MeshFilter m ) )
            m = gameObject.AddComponent<MeshFilter>();
        return m.mesh;
    }

    void RemoveMesh()
    {
        if ( TryGetComponent( out MeshFilter m ) )
        {
            m.mesh.Clear();
            Destroy( m.mesh );
        }
    }

    void RefreshVertices()
    {
        Vector3[] pVerts = GetCubeVertices();
        Vector3 vSideLength = m_gGeometry.m_vSideLength;
        Vector3 ptGeometryOrigin = m_gGeometry.m_ptGeometryOrigin;
        Quaternion qGeometryRotation = m_gGeometry.m_qGeometryRotation;
        switch ( m_gGeometry.m_iGeometry )
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
                break;

            case Geometry.CYLINDRICAL:
            {
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
            }

            case Geometry.SPHERICAL:
            {
                Vector3 ptLocalOrigin = Quaternion.Inverse( qGeometryRotation ) * ( transform.position - ptGeometryOrigin );
                float fRadius = ptLocalOrigin.magnitude;
                float fInnerRadius = fRadius - vSideLength[ 0 ] / 2.0f;
                float fOuterRadius = fRadius + vSideLength[ 0 ] / 2.0f;

                float fPhi = Vector3.SignedAngle( new Vector3( 0, 0, 1 ), new Vector3( ptLocalOrigin.x, 0, ptLocalOrigin.z ), Vector3.up ) * Mathf.Deg2Rad;
                float fTheta = Vector3.Angle( new Vector3( 0, 1, 0 ), ptLocalOrigin ) * Mathf.Deg2Rad;

                // fOuterRadius * delta theta <= vSideLength[ 1 ]
                // delta theta should start at 90 deg and half until it gets to the neccesary value
                float fDeltaTheta = Mathf.PI / 2.0f;
                float fDeltaThetaInner = Mathf.PI / 2.0f;
                while ( fOuterRadius * fDeltaTheta > vSideLength[ 1 ] )
                    fDeltaTheta /= 2.0f;
                while ( fInnerRadius * fDeltaThetaInner > vSideLength[ 1 ] )
                    fDeltaThetaInner /= 2.0f;

                float fDeltaPhi = Mathf.PI / 2.0f;
                float fDeltaPhiInner = Mathf.PI / 2.0f;
                while ( fOuterRadius * Mathf.Sin( fTheta ) * fDeltaPhi > vSideLength[ 2 ] )
                    fDeltaPhi /= 2.0f;
                while ( fInnerRadius * fDeltaPhiInner > vSideLength[ 2 ] )
                    fDeltaPhiInner /= 2.0f;


                // for calculating upper and lower theta, we use the fDeltaTheta using the outer radius
                // the only thing fDeltaThetaInner being different will change is how far the bisector has to extend
                // to get to the layer before it
                // see https://i.imgur.com/ZrEV23Y.png
                float fMinTheta = Mathf.Floor( fTheta / fDeltaTheta ) * fDeltaTheta;
                float fMaxTheta = Mathf.Ceil( fTheta / fDeltaTheta ) * fDeltaTheta;

                float fMinPhi = Mathf.Floor( fPhi / fDeltaPhi ) * fDeltaPhi;
                float fMaxPhi = Mathf.Ceil( fPhi / fDeltaPhi ) * fDeltaPhi;

                for ( int i = 0; i < pVerts.Length; ++i )
                {
                    // TODO: make r decrease to appropriate value for innerradius when fDeltaThetaInner != fDeltaThetaOuter
                    float r = pVerts[ i ][ 0 ] > 0 ? fOuterRadius : fInnerRadius;
                    float t = pVerts[ i ][ 1 ] > 0 ? fMaxTheta : fMinTheta;
                    float p = pVerts[ i ][ 2 ] > 0 ? fMaxPhi : fMinPhi;


                    pVerts[ i ][ 0 ] = r * Mathf.Sin( t ) * Mathf.Sin( p ) - ptLocalOrigin[ 0 ];
                    pVerts[ i ][ 1 ] = r * Mathf.Cos( t ) - ptLocalOrigin[ 1 ];
                    pVerts[ i ][ 2 ] = r * Mathf.Sin( t ) * Mathf.Cos( p ) - ptLocalOrigin[ 2 ];
                    //pVerts[ i ] = qGeometryRotation * pVerts[ i ];
                }
                break;
            }
        }

        m_pvVertices = pVerts;

        if ( m_vNewExposedNormals.Any() )
        {
            Mesh m = GetOrAddMesh();
            m.vertices = m_pvVertices;
            m.RecalculateBounds();
            m.RecalculateNormals();
            m.RecalculateTangents();
        }
        UpdateCollider();
    }

    void RefreshUVs()
    {
        Vector3 vUpVector = GetUpVector();

        Vector2 vTopTextureMaxs;
        Vector2 vTopTextureMins;
        Vector2 vSideTextureMaxs;
        Vector2 vSideTextureMins;
        Vector2 vBottomTextureMaxs;
        Vector2 vBottomTextureMins;
        switch ( m_iType )
        {
            case BlockType.GRASS:
            {
                (vTopTextureMins, vTopTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.GRASS );
                (vSideTextureMins, vSideTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.DIRT );
                (vBottomTextureMins, vBottomTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.DIRT );
                break;
            }
            case BlockType.DIRT:
            {
                (vTopTextureMins, vTopTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.DIRT );
                (vSideTextureMins, vSideTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.DIRT );
                (vBottomTextureMins, vBottomTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.DIRT );
                break;
            }

            default:
            {
                (vTopTextureMins, vTopTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.NONE );
                (vSideTextureMins, vSideTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.NONE );
                (vBottomTextureMins, vBottomTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.NONE );
                break;
            }
        }

        for ( int i = 0; i < 8; ++i )
        {
            Vector2[] ppvUV = GetCubeUV( i );

            if ( !m_ppvUV[ i ].Any() )
                continue;

            for ( int j = 0; j < m_piTriangles.Length; j += 6 )
            {
                for ( int n = 0; n < 6; ++n )
                {
                    for ( int k = 0; k < 2; ++k )
                    {
                        if ( Vector3.Dot( GetFaceNormal( j ), vUpVector ) > 0.97f )
                            ppvUV[ m_piTriangles[ j + n ] ][ k ] = m_ppvUV[ i ][ m_piTriangles[ j + n ] ][ k ] > 0.5f ? vTopTextureMaxs[ k ] : vTopTextureMins[ k ];
                        else if ( Vector3.Dot( GetFaceNormal( j ), vUpVector ) < -0.97f )
                            ppvUV[ m_piTriangles[ j + n ] ][ k ] = m_ppvUV[ i ][ m_piTriangles[ j + n ] ][ k ] > 0.5f ? vBottomTextureMaxs[ k ] : vBottomTextureMins[ k ];
                        else
                            ppvUV[ m_piTriangles[ j + n ] ][ k ] = m_ppvUV[ i ][ m_piTriangles[ j + n ] ][ k ] > 0.5f ? vSideTextureMaxs[ k ] : vSideTextureMins[ k ];
                    }
                }
            }

            m_ppvUV[ i ] = ppvUV;
        }

        if ( m_vNewExposedNormals.Any() )
        {
            Mesh m = GetOrAddMesh();
            for ( int i = 0; i < 8; ++i )
            {
                List<Vector2> muvs = new();
                m.GetUVs( i, muvs );
                if ( muvs.Any() )
                    m.SetUVs( i, m_ppvUV[ i ] );
            }
            m.RecalculateUVDistributionMetrics();
        }
    }

    void RefreshTriangles()
    {
        List<int> piTriangles = new();
        foreach ( Vector3 vNorm in m_vNewExposedNormals )
            for ( int i = 0; i < m_piTriangles.Length; i += 6 )
                if ( Vector3.Dot( GetFaceNormal( i ), vNorm ) > 0.97f )
                    for ( int u = 0; u < 6; ++u )
                        piTriangles.Add( m_piTriangles[ i + u ] );


        if ( piTriangles.Any() )
        {
            Mesh m = GetOrAddMesh();
            m.vertices = m_pvVertices;
            for ( int i = 0; i < 8; ++i )
                m.SetUVs( i, m_ppvUV[ i ] );
            m_piTriangles = piTriangles.ToArray();
            m.triangles = m_piTriangles;
        }

        UpdateCollider(); // enable/disable if we have no exposed faces
    }

    Vector3 GetUpVector() 
    {
        return m_gGeometry.m_iGeometry switch
        {
            Geometry.CARTESIAN => Vector3.up,
            Geometry.CYLINDRICAL => Vector3.ProjectOnPlane(Quaternion.Inverse(m_gGeometry.m_qGeometryRotation) * (transform.position - m_gGeometry.m_ptGeometryOrigin), Vector3.up),
            Geometry.SPHERICAL => Quaternion.Inverse(m_gGeometry.m_qGeometryRotation) * (transform.position - m_gGeometry.m_ptGeometryOrigin),
            _ => Vector3.up,
        };
    }

    void UpdateCollider()
    {
        if ( m_gGeometry.m_iGeometry != Geometry.CARTESIAN )
        {
            if ( m_vNewExposedNormals.Any() )
            {
                if ( !TryGetComponent( out MeshCollider mc ) )
                {
                    mc = gameObject.AddComponent<MeshCollider>();
                    mc.convex = true;
                }
                mc.sharedMesh = GetComponent<MeshFilter>().mesh;
            }
            else if ( TryGetComponent( out MeshCollider mc ) )
                Destroy( mc );
        }
        else
        {
            if ( m_vNewExposedNormals.Any() )
            {
                if ( !TryGetComponent( out BoxCollider bc ) )
                    bc = gameObject.AddComponent<BoxCollider>();

                Vector3 vBoxSize = Vector3.zero;
                for ( int i = 0; i < 3; ++i )
                    vBoxSize[ i ] = m_gGeometry.m_vSideLength[ i ] / transform.lossyScale[ i ];
                bc.size = vBoxSize;
            }
            else if ( TryGetComponent( out BoxCollider bc ) )
                Destroy( bc );
        }
    }

    Vector3 GetFaceNormal( int i )
    {
        if ( i % 6 != 0 )
            throw new ArgumentException( nameof(i) + " = " + i + ": not a valid face index - must be multiple of 6" );
        Vector3[] vPoints = new Vector3[] {
            m_pvVertices[ m_piTriangles[ i ] ],
            m_pvVertices[ m_piTriangles[ i + 1 ] ],
            m_pvVertices[ m_piTriangles[ i + 2 ] ],
            m_pvVertices[ m_piTriangles[ i + 3 ] ],
            m_pvVertices[ m_piTriangles[ i + 4 ] ],
            m_pvVertices[ m_piTriangles[ i + 5 ] ],
        };
        Vector3 A = vPoints[ 1 ] - vPoints[ 0 ];
        Vector3 B = vPoints[ 2 ] - vPoints[ 0 ];
        Vector3 C = vPoints[ 5 ] - vPoints[ 3 ];
        Vector3 D = vPoints[ 4 ] - vPoints[ 3 ];

        Vector3 vTriangleNorm1 = Vector3.Cross( A, B ).normalized;
        Vector3 vTriangleNorm2 = Vector3.Cross( C, D ).normalized;

        // for degenerate points, normal should be zero but cross may not be in specific cases
        if ( vPoints[ 1 ] == vPoints[ 2 ] )
            vTriangleNorm1 = Vector3.zero;
        if ( vPoints[ 4 ] == vPoints[ 5 ] )
            vTriangleNorm2 = Vector3.zero;

        // only return zero if both triangle norms are zero
        return vTriangleNorm1 == Vector3.zero ? -vTriangleNorm2 : vTriangleNorm1;
    }


    void OnDisable()
    {
        // Unity Docs:
        //    It is your responsibility to destroy the automatically instantiated mesh when the game object is being destroyed.
        //    Resources.UnloadUnusedAssets also destroys the mesh but it is usually only called when loading a new level.
        RemoveMesh();
    }
}