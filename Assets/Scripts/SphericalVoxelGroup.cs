#define DEBUG

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Jobs;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.VFX;
using static Statics;


public class SphericalVoxelGroup : VoxelGroup
{
    public override Geometry GetGeometry() => Geometry.SPHERICAL;

    public override Vector3 IndexToLocalCoordinate( Vector3Int vPos )
    {
        return IndexToLocalCoordinateDirect( vPos ); //temp for now
    }

    private Vector3 IndexToLocalCoordinateDirect( Vector3Int vPos )
    {
        // this method is the same as IndexToLocalCoordinate but without the lerping logic for when delta theta changes between rings
        // to save time (and prevent a stackoverflow) in the lerping logic

        // y acts as normal, x->r, z->theta
        float fRadius = vPos.x * m_vVoxelSize.x;

        float fTheta = vPos.y * GetDeltaTheta( vPos.x );
        float fPhi = vPos.z * GetDeltaPhi( vPos.x, vPos.y );

        return new( fRadius * Mathf.Sin( fTheta ) * Mathf.Sin( fPhi ),
                    fRadius * Mathf.Cos( fTheta ),
                    fRadius * Mathf.Sin( fTheta ) * Mathf.Cos( fPhi ) );
    }

    public override Vector3Int TravelAlongDirection( Vector3Int vPos, Vector3Int vDir )
    {
        // For a difference in phi, the same logic as theta in cylindrical applies
        // we just need to wrap around at 2pi
        float fDeltaTheta = GetDeltaTheta( vPos.x );
        float fDeltaPhi = GetDeltaPhi( vPos.x, vPos.y );
        float fDeltaPhiH = GetDeltaPhi( vPos.x, vPos.y + 1 );
        int iPhiHFactor = Mathf.RoundToInt( Mathf.Max( 1, fDeltaPhiH / fDeltaPhi ) );
        int iThetaSteps = Mathf.RoundToInt( Mathf.PI / fDeltaTheta );
        int iPhiSteps = Mathf.RoundToInt( 2 * Mathf.PI / fDeltaPhi );

        // Given the delta phi calculation, we should never have less than 4 steps in phi - min shape should be an octahedron
        Debug.Assert( iPhiSteps > 3 && iThetaSteps > 1 );

        if ( vDir.x == 0 && vDir.y == 0 )
        {
            int iPhiIndex = (vPos.z + vDir.z * iPhiHFactor) % iPhiSteps;
            if ( iPhiIndex < 0 )
                iPhiIndex += iPhiSteps;
            return new( vPos.x, vPos.y, iPhiIndex );
        }
        // For a difference in theta, we need to account for the number of phi steps changing as we move in theta
        else if ( vDir == Vector3Int.up || vDir == Vector3Int.down )
        {
            // special case for theta=0 or pi, we actually want to move phi by 180 degrees
            //if ( vDir == Vector3Int.down && vPos.y == 0 )
            //    return new( vPos.x, vPos.y, (vPos.z + iPhiSteps / 2) % iPhiSteps );
            //else if ( vDir == Vector3Int.up && vPos.y == iThetaSteps - 1 )
            //    return new( vPos.x, vPos.y, (vPos.z + iPhiSteps / 2) % iPhiSteps );

            // otherwise, we need to do a similar conversion as in cylindrical to find the new phi index
            float fNewDeltaPhi = GetDeltaPhi( vPos.x, vPos.y + vDir.y );
            int iNewPhiSteps = Mathf.RoundToInt( 2 * Mathf.PI / fNewDeltaPhi );

            // unlike in cylindrical, delta phi can both increase and decrease as we move in theta, so we need to account for both cases
            // these may look like the same expression but the integer division will cause them to behave differently, as intended
            if ( iNewPhiSteps > iPhiSteps )
            {
                int iPhiIndex = vPos.z * ( iNewPhiSteps / iPhiSteps );
                return new( vPos.x, vPos.y + vDir.y, iPhiIndex );
            }
            else
            {
                int iPhiIndex = vPos.z / ( iPhiSteps / iNewPhiSteps ); // truncation!
                return new( vPos.x, vPos.y + vDir.y, iPhiIndex );
            }
        }

        // For a difference in radius, its easier to split it up into the two separate cases
        else if ( vDir == Vector3Int.left )
        {
            // First get the modified theta index, then use that to get the modified phi index
            float fDeltaThetaBelow = GetDeltaTheta( vPos.x - 1 );
            int iDivFactor = Mathf.RoundToInt( fDeltaThetaBelow / fDeltaTheta );
            Debug.Assert( iDivFactor >= 1 );
            int iThetaIndex = vPos.y / iDivFactor; // truncation!

            float fDeltaPhiBelow = GetDeltaPhi( vPos.x - 1, iThetaIndex );
            int iPhiDivFactor = Mathf.RoundToInt( fDeltaPhiBelow / fDeltaPhi );
            //Debug.Assert( iPhiDivFactor >= 1 );
            int iPhiIndex;
            if ( iPhiDivFactor < 1 ) // this is possible if both delta theta and delta phi are changing in the south pole
            {
                int iPhiMultFactor = Mathf.RoundToInt( fDeltaPhi / fDeltaPhiBelow );
                Debug.Assert( iPhiMultFactor >= 1 );
                iPhiIndex = vPos.z * iPhiMultFactor; 
            }
            else
                iPhiIndex = vPos.z / iPhiDivFactor; // truncation!

            return new( vPos.x - 1, iThetaIndex, iPhiIndex );
        }

        // when radius increases, in principle we have many voxels to choose from, as in the cylindrical case
        // so we employ the same logic of integer multiplication to essentially select the corner voxel
        else if ( vDir == Vector3Int.right )
        {
            float fDeltaThetaAbove = GetDeltaTheta( vPos.x + 1 );
            int iMultFactor = Mathf.RoundToInt( fDeltaTheta / fDeltaThetaAbove );
            Debug.Assert( iMultFactor >= 1 );
            int iThetaIndex = vPos.y * iMultFactor;

            float fDeltaPhiAbove = GetDeltaPhi( vPos.x + 1, iThetaIndex );
            int iPhiMultFactor = Mathf.RoundToInt( fDeltaPhi / fDeltaPhiAbove );
            Debug.Assert( iPhiMultFactor >= 1 );
            int iPhiIndex = vPos.z * iPhiMultFactor;

            return new( vPos.x + 1, iThetaIndex, iPhiIndex );
        }
        else
            throw new ArgumentException( "Invalid direction for TravelAlongDirection in SphericalVoxelGroup. Direction must be along one of the three axes." );
    }

