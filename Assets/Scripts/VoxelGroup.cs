using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UIElements;

public abstract class VoxelGroup : MonoBehaviour
{
    public bool m_bNotifyNeighborsOnVoxelDelete = false;
    public Vector3 VoxelSize => m_vVoxelSize;
    [SerializeField] protected Vector3 m_vVoxelSize;
    private Geometry m_iGeometry = Geometry.NONE;
    // Cartesian:    u - x, v - y, w - z
    // Cylindrical:  u - r, v - y, w - theta
    // Spherical:    u - r, v - theta, w - phi 
    protected readonly Dictionary<(uint, uint, uint), Voxel> m_pVoxels = new();

    // delta w is a function of u and v at most
    protected float GetDeltaW( uint u, uint v )
    {
        switch ( m_iGeometry )
        {
            case Geometry.CARTESIAN:
                return m_vVoxelSize[ 2 ];
            
            case Geometry.CYLINDRICAL:
            {
                float fDeltaRadius = GetDeltaU();
                float fRadius = u * fDeltaRadius + fDeltaRadius / 2.0f;
                float fOuterRadius = fRadius + fDeltaRadius / 2.0f;
                float fDeltaTheta = Mathf.PI / 2.0f;
                while ( fOuterRadius * fDeltaTheta > m_vVoxelSize[ 2 ] )
                    fDeltaTheta /= 2.0f;
                return fDeltaTheta;
            }

            case Geometry.SPHERICAL:
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

            default:
                return m_vVoxelSize[ 2 ];
        }
    }
    // delta v is a function only of u at most
    protected float GetDeltaV( uint u )
    {
        switch ( m_iGeometry )
        {
            case Geometry.CARTESIAN:
                return m_vVoxelSize[ 1 ];
            
            case Geometry.CYLINDRICAL:
                return m_vVoxelSize[ 1 ];

            case Geometry.SPHERICAL:
                float fSDeltaRadius = GetDeltaU();
                float fSRadius = u * fSDeltaRadius + fSDeltaRadius / 2.0f;
                float fOuterRadius = fSRadius + fSDeltaRadius / 2.0f;
                float fDeltaTheta = Mathf.PI / 2.0f;
                while ( fOuterRadius * fDeltaTheta > m_vVoxelSize[ 1 ] )
                    fDeltaTheta /= 2.0f;
                return fDeltaTheta;

            default:
                return m_vVoxelSize[ 1 ];
        }
    }
    protected float GetDeltaU() => m_vVoxelSize[ 0 ];

    public Geometry GetGeometry()
    {
        if ( m_iGeometry != Geometry.NONE )
            return m_iGeometry;
        

        foreach ( var pCollider in GetComponents<Collider>() )
        {
            if ( !pCollider.isTrigger )
            {
                pCollider.enabled = false;
                if ( TryGetComponent<BoxCollider>( out _ ) )
                    m_iGeometry = Geometry.CARTESIAN;
                else if ( TryGetComponent<CapsuleCollider>( out _ ) )
                    m_iGeometry = Geometry.CYLINDRICAL;
                else if ( TryGetComponent<SphereCollider>( out _ ) )
                    m_iGeometry = Geometry.SPHERICAL;
                else throw new NotImplementedException( "Please have one of a BoxCollider, CapsuleCollider, or SphereCollider on a Voxelify Object" );
            }
        }

        return m_iGeometry;
    }

