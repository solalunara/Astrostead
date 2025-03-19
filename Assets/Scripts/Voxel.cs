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

public class Voxel : MonoBehaviour
{
    // we assume for all voxels that the transform.position will be in the centre of the object
    public VoxelGroup OwningGroup
    { 
        get => m_pOwningGroup;
        set
        {
            if ( m_pOwningGroup != value )
            {
                m_pOwningGroup = value;
                RefreshVertices();
                RefreshTriangles();
            }
        }
    }

    public float DeltaV => m_fDeltaV;
    public float DeltaW => m_fDeltaW;

    public (uint, uint, uint) UVW
    {
        get => new( U, V, W );
        set
        {
            m_u = value.Item1;
            m_v = value.Item2;
            m_w = value.Item3;
            RefreshVertices();
            RefreshTriangles();
        }
    }

    public uint U
    { 
        get => m_u; 
        set
        {
            m_u = value;
            RefreshVertices();
            RefreshTriangles();
        }
    }
    public uint V
    { 
        get => m_v; 
        set
        {
            m_v = value;
            RefreshVertices();
            RefreshTriangles();
        }
    }
    public uint W
    { 
        get => m_w; 
        set
        {
            m_w = value;
            RefreshVertices();
            RefreshTriangles();
        }
    }

    uint m_u;
    uint m_v;
    uint m_w;

    public bool m_bCalculatingExposedNormals = false;

    VoxelGroup m_pOwningGroup;
    public BlockType Block
    {
        get => m_iType;
        set
        {
            m_iType = value;
            RefreshUVs();
        }
    }

    Vector3[] m_pvVertices;
    readonly Vector2[][] m_ppvUV = new Vector2[ 8 ][];
    int[] m_piTriangles;
    BlockType m_iType;

    // for cylindrical and spherical, the delta won't exactly match the size
    // it starts at pi/2 and halves as neccesary
    float m_fDeltaV;
    float m_fDeltaW;

    readonly Vector3[] m_pvNormals = new Vector3[ 6 ];

    void Awake()
    {
        m_pvVertices = GetCubeVertices();
        for ( int i = 0; i < 8; ++i )
            m_ppvUV[ i ] = GetCubeUV( i );
        m_piTriangles = new int[ 0 ];
    }

    List<Vector3> GetExposedNormals()
    {
        /*
        List<Vector3> pVoxelNorms = m_pvNormals.ToList();
        pVoxelNorms.RemoveAll( v => v == Vector3.zero );

        if ( !m_pOwningGroup )
            return new();

        foreach ( Vector3 vNorm in m_pOwningGroup.GetNearestNeighborNormals( this ) )
            if ( pVoxelNorms.Contains( GetNormalClosestToVector( vNorm ) ) ) //closest so that it's an exact match
                pVoxelNorms.Remove( GetNormalClosestToVector( vNorm ) );

        return pVoxelNorms;
        */

        HashSet<Vector3> pExposedNorms = new( m_pOwningGroup.GetExposedNeighborNormals( this ) );
        return pExposedNorms.ToList();
    }

    Mesh GetOrAddMesh()
    {
        if ( !TryGetComponent( out MeshFilter m ) )
            m = gameObject.AddComponent<MeshFilter>();

        if ( m.sharedMesh )
            return m.sharedMesh;
        else
            return m.mesh;
    }

    void RemoveMesh()
    {
        if ( TryGetComponent( out MeshFilter m ) )
        {
            if ( m.sharedMesh )
            {
                m.sharedMesh.Clear();
                DestroyImmediate( m.sharedMesh );
            }
        }
    }

