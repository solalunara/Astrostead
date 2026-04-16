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

/*
public class SphericalVoxelGroup : VoxelGroup
{
    public override float GetDeltaV(int u)
    {
        float fSDeltaRadius = GetDeltaU();
        float fSRadius = u * fSDeltaRadius + fSDeltaRadius / 2.0f;
        float fOuterRadius = fSRadius + fSDeltaRadius / 2.0f;
        float fDeltaTheta = Mathf.PI / 2.0f;
        while ( fOuterRadius * fDeltaTheta > m_vVoxelSize[ 1 ] )
            fDeltaTheta /= 2.0f;
        return fDeltaTheta;
    }

    public override float GetDeltaW(int u, int v)
    {
        float fSDeltaRadius = GetDeltaU();
        float fSRadius = u * fSDeltaRadius + fSDeltaRadius / 2.0f;
        float fOuterRadius = fSRadius + fSDeltaRadius / 2.0f;

        float fDeltaTheta = GetDeltaV( u );
        float fTheta = v * fDeltaTheta + fDeltaTheta / 2.0f;

        float fDeltaPhi = Mathf.PI / 2.0f;
        while ( fOuterRadius * Mathf.Sin( fTheta ) * fDeltaPhi > m_vVoxelSize[ 2 ] )
            fDeltaPhi /= 2.0f;

        return fDeltaPhi;
    }

    public override Geometry GetGeometry() { return Geometry.SPHERICAL; }

    public override Voxel GetVoxel(int u, int v, int w)
    {
        if ( u < 0 ) return null;
        if ( !m_pVoxels.ContainsKey( (uint)u ) ) return null;

        var double_buffer = m_pVoxels[(uint)u] ?? throw new ArgumentNullException("the double buffer should not be null for a valid radius");
        var buffer = double_buffer[v] ?? throw new ArgumentNullException("the buffer should not be null for a valid radius and theta");

        return buffer[ w ];
    }

    public override IEnumerable<Voxel> GetVoxels()
    {
        foreach ( var keyValuePair in m_pVoxels.AsEnumerable() )
        {
            var double_buffer = keyValuePair.Value;
            foreach ( var buffer in double_buffer )
                foreach ( var voxel in buffer )
                    yield return voxel;
        }
    }

    /// protected elements ///

    // r -> voxels( theta( phi ) )
    protected readonly Dictionary<uint, CircularBuffer<CircularBuffer<Voxel>>> m_pVoxels = new();
}

// Ordering: R, Z, Theta
public class CylindricalVoxelGroup : VoxelGroup
{
    public override Geometry GetGeometry()
    {
        return Geometry.CYLINDRICAL;
    }

    public override Voxel GetVoxel(int u, int v, int w)
    {
        if ( u < 0 ) return null;

        var key = ((uint)u, w);
        if ( !m_pVoxels.ContainsKey( key ) ) return null;

        var buffer = m_pVoxels[((uint)u, w)] ?? throw new NullReferenceException("buffer should not be null");
        return buffer[ v ];
    }

    public override IEnumerable<Voxel> GetVoxels()
    {
        foreach ( var keyValuePair in m_pVoxels.AsEnumerable() )
        {
            var buffer = keyValuePair.Value;
            foreach ( var voxel in buffer )
                yield return voxel;
        }
    }

    protected override IEnumerable<triple_int> GetNeighbors(triple_int uvw, bool bAir)
    {
        (int u, int v, int w) = uvw;

        // easy neighbors at +/- z and +/- theta
        var adjacent_indices = new (int u, int v, int w)[] { (u, v, w-1), (u, v, w+1), (u, v-1, w), (u, v+1, w) };
        foreach ( var adjacent_index in adjacent_indices )
        {
            Voxel a = GetVoxel( adjacent_index.u, adjacent_index.v, adjacent_index.w );
            if ( a == null == bAir )
                yield return new triple_int( adjacent_index );
        }

        // if r != 0, easy neighbor at -r
        if ( u > 0 )
        {
            Voxel a = GetVoxel( u-1, v, w );
            if ( a == null == bAir )
                yield return new triple_int( u-1, v, w );
        }
        else if ( u < 0 )
            throw new ArgumentOutOfRangeException( $"Value of u {u} invalid for cylindrical voxel group (cannot be < 0)" );

        // check if we have a buffer for r+1, z so we can check its count vs our count
        if ( !m_pVoxels.ContainsKey( ((uint)u, w) ) )
    }

    /// protected elements ///

    // r, z -> voxels( theta )
    protected readonly Dictionary<(uint, int), CircularBuffer<Voxel>> m_pVoxels = new();
}*/


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

    public IEnumerable<Voxel> GetVoxelNeighbors( Voxel pVoxel )
    {
        foreach ( (Vector3Int vNeighborPos, Vector3Int vNeighborDir) in GetNeighbors( pVoxel.Position ) )
        {
            Voxel? pNeighborVoxel = GetVoxel( vNeighborPos );
            if ( pNeighborVoxel != null )
                yield return pNeighborVoxel;
        }

    }
    public IEnumerable<Vector3Int> GetExposedNeighbors( Voxel pVoxel ) 
    {
        foreach ( (Vector3Int vNeighborPos, Vector3Int vNeighborDir) in GetNeighbors( pVoxel.Position ) )
        {
            Voxel? pNeighborVoxel = GetVoxel( vNeighborPos );
            if ( pNeighborVoxel == null )
                yield return vNeighborPos;
        }
    }

    /// <summary>
    /// Get neighbors for a voxel, filtering either for neighbors that are solid voxels or air blocks
    /// Returns a set of tuples, the first element is the index of the neighbor, the second is the direction its found in
    /// </summary>
    /// <param name="vPos">The u, v, and w coordinates as a Vector3Int of the item to search the neighbors of</param>
    protected abstract IEnumerable<(Vector3Int, Vector3Int)> GetNeighbors( Vector3Int vPos );
    public void BreakVoxel( Voxel pVoxel, bool bCalledFromVoxelDisable = false )
    {
        Vector3Int vPos = pVoxel.Position;
        if ( !m_pVoxels.ContainsKey( vPos ) || m_pVoxels[ vPos ] != pVoxel )
            return;
        
        m_pVoxels.Remove( vPos );
        foreach ( Voxel pNN in GetVoxelNeighbors( pVoxel ) )
            pNN.RefreshTriangles(); // re-check exposed surfaces and un-occlude if we should

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

    public IEnumerable<Vector3> GetCornersAtIndex( Vector3Int vPos )
    {
        // order: center, forward, right, up, forward+right, forward+up, right+up, forward+right+up
        // TravelAlongDirection is neccesary for non-cartesian geometries
        //     e.g. in cylindrical, [ i_r=0, i_y, i_theta=4 ] + [ 1, 0, 0 ] is not neccesarily [ i_r=1, i_y, i_theta=4 ], 
        //          it could be [ i_r=1, i_y, i_theta=8 ], or any multiple of 2 times the original i_theta depending on how many theta divisions we have at that radius
        yield return IndexToLocalCoordinate( vPos );
        yield return IndexToLocalCoordinate( TravelAlongDirection( vPos, Vector3Int.forward ) );
        yield return IndexToLocalCoordinate( TravelAlongDirection( vPos, Vector3Int.right ) );
        yield return IndexToLocalCoordinate( TravelAlongDirection( vPos, Vector3Int.up ) );
        yield return IndexToLocalCoordinate( TravelAlongDirection( TravelAlongDirection( vPos, Vector3Int.forward ), Vector3Int.right ) );
        yield return IndexToLocalCoordinate( TravelAlongDirection( TravelAlongDirection( vPos, Vector3Int.forward ), Vector3Int.up ) );
        yield return IndexToLocalCoordinate( TravelAlongDirection( TravelAlongDirection( vPos, Vector3Int.right ), Vector3Int.up ) );
        yield return IndexToLocalCoordinate( TravelAlongDirection( TravelAlongDirection( TravelAlongDirection( vPos, Vector3Int.forward ), Vector3Int.right ), Vector3Int.up ) );
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