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


public class CartesianVoxelGroup : VoxelGroup
{
    public override Geometry GetGeometry() { return Geometry.CARTESIAN; }

    public override Vector3 IndexToLocalCoordinate( Vector3Int vPos ) => new( vPos.x * m_vVoxelSize.x, vPos.y * m_vVoxelSize.y, vPos.z * m_vVoxelSize.z );

    public override Vector3Int TravelAlongDirection( Vector3Int vPos, Vector3Int vDir )
    {
        // For a cartesian grid, traveling along a direction is just adding the direction to the position
        return vPos + vDir;
    }

    protected override IEnumerable<(Vector3Int, Vector3Int)> GetNeighbors(Vector3Int vPos)
    {
        Vector3Int[] pvDirections = new Vector3Int[] { 
            Vector3Int.forward,
            Vector3Int.back,
            Vector3Int.right,
            Vector3Int.left,
            Vector3Int.up,
            Vector3Int.down
        };

        foreach ( Vector3Int vDir in pvDirections )
            yield return (TravelAlongDirection( vPos, vDir ), vDir);
    }
}