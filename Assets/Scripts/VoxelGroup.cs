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
    private Geometry m_iGeometry = Geometry.NONE;

    // Cartesian:    u - x, v - y, w - z
    // Cylindrical:  u - r, v - y, w - theta
    // Spherical:    u - r, v - theta, w - phi 
    protected readonly Dictionary<Vector3Int, Voxel> m_pVoxels = new();

    // list for if this w value is the last one before the number doubles
    // this is useful for cylindrical and spherical coords
    protected readonly List<uint> m_pWDoubles = new();

    // same but for the v value - only useful for spherical coords
    protected readonly List<uint> m_pVDoubles = new();
}