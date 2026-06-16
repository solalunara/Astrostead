#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Unity.Jobs;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEngine;
using UnityEngine.UIElements;
using static Statics;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public abstract class VoxelGroup : MonoBehaviour
{
    public bool m_bNotifyNeighborsOnVoxelDelete = false;
    public Vector3 VoxelSize => m_vVoxelSize;
    [SerializeField] protected Vector3 m_vVoxelSize;
    // Cartesian:    u - x, v - y, w - z
    // Cylindrical:  u - r, v - y, w - theta
    // Spherical:    u - r, v - theta, w - phi 

    public Voxel? GetVoxel( Vector3Int vPos ) 
    {
        if ( m_pVoxels.ContainsKey( vPos ) )
            return m_pVoxels[ vPos ];
        else
            return null;
    }

    public IEnumerable<Voxel> GetVoxels() 
    {
        foreach ( Voxel pVoxel in m_pVoxels.Values )
            yield return pVoxel;
    }

    public abstract Geometry GetGeometry();

    public abstract Vector3 IndexToLocalCoordinate( Vector3Int vPos );

    public abstract Vector3Int TravelAlongDirection( Vector3Int vPos, Vector3Int vDir );

    public void CombineMeshes()
    {
        List<CombineInstance> pCombine = new();
        foreach ( Voxel pVoxel in GetVoxels() )
        {
            if ( pVoxel.TryGetComponent( out MeshFilter mf ) && mf.sharedMesh )
                pCombine.Add( new() 
                {
                    mesh = mf.sharedMesh,
                    transform = Matrix4x4.TRS( pVoxel.transform.localPosition, pVoxel.transform.localRotation, pVoxel.transform.localScale ),
                } );
        }
        Mesh mesh = new()
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        mesh.CombineMeshes( pCombine.ToArray() );
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().enabled = true;

        // disabling voxel deletes all render data, thus do it only once we've combined the meshes
        foreach ( Voxel pVoxel in GetVoxels() )
            pVoxel.gameObject.SetActive( false );
    }

    public void DeCombine()
    {
        foreach ( Voxel pVoxel in GetVoxels() )
            pVoxel.gameObject.SetActive( true );
        RemoveMesh();
        GetComponent<MeshRenderer>().enabled = false;
    }

    public IEnumerable<Voxel> GetVoxelNeighbors( Voxel pVoxel ) => GetVoxelNeighbors( pVoxel.Position );
    public IEnumerable<Voxel> GetVoxelNeighbors( Vector3Int vPos )
    {
        foreach ( (Vector3Int vNeighborPos, Vector3Int vNeighborDir) in GetNeighbors( vPos ) )
        {
            if ( !m_pVoxels.ContainsKey( vNeighborPos ) )
                continue; //neighbor not in geometry

            Voxel? pNeighborVoxel = GetVoxel( vNeighborPos );
            if ( pNeighborVoxel != null )
                yield return pNeighborVoxel;
            else
            {
                Voxel? pRealNeighbor = GetAssociatedRealBlock( vNeighborPos );
                if ( pRealNeighbor != null )
                    yield return pRealNeighbor;
            }
        }
    }

    /// <summary>
    /// Get neighbors for a voxel, filtering either for neighbors that are solid voxels or air blocks
    /// Returns a set of tuples, the first element is the index of the neighbor, the second is the direction its found in
    /// </summary>
    /// <param name="vPos">The u, v, and w coordinates as a Vector3Int of the item to search the neighbors of</param>
    protected abstract IEnumerable<(Vector3Int, Vector3Int)> GetNeighbors( Vector3Int vPos );
    protected virtual IEnumerable<Vector3Int> GetAssociatedGhostBlocks( Voxel pVoxel ) => Enumerable.Empty<Vector3Int>();
    protected virtual Voxel? GetAssociatedRealBlock( Vector3Int vGhostBlockPos ) => null;
    public void BreakVoxel( Voxel pVoxel, bool bCalledFromVoxelDisable = false )
    {
        Vector3Int vPos = pVoxel.Position;
        if ( !m_pVoxels.ContainsKey( vPos ) || m_pVoxels[ vPos ] != pVoxel )
            return;

        foreach ( Vector3Int vGhostBlockPos in GetAssociatedGhostBlocks( pVoxel ) )
            m_pVoxels.Remove( vGhostBlockPos );
        
        m_pVoxels.Remove( vPos );

        HashSet<Voxel> pVoxelsNeedingUpdating = new();
        foreach ( Voxel pNN in GetVoxelNeighbors( pVoxel ) )
            pVoxelsNeedingUpdating.Add( pNN );

        foreach ( Vector3Int vGhostBlockPos in GetAssociatedGhostBlocks( pVoxel ) )
            foreach ( Voxel pNN in GetVoxelNeighbors( vGhostBlockPos ) )
                pVoxelsNeedingUpdating.Add( pNN );

        foreach ( Voxel pNN in pVoxelsNeedingUpdating )
            pNN.RefreshTriangles();

        if ( !bCalledFromVoxelDisable )
            Destroy( pVoxel.gameObject );
    }
    public void AddVoxelToGroup( Voxel pVoxel, bool bIgnoreOcclusionCheck = false )
    {
        if ( m_pVoxels.ContainsKey( pVoxel.Position ) )
            throw new ArgumentException( $"Cannot create voxel at already filled position {pVoxel.Position.x}, {pVoxel.Position.y}, {pVoxel.Position.z}", nameof( pVoxel ) );
        
        m_pVoxels.Add( pVoxel.Position, pVoxel );

        if ( !bIgnoreOcclusionCheck )
            foreach ( Voxel pNeighbors in GetVoxelNeighbors( pVoxel ) )
                pNeighbors.RefreshTriangles(); // re-check exposed surfaces and occlude if we should

        pVoxel.OwningGroup = this;
    }
    public void AddGhostVoxel( Vector3Int vPosition )
    {
        if ( m_pVoxels.ContainsKey( vPosition ) )
            throw new ArgumentException( $"Cannot create voxel at already filled position {vPosition.x}, {vPosition.y}, {vPosition.z}", nameof( vPosition ) );

        m_pVoxels.Add( vPosition, null );
    }

    public virtual IEnumerable<Vector3> GetCornersAtIndex( Vector3Int vPos )
    {
        // order: center, forward, right, up, forward+right, forward+up, up+right, forward+up+right
        // TravelAlongDirection is neccesary for non-cartesian geometries
        //     e.g. in cylindrical, [ i_r=0, i_y, i_theta=4 ] + [ 1, 0, 0 ] is not neccesarily [ i_r=1, i_y, i_theta=4 ], 
        //          it could be [ i_r=1, i_y, i_theta=8 ], or any multiple of 2 times the original i_theta depending on how many theta divisions we have at that radius
        yield return IndexToLocalCoordinate( vPos );
        yield return IndexToLocalCoordinate( TravelAlongDirection( vPos, Vector3Int.forward ) );
        yield return IndexToLocalCoordinate( TravelAlongDirection( vPos, Vector3Int.right ) );
        yield return IndexToLocalCoordinate( TravelAlongDirection( vPos, Vector3Int.up ) );
        yield return IndexToLocalCoordinate( TravelAlongDirection( TravelAlongDirection( vPos, Vector3Int.forward ), Vector3Int.right ) );
        yield return IndexToLocalCoordinate( TravelAlongDirection( TravelAlongDirection( vPos, Vector3Int.forward ), Vector3Int.up ) );
        yield return IndexToLocalCoordinate( TravelAlongDirection( TravelAlongDirection( vPos, Vector3Int.up ), Vector3Int.right ) );
        yield return IndexToLocalCoordinate( TravelAlongDirection( TravelAlongDirection( TravelAlongDirection( vPos, Vector3Int.forward ), Vector3Int.up ), Vector3Int.right ) );
    }

    public IEnumerable<int> GetExposedFaces( Voxel pVoxel )
    {
        foreach ( (Vector3Int vNeighborPos, Vector3Int vNeighborDir) in GetNeighbors( pVoxel.Position ) )
        {
            if ( m_pVoxels.ContainsKey( vNeighborPos ) )
                continue;

            int? iExposedFace = pVoxel.GetFaceFacingDirection( vNeighborDir );
            if ( iExposedFace == null )
            {
                Debug.LogError( $"Could not find face for direction {vNeighborDir.x}, {vNeighborDir.y}, {vNeighborDir.z}" );
                continue;
            }
            yield return (int)iExposedFace;
        }
    }

    void RemoveMesh()
    {
        if ( TryGetComponent( out MeshFilter m ) )
        {
            if ( m.sharedMesh )
            {
                m.sharedMesh.Clear();
                Destroy( m.sharedMesh );
            }
        }
    }

    void OnDisable()
    {
        // Unity Docs:
        //    It is your responsibility to destroy the automatically instantiated mesh when the game object is being destroyed.
        //    Resources.UnloadUnusedAssets also destroys the mesh but it is usually only called when loading a new level.
        //RemoveMesh();
    }

    protected Dictionary<Vector3Int, Voxel> m_pVoxels = new();
}