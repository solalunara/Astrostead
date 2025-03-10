using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;

public class Voxelify : MonoBehaviour
{
    public Vector3 m_vVoxelSize;
    public Material m_pVoxelMaterial;
    Geometry m_iGeometry;
    List<Voxel> m_pVoxels = new();

    void OnEnable()
    {
        m_pVoxels.Clear();

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

        foreach ( var pRenderer in GetComponents<Renderer>() )
            pRenderer.enabled = false;

        switch ( m_iGeometry )
        {
            case Geometry.CARTESIAN:
                BoxCollider pBoxCollider = GetComponent<BoxCollider>();
                Vector3 vSize = pBoxCollider.size;
                for ( int i = 0; i < 3; ++i )
                    vSize[ i ] *= transform.lossyScale[ i ];

                for ( float u = -vSize.x + m_vVoxelSize.x / 2; u < vSize.x; u += m_vVoxelSize.x )
                {
                    for ( float v = -vSize.y + m_vVoxelSize.y / 2; v < vSize.y; v += m_vVoxelSize.y )
                    {
                        for ( float w = -vSize.z + m_vVoxelSize.z / 2; w < vSize.z; w += m_vVoxelSize.z )
                        {
                            Vector3 vVoxelCentre = new( u, v, w );
                            GameObject pVoxelObj = GameObject.CreatePrimitive( PrimitiveType.Cube );
                            pVoxelObj.GetComponent<Renderer>().material = m_pVoxelMaterial;
                            pVoxelObj.transform.parent = transform;
                            pVoxelObj.transform.position = vVoxelCentre + transform.position;
                            pVoxelObj.transform.localRotation = Quaternion.identity;
                            m_pVoxels.Add( pVoxelObj.AddComponent<Voxel>() );
                            m_pVoxels.Last().InstantiateGeometryData( m_iGeometry, transform.position, transform.rotation, m_vVoxelSize );
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

                for ( float fRadius = fDeltaRadius / 2.0f; fRadius < fRadiusMax; fRadius += fDeltaRadius )
                {
                    float fInnerRadius = fRadius - m_vVoxelSize[ 0 ] / 2.0f;
                    float fOuterRadius = fRadius + m_vVoxelSize[ 0 ] / 2.0f;

                    float fDeltaTheta = Mathf.PI / 2.0f;
                    float fDeltaThetaInner = Mathf.PI / 2.0f;
                    while ( fOuterRadius * fDeltaTheta > m_vVoxelSize[ 2 ] )
                        fDeltaTheta /= 2.0f;
                    while ( fInnerRadius * fDeltaThetaInner > m_vVoxelSize[ 2 ] )
                        fDeltaThetaInner /= 2.0f;

                    for ( float fTheta = fDeltaTheta / 2.0f; fTheta < 2 * Mathf.PI; fTheta += fDeltaTheta )
                    {
                        for ( float z = -fHeight / 2.0f + fDeltaHeight / 2.0f; z < fHeight / 2.0f; z += fDeltaHeight )
                        {
                            Vector3 vVoxelCentre = new( fRadius * Mathf.Cos( fTheta ), z, fRadius * Mathf.Sin( fTheta ) );
                            GameObject pVoxelObj = GameObject.CreatePrimitive( PrimitiveType.Cube );
                            pVoxelObj.GetComponent<Renderer>().material = m_pVoxelMaterial;
                            pVoxelObj.transform.parent = transform;
                            pVoxelObj.transform.position = vVoxelCentre + transform.position;
                            pVoxelObj.transform.localRotation = Quaternion.identity;
                            m_pVoxels.Add( pVoxelObj.AddComponent<Voxel>() );
                            m_pVoxels.Last().InstantiateGeometryData( m_iGeometry, transform.position, transform.rotation, m_vVoxelSize );
                        }
                    }
                }

                break;

            case Geometry.SPHERICAL:
                SphereCollider pSphereCollider = GetComponent<SphereCollider>();
                break;
        }
    }
}
