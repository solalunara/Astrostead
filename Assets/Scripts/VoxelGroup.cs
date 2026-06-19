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

    public IVoxelBase? GetVoxel( Vector3Int vPos ) 
    {
        if ( m_pVoxels.ContainsKey( vPos ) )
            return m_pVoxels[ vPos ];
        else
            return null;
    }

    public IEnumerable<IVoxelBase> GetVoxels() 
    {
        foreach ( IVoxelBase pVoxel in m_pVoxels.Values )
            yield return pVoxel;
    }

    public abstract Geometry GetGeometry();

    public abstract Vector3 IndexToLocalCoordinate( Vector3Int vPos );

    public abstract Vector3Int TravelAlongDirection( Vector3Int vPos, Vector3Int vDir );

    public void CombineMeshes()
    {
        List<CombineInstance> pCombine = new();
        foreach ( IVoxelBase pVoxel in GetVoxels() )
        {
            Mesh m = pVoxel.GetOrAddMesh();
            if ( m )
                pCombine.Add( new() 
                {
                    mesh = m,
                    transform = Matrix4x4.TRS( pVoxel.GetTransform().localPosition, pVoxel.GetTransform().localRotation, pVoxel.GetTransform().localScale ),
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
        foreach ( IVoxelBase pVoxel in GetVoxels() )
            pVoxel.SetActive( false );
    }

    public void DeCombine()
    {
        foreach ( IVoxelBase pVoxel in GetVoxels() )
            pVoxel.SetActive( true );
        RemoveMesh();
        GetComponent<MeshRenderer>().enabled = false;
    }

    public IEnumerable<IVoxelBase> GetVoxelNeighbors( IVoxelBase pVoxel ) => GetVoxelNeighbors( pVoxel.Position );
    public IEnumerable<IVoxelBase> GetVoxelNeighbors( Vector3Int vPos )
    {
        foreach ( (Vector3Int vNeighborPos, Vector3Int vNeighborDir) in GetNeighbors( vPos ) )
        {
            if ( !m_pVoxels.ContainsKey( vNeighborPos ) )
                continue; //neighbor not in geometry

            IVoxelBase? pNeighborVoxel = GetVoxel( vNeighborPos );
            Debug.Assert( pNeighborVoxel is not GhostVoxel ); //assert not ghost voxel
            if ( pNeighborVoxel != null )
                yield return pNeighborVoxel;
        }
    }

    /// <summary>
    /// Get neighbors for a voxel, filtering either for neighbors that are solid voxels or air blocks
    /// Returns a set of tuples, the first element is the index of the neighbor, the second is the direction its found in
    /// </summary>
    /// <param name="vPos">The u, v, and w coordinates as a Vector3Int of the item to search the neighbors of</param>
    protected abstract IEnumerable<(Vector3Int, Vector3Int)> GetNeighbors( Vector3Int vPos );
    protected virtual IEnumerable<Vector3Int> GetAssociatedGhostBlocks( Vector3Int pVoxel )
    {
        yield break;
    }
    protected virtual Vector3Int RealBlock( Vector3Int vBlockPos ) => vBlockPos;
    protected bool IsRealBlock( Vector3Int vPos ) => RealBlock( vPos ) == vPos;

    public virtual void BreakVoxel( Voxel pVoxel, bool bCalledFromVoxelDisable = false )
    {
        Vector3Int vPos = pVoxel.Position;

        if ( !m_pVoxels.ContainsKey( vPos ) || m_pVoxels[ vPos ] != (IVoxelBase)pVoxel )
            return;

        foreach ( Vector3Int vGhostBlockPositions in GetAssociatedGhostBlocks( pVoxel.Position ) )
            if ( m_pVoxels.ContainsKey( vGhostBlockPositions ) )
                m_pVoxels.Remove( vGhostBlockPositions );
        
        m_pVoxels.Remove( vPos );

        foreach ( IVoxelBase pNN in GetVoxelNeighbors( pVoxel ) )
            pNN.RefreshTriangles();

        if ( !bCalledFromVoxelDisable )
            Destroy( pVoxel.gameObject );
    }
    public void AddVoxelToGroup( IVoxelBase pVoxel, bool bIgnoreOcclusionCheck = false )
    {
        if ( m_pVoxels.ContainsKey( pVoxel.Position ) )
            throw new ArgumentException( $"Cannot create voxel at already filled position {pVoxel.Position.x}, {pVoxel.Position.y}, {pVoxel.Position.z}", nameof( pVoxel ) );
        
        m_pVoxels.Add( pVoxel.Position, pVoxel );

        if ( !bIgnoreOcclusionCheck )
            foreach ( IVoxelBase pNeighbors in GetVoxelNeighbors( pVoxel ) )
                pNeighbors.RefreshTriangles(); // re-check exposed surfaces and occlude if we should

        pVoxel.OwningGroup = this;
    }
    public void AddGhostVoxel( Vector3Int vPosition )
    {
        if ( m_pVoxels.ContainsKey( vPosition ) )
            throw new ArgumentException( $"Cannot create voxel at already filled position {vPosition.x}, {vPosition.y}, {vPosition.z}", nameof( vPosition ) );
        
        if ( IsRealBlock( vPosition ) )
            throw new ArgumentException( $"Cannot create ghost voxel at non-ghost position {vPosition.x}, {vPosition.y}, {vPosition.z}", nameof( vPosition ) );

        IVoxelBase? pRealVoxel = GetVoxel(RealBlock(vPosition)) ?? throw new ArgumentException( $"Can only create ghost voxel when real block already exists. Ghost position {vPosition.x}, {vPosition.y}, {vPosition.z}", nameof(vPosition) );
        Debug.Assert( pRealVoxel is Voxel );
        m_pVoxels.Add( vPosition, new GhostVoxel( (Voxel)pRealVoxel ) );
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

    protected Dictionary<Vector3Int, IVoxelBase> m_pVoxels = new();
}