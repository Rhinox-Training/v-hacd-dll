using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Rhinox.Lightspeed;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MeshProcess
{ 
    public class VHACD : MonoBehaviour
    {
        private bool HasGeneratedColliders => !_generatedColliders.IsNullOrEmpty();
        [ShowIf(nameof(HasGeneratedColliders)), ListDrawerSettings(IsReadOnly = true)]
        [InlineButton(nameof(ClearColliders), "Clear")]
        [SerializeField]
        private List<MeshCollider> _generatedColliders;
        
        public IList<MeshCollider> GeneratedColliders => _generatedColliders ?? (IList<MeshCollider>)Array.Empty<MeshCollider>();

        [Title("Parameters"), InlineProperty, HideLabel]
        [System.Serializable]
        public unsafe struct Parameters
        {
            public void Init()
            {
                m_callback = null;
                m_logger = null;
                m_taskRunner = null;
                m_maxConvexHulls = 64;
                m_resolution = 400000;
                m_minimumVolumePercentErrorAllowed = 1;
                m_maxRecursionDepth = 10;
                m_shrinkWrap = true;
                m_fillMode = 0;
                m_maxNumVerticesPerCH = 64;
                m_asyncACD = true;
                m_minEdgeLength = 2;
                m_findBestPlane = false;
            }
            
            public void* m_callback; // Optional user provided callback interface for progress
            public void* m_logger; // Optional user provided callback interface for log messages
            public void* m_taskRunner; // Optional user provided interface for creating tasks

            [Tooltip("The maximum number of convex hulls to produce")]
            public uint m_maxConvexHulls;
            
            [Tooltip("maximum number of voxels generated during the voxelization stage")] [Range(10000, 64000000)]
            public uint m_resolution;
            
            [Tooltip("maximum concavity")] [Range(0, 100)]
            public double m_minimumVolumePercentErrorAllowed;
            
            [Tooltip("The maximum recursion depth")] [Range(1, 32)]
            public int m_maxRecursionDepth;

            [Tooltip("This will project the output convex hull vertices onto the original source mesh to increase the floating point accuracy of the results")]
            public bool m_shrinkWrap;
            
            [Tooltip("How to fill the interior of the voxelized mesh.\n" +
                     "0: FLOOD_FILL - after the voxelization step it uses a flood fill to determine 'inside' from outside. However, meshes with holes can fail and create hollow results.\n" +
                     "1: SURFACE_ONLY - Only consider the 'surface', will create 'skins' with hollow centers.\n" +
                     "2: RAYCAST_FILL, Uses raycasting to determine inside from outside")] [Range(0, 2)]
            public uint m_fillMode;
            
            [Tooltip("The maximum number of vertices allowed in any output convex hull")] [Range(4, 1024)]
            public uint m_maxNumVerticesPerCH;

            [Tooltip("Whether or not to run asynchronously, taking advantage of additional cores")]
            public bool m_asyncACD;

            [Tooltip("Once a voxel patch has an edge length of less than 4 on all 3 sides, we don't keep recursing")]
            [Range(1, 4)]
            public uint m_minEdgeLength;

            [Tooltip("Whether or not to attempt to split planes along the best location. Experimental feature.")]
            public bool m_findBestPlane;

        };

        unsafe struct Vertex
        {
            public double mx;
            public double my;
            public double mz;
        }
        
        unsafe struct Triangle
        {
            public uint mI0;
            public uint mI1;
            public uint mI2;
        }
        
        unsafe struct ConvexHull
        {
            public Vertex* m_points; // array of vertices (which are 3 doubles)
            public Triangle* m_triangles;
            public double m_volume;
            public int m_meshId;
            public fixed double m_center[3];
            public fixed double mBmin[3];
            public fixed double mBmax[3];
        };

        [DllImport("libvhacd")]
        static extern unsafe void* CreateVHACD();
        
        [DllImport("libvhacd")]
        static extern unsafe void* CreateVHACD_ASYNC();

        [DllImport("libvhacd")]
        static extern unsafe bool ComputeFloat(
            void* pVHACD,
            float* points,
            uint countPoints,
            uint* triangles,
            uint countTriangles,
            Parameters* parameters);

        [DllImport("libvhacd")]
        static extern unsafe bool ComputeDouble(
            void* pVHACD,
            double* points,
            uint countPoints,
            uint* triangles,
            uint countTriangles,
            Parameters* parameters);

        [DllImport("libvhacd")]
        static extern unsafe uint GetNConvexHulls(void* pVHACD);

        [DllImport("libvhacd")]
        static extern unsafe bool GetConvexHull(
            void* pVHACD,
            uint index,
            ConvexHull* ch);

        [DllImport("libvhacd")]
        static extern unsafe uint GetConvexHullVerticesCount(
            void* pVHACD,
            uint index);

        [DllImport("libvhacd")]
        static extern unsafe uint GetConvexHullTrianglesCount(
            void* pVHACD,
            uint index);

        public Parameters m_parameters;

        public VHACD()
        {
            m_parameters.Init();
        }

        private unsafe void GenerateVHACD(Mesh mesh, out void* vhacd, out uint numHulls)
        {
            vhacd = CreateVHACD();
            var parameters = m_parameters;

            var verts = mesh.vertices;
            var tris = mesh.triangles;
            fixed (Vector3* pVerts = verts)
            fixed (int* pTris = tris)
            {
                ComputeFloat(
                    vhacd,
                    (float*) pVerts, (uint) verts.Length,
                    (uint*) pTris, (uint) tris.Length / 3,
                    &parameters);
            }

            numHulls = GetNConvexHulls(vhacd);
        }

        [ContextMenu("Clear Generated Colliders")]
        private void ClearColliders()
        {
            if (!_generatedColliders.IsNullOrEmpty())
            {
                foreach (var collider in _generatedColliders)
                    DestroyImmediate(collider);
                _generatedColliders.Clear();
            }

            if (_generatedColliders == null)
                _generatedColliders = new List<MeshCollider>();
        }

        public unsafe List<Mesh> GenerateConvexMeshes(void* vhacd, int numHulls)
        {
            List<Mesh> convexMesh = new List<Mesh>(numHulls);
            for (uint index = 0; index < numHulls; ++index)
            {
                ConvexHull hull;
                if (!GetConvexHull(vhacd, index, &hull))
                    continue;

                uint nVertices = GetConvexHullVerticesCount(vhacd, index);
                uint nTriangles = GetConvexHullTrianglesCount(vhacd, index);

                var hullMesh = new Mesh();
                var hullVerts = new Vector3[nVertices];
                var pComponents = hull.m_points;

                for (int i = 0; i < hullVerts.Length; ++i)
                {
                    hullVerts[i] = new Vector3(
                        (float) pComponents[0].mx,
                        (float) pComponents[0].my,
                        (float) pComponents[0].mz
                        );
                    
                    pComponents += 1;
                }

                hullMesh.SetVertices(hullVerts);
                
                var indices = new int[nTriangles * 3];

                for (int i = 0; i < nTriangles; ++i)
                {
                    var triangle = hull.m_triangles[i];
                    Debug.Log($"Tri: {triangle.mI0}; {triangle.mI1}; {triangle.mI2}");
                    indices[i*3+0] = (int) triangle.mI0;
                    indices[i*3+1] = (int) triangle.mI1;
                    indices[i*3+2] = (int) triangle.mI2;
                }
                
                hullMesh.SetTriangles(indices, 0);


                convexMesh.Add(hullMesh);
            }

            return convexMesh;
        }

        public unsafe void GenerateVHACDScript()
        {
            var meshFilters = GetComponentsInChildren<MeshFilter>(true);
            if (!meshFilters.Any())
            {
                Debug.LogWarning("You need a meshfilter(s) to generate a v-vacd collider.");
                return;
            }

            List<VHACD_Info> infos = new List<VHACD_Info>();
            foreach (var meshFilter in meshFilters)
            {
                var mesh = meshFilter.sharedMesh;
                if (mesh && mesh.isReadable)
                {
                    if (mesh.bounds.size.x > 0.0f && mesh.bounds.size.y > 0.0f && mesh.bounds.size.z > 0.0f)
                    {                        
                        GenerateVHACD(meshFilter.sharedMesh, out var vhacd, out var numHulls);
                        infos.Add(new VHACD_Info
                        {
                            MeshFilter = meshFilter,
                            NumHulls = (int) numHulls,
                            VHACD = vhacd
                        });
                    }
                    else
                    {
                        Debug.LogWarning("Can't calculate VHACD on mesh with a dimensions of size 0");
                    }
                }
            }

            ClearColliders();

            foreach (var info in infos)
            {
                var root = info.GetOrCreateRoot();
                var meshes = GenerateConvexMeshes(info.VHACD, info.NumHulls);

                foreach (var mesh in meshes)
                {
                    var collider = root.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    collider.convex = true;

                    _generatedColliders.Add(collider);
                }
            }
        }


        private const string GEN_NAME = "[Generated] VHACD";
        private unsafe struct VHACD_Info
        {
            public void* VHACD;
            public int NumHulls;
            public MeshFilter MeshFilter;

            public Transform GetOrCreateRoot()
            {
                var root = MeshFilter.transform.Find(GEN_NAME);
                if (root == null)
                {
                    root = new GameObject(GEN_NAME).transform;
                    root.SetParent(MeshFilter.transform, false);
                }

                return root;
            }

        }


#if UNITY_EDITOR
        [ContextMenu("Generate VHACD")]
        private unsafe void GenerateVHACDCollision()
        {
            var meshFilters = GetComponentsInChildren<MeshFilter>(true);
            if (!meshFilters.Any())
            {
                Debug.LogWarning("You need a meshfilter(s) to generate a v-vacd collider.");
                return;
            }

            List<VHACD_Info> infos = new List<VHACD_Info>();
            foreach (var meshFilter in meshFilters)
            {
                var mesh = meshFilter.sharedMesh;
                if (mesh)
                {
                    if (mesh.bounds.size.x > 0.0f && mesh.bounds.size.y > 0.0f && mesh.bounds.size.z > 0.0f)
                    {                        
                        GenerateVHACD(meshFilter.sharedMesh, out var vhacd, out var numHulls);
                        infos.Add(new VHACD_Info
                        {
                            MeshFilter = meshFilter,
                            NumHulls = (int) numHulls,
                            VHACD = vhacd
                        });
                    }
                    else
                    {
                        Debug.LogWarning("Can't calculate VHACD on mesh with a dimensions of size 0");
                    }
                }
            }

            var totalMeshes = infos.Sum(x => x.NumHulls);

            if (totalMeshes > 10)
            {
                var cont = EditorUtility.DisplayDialog(
                    "Lots of meshes",
                    $"A total of {totalMeshes} colliders will be generated. Are you sure you wish to continue?",
                    "Yes", "No");

                if (!cont) return;
            }

            ClearColliders();

            foreach (var info in infos)
            {
                var root = info.GetOrCreateRoot();
                var meshes = GenerateConvexMeshes(info.VHACD, info.NumHulls);

                foreach (var mesh in meshes)
                {
                    var collider = root.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    collider.convex = true;

                    _generatedColliders.Add(collider);
                }
            }
        }
#endif
    }
}
