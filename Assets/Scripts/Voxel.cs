#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http.Headers;
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
    DIRT,
    CHEESE_TOP,
    CHEESE
}

public class Voxel : MonoBehaviour
{
    // we assume for all voxels that the transform.position will be in the centre of the object
    public VoxelGroup? OwningGroup
    { 
        get => m_pOwningGroup;
        set
        {
            if ( m_pOwningGroup != value )
            {
                m_pOwningGroup = value;
                RefreshVertices();
                RefreshTriangles();
                RefreshUVs();
            }
        }
    }
    private VoxelGroup? m_pOwningGroup;

    public Vector3Int Position
    {
        get => m_vPos;
        set
        {
            m_vPos = value;
            RefreshVertices();
            RefreshTriangles();
        }
    }
    [SerializeField] private Vector3Int m_vPos;

    public bool m_bCalculatingExposedNormals = false;

    public BlockType Block
    {
        get => m_iType;
        set
        {
            m_iType = value;
            RefreshUVs();
        }
    }
    private BlockType m_iType;

    private Vector3[] m_pvVertices = new Vector3[ 0 ];
    private readonly Vector2[][] m_ppvUV = new Vector2[ 8 ][];
    private int[] m_piTriangles = new int[ 0 ];

    void Awake()
    {
        m_pvVertices = GetCubeVertices();
        for ( int i = 0; i < 8; ++i )
            m_ppvUV[ i ] = GetCubeUV( i );
        m_piTriangles = new int[ 0 ];
    }