    public void CombineMeshes()
    {
        List<CombineInstance> pCombine = new();
        for ( int i = 0; i < m_pVoxels.Count; ++i )
        {
            Voxel pVoxel = m_pVoxels.ElementAt( i ).Value;

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
        for ( int i = 0; i < m_pVoxels.Count; ++i )
        {
            Voxel pVoxel = m_pVoxels.ElementAt( i ).Value;
            pVoxel.gameObject.SetActive( false );
        }
    }

    public void DeCombine()
    {
        for ( int i = 0; i < m_pVoxels.Count; ++i )
        {
            Voxel pVoxel = m_pVoxels.ElementAt( i ).Value;
            pVoxel.gameObject.SetActive( true );
        }
        RemoveMesh();
        GetComponent<MeshRenderer>().enabled = false;
    }

    public IEnumerable<Voxel> GetNearestNeighbors( Voxel pVoxel )
    {
        foreach ( var val in GetNeighbors( pVoxel, false ) )
        {
            var uvw = ((uint)val.Item1, (uint)val.Item2, (uint)val.Item3);
            yield return m_pVoxels[ uvw ];
        }
    }
    public IEnumerable<(long, long, long)> GetExposedNeighbors( Voxel pVoxel ) 
    {
        foreach ( var val in GetNeighbors( pVoxel, true ) )
            yield return val;
    }

    IEnumerable<(long, long, long)> GetNeighbors( Voxel pVoxel, bool bExposed )
    {
        (uint u, uint v, uint w) = pVoxel.UVW;
        // for u-1, we will only ever have to update one block
        // for u+1, we need to figure out how many additional blocks there are on the layer above us
        // will always be 1 for cartesian, will be either 2 or 1 for large u but for small u may be much larger
        uint iNumVAtAboveU = (uint)Mathf.RoundToInt( GetDeltaV( u ) / GetDeltaV( u + 1 ) );
        for ( uint i = 0; i < iNumVAtAboveU; ++i )
        {
            uint iNumWAtAboveU = (uint)Mathf.RoundToInt( GetDeltaW( u, v ) / GetDeltaW( u + 1, v * iNumVAtAboveU + i ) );
            for ( uint j = 0; j < iNumWAtAboveU; ++j )
            {
                if ( m_pVoxels.ContainsKey( (u+1, v * iNumVAtAboveU + i, w * iNumWAtAboveU + j) ) == !bExposed )
                    yield return (u+1, v * iNumVAtAboveU + i, w * iNumWAtAboveU + j);
            }
        }
        if ( u != 0 )
        {
            uint iInverseNumVAtBelowU = (uint)Mathf.RoundToInt( GetDeltaV( u - 1 ) / GetDeltaV( u ) );
            uint iInverseNumWAtBelowU = (uint)Mathf.RoundToInt( GetDeltaW( u - 1, v / iInverseNumVAtBelowU ) / GetDeltaW( u, v ) );
            if ( m_pVoxels.ContainsKey( (u-1, v / iInverseNumVAtBelowU, w / iInverseNumWAtBelowU) ) == !bExposed )
                yield return (u-1, v / iInverseNumVAtBelowU, w / iInverseNumWAtBelowU);
        }
        else if ( GetGeometry() == Geometry.CARTESIAN && bExposed )
            yield return (u-1, v, w);

        // with v it's similar except the direction of more/less will vary, so store both
        // we also want to still notify even if the count is lower on that end
        for ( int v_iter = -1; v_iter <= 1; v_iter += 2 )
        {
            if ( GetGeometry() == Geometry.SPHERICAL )
                if ( ( v + v_iter ) * GetDeltaV( u ) > 2 * Mathf.PI )
                    continue;

            float fNumAtDiffV = GetDeltaW( u, v ) / GetDeltaW( u, (uint)(v + v_iter) );
            uint iNumAtDiffV = (uint)Mathf.RoundToInt( fNumAtDiffV );
            uint iInverseNumAtDiffV = (uint)Mathf.RoundToInt( 1.0f / fNumAtDiffV );
            if ( iNumAtDiffV == 0 ) ++iNumAtDiffV;
            if ( iInverseNumAtDiffV == 0 ) ++iInverseNumAtDiffV;

            for ( uint i = 0; i < iNumAtDiffV; ++i )
            {
                uint w_iter = iInverseNumAtDiffV > 1 ? w / iInverseNumAtDiffV : w * iNumAtDiffV + i;
                if ( m_pVoxels.ContainsKey( (u, (uint)(v + v_iter), w_iter) ) == !bExposed )
                    yield return (u, v + v_iter, w_iter);
            }
        }

        // w frequency varies between different u and v, but at a given value of u and v it is fixed
        // the main difference with w is that in spherical and cylindrical geometry it wraps around
        for ( int w_iter = -1; w_iter <= 1; w_iter += 2 )
        {
            if ( GetGeometry() != Geometry.CARTESIAN )
            {
                uint w_count = (uint)Mathf.RoundToInt( 2 * Mathf.PI / GetDeltaW( u, v ) );
                uint w_diff = w + w_iter < 0 ? w_count - 1u : ( w + w_iter >= w_count ? 0u : (uint)(w + w_iter) );
                if ( m_pVoxels.ContainsKey( (u, v, w_diff) ) == !bExposed )
                    yield return (u, v, w_diff);
            }
            else
                if ( m_pVoxels.ContainsKey( (u, v, (uint)(w + w_iter)) ) == !bExposed )
                    yield return (u, v, (uint)(w + w_iter) );
        }
    }
    public void BreakVoxel( Voxel pVoxel, bool bCalledFromVoxelDisable = false )
    {
        (uint u, uint v, uint w) = pVoxel.UVW;
        if ( !m_pVoxels.ContainsKey( (u, v, w) ) || m_pVoxels[ (u, v, w) ] != pVoxel )
            return;
        
        m_pVoxels.Remove( (u, v, w) );
        foreach ( Voxel pNN in GetNearestNeighbors( pVoxel ) )
            pNN.RefreshTriangles();

        if ( !bCalledFromVoxelDisable )
            Destroy( pVoxel.gameObject );
    }
    public void PlaceVoxel( (uint, uint, uint) pVoxelUVW, Voxel pVoxel = null )
    {
        (uint u, uint v, uint w) = ((uint)pVoxelUVW.Item1, (uint)pVoxelUVW.Item2, (uint)pVoxelUVW.Item3);
        if ( m_pVoxels.ContainsKey( (u, v, w) ) )
            return;
        
        if ( !pVoxel )
            throw new NotImplementedException( "cannot create voxel in PlaceVoxel yet" );

        m_pVoxels.Add( (u, v, w), pVoxel );
        foreach ( Voxel pNN in GetNearestNeighbors( pVoxel ) )
            pNN.RefreshTriangles();
    }
    public IEnumerable<Vector3> GetNearestNeighborNormals( Voxel pVoxel )
    {
        (uint u, uint v, uint w) = pVoxel.UVW;
        float fDeltaRadius = GetDeltaU();
        float fDeltaTheta = GetDeltaV( u );
        float fDeltaPhi = GetDeltaW( u, v );
        foreach ( Voxel pNeighbor in GetNearestNeighbors( pVoxel ) )
        {
            int u_diff = (int)((long)pNeighbor.U - pVoxel.U);
            if ( u_diff != 0 )
            {
                switch ( GetGeometry() )
                {
                    case Geometry.CARTESIAN:
                        yield return ( u_diff * Vector3.right ).normalized;
                        break;

                    case Geometry.CYLINDRICAL:
                        yield return ( u_diff * new Vector3( pVoxel.transform.localPosition.x, 0, pVoxel.transform.localPosition.z ) ).normalized;
                        break;

                    case Geometry.SPHERICAL:
                        yield return ( u_diff * pVoxel.transform.localPosition ).normalized;
                        break;
                }
                continue;
            }
            int v_diff = (int)((long)pNeighbor.V - pVoxel.V);
            if ( v_diff != 0 )
            {
                switch ( GetGeometry() )
                {
                    case Geometry.CARTESIAN:
                        yield return ( v_diff * Vector3.up ).normalized;
                        break;

                    case Geometry.CYLINDRICAL:
                        yield return ( v_diff * Vector3.up ).normalized;
                        break;

                    case Geometry.SPHERICAL:
                        float fSRadius = u * fDeltaRadius + fDeltaRadius / 2.0f;
                        float fTheta = ( v + v_diff ) * fDeltaTheta + fDeltaTheta / 2.0f;
                        float fPhi = w * fDeltaPhi + fDeltaPhi / 2.0f;
                        Vector3 vTheoreticalVoxelCentre = new( fSRadius * Mathf.Sin( fTheta ) * Mathf.Sin( fPhi ) / transform.localScale.x, 
                                                               fSRadius * Mathf.Cos( fTheta ) / transform.localScale.y, 
                                                               fSRadius * Mathf.Sin( fTheta ) * Mathf.Cos( fPhi ) / transform.localScale.z );
                        yield return ( vTheoreticalVoxelCentre - pVoxel.transform.localPosition ).normalized;
                        break;
                }
                continue;
            }
            int w_diff = (int)((long)pNeighbor.W - pVoxel.W);
            if ( w_diff != 0 )
            {
                switch ( GetGeometry() )
                {
                    case Geometry.CARTESIAN:
                        yield return ( w_diff * Vector3.forward ).normalized;
                        break;

                    case Geometry.CYLINDRICAL:
                    {
                        int w_diff_theta = Mathf.Abs( w_diff ) > 1 ? -w_diff / Mathf.Abs( w_diff ) : w_diff; //only way for >1 is if wraparound
                        float fRadius = u * fDeltaRadius + fDeltaRadius / 2.0f;
                        float fTheta = ( w + w_diff_theta ) * fDeltaTheta + fDeltaTheta / 2.0f;
                        Vector3 vTheoreticalVoxelCentre = new( fRadius * Mathf.Cos( fTheta ) / transform.localScale.x, pVoxel.transform.localPosition.y, fRadius * Mathf.Sin( fTheta ) / transform.localScale.z );
                        yield return ( vTheoreticalVoxelCentre - pVoxel.transform.localPosition ).normalized;
                        break;
                    }

                    case Geometry.SPHERICAL:
                    {
                        int w_diff_phi = Mathf.Abs( w_diff ) > 1 ? -w_diff / Mathf.Abs( w_diff ) : w_diff; //only way for >1 is if wraparound
                        float fSRadius = u * fDeltaRadius + fDeltaRadius / 2.0f;
                        float fTheta = v * fDeltaTheta + fDeltaTheta / 2.0f;
                        float fPhi = ( w + w_diff_phi ) * fDeltaPhi + fDeltaPhi / 2.0f;
                        Vector3 vTheoreticalVoxelCentre = new( fSRadius * Mathf.Sin( fTheta ) * Mathf.Sin( fPhi ) / transform.localScale.x, 
                                                               fSRadius * Mathf.Cos( fTheta ) / transform.localScale.y, 
                                                               fSRadius * Mathf.Sin( fTheta ) * Mathf.Cos( fPhi ) / transform.localScale.z );
                        yield return ( vTheoreticalVoxelCentre - pVoxel.transform.localPosition ).normalized;
                        break;
                    }
                }
                continue;
            }

            throw new InvalidProgramException( "u_diff, v_diff, and w_diff cannot all be 0 for a nearest neighbor" );
        }
    }
    public IEnumerable<Vector3> GetExposedNeighborNormals( Voxel pVoxel )
    {
        (uint u, uint v, uint w) = pVoxel.UVW;
        float fDeltaRadius = GetDeltaU();
        float fDeltaTheta = GetDeltaV( u );
        float fDeltaPhi = GetDeltaW( u, v );
        foreach ( var pNeighbor in GetExposedNeighbors( pVoxel ) )
        {
            int u_diff = (int)(pNeighbor.Item1 - pVoxel.U);
            if ( u_diff != 0 )
            {
                switch ( GetGeometry() )
                {
                    case Geometry.CARTESIAN:
                        yield return ( u_diff * Vector3.right ).normalized;
                        break;

                    case Geometry.CYLINDRICAL:
                        yield return ( u_diff * new Vector3( pVoxel.transform.localPosition.x, 0, pVoxel.transform.localPosition.z ) ).normalized;
                        break;

                    case Geometry.SPHERICAL:
                        yield return ( u_diff * pVoxel.transform.localPosition ).normalized;
                        break;
                }
                continue;
            }
            int v_diff = (int)(pNeighbor.Item2 - pVoxel.V);
            if ( v_diff != 0 )
            {
                switch ( GetGeometry() )
                {
                    case Geometry.CARTESIAN:
                        yield return ( v_diff * Vector3.up ).normalized;
                        break;

                    case Geometry.CYLINDRICAL:
                        yield return ( v_diff * Vector3.up ).normalized;
                        break;

                    case Geometry.SPHERICAL:
                        float fSRadius = u * fDeltaRadius + fDeltaRadius / 2.0f;
                        float fTheta = ( v + v_diff ) * fDeltaTheta + fDeltaTheta / 2.0f;
                        float fPhi = w * fDeltaPhi + fDeltaPhi / 2.0f;
                        Vector3 vTheoreticalVoxelCentre = new( fSRadius * Mathf.Sin( fTheta ) * Mathf.Sin( fPhi ) / transform.localScale.x, 
                                                               fSRadius * Mathf.Cos( fTheta ) / transform.localScale.y, 
                                                               fSRadius * Mathf.Sin( fTheta ) * Mathf.Cos( fPhi ) / transform.localScale.z );
                        yield return ( vTheoreticalVoxelCentre - pVoxel.transform.localPosition ).normalized;
                        break;
                }
                continue;
            }
            int w_diff = (int)(pNeighbor.Item3 - pVoxel.W);
            if ( w_diff != 0 )
            {
                switch ( GetGeometry() )
                {
                    case Geometry.CARTESIAN:
                        yield return ( w_diff * Vector3.forward ).normalized;
                        break;

                    case Geometry.CYLINDRICAL:
                    {
                        int w_diff_theta = Mathf.Abs( w_diff ) > 1 ? -w_diff / Mathf.Abs( w_diff ) : w_diff; //only way for >1 is if wraparound
                        float fRadius = u * fDeltaRadius + fDeltaRadius / 2.0f;
                        float fTheta = ( w + w_diff_theta ) * fDeltaPhi + fDeltaPhi / 2.0f;
                        Vector3 vTheoreticalVoxelCentre = new( fRadius * Mathf.Cos( fTheta ) / transform.localScale.x, 
                                                               pVoxel.transform.localPosition.y, 
                                                               fRadius * Mathf.Sin( fTheta ) / transform.localScale.z );
                        yield return ( vTheoreticalVoxelCentre - pVoxel.transform.localPosition ).normalized;
                        break;
                    }

                    case Geometry.SPHERICAL:
                    {
                        int w_diff_phi = Mathf.Abs( w_diff ) > 1 ? -w_diff / Mathf.Abs( w_diff ) : w_diff; //only way for >1 is if wraparound
                        float fSRadius = u * fDeltaRadius + fDeltaRadius / 2.0f;
                        float fTheta = v * fDeltaTheta + fDeltaTheta / 2.0f;
                        float fPhi = ( w + w_diff_phi ) * fDeltaPhi + fDeltaPhi / 2.0f;
                        Vector3 vTheoreticalVoxelCentre = new( fSRadius * Mathf.Sin( fTheta ) * Mathf.Sin( fPhi ) / transform.localScale.x, 
                                                               fSRadius * Mathf.Cos( fTheta ) / transform.localScale.y, 
                                                               fSRadius * Mathf.Sin( fTheta ) * Mathf.Cos( fPhi ) / transform.localScale.z );
                        yield return ( vTheoreticalVoxelCentre - pVoxel.transform.localPosition ).normalized;
                        break;
                    }
                }
                continue;
            }

            throw new InvalidProgramException( "u_diff, v_diff, and w_diff cannot all be 0 for a nearest neighbor" );
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
}