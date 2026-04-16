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
                        for ( vPos.y = 0; vPos.y * fDeltaHeight < fHeight; ++vPos.y )
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
            /*
            case Geometry.SPHERICAL:
                SphereCollider pSphereCollider = GetComponent<SphereCollider>();
                float fSRadiusMax = pSphereCollider.radius * transform.localScale[ 0 ];
                float fSDeltaRadius = m_vVoxelSize[ 0 ];
                for ( u = 0; ( u + 0.5f ) * fSDeltaRadius < fSRadiusMax; ++u )
                {
                    float fSRadius = u * fSDeltaRadius + fSDeltaRadius / 2.0f;
                    float fDeltaTheta = GetDeltaV( u );
                    float fDeltaThetaInner = (int)u - 1 < 0 ? fDeltaTheta : GetDeltaV( u - 1 );

                    for ( v = 0; ( v + 0.5f ) * fDeltaTheta < Mathf.PI; ++v )
                    {
                        float fTheta = v * fDeltaTheta + fDeltaTheta / 2.0f;
                        float fDeltaPhi = GetDeltaW( u, v );

                        for ( w = 0; ( w + 0.5f ) * fDeltaPhi < 2 * Mathf.PI; ++w )
                        {
                            float fPhi = w * fDeltaPhi + fDeltaPhi / 2.0f;
                            Vector3 vVoxelCentre = new( fSRadius * Mathf.Sin( fTheta ) * Mathf.Sin( fPhi ), 
                                                        fSRadius * Mathf.Cos( fTheta ), 
                                                        fSRadius * Mathf.Sin( fTheta ) * Mathf.Cos( fPhi ) );
                            List<Vector3> pNorms = new();
                            if ( fSRadius + fSDeltaRadius >= fSRadiusMax )
                                pNorms.Add( vVoxelCentre.normalized );

                            for ( int i = 0; i < 3; ++i )
                                vVoxelCentre[ i ] /= transform.localScale[ i ];
                            GameObject pVoxelObj = new( "svoxel" );
                            pVoxelObj.transform.parent = transform;
                            pVoxelObj.transform.localPosition = vVoxelCentre;
                            pVoxelObj.transform.localRotation = Quaternion.identity;
                            m_pVoxels.Add( (u, v, w), pVoxelObj.AddComponent<Voxel>() );
                            m_pVoxels[ (u, v, w) ].UVW = (u, v, w);
                            m_pVoxels[ (u, v, w) ].OwningGroup = this;
                            m_pVoxels[ (u, v, w) ].Block = pNorms.Any() ? BlockType.GRASS : BlockType.DIRT;
                            m_pVoxels[ (u, v, w) ].m_bCalculatingExposedNormals = true;
                            pVoxelObj.AddComponent<MeshRenderer>().material = VoxelAtlas.VoxelMaterial;

                            if ( pNorms.Any() )
                                pRenderingInitially.Add( m_pVoxels[ (u, v, w) ] );
                        }
                    }
                }
                break;
                */
        }

        foreach ( Voxel pRender in m_pVoxelGroup.GetVoxels() )
            pRender.RefreshTriangles();
        foreach ( var pCollider in GetComponents<Collider>() )
            pCollider.enabled = false;
    }
}
