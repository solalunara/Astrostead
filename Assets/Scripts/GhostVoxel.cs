using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http.Headers;
using Unity.VisualScripting;
using UnityEngine;
using static Statics;


public class GhostVoxel : IVoxelBase
{
    public GhostVoxel( Voxel pRealVoxel )
    {
        m_pAssociatedRealVoxel = pRealVoxel;
    }

    public VoxelGroup OwningGroup { get => m_pAssociatedRealVoxel.OwningGroup; set => m_pAssociatedRealVoxel.OwningGroup = value; }
    public Vector3Int Position { get => m_pAssociatedRealVoxel.Position; set => m_pAssociatedRealVoxel.Position = value; }
    public bool CalculatingExposedNormals { get => m_pAssociatedRealVoxel.CalculatingExposedNormals; set => m_pAssociatedRealVoxel.CalculatingExposedNormals = value; }
    public BlockType Block { get => m_pAssociatedRealVoxel.Block; set => m_pAssociatedRealVoxel.Block = value; }

    public Vector3 Centre => m_pAssociatedRealVoxel.Centre;

    public Vector3 LocalUp => m_pAssociatedRealVoxel.LocalUp;

    public int? GetFaceFacingDirection( Vector3Int vDirection ) => m_pAssociatedRealVoxel.GetFaceFacingDirection( vDirection );

    public Mesh GetOrAddMesh() => m_pAssociatedRealVoxel.GetOrAddMesh();

    public Transform GetTransform() => m_pAssociatedRealVoxel.GetTransform();

    public void RefreshTriangles() => m_pAssociatedRealVoxel.RefreshTriangles();

    public void RefreshUVs() => m_pAssociatedRealVoxel.RefreshUVs();

    public void RefreshVertices() => m_pAssociatedRealVoxel.RefreshVertices();

    public void SetActive( bool bActive ) => m_pAssociatedRealVoxel.SetActive( bActive );

    private readonly Voxel m_pAssociatedRealVoxel;
}