    List<int> GetExposedFaces()
    {
        if ( m_pOwningGroup == null )
        {
            Debug.LogError( "Trying to get exposed faces of a voxel with no owning group - this is probably an error" );
            return new();
        }

        return m_pOwningGroup.GetExposedFaces( this ).ToList();
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

    IEnumerable<Vector3> GetCornersFromVoxelGroup()
    {
        if ( m_pOwningGroup == null )
        {
            Debug.LogError( "Trying to get corners of a voxel with no owning group - this is probably an error" );
            yield break;
        }

        foreach ( Vector3 vPos in m_pOwningGroup.GetCornersAtIndex( Position ) )
            yield return vPos;
    }

    public Vector3 Centre
    {
        get
        {
            if ( m_vCentre == null )
                m_vCentre = GetCentrePositionFromCorners( GetCornersFromVoxelGroup() );
            return (Vector3)m_vCentre;
        }
    }
    private Vector3? m_vCentre;

    public void RefreshVertices()
    {
        if ( !m_pOwningGroup )
            return;

        Vector3[] pvCorners = GetCornersFromVoxelGroup().ToArray();
        m_vCentre = GetCentrePositionFromCorners( pvCorners );
        m_pvVertices = VerticesFromCorners( pvCorners ).ToArray();
        for ( int i = 0; i < m_pvVertices.Length; ++i )
            m_pvVertices[ i ] -= pvCorners[ 0 ]; // convert to local space for mesh

        if ( m_piTriangles.Any() )
        {
            Mesh m = GetOrAddMesh();
            m.vertices = m_pvVertices;
            m.RecalculateBounds();
            m.RecalculateNormals();
            m.RecalculateTangents();
            UpdateCollider();
        }
    }

    public void RefreshUVs()
    {
        if ( m_pOwningGroup == null )
            return;

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
            case BlockType.CHEESE_TOP:
            {
                (vTopTextureMins, vTopTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.LIGHT_CHEESE );
                (vSideTextureMins, vSideTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.DARK_CHEESE );
                (vBottomTextureMins, vBottomTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.DARK_CHEESE );
                break;
            }
            case BlockType.CHEESE:
            {
                (vTopTextureMins, vTopTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.DARK_CHEESE );
                (vSideTextureMins, vSideTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.DARK_CHEESE );
                (vBottomTextureMins, vBottomTextureMaxs) = GetStandardAtlasTextureMinsMaxs( StandardAtlasTextures.DARK_CHEESE );
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
        List<int> piExposedFaces = GetExposedFaces();
        foreach ( int iExposedFace in piExposedFaces )
            for ( int u = 0; u < 6; ++u )
                piTriangles.Add( piAllTriangles[ iExposedFace * 6 + u ] );


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
        if ( m_pOwningGroup == null )
        {
            Debug.LogError( "Trying to get up vector of a voxel with no owning group - this is probably an error" );
            return Vector3.zero;
        }

        return m_pOwningGroup.GetGeometry() switch
        {
            Geometry.CARTESIAN => Vector3.up,
            Geometry.CYLINDRICAL => Vector3.ProjectOnPlane( Centre, Vector3.up ).normalized,
            Geometry.SPHERICAL => Centre.normalized,
            _ => Vector3.up,
        };
    }

    void UpdateCollider()
    {
        if ( m_pOwningGroup == null || m_pOwningGroup.GetGeometry() != Geometry.CARTESIAN )
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
        else // box colliders are presumably much cheaper, use them if we can (cartesian geometry)
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

    public int? GetFaceFacingDirection( Vector3Int vDirection )
    {
        // m_pvNormals are in order of cube triangles, so compare with cube vertices
        Vector3[] pvCubeVertices = GetCubeVertices();
        int[] piCubeTriangles = GetCubeTriangles();
        for ( int i = 0; i < 6; ++i )
        {
            Vector3 vCubeFaceNormal = GetFaceNormal( i * 6, piCubeTriangles, pvCubeVertices ); // approximately -1, 0, or 1 for each element
            Vector3Int vCubeFaceNormalDirection = new( Mathf.RoundToInt( vCubeFaceNormal.x ), Mathf.RoundToInt( vCubeFaceNormal.y ), Mathf.RoundToInt( vCubeFaceNormal.z ) );
            if ( vDirection == vCubeFaceNormalDirection )
                return i;
        }
        return null;
    }

    Vector3 GetFaceNormal( int i, int[] piTriangles, Vector3[]? pvVertices = null )
    {
        pvVertices ??= m_pvVertices;

        if ( i % 6 != 0 )
            throw new ArgumentException( nameof(i) + " = " + i + ": not a valid face index - must be multiple of 6" );
        Vector3[] vPoints = new Vector3[] {
            pvVertices[ piTriangles[ i ] ],
            pvVertices[ piTriangles[ i + 1 ] ],
            pvVertices[ piTriangles[ i + 2 ] ],
            pvVertices[ piTriangles[ i + 3 ] ],
            pvVertices[ piTriangles[ i + 4 ] ],
            pvVertices[ piTriangles[ i + 5 ] ],
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

        // if both are zero, return zero - this is a degenerate face and we have no normal information to give
        if ( vTriangleNorm1 == Vector3.zero && vTriangleNorm2 == Vector3.zero )
            return Vector3.zero;

        // by convention defined here, the normal should always point outward from the centre of the voxel
        Vector3 vTriangleCentre1 = (vPoints[ 0 ] + vPoints[ 1 ] + vPoints[ 2 ]) / 3.0f;
        Vector3 vTriangleCentre2 = (vPoints[ 3 ] + vPoints[ 4 ] + vPoints[ 5 ]) / 3.0f;
        Vector3 vFaceCentre = (vTriangleCentre1 + vTriangleCentre2) / 2.0f;

        // we want to find the center of all the vertices in pvVertices and subtract that from the face centre
        // each vertex of the cube is counted 3 times, which isn't ideal but its probably cheaper to average than to use a HashSet
        Vector3 vVerticesCentre = Vector3.zero;
        for ( int u = 0; u < pvVertices.Length; ++u )
            vVerticesCentre += pvVertices[ u ];
        vVerticesCentre /= pvVertices.Length;
        vFaceCentre -= vVerticesCentre;

        // Sometimes one triangle will be degenerate and the other won't be. Here we know they aren't both degenerate,
        // so we can use the non-degenerate triangle's normal, ensuring it points outward from the centre of the voxel.
        Vector3 vNorm = vTriangleNorm1 != Vector3.zero ? vTriangleNorm1 : vTriangleNorm2;
        return vNorm * Mathf.Sign( Vector3.Dot( vFaceCentre, vNorm ) );
    }

    void OnDisable()
    {
        if ( m_pOwningGroup != null && m_pOwningGroup.m_bNotifyNeighborsOnVoxelDelete )
            m_pOwningGroup.BreakVoxel( this, true );
        // Unity Docs:
        //    It is your responsibility to destroy the automatically instantiated mesh when the game object is being destroyed.
        //    Resources.UnloadUnusedAssets also destroys the mesh but it is usually only called when loading a new level.
        RemoveMesh();
        if ( TryGetComponent( out Collider c ) )
            c.enabled = false;
    }
}