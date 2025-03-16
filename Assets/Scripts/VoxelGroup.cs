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
    public Vector3 m_vVoxelSize;

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
        for ( int u = 0; u < m_pVoxels.Count; ++u )
        for ( int v = 0; v < m_pVoxels[ u ].Count; ++v )
        for ( int w = 0; w < m_pVoxels[ u ][ v ].Count; ++w )
        {
            Voxel pVoxel = m_pVoxels[ u ][ v ][ w ];

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
        for ( int u = 0; u < m_pVoxels.Count; ++u )
        for ( int v = 0; v < m_pVoxels[ u ].Count; ++v )
        for ( int w = 0; w < m_pVoxels[ u ][ v ].Count; ++w )
        {
            Voxel pVoxel = m_pVoxels[ u ][ v ][ w ];
            pVoxel.gameObject.SetActive( false );
        }
    }
    private Geometry m_iGeometry = Geometry.NONE;

    // Cartesian:    u - x, v - y, w - z
    // Cylindrical:  u - r, v - y, w - theta
    // Spherical:    u - r, v - theta, w - phi 
    protected readonly List<List<List<Voxel>>> m_pVoxels = new();

    // list for if this w value is the last one before the number doubles
    // this is useful for cylindrical and spherical coords
    protected readonly List<uint> m_pWDoubles = new();

    // same but for the v value - only useful for spherical coords
    protected readonly List<uint> m_pVDoubles = new();

    void RemoveMesh()
    {
        if ( TryGetComponent( out MeshFilter m ) )
        {
            m.mesh.Clear();
            Destroy( m.mesh );
        }
    }

    void OnDisable()
    {
        // Unity Docs:
        //    It is your responsibility to destroy the automatically instantiated mesh when the game object is being destroyed.
        //    Resources.UnloadUnusedAssets also destroys the mesh but it is usually only called when loading a new level.
        RemoveMesh();
    }
}