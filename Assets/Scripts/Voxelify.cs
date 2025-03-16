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

        GeometryData gGeoData = new()
        {
            m_iGeometry = GetGeometry(),
            m_ptGeometryOrigin = transform.position,
            m_qGeometryRotation = transform.rotation,
            m_vSideLength = m_vVoxelSize
        };

        int u, v, w;
        u = v = w = 0;
        switch ( GetGeometry() )
        {
            case Geometry.CARTESIAN:
                BoxCollider pBoxCollider = GetComponent<BoxCollider>();
                Vector3 vSize = pBoxCollider.size;
                for ( int i = 0; i < 3; ++i )
                    vSize[ i ] *= transform.lossyScale[ i ];

                for ( u = 0; u * m_vVoxelSize.x + ( -vSize.x + m_vVoxelSize.x ) / 2.0f < vSize.x / 2.0f; ++u )
                {
                    m_pVoxels.Add( new() );
                    for ( v = 0; v * m_vVoxelSize.y + ( -vSize.y + m_vVoxelSize.y ) / 2.0f < vSize.y / 2.0f; ++v )
                    {
                        m_pVoxels[ u ].Add( new() );
                        for ( w = 0; w * m_vVoxelSize.z + ( -vSize.z + m_vVoxelSize.z ) / 2.0f < vSize.z / 2.0f; ++w )
                        {
                            Vector3 vVoxelCentre = new( u * m_vVoxelSize.x + ( -vSize.x + m_vVoxelSize.x ) / 2.0f, 
                                                        v * m_vVoxelSize.y + ( -vSize.y + m_vVoxelSize.y ) / 2.0f, 
                                                        w * m_vVoxelSize.z + ( -vSize.z + m_vVoxelSize.z ) / 2.0f );
                            List<Vector3> pNorms = new();
                            if ( u == 0 )
                                pNorms.Add( -Vector3.right   );
                            if ( vVoxelCentre.x * transform.lossyScale.x + m_vVoxelSize.x >= vSize.x / 2.0f )
                                pNorms.Add(  Vector3.right   );
                            if ( v == 0 )
                                pNorms.Add( -Vector3.up      );
                            if ( vVoxelCentre.y * transform.lossyScale.y + m_vVoxelSize.y >= vSize.y / 2.0f )
                                pNorms.Add(  Vector3.up      );
                            if ( w == 0 )
                                pNorms.Add( -Vector3.forward );
                            if ( vVoxelCentre.z * transform.lossyScale.z + m_vVoxelSize.z >= vSize.z / 2.0f )
                                pNorms.Add(  Vector3.forward );

                            for ( int i = 0; i < 3; ++i )
                                vVoxelCentre[ i ] /= transform.lossyScale[ i ];
                            GameObject pVoxelObj = new( "voxel" );
                            pVoxelObj.transform.parent = transform;
                            pVoxelObj.transform.localPosition = vVoxelCentre;
                            pVoxelObj.transform.localRotation = Quaternion.identity;
                            m_pVoxels[ u ][ v ].Add( pVoxelObj.AddComponent<Voxel>() );
                            m_pVoxels[ u ][ v ][ w ].GeoData = gGeoData;
                            m_pVoxels[ u ][ v ][ w ].ExposedNormals = pNorms;
                            m_pVoxels[ u ][ v ][ w ].Block = pNorms.Contains( Vector3.up ) ? BlockType.GRASS : BlockType.DIRT;

                            pVoxelObj.AddComponent<MeshRenderer>().material = VoxelAtlas.VoxelMaterial;
                        }
                    }
                }
                break;
            
            case Geometry.CYLINDRICAL:
                CapsuleCollider pCapsuleCollider = GetComponent<CapsuleCollider>();
                float fHeight = pCapsuleCollider.height * transform.lossyScale[ 1 ];
                float fDeltaHeight = m_vVoxelSize[ 1 ];
                float fRadiusMax = pCapsuleCollider.radius * transform.lossyScale[ 0 ];
                float fDeltaRadius = m_vVoxelSize[ 0 ];

                for ( u = 0; u * fDeltaRadius + fDeltaRadius / 2.0f < fRadiusMax; ++u )
                {
                    m_pVoxels.Add( new() );
                    float fRadius = u * fDeltaRadius + fDeltaRadius / 2.0f;
                    float fInnerRadius = fRadius - m_vVoxelSize[ 0 ] / 2.0f;
                    float fOuterRadius = fRadius + m_vVoxelSize[ 0 ] / 2.0f;

                    float fDeltaTheta = Mathf.PI / 2.0f;
                    float fDeltaThetaInner = Mathf.PI / 2.0f;
                    while ( fOuterRadius * fDeltaTheta > m_vVoxelSize[ 2 ] )
                        fDeltaTheta /= 2.0f;
                    while ( fInnerRadius * fDeltaThetaInner > m_vVoxelSize[ 2 ] )
                        fDeltaThetaInner /= 2.0f;

                    for ( w = 0; ( w + 0.5f ) * fDeltaTheta < 2 * Mathf.PI; ++w )
                    {
                        m_pVoxels[ u ].Add( new() );
                        float fTheta = w * fDeltaTheta + fDeltaTheta / 2.0f;
                        for ( v = 0; v * fDeltaHeight + ( -fHeight + fDeltaHeight ) / 2.0f < fHeight / 2.0f; ++v )
                        {
                            float fY = v * fDeltaHeight + ( -fHeight + fDeltaHeight ) / 2.0f;
                            Vector3 vVoxelCentre = new( fRadius * Mathf.Cos( fTheta ), fY, fRadius * Mathf.Sin( fTheta ) );
                            for ( int i = 0; i < 3; ++i )
                                vVoxelCentre[ i ] /= transform.lossyScale[ i ];
                            GameObject pVoxelObj = new( "cvoxel" );
                            pVoxelObj.transform.parent = transform;
                            pVoxelObj.transform.localPosition = vVoxelCentre;
                            pVoxelObj.transform.localRotation = Quaternion.identity;
                            m_pVoxels[ u ][ v ].Add( pVoxelObj.AddComponent<Voxel>() );
                            m_pVoxels[ u ][ v ][ w ].GeoData = gGeoData;

                            // if we're an edge, tell the voxel that
                            List<Vector3> pNorms = new();
                            if ( fRadius + fDeltaRadius >= fRadiusMax )
                                pNorms.Add( new Vector3( Mathf.Cos( fTheta ), 0, Mathf.Sin( fTheta ) ) );
                            if ( v == 0 )
                                pNorms.Add( -Vector3.up );
                            if ( fY + fDeltaHeight >= fHeight / 2.0f )
                                pNorms.Add(  Vector3.up );

                            m_pVoxels[ u ][ v ][ w ].ExposedNormals = pNorms;
                            m_pVoxels[ u ][ v ][ w ].Block = fRadius + fDeltaRadius >= fRadiusMax ? BlockType.GRASS : BlockType.DIRT;

                            pVoxelObj.AddComponent<MeshRenderer>().material = VoxelAtlas.VoxelMaterial;
                        }
                    }
                }

                break;

            case Geometry.SPHERICAL:
                SphereCollider pSphereCollider = GetComponent<SphereCollider>();
                float fSRadiusMax = pSphereCollider.radius * transform.lossyScale[ 0 ];
                float fSDeltaRadius = m_vVoxelSize[ 0 ];
                for ( u = 0; ( u + 0.5f ) * fSDeltaRadius < fSRadiusMax; ++u )
                {
                    m_pVoxels.Add( new() );
                    float fSRadius = u * fSDeltaRadius + fSDeltaRadius / 2.0f;
                    float fInnerRadius = fSRadius - m_vVoxelSize[ 0 ] / 2.0f;
                    float fOuterRadius = fSRadius + m_vVoxelSize[ 0 ] / 2.0f;

                    float fDeltaTheta = Mathf.PI / 2.0f;
                    float fDeltaThetaInner = Mathf.PI / 2.0f;
                    while ( fOuterRadius * fDeltaTheta > m_vVoxelSize[ 1 ] )
                        fDeltaTheta /= 2.0f;
                    while ( fInnerRadius * fDeltaThetaInner > m_vVoxelSize[ 1 ] )
                        fDeltaThetaInner /= 2.0f;


                    for ( v = 0; ( v + 0.5f ) * fDeltaTheta < Mathf.PI; ++v )
                    {
                        m_pVoxels[ u ].Add( new() );
                        float fTheta = v * fDeltaTheta + fDeltaTheta / 2.0f;
                        float fUpperTheta = fTheta - fDeltaTheta / 2.0f;
                        float fLowerTheta = fTheta + fDeltaTheta / 2.0f;
                        float fThetaRadius = fSRadius * Mathf.Sin( fTheta );

                        float fDeltaPhi = Mathf.PI / 2.0f;
                        while ( fOuterRadius * Mathf.Sin( fTheta ) * fDeltaPhi > m_vVoxelSize[ 2 ] )
                            fDeltaPhi /= 2.0f;

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
                                vVoxelCentre[ i ] /= transform.lossyScale[ i ];
                            GameObject pVoxelObj = new( "svoxel" );
                            pVoxelObj.transform.parent = transform;
                            pVoxelObj.transform.localPosition = vVoxelCentre;
                            pVoxelObj.transform.localRotation = Quaternion.identity;
                            m_pVoxels[ u ][ v ].Add( pVoxelObj.AddComponent<Voxel>() );
                            m_pVoxels[ u ][ v ][ w ].GeoData = gGeoData;
                            m_pVoxels[ u ][ v ][ w ].ExposedNormals = pNorms;
                            m_pVoxels[ u ][ v ][ w ].Block = pNorms.Any() ? BlockType.GRASS : BlockType.DIRT;
                            pVoxelObj.AddComponent<MeshRenderer>().material = VoxelAtlas.VoxelMaterial;
                        }
                    }
                }
                break;
        }

        CombineMeshes();
    }
}
