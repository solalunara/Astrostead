using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;

public class Voxelify : VoxelGroup
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
    // Cartesian:    u - x, v - y, w - z
    // Cylindrical:  u - r, v - y, w - theta
    // Spherical:    u - r, v - theta, w - phi 
    void OnEnable()
    {
        m_pVoxels.Clear();

        foreach ( var pRenderer in GetComponents<Renderer>() )
            pRenderer.enabled = false;

        List<Voxel> pRenderingInitially = new();

        uint u, v, w;
        u = v = w = 0;
        switch ( GetGeometry() )
        {
            case Geometry.CARTESIAN:
                BoxCollider pBoxCollider = GetComponent<BoxCollider>();
                Vector3 vSize = pBoxCollider.size;
                for ( int i = 0; i < 3; ++i )
                    vSize[ i ] *= transform.localScale[ i ];

                for ( u = 0; u * m_vVoxelSize.x + ( -vSize.x + m_vVoxelSize.x ) / 2.0f < vSize.x / 2.0f; ++u )
                {
                    for ( v = 0; v * m_vVoxelSize.y + ( -vSize.y + m_vVoxelSize.y ) / 2.0f < vSize.y / 2.0f; ++v )
                    {
                        for ( w = 0; w * m_vVoxelSize.z + ( -vSize.z + m_vVoxelSize.z ) / 2.0f < vSize.z / 2.0f; ++w )
                        {
                            Vector3 vVoxelCentre = new( u * m_vVoxelSize.x + ( -vSize.x + m_vVoxelSize.x ) / 2.0f, 
                                                        v * m_vVoxelSize.y + ( -vSize.y + m_vVoxelSize.y ) / 2.0f, 
                                                        w * m_vVoxelSize.z + ( -vSize.z + m_vVoxelSize.z ) / 2.0f );
                            List<Vector3> pNorms = new();
                            if ( u == 0 )
                                pNorms.Add( -Vector3.right   );
                            if ( vVoxelCentre.x + m_vVoxelSize.x >= vSize.x / 2.0f )
                                pNorms.Add(  Vector3.right   );
                            if ( v == 0 )
                                pNorms.Add( -Vector3.up      );
                            if ( vVoxelCentre.y + m_vVoxelSize.y >= vSize.y / 2.0f )
                                pNorms.Add(  Vector3.up      );
                            if ( w == 0 )
                                pNorms.Add( -Vector3.forward );
                            if ( vVoxelCentre.z + m_vVoxelSize.z >= vSize.z / 2.0f )
                                pNorms.Add(  Vector3.forward );

                            for ( int i = 0; i < 3; ++i )
                                vVoxelCentre[ i ] /= transform.localScale[ i ];
                            GameObject pVoxelObj = new( "voxel" );
                            pVoxelObj.transform.parent = transform;
                            pVoxelObj.transform.localPosition = vVoxelCentre;
                            pVoxelObj.transform.localRotation = Quaternion.identity;
                            m_pVoxels.Add( (u, v, w), pVoxelObj.AddComponent<Voxel>() );
                            m_pVoxels[ (u, v, w) ].UVW = (u, v, w);
                            m_pVoxels[ (u, v, w) ].OwningGroup = this;
                            m_pVoxels[ (u, v, w) ].Block = pNorms.Contains( Vector3.up ) ? BlockType.GRASS : BlockType.DIRT;
                            m_pVoxels[ (u, v, w) ].m_bCalculatingExposedNormals = true;

                            pVoxelObj.AddComponent<MeshRenderer>().material = VoxelAtlas.VoxelMaterial;

                            if ( pNorms.Any() )
                                pRenderingInitially.Add( m_pVoxels[ (u, v, w) ] );
                        }
                    }
                }
                break;
            
            case Geometry.CYLINDRICAL:
                CapsuleCollider pCapsuleCollider = GetComponent<CapsuleCollider>();
                float fHeight = pCapsuleCollider.height * transform.localScale[ 1 ];
                float fDeltaHeight = m_vVoxelSize[ 1 ];
                float fRadiusMax = pCapsuleCollider.radius * transform.localScale[ 0 ];
                float fDeltaRadius = m_vVoxelSize[ 0 ];

                for ( u = 0; u * fDeltaRadius + fDeltaRadius / 2.0f < fRadiusMax; ++u )
                {
                    float fRadius = u * fDeltaRadius + fDeltaRadius / 2.0f;
                    float fDeltaTheta = GetDeltaW( u, 0 );
                    float fDeltaThetaInner = (int)u - 1 < 0 ? fDeltaTheta : GetDeltaW( u - 1, 0 );

                    for ( w = 0; ( w + 0.5f ) * fDeltaTheta < 2 * Mathf.PI; ++w )
                    {
                        float fTheta = w * fDeltaTheta + fDeltaTheta / 2.0f;
                        for ( v = 0; v * fDeltaHeight + ( -fHeight + fDeltaHeight ) / 2.0f < fHeight / 2.0f; ++v )
                        {
                            float fY = v * fDeltaHeight + ( -fHeight + fDeltaHeight ) / 2.0f;
                            Vector3 vVoxelCentre = new( fRadius * Mathf.Cos( fTheta ), fY, fRadius * Mathf.Sin( fTheta ) );

                            List<Vector3> pNorms = new();
                            if ( fRadius + fDeltaRadius >= fRadiusMax )
                                pNorms.Add( new Vector3( Mathf.Cos( fTheta ), 0, Mathf.Sin( fTheta ) ) );
                            if ( v == 0 )
                                pNorms.Add( -Vector3.up );
                            if ( fY + fDeltaHeight >= fHeight / 2.0f )
                                pNorms.Add(  Vector3.up );

                            for ( int i = 0; i < 3; ++i )
                                vVoxelCentre[ i ] /= transform.localScale[ i ];
                            GameObject pVoxelObj = new( "cvoxel" );
                            pVoxelObj.transform.parent = transform;
                            pVoxelObj.transform.localPosition = vVoxelCentre;
                            pVoxelObj.transform.localRotation = Quaternion.identity;
                            m_pVoxels.Add( (u, v, w), pVoxelObj.AddComponent<Voxel>() );
                            m_pVoxels[ (u, v, w) ].UVW = (u, v, w);
                            m_pVoxels[ (u, v, w) ].OwningGroup = this;
                            m_pVoxels[ (u, v, w) ].m_bCalculatingExposedNormals = true;
                            m_pVoxels[ (u, v, w) ].Block = fRadius + fDeltaRadius >= fRadiusMax ? BlockType.GRASS : BlockType.DIRT;
                            pVoxelObj.AddComponent<MeshRenderer>().material = VoxelAtlas.VoxelMaterial;

                            if ( pNorms.Any() )
                                pRenderingInitially.Add( m_pVoxels[ (u, v, w) ] );
                        }
                    }
                }

                break;

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
        }

        foreach ( Voxel pRender in pRenderingInitially )
            pRender.RefreshTriangles();
    }
}
