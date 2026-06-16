using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;

[RequireComponent(typeof(VoxelGroup))]
public class Voxelify : MonoBehaviour
{
    public TextureAtlas VoxelAtlas
    {
        get
        {
            if ( m_pVoxelAtlas == null )
                m_pVoxelAtlas = TextureAtlas.GetAtlas( m_iAtlas );
            return m_pVoxelAtlas;
        }
    } 
    private TextureAtlas m_pVoxelAtlas = null;
    [SerializeField] private AtlasType m_iAtlas;

    private VoxelGroup m_pVoxelGroup;

    // Cartesian:    u - x, v - y, w - z
    // Cylindrical:  u - r, v - y, w - theta
    // Spherical:    u - r, v - theta, w - phi 
    void OnEnable()
    {
        m_pVoxelGroup = GetComponent<VoxelGroup>();

        foreach ( var pRenderer in GetComponents<Renderer>() )
            pRenderer.enabled = false;

        Vector3Int vPos = Vector3Int.zero;
        switch ( m_pVoxelGroup.GetGeometry() )
        {
            case Geometry.CARTESIAN:
            {
                BoxCollider pBoxCollider = GetComponent<BoxCollider>();
                Vector3 vSize = pBoxCollider.size;
                for ( int i = 0; i < 3; ++i )
                    vSize[ i ] *= transform.localScale[ i ];

                Vector3Int vCounts = new( Mathf.RoundToInt( vSize.x / m_pVoxelGroup.VoxelSize.x ), Mathf.RoundToInt( vSize.y / m_pVoxelGroup.VoxelSize.y ), Mathf.RoundToInt( vSize.z / m_pVoxelGroup.VoxelSize.z ) );

                for ( vPos.x = -vCounts.x / 2; vPos.x < vCounts.x / 2; ++vPos.x )
                {
                    for ( vPos.y = -vCounts.y / 2; vPos.y < vCounts.y / 2; ++vPos.y )
                    {
                        for ( vPos.z = -vCounts.z / 2; vPos.z < vCounts.z / 2; ++vPos.z )
                        {
                            Vector3 vVoxelPos = m_pVoxelGroup.IndexToLocalCoordinate( vPos );

                            for ( int i = 0; i < 3; ++i )
                                vVoxelPos[ i ] /= transform.localScale[ i ];
                            GameObject pVoxelObj = new( "voxel" );
                            pVoxelObj.transform.parent = m_pVoxelGroup.transform;
                            pVoxelObj.transform.localPosition = vVoxelPos;
                            pVoxelObj.transform.localRotation = Quaternion.identity;
                            Voxel pVoxel = pVoxelObj.AddComponent<Voxel>();
                            pVoxel.Position = vPos;
                            pVoxel.Block = vPos.y + 1 >= vCounts.y / 2 ? BlockType.GRASS : BlockType.DIRT;

                            // ignore neighbor occlusion check since we don't want to update neighbors every time a voxel
                            // is added during voxelify - we do our own occlusion check at the very end. Also don't enable
                            // calculating exposed normals until after the RefreshTriangles() call from adding to group
                            m_pVoxelGroup.AddVoxelToGroup( pVoxel, bIgnoreOcclusionCheck: true );
                            pVoxel.m_bCalculatingExposedNormals = true;

                            pVoxelObj.AddComponent<MeshRenderer>().material = VoxelAtlas.VoxelMaterial;

                        }
                    }
                }
                break;
            }
            case Geometry.CYLINDRICAL:
            {
                CapsuleCollider pCapsuleCollider = GetComponent<CapsuleCollider>();
                float fHeight = pCapsuleCollider.height * transform.localScale[ 1 ];
                float fDeltaHeight = m_pVoxelGroup.VoxelSize[ 1 ];
                float fRadiusMax = pCapsuleCollider.radius * transform.localScale[ 0 ];
                float fDeltaRadius = m_pVoxelGroup.VoxelSize[ 0 ];

                for ( vPos.x = 0; vPos.x * fDeltaRadius < fRadiusMax; ++vPos.x )
                {
                    float fDeltaTheta = ((CylindricalVoxelGroup)m_pVoxelGroup).GetDeltaTheta( vPos.x );

                    for ( vPos.z = 0; vPos.z * fDeltaTheta < 2 * Mathf.PI; ++vPos.z )
                    {
                        for ( vPos.y = -Mathf.RoundToInt( fHeight / ( 2 * fDeltaHeight ) ); vPos.y * fDeltaHeight < fHeight / 2; ++vPos.y )
                        {
                            Vector3 vVoxelPos = m_pVoxelGroup.IndexToLocalCoordinate( vPos );
                            for ( int i = 0; i < 3; ++i )
                                vVoxelPos[ i ] /= transform.localScale[ i ];

                            GameObject pVoxelObj = new( "cvoxel" );
                            pVoxelObj.transform.parent = m_pVoxelGroup.transform;
                            pVoxelObj.transform.localPosition = vVoxelPos;
                            pVoxelObj.transform.localRotation = Quaternion.identity;
                            Voxel pVoxel = pVoxelObj.AddComponent<Voxel>();
                            pVoxel.Position = vPos;
                            pVoxel.Block = ( vPos.x + 1 ) * fDeltaRadius >= fRadiusMax ? BlockType.GRASS : BlockType.DIRT;
                            m_pVoxelGroup.AddVoxelToGroup( pVoxel, bIgnoreOcclusionCheck: true );

                            pVoxel.m_bCalculatingExposedNormals = true;
                            pVoxelObj.AddComponent<MeshRenderer>().material = VoxelAtlas.VoxelMaterial;
                        }
                    }
                }

                break;
            }
            case Geometry.SPHERICAL:
            {
                SphereCollider pSphereCollider = GetComponent<SphereCollider>();
                float fRadiusMax = pSphereCollider.radius * transform.localScale[ 0 ];
                float fDeltaRadius = m_pVoxelGroup.VoxelSize[ 0 ];

                for ( vPos.x = 0; vPos.x * fDeltaRadius < fRadiusMax; ++vPos.x )
                {
                    float fDeltaTheta = ((SphericalVoxelGroup)m_pVoxelGroup).GetDeltaTheta( vPos.x );

                    for ( vPos.y = 0; vPos.y * fDeltaTheta < Mathf.PI; ++vPos.y )
                    {
                        float fDeltaPhiL = ((SphericalVoxelGroup)m_pVoxelGroup).GetDeltaPhi( vPos.x, vPos.y );
                        float fDeltaPhiH = ((SphericalVoxelGroup)m_pVoxelGroup).GetDeltaPhi( vPos.x, vPos.y + 1 );
                        int iPhiHFactor = Mathf.RoundToInt( Mathf.Max( 1, fDeltaPhiH / fDeltaPhiL ) );

                        for ( vPos.z = 0; vPos.z * fDeltaPhiL < 2 * Mathf.PI; ++vPos.z )
                        {
                            // ghost blocks are a HACK neccesary for iPhiHFactor > 1 to occlude faces covered by adjacent blocks
                            bool bGhostBlock = vPos.z % iPhiHFactor != 0;
                            if ( bGhostBlock )
                            {
                                m_pVoxelGroup.AddGhostVoxel( vPos );
                                continue;
                            }

                            Vector3 vVoxelPos = m_pVoxelGroup.IndexToLocalCoordinate( vPos );
                            for ( int i = 0; i < 3; ++i )
                                vVoxelPos[ i ] /= transform.localScale[ i ];

                            GameObject pVoxelObj = new( "svoxel" );
                            pVoxelObj.transform.parent = m_pVoxelGroup.transform;
                            pVoxelObj.transform.localPosition = vVoxelPos;
                            pVoxelObj.transform.localRotation = Quaternion.identity;
                            Voxel pVoxel = pVoxelObj.AddComponent<Voxel>();
                            pVoxel.Position = vPos;
                            pVoxel.Block = ( vPos.x + 1 ) * fDeltaRadius >= fRadiusMax ? BlockType.GRASS : BlockType.DIRT;
                            m_pVoxelGroup.AddVoxelToGroup( pVoxel, bIgnoreOcclusionCheck: true );

                            pVoxel.m_bCalculatingExposedNormals = true;
                            pVoxelObj.AddComponent<MeshRenderer>().material = VoxelAtlas.VoxelMaterial;
                            if ( bGhostBlock )
                                pVoxelObj.SetActive( false );

                        }
                    }
                }
                break;
            }
        }

        foreach ( Voxel pRender in m_pVoxelGroup.GetVoxels() )
            if ( pRender )
                pRender.RefreshTriangles();
        foreach ( var pCollider in GetComponents<Collider>() )
            pCollider.enabled = false;
    }
}
