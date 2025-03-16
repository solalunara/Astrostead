using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;

public enum AtlasType : int
{
    STANDARD,
}

public enum StandardAtlasTextures
{
    NONE,
    GRASS,
    DIRT
}

public class TextureAtlas : MonoBehaviour
{
    public static bool AtlasExists( AtlasType t ) => s_pAtlasDict.ContainsKey( t );
    public static TextureAtlas GetAtlas( AtlasType t ) 
    {
        if ( !s_pAtlasDict.ContainsKey( t ) )
        {
            foreach ( TextureAtlas pAtlas in FindObjectsOfType<TextureAtlas>() )
            {
                if ( pAtlas.m_iType == t )
                    s_pAtlasDict.Add( t, pAtlas );
            }
        }
        return s_pAtlasDict[ t ];
    }
    readonly static Dictionary<AtlasType, TextureAtlas> s_pAtlasDict = new();
    public Material VoxelMaterial => m_pVoxelMaterial;
    public AtlasType Type => m_iType;
    public uint TexturesCountX => m_iTexturesCountX;
    public uint TexturesCountY => m_iTexturesCountY;
    [SerializeField] private Material m_pVoxelMaterial;
    [SerializeField] private AtlasType m_iType;
    [SerializeField] private uint m_iTexturesCountX;
    [SerializeField] private uint m_iTexturesCountY;

    void OnEnable()
    {
        // second condition will force an exception in the event of a false duplicate
        if ( !s_pAtlasDict.ContainsKey( m_iType ) || s_pAtlasDict[ m_iType ] != this )
            s_pAtlasDict.Add( m_iType, this );
    }
}