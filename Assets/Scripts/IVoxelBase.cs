using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http.Headers;
using Unity.VisualScripting;
using UnityEngine;
using static Statics;

public interface IVoxelBase
{
    public VoxelGroup OwningGroup { get; set; }

    public Vector3Int Position { get; set; }

    public bool CalculatingExposedNormals { get; set; }

    public BlockType Block { get; set; }

    public Vector3 Centre { get; }

    public Vector3 LocalUp { get; }

    public Transform GetTransform();
    public void SetActive( bool bActive );

    public Mesh GetOrAddMesh();

    public void RefreshVertices();
    public void RefreshUVs();
    public void RefreshTriangles();

    public int? GetFaceFacingDirection( Vector3Int vDirection );

}