    public void RefreshVertices()
    {
        if ( !m_pOwningGroup )
            return;

        Vector3[] pVerts = GetCubeVertices();
        Vector3 vSideLength = m_pOwningGroup.VoxelSize;
        switch ( m_pOwningGroup.GetGeometry() )
        {
            case Geometry.CARTESIAN:
                Vector3 vDirectionalSideLength = Vector3.zero;
                for ( int i = 0; i < pVerts.Length; ++i )
                {
                    for ( int j = 0; j < 3; ++j )
                        vDirectionalSideLength[ j ] = pVerts[ i ][ j ] > 0 ? vSideLength[ j ] : -vSideLength[ j ];
                    pVerts[ i ] = vDirectionalSideLength / 2;
                }
                m_fDeltaV = vSideLength[ 1 ];
                m_fDeltaW = vSideLength[ 2 ];
                break;

            case Geometry.CYLINDRICAL:
            {
                Vector3 ptLocalOrigin = transform.localPosition;
                for ( int i = 0; i < 3; ++i ) 
                    ptLocalOrigin[ i ] *= transform.parent.localScale[ i ];
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

                m_fDeltaV = vSideLength[ 1 ];
                m_fDeltaW = fDeltaTheta;
                break;
            }

            case Geometry.SPHERICAL:
            {
                Vector3 ptLocalOrigin = transform.localPosition;
                for ( int i = 0; i < 3; ++i ) 
                    ptLocalOrigin[ i ] *= transform.parent.localScale[ i ];
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
                while ( fInnerRadius * Mathf.Sin( fTheta ) * fDeltaPhiInner > vSideLength[ 2 ] )
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

                m_fDeltaV = fDeltaTheta;
                m_fDeltaW = fDeltaPhi;
                break;
            }
        }

        m_pvVertices = pVerts;

        if ( m_piTriangles.Any() )
        {
            Mesh m = GetOrAddMesh();
            m.vertices = m_pvVertices;
            m.RecalculateBounds();
            m.RecalculateNormals();
            m.RecalculateTangents();
            UpdateCollider();
        }

        int[] piTriangles = GetCubeTriangles();
        for ( int i = 0; i < m_pvNormals.Length; ++i )
        {
            bool bDegenerate = false;
            Vector3 vNorm = GetFaceNormal( i * 6, piTriangles );
            for ( int j = 0; j < i; ++j )
                if ( Vector3.Dot( m_pvNormals[ j ], vNorm ) > 0.97f )
                    bDegenerate = true;
            m_pvNormals[ i ] = bDegenerate ? Vector3.zero : vNorm;
        }
    }

    public void RefreshUVs()
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

        int[] piTriangles = GetCubeTriangles();
        for ( int i = 0; i < 8; ++i )
        {
            Vector2[] ppvUV = GetCubeUV( i );

            if ( !m_ppvUV[ i ].Any() )
                continue;

            for ( int j = 0; j < piTriangles.Length; j += 6 )
            {
                for ( int n = 0; n < 6; ++n )
                {
                    for ( int k = 0; k < 2; ++k )
                    {
                        if ( Vector3.Dot( GetFaceNormal( j, piTriangles ), vUpVector ) > 0.97f )
                            ppvUV[ piTriangles[ j + n ] ][ k ] = m_ppvUV[ i ][ piTriangles[ j + n ] ][ k ] > 0.5f ? vTopTextureMaxs[ k ] : vTopTextureMins[ k ];
                        else if ( Vector3.Dot( GetFaceNormal( j, piTriangles ), vUpVector ) < -0.97f )
                            ppvUV[ piTriangles[ j + n ] ][ k ] = m_ppvUV[ i ][ piTriangles[ j + n ] ][ k ] > 0.5f ? vBottomTextureMaxs[ k ] : vBottomTextureMins[ k ];
                        else
                            ppvUV[ piTriangles[ j + n ] ][ k ] = m_ppvUV[ i ][ piTriangles[ j + n ] ][ k ] > 0.5f ? vSideTextureMaxs[ k ] : vSideTextureMins[ k ];
                    }
                }
            }

            m_ppvUV[ i ] = ppvUV;
        }

        if ( m_piTriangles.Any() )
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

    public void RefreshTriangles()
    {
        if ( !m_bCalculatingExposedNormals || !m_pOwningGroup )
            return;

        var piAllTriangles = GetCubeTriangles();
        List<int> piTriangles = new();
        foreach ( Vector3 vNorm in GetExposedNormals() )
            for ( int i = 0; i < m_pvNormals.Length; ++i )
                if ( Vector3.Dot( m_pvNormals[ i ], vNorm ) > 0.97f )
                    for ( int u = 0; u < 6; ++u )
                        piTriangles.Add( piAllTriangles[ i * 6 + u ] );


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

    public Vector3 GetUpVector() 
    {
        return m_pOwningGroup.GetGeometry() switch
        {
            Geometry.CARTESIAN => Vector3.up,
            Geometry.CYLINDRICAL => Vector3.ProjectOnPlane( transform.localPosition, Vector3.up ).normalized,
            Geometry.SPHERICAL => transform.localPosition.normalized,
            _ => Vector3.up,
        };
    }

    void UpdateCollider()
    {
        if ( !m_pOwningGroup )
            return;

        if ( m_pOwningGroup.GetGeometry() != Geometry.CARTESIAN )
        {
            if ( m_piTriangles.Any() )
            {
                if ( !TryGetComponent( out MeshCollider mc ) )
                {
                    mc = gameObject.AddComponent<MeshCollider>();
                    mc.convex = true;
                }
                mc.enabled = true;
                mc.sharedMesh = GetComponent<MeshFilter>().sharedMesh;
            }
            else if ( TryGetComponent( out MeshCollider mc ) )
                Destroy( mc );
        }
        else
        {
            if ( m_piTriangles.Any() )
            {
                if ( !TryGetComponent( out BoxCollider bc ) )
                    bc = gameObject.AddComponent<BoxCollider>();

                Vector3 vBoxSize = Vector3.zero;
                for ( int i = 0; i < 3; ++i )
                    vBoxSize[ i ] = m_pOwningGroup.VoxelSize[ i ] / transform.lossyScale[ i ];
                bc.size = vBoxSize;
                bc.enabled = true;
            }
            else if ( TryGetComponent( out BoxCollider bc ) )
                Destroy( bc );
        }
    }

    Vector3 GetFaceNormal( int i, int[] piTriangles )
    {
        if ( i % 6 != 0 )
            throw new ArgumentException( nameof(i) + " = " + i + ": not a valid face index - must be multiple of 6" );
        Vector3[] vPoints = new Vector3[] {
            m_pvVertices[ piTriangles[ i ] ],
            m_pvVertices[ piTriangles[ i + 1 ] ],
            m_pvVertices[ piTriangles[ i + 2 ] ],
            m_pvVertices[ piTriangles[ i + 3 ] ],
            m_pvVertices[ piTriangles[ i + 4 ] ],
            m_pvVertices[ piTriangles[ i + 5 ] ],
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

    public Vector3 GetNormalClosestToVector( Vector3 v )
    {
        Vector3 vNorm = Vector3.zero;
        for ( int i = 0; i < m_pvNormals.Length; ++i )
            if ( Vector3.Dot( m_pvNormals[ i ], v ) >= Vector3.Dot( vNorm, v ) )
                vNorm = m_pvNormals[ i ];
        return vNorm;
    }

    void OnDisable()
    {
        if ( m_pOwningGroup.m_bNotifyNeighborsOnVoxelDelete )
            m_pOwningGroup.BreakVoxel( this, true );
        // Unity Docs:
        //    It is your responsibility to destroy the automatically instantiated mesh when the game object is being destroyed.
        //    Resources.UnloadUnusedAssets also destroys the mesh but it is usually only called when loading a new level.
        RemoveMesh();
        if ( TryGetComponent( out Collider c ) )
            c.enabled = false;
    }
}