    protected override IEnumerable<(Vector3Int, Vector3Int)> GetNeighbors( Vector3Int vPos )
    {
        HashSet<(Vector3Int, Vector3Int)> pValues = new( GetNonuniqueNeighbors( vPos ) );

#if DEBUG
        // Debug code to check each voxel really is only included once
        List<Vector3Int> pVoxelPositionList = new();
        foreach ( (Vector3Int vVoxel, Vector3Int VDir) in pValues )
        {
            Debug.Assert( !pVoxelPositionList.Contains( vVoxel ) );
            pVoxelPositionList.Add( vVoxel );
        }
#endif

        return pValues;
    }
    protected IEnumerable<(Vector3Int, Vector3Int)> GetNonuniqueNeighbors(Vector3Int vPos)
    {
        // For any direction where there is only one voxel in that direction (changes in phi, going down in radius)
        // we can use TravelAlongDirection to get the neighbor
        Vector3Int[] pvTrivialDirections = new Vector3Int[] {
            Vector3Int.forward,
            Vector3Int.back,
            Vector3Int.left,
        };

        foreach ( Vector3Int vDir in pvTrivialDirections )
            yield return (RealBlock(TravelAlongDirection( vPos, vDir )), vDir);

        // For the other directions, we need to account for multiple possible neighbors
        // and the fact that the number of neighbors can change based on the position
        float fDeltaTheta = GetDeltaTheta( vPos.x );
        float fDeltaPhi = GetDeltaPhi( vPos.x, vPos.y );
        float fDeltaPhiH = GetDeltaPhi( vPos.x, vPos.y + 1 );
        int iThetaSteps = Mathf.RoundToInt( Mathf.PI / fDeltaTheta );
        int iPhiSteps = Mathf.RoundToInt( 2 * Mathf.PI / fDeltaPhi );
        int iPhiHFactor = Mathf.RoundToInt( Mathf.Max( 1, fDeltaPhiH / fDeltaPhi ) );

        // This deals with the case of increasing radius, using a double for loop to cover all possible neighbors
        float fDeltaThetaAbove = GetDeltaTheta( vPos.x + 1 );
        int iMultFactor = Mathf.RoundToInt( fDeltaTheta / fDeltaThetaAbove );
        Debug.Assert( iMultFactor >= 1 );
        for ( int i = 0; i < iMultFactor; ++i )
        {
            float fDeltaPhiAbove = GetDeltaPhi( vPos.x + 1, vPos.y * iMultFactor + i );

            int iPhiMultFactor = Mathf.RoundToInt( fDeltaPhi / fDeltaPhiAbove );
            //Debug.Assert( iPhiMultFactor >= 1 );
            if ( iPhiMultFactor < 1 ) // this is possible if both delta theta and delta phi are changing in the south pole
            {
                int iPhiDivFactor = Mathf.RoundToInt( fDeltaPhiAbove / fDeltaPhi );
                int iPhiIndex = vPos.z / iPhiDivFactor; // truncation!
                Vector3Int vNeighborPos = new( vPos.x + 1, vPos.y * iMultFactor + i, iPhiIndex );
                yield return (RealBlock( vNeighborPos ), Vector3Int.right);
                continue;
            }
            for ( int j = 0; j < iPhiMultFactor; ++j )
            {
                Vector3Int vNeighborPos = new( vPos.x + 1, vPos.y * iMultFactor + i, vPos.z * iPhiMultFactor + j );
                yield return (RealBlock( vNeighborPos ), Vector3Int.right);
            }
        }

        // For theta, one direction will be trivial while the other will require an iteration
        // Also, discard the (theta=0, vDir=down) and (theta=max, vDir=up) cases since they don't actually have neighbors in that direction
        if ( vPos.y > 0 )
        {
            float fDeltaPhiDecTheta = GetDeltaPhi( vPos.x, vPos.y - 1 );
            int iPhiHFactorDecTheta = Mathf.RoundToInt( Mathf.Max( 1, fDeltaPhi / fDeltaPhiDecTheta ) );
            if ( fDeltaPhiDecTheta >= fDeltaPhi )
            {
                int iPhiDivFactor = Mathf.RoundToInt( fDeltaPhiDecTheta / fDeltaPhi );
                Debug.Assert( iPhiDivFactor >= 1 );
                int iPhiIndex = vPos.z / iPhiDivFactor; // truncation!
                Vector3Int vNeighborPos = new( vPos.x, vPos.y - 1, iPhiIndex );
                yield return (RealBlock( vNeighborPos ), Vector3Int.down);
            }
            else
            {
                for ( int i = 0; i < Mathf.RoundToInt( fDeltaPhi / fDeltaPhiDecTheta ); ++i )
                {
                    Vector3Int vNeighborPos = new( vPos.x, vPos.y - 1, vPos.z * Mathf.RoundToInt( fDeltaPhi / fDeltaPhiDecTheta ) + i );
                    yield return (RealBlock( vNeighborPos ), Vector3Int.down);
                }
            }
        }
        if ( vPos.y < iThetaSteps - 1 )
        {
            float fDeltaPhiIncTheta = GetDeltaPhi( vPos.x, vPos.y + 1 );
            if ( fDeltaPhiIncTheta >= fDeltaPhi )
            {
                int iPhiDivFactor = Mathf.RoundToInt( fDeltaPhiIncTheta / fDeltaPhi );
                Debug.Assert( iPhiDivFactor >= 1 );
                int iPhiIndex = vPos.z / iPhiDivFactor; // truncation!
                Vector3Int vNeighborPos = new( vPos.x, vPos.y + 1, iPhiIndex );
                yield return (RealBlock( vNeighborPos ), Vector3Int.up);
            }
            else
            {
                for ( int i = 0; i < Mathf.RoundToInt( fDeltaPhi / fDeltaPhiIncTheta ); ++i )
                {
                    Vector3Int vNeighborPos = new( vPos.x, vPos.y + 1, vPos.z * Mathf.RoundToInt( fDeltaPhi / fDeltaPhiIncTheta ) + i );
                    yield return (RealBlock( vNeighborPos ), Vector3Int.up);
                }
            }
        }

        // if we are not a ghost block, check for associated ghost blocks and return for them
        if ( IsRealBlock( vPos ) )
        {
            foreach ( Vector3Int vGhostBlockPos in GetAssociatedGhostBlocks( vPos ) )
                foreach ( (Vector3Int vNeighbor, Vector3Int vDir) in GetNeighbors( vGhostBlockPos ) )
                    yield return (vNeighbor, vDir);
        }
    }

    protected override Vector3Int RealBlock( Vector3Int vBlockPos )
    {
        float fDeltaPhiL = GetDeltaPhi( vBlockPos.x, vBlockPos.y );
        float fDeltaPhiH = GetDeltaPhi( vBlockPos.x, vBlockPos.y + 1 );
        int iPhiHFactor = Mathf.RoundToInt( Mathf.Max( 1, fDeltaPhiH / fDeltaPhiL ) );

        if ( iPhiHFactor == 1 )
            return vBlockPos; // not a ghost block

        Vector3Int vPos = new( vBlockPos.x, vBlockPos.y, vBlockPos.z / iPhiHFactor * iPhiHFactor );

        return vPos;
    }

    protected override IEnumerable<Vector3Int> GetAssociatedGhostBlocks( Vector3Int vPos )
    {
        float fDeltaPhiL = GetDeltaPhi( vPos.x, vPos.y );
        float fDeltaPhiH = GetDeltaPhi( vPos.x, vPos.y + 1 );
        int iPhiHFactor = Mathf.RoundToInt( Mathf.Max( 1, fDeltaPhiH / fDeltaPhiL ) );

        if ( iPhiHFactor == 1 )
            yield break;

        for ( int i = 1; i < iPhiHFactor; ++i )
            yield return vPos + i * Vector3Int.forward;
    }

    public float GetDeltaTheta( int iRadCoord )
    {
        // cache delta theta calculations
        float fRadius = iRadCoord * m_vVoxelSize.x;
        if ( !m_pDeltaThetaMap.TryGetValue( iRadCoord, out float fDeltaTheta ) )
        {
            fDeltaTheta = Mathf.PI / 2.0f;
            while ( fRadius * fDeltaTheta > m_vVoxelSize[ 1 ] )
                fDeltaTheta /= 2.0f;
            m_pDeltaThetaMap.Add( iRadCoord, fDeltaTheta );
        }
        return fDeltaTheta;
    }
    public float GetDeltaPhi( int iRadCoord, int iThetaCoord )
    {
        // cache delta phi calculations
        float fRadius = iRadCoord * m_vVoxelSize.x;
        if ( !m_pDeltaPhiMap.TryGetValue( (iRadCoord, iThetaCoord), out float fDeltaPhi ) )
        {
            float fTheta = iThetaCoord * GetDeltaTheta( iRadCoord );

            fDeltaPhi = Mathf.PI / 2.0f;
            while ( fRadius * Mathf.Sin( fTheta ) * fDeltaPhi > m_vVoxelSize[ 2 ] )
                fDeltaPhi /= 2.0f;
            m_pDeltaPhiMap.Add( (iRadCoord, iThetaCoord), fDeltaPhi );
        }
        return fDeltaPhi;
    }

    private readonly Dictionary<int, float> m_pDeltaThetaMap = new();
    private readonly Dictionary<(int, int), float> m_pDeltaPhiMap = new();
}