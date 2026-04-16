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

public class CylindricalVoxelGroup : VoxelGroup
{
    public override Geometry GetGeometry() => Geometry.CYLINDRICAL;

    public override Vector3 IndexToLocalCoordinate( Vector3Int vPos )
    {
        // y acts as normal, x->r, z->theta
        float fRadius = vPos.x * m_vVoxelSize.x;
        float fTheta = -vPos.z * GetDeltaTheta( vPos.x );

        return new( fRadius * Mathf.Sin( fTheta ), vPos.y * m_vVoxelSize.y, fRadius * Mathf.Cos( fTheta ) );
    }

    protected override IEnumerable<(Vector3Int, Vector3Int)> GetNeighbors(Vector3Int vPos)
    {
        // up and down the cylinder are trivial
        yield return (TravelAlongDirection(vPos, Vector3Int.up), Vector3Int.up);
        yield return (TravelAlongDirection(vPos, Vector3Int.down), Vector3Int.down);

        // as are increasing and decreasing theta
        yield return (TravelAlongDirection(vPos, Vector3Int.forward), Vector3Int.forward);
        yield return (TravelAlongDirection(vPos, Vector3Int.back), Vector3Int.back);

        // fDeltaTheta is monatonically decreasing, so there can only be one voxel at a lower radius
        // but its theta index will be different based on the ratio of fDeltaTheta
        float fDeltaTheta = GetDeltaTheta( vPos.x );
        if ( vPos.x > 1 )
        {
            float fDeltaThetaBelow = GetDeltaTheta( vPos.x - 1 );
            int iDivFactor = Mathf.RoundToInt( fDeltaThetaBelow / fDeltaTheta );
            Debug.Assert( iDivFactor >= 1 );

            Vector3Int vPosBelow = new( vPos.x - 1, vPos.y, vPos.z / iDivFactor );
            yield return (vPosBelow, Vector3Int.left);
        }
        // the opposite is true for higher radii
        float fDeltaThetaAbove = GetDeltaTheta( vPos.x + 1 );
        int iMultFactor = Mathf.RoundToInt( fDeltaTheta / fDeltaThetaAbove );
        Debug.Assert( iMultFactor >= 1 );
        for ( int i = 0; i < iMultFactor; ++i )
        {
            Vector3Int vPosAbove = new( vPos.x + 1, vPos.y, vPos.z * iMultFactor + i );
            yield return (vPosAbove, Vector3Int.right);
        }
    }

    public float GetDeltaTheta( int iRadCoord )
    {
        // cache delta theta calculations
        float fRadius = iRadCoord * m_vVoxelSize.x;
        if ( !m_pDeltaThetaMap.TryGetValue( iRadCoord, out float fDeltaTheta ) )
        {
            fDeltaTheta = Mathf.PI / 2.0f;
            while ( fRadius * fDeltaTheta > m_vVoxelSize[ 2 ] )
                fDeltaTheta /= 2.0f;
            m_pDeltaThetaMap.Add( iRadCoord, fDeltaTheta );
        }
        return fDeltaTheta;
    }

    public override Vector3Int TravelAlongDirection(Vector3Int vPos, Vector3Int vDir)
    {
        if ( vDir == Vector3Int.up || vDir == Vector3Int.down )
            return vPos + vDir;
        else if ( vDir == Vector3Int.forward || vDir == Vector3Int.back )
        {
            // we can just add to the theta index, but we need to wrap around if we go over the max theta index for this radius
            float fDeltaTheta = GetDeltaTheta( vPos.x );
            int iThetaSteps = Mathf.RoundToInt( 2 * Mathf.PI / fDeltaTheta );

            // Given the delta theta calculation, we should never have less than 4 steps in theta - min shape should be a square prism
            Debug.Assert( iThetaSteps > 3 );

            int iThetaIndex = (vPos.z + vDir.z) % iThetaSteps;
            if ( iThetaIndex < 0 )
                iThetaIndex += iThetaSteps;
            return new( vPos.x, vPos.y, iThetaIndex );
        }
        else if ( vDir == Vector3Int.left )
        {
            float fDeltaTheta = GetDeltaTheta( vPos.x );
            int iThetaSteps = Mathf.RoundToInt( fDeltaTheta / GetDeltaTheta( vPos.x - 1 ) );
            return new( vPos.x - 1, vPos.y, Mathf.RoundToInt( vPos.z / iThetaSteps ) );
        }
        else if ( vDir == Vector3Int.right )
        {
            float fDeltaTheta = GetDeltaTheta( vPos.x );
            int iThetaSteps = Mathf.RoundToInt( fDeltaTheta / GetDeltaTheta( vPos.x + 1 ) );
            return new( vPos.x + 1, vPos.y, Mathf.RoundToInt( vPos.z * iThetaSteps ) );
        }
        else
            // increasing r then theta versus increasing theta then r give different results, so we need to specify the direction for disambiguation
            throw new ArgumentException( $"Invalid direction {vDir} for cylindrical voxel group" );
    }

    private readonly Dictionary<int, float> m_pDeltaThetaMap = new();
}