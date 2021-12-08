// Copyright (c) Adam Jůva.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.Assertions;

namespace WaterSystem
{
	/// <summary>
	/// Class responsible for creating water mesh and flow map.
	/// </summary>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(MeshFilter))]
	[RequireComponent(typeof(MeshRenderer))]
	[RequireComponent(typeof(MeshCollider))]
    public class WaterBuilder : MonoBehaviour
    {
		[Tooltip("Density of the water mesh.")]
	    [SerializeField, Range(1.0f, 100.0f)] private float triangulationDensity = 98.0f;

		[Tooltip("Value used for width and height of the flow map, greater value means better quality.")]
		[SerializeField] private int flowMapWidth = 256;

		[Tooltip("Value used for width and height of the water height map, greater value means better quality.")]
		[SerializeField] private int waterHeightMapWidth = 256;

		private MeshFilter waterMeshFilter;
	    private MeshRenderer waterRenderer;
		private MeshCollider waterCollider;

		private WaterMeshCreator waterMeshCreator;
		private WaterFlowMapCreator waterFlowMapCreator;

		private Mesh waterMesh;

		private Transform waterMeshPointsTransform;

		private RenderTexture waterHeightInputTexture;
		private RenderTexture flowMap;
		private Material drawFlowMapMaterial;
		private Material drawVertexPositionsMaterial;

		private static readonly int FlowMapID = Shader.PropertyToID("_FlowMap");


		private const string DrawFlowMapShaderPath = "Shaders/DrawFlowMap";
		private const string DrawVertexHeightsShaderPath = "Shaders/DrawVertexHeights";

		private const string WaterMaterialsDirectoryPath = "Assets/WaterSystem/Generated/Materials";
		private const string WaterMeshesDirectoryPath = "Assets/WaterSystem/Generated/Meshes";
		private const string WaterFlowMapsDirectoryPath = "Assets/WaterSystem/Generated/FlowMaps";

		private const string WaterMaterialPath = "Assets/WaterSystem/Generated/Materials/WaterSurface-{0}.mat";
		private const string WaterMeshPath = "Assets/WaterSystem/Generated/Meshes/Water-{0}.asset";
		private const string WaterFlowMapPath = "Assets/WaterSystem/Generated/FlowMaps/FlowMap-{0}.png";

		private const string MeshPoints = "MeshPoints";
		private const string FlowVectors = "FlowVectors";


		/// <summary>
		/// Contains heights of the water for individual vertices without including waves.
		/// </summary>
		public RenderTexture WaterHeightInputTexture
		{
			get
			{
				if (waterHeightInputTexture == null)
				{
					waterHeightInputTexture = new RenderTexture(waterHeightMapWidth, waterHeightMapWidth, 0,
						RenderTextureFormat.RFloat)
					{
						filterMode = FilterMode.Bilinear
					};
					waterHeightInputTexture.Create();

					if (Application.isPlaying)
						WriteVerticesToTexture(WaterMeshFilter.sharedMesh.vertices, WaterMeshFilter.sharedMesh.triangles);
				}

				return waterHeightInputTexture;
			}
		}

		public float VerticesScaleFactorXZ { get; private set; } // Factor by which vertices are scaled in the texture on XZ axes
		public float VerticesScaleFactorY { get; private set; } // Factor by which vertices are scaled in the texture on Y axis
		public Vector3 VerticesMinWorldPos { get; private set; } // Minimum values on all axes of all vertices

		public Material WaterSurfaceMaterial => Application.isPlaying ? WaterRenderer.material : WaterRenderer.sharedMaterial;

		private MeshFilter WaterMeshFilter => waterMeshFilter ?? (waterMeshFilter = GetComponent<MeshFilter>());
		private MeshRenderer WaterRenderer => waterRenderer ?? (waterRenderer = GetComponent<MeshRenderer>());
		private MeshCollider WaterCollider => waterCollider ?? (waterCollider = GetComponent<MeshCollider>());

		private WaterMeshCreator WaterMeshCreator => waterMeshCreator ?? (waterMeshCreator = new WaterMeshCreator());
		private WaterFlowMapCreator WaterFlowMapCreator => waterFlowMapCreator ?? (waterFlowMapCreator = new WaterFlowMapCreator(WaterMeshFilter, DrawFlowMapMaterial));

		/// <summary>
		/// Material for drawing flow map.
		/// </summary>
		private Material DrawFlowMapMaterial
		{
			get
			{
				if (drawFlowMapMaterial == null)
				{
					var drawFlowMapShader = Resources.Load<Shader>(DrawFlowMapShaderPath);
					Assert.IsNotNull(drawFlowMapShader, $"Shader at path {DrawFlowMapShaderPath} not found!");
					drawFlowMapMaterial = new Material(drawFlowMapShader);
				}

				return drawFlowMapMaterial;
			}
		}

		/// <summary>
		/// Material for drawing vertex positions of this mesh.
		/// </summary>
		private Material DrawVertexPositionsMaterial
		{
			get
			{
				if (drawVertexPositionsMaterial == null)
				{
					var drawVertexPositionsShader = Resources.Load<Shader>(DrawVertexHeightsShaderPath);
					Assert.IsNotNull(drawVertexPositionsShader, $"Shader at path {DrawVertexHeightsShaderPath} not found!");
					drawVertexPositionsMaterial = new Material(drawVertexPositionsShader);
				}

				return drawVertexPositionsMaterial;
			}
		}


		public event Action<float, float, Vector3> WaterBoundsChanged;
		public event Action<Texture2D> FlowMapCreated;


		private async void Reset()
		{
			name = "Water";
			gameObject.layer = 4; // Water layer

			CreateWaterSurfaceMaterial();

			await CreateWaterMesh();

			var waterBase = GetComponent<WaterBase>();
			if (waterBase != null)
			{
				waterBase.Reset();
			}
			else
			{
				AddWaterBase();
			}

			CreateFlowMap();
		}

		private void OnEnable()
		{
			WaterCollider.convex = true;
			WaterCollider.isTrigger = true;

			InitializeMeshPoints();
			InitializeFlowVectors();

			waterMesh = WaterMeshFilter.sharedMesh;
		}

		private void OnDisable()
		{
			if (Application.isPlaying)
				Destroy(WaterSurfaceMaterial);

			if (drawFlowMapMaterial != null)
				DestroyImmediate(drawFlowMapMaterial);

			if (drawVertexPositionsMaterial != null)
				DestroyImmediate(drawVertexPositionsMaterial);

			if (flowMap != null)
				flowMap.Release();

			if (waterHeightInputTexture != null)
				waterHeightInputTexture.Release();

			Resources.UnloadUnusedAssets();
		}

#if UNITY_EDITOR
		private void OnDrawGizmos()
		{
			if (waterMeshPointsTransform == null)
			{
				waterMeshPointsTransform = transform.Find(MeshPoints);
			}

			if (waterMeshPointsTransform == null || waterMeshPointsTransform.childCount < 3)
				return;

			for (var i = 0; i < waterMeshPointsTransform.childCount; i++)
			{
				var p1 = waterMeshPointsTransform.GetChild(i).position;
				var p2 = i == waterMeshPointsTransform.childCount - 1 ? waterMeshPointsTransform.GetChild(0).position : waterMeshPointsTransform.GetChild(i + 1).position;

				Handles.color = Color.blue;
				Handles.DrawBezier(p1, p2, p1, p2, Color.blue, null, 4);
			}

			//var normals = meshFilter.sharedMesh.normals;
			//var vertices = meshFilter.sharedMesh.vertices;
			//for (var i = 0; i < normals.Length; i++)
			//{
			//	Debug.DrawRay(vertices[i], normals[i] * 2.5f, Color.green, 0.5f);
			//}
		}
#endif

	    private void CreateWaterSurfaceMaterial()
	    {
#if UNITY_EDITOR
		    var waterSurfaceMaterial = new Material(Shader.Find("Water System/Water Surface"));

		    Directory.CreateDirectory(WaterMaterialsDirectoryPath);

		    var path = GetAvailableFilePath(WaterMaterialPath);
		    AssetDatabase.CreateAsset(waterSurfaceMaterial, path);
		    AssetDatabase.SaveAssets();

		    // Loading texture from storage to have same reference
		    waterSurfaceMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
		    WaterRenderer.sharedMaterial = waterSurfaceMaterial;

		    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
	    }

	    /// <summary>
		/// Creates 4 default mesh points.
		/// </summary>
	    private void CreateDefaultMeshPoints()
	    {
		    waterMeshPointsTransform = transform.Find(MeshPoints);

		    if (waterMeshPointsTransform == null)
		    {
			    waterMeshPointsTransform = CreateMeshPointsTransform();

				var pos = waterMeshPointsTransform.position;
			    CreateMeshPoint(new Vector3(-50f, 0f, +50f));
			    CreateMeshPoint(new Vector3(-50f, 0f, -50f));
			    CreateMeshPoint(new Vector3(+50f, 0f, -50f));
			    CreateMeshPoint(new Vector3(+50f, 0f, +50f));
		    }
	    }

		/// <summary>
		/// Creates default flow vector.
		/// </summary>
	    private void CreateDefaultFlowVectors()
	    {
		    // Set default flow vector as global flow
		    CreateFlowVector(Vector3.zero).IsGlobalFlow = true;
	    }

		private Transform CreateMeshPointsTransform()
		{
			var waterMeshPointsTransform = new GameObject(MeshPoints).transform;
			waterMeshPointsTransform.SetParent(transform);
			waterMeshPointsTransform.SetPositionAndRotation(transform.position, transform.rotation);
			return waterMeshPointsTransform;
		}

		private Transform CreateFlowVectorsTransform()
		{
			var flowVectorsTransform = new GameObject(FlowVectors).transform;
			flowVectorsTransform.SetParent(transform);
			flowVectorsTransform.SetPositionAndRotation(transform.position, transform.rotation);
			return flowVectorsTransform;
		}

		public void CreateMeshPoint(Vector3 localPosition = new Vector3())
		{
			if (waterMeshPointsTransform == null)
			{
				waterMeshPointsTransform = transform.Find(MeshPoints);
			}

			CreateTransformWithIcon("MeshPoint", localPosition, 1, waterMeshPointsTransform);
		}

		public WaterFlow CreateFlowVector(Vector3 localPosition = new Vector3())
		{
			var flowVectorsTransform = transform.Find(FlowVectors);
			if (flowVectorsTransform == null)
			{
				flowVectorsTransform = CreateFlowVectorsTransform();
			}

			var flowTransform = CreateTransformWithIcon("FlowVector", localPosition, 4, flowVectorsTransform);
			var flow = flowTransform.gameObject.AddComponent<WaterFlow>();
			flow.Initialize(OnGlobalFlowChanged);
			return flow;
		}

		private Transform CreateTransformWithIcon(string transformName, Vector3 localPosition, int iconIndex, Transform parent)
		{
			var property = new GameObject(transformName).transform;
			property.SetParent(parent);
			property.localPosition = localPosition;
			property.localRotation = Quaternion.identity;

			DrawPointIcon(property.gameObject, iconIndex);
			return property;
		}

		private void DrawPointIcon(GameObject obj, int index)
		{
#if UNITY_EDITOR
			var largeIcons = new GUIContent[8];
			for (var i = 0; i < largeIcons.Length; i++)
			{
				largeIcons[i] = EditorGUIUtility.IconContent("sv_label_" + (i));
			}

			var icon = largeIcons[index];
			var egu = typeof(EditorGUIUtility);
			var flags = BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic;
			var args = new object[] { obj, icon.image };
			var setIcon = egu.GetMethod("SetIconForObject", flags, null, new Type[] { typeof(UnityEngine.Object), typeof(Texture2D) }, null);
			setIcon?.Invoke(null, args);
#endif
		}

		private void InitializeMeshPoints()
		{
			waterMeshPointsTransform = transform.Find(MeshPoints);
			if (waterMeshPointsTransform == null)
			{
				CreateDefaultMeshPoints();
			}
		}

		private void InitializeFlowVectors()
		{
			var flowVectorsTransform = transform.Find(FlowVectors);
			if (flowVectorsTransform == null)
			{
				flowVectorsTransform = CreateFlowVectorsTransform();
				CreateDefaultFlowVectors();
			}

			foreach (Transform flowVector in flowVectorsTransform)
			{
				var flow = flowVector.GetComponent<WaterFlow>();
				flow.Initialize(OnGlobalFlowChanged);
			}
		}

		/// <summary>
		/// Called when global flow is changed on flow vectors.
		/// </summary>
		/// <param name="globalFlow">New global flow or null if there is not any global flow.</param>
		private void OnGlobalFlowChanged(WaterFlow globalFlow)
		{
			var flowVectorsTransform = transform.Find(FlowVectors);
			if (flowVectorsTransform == null)
			{
				return;
			}

			foreach (Transform flowVector in flowVectorsTransform)
			{
				var flow = flowVector.GetComponent<WaterFlow>();
				if (flow.IsGlobalFlow && !flow.Equals(globalFlow))
				{
					flow.IsGlobalFlow = false;
				}
			}
		}

		/// <summary>
		/// Creates water mesh.
		/// </summary>
		public async Task CreateWaterMesh()
		{
#if UNITY_EDITOR
			// Mesh has to be destroyed if it is not stored permanently
			if (waterMesh != null && !AssetDatabase.Contains(waterMesh))
				DestroyImmediate(waterMesh);
#endif

			if (waterMeshPointsTransform == null)
			{
				waterMeshPointsTransform = transform.Find(MeshPoints);
			}

			var newWaterMesh = await WaterMeshCreator.CreateWaterMesh(waterMeshPointsTransform, triangulationDensity);
			if (newWaterMesh == null)
			{
				Debug.LogError($"Problem occurred while creating water mesh.");
				return;
			}

			if (waterMeshPointsTransform == null || waterMeshPointsTransform.childCount < 3)
			{
				Debug.LogError("Mesh Points transform must be created and contains at least 3 Points before creating water mesh.");
				return;
			}

			waterMesh = newWaterMesh;
			WaterMeshFilter.sharedMesh = waterMesh;
			WaterCollider.sharedMesh = waterMesh;

			if (Application.isPlaying)
				WriteVerticesToTexture(waterMesh.vertices, waterMesh.triangles);
		}

		/// <summary>
		/// Saves water mesh to storage (available only in editor).
		/// </summary>
		public void SaveWaterMesh()
		{
#if UNITY_EDITOR
			if (waterMesh == null)
			{
				Debug.LogError("Water Mesh is not created! Create Water Mesh first and then save it.");
				return;
			}

			Directory.CreateDirectory(WaterMeshesDirectoryPath);

			var path = GetAvailableFilePath(WaterMeshPath);
			AssetDatabase.CreateAsset(waterMesh, path);
			AssetDatabase.SaveAssets();
			EditorSceneManager.MarkSceneDirty(gameObject.scene);

			EditorUtility.DisplayDialog("Water Mesh Saved Successfully!", $"Water Mesh saved successfully to: '{path}'", "OK");
#endif
		}

		/// <summary>
		/// Creates flow map texture.
		/// </summary>
		public void CreateFlowMap()
		{
			var flowVectorsTransform = transform.Find(FlowVectors);
			if (flowVectorsTransform == null)
			{
				CreateFlowVectorsTransform();
				CreateDefaultFlowVectors();
			}

			if (flowMap != null)
				flowMap.Release();

			flowMap = WaterFlowMapCreator.CreateFlowMap(flowVectorsTransform, flowMapWidth);
			WaterSurfaceMaterial.SetTexture(FlowMapID, flowMap);

			var texture = new Texture2D(flowMap.width, flowMap.height, TextureFormat.RGB24, false);
			RenderTexture.active = flowMap;
			texture.ReadPixels(new Rect(0, 0, flowMap.width, flowMap.height), 0, 0);
			texture.Apply();
			RenderTexture.active = null;

			FlowMapCreated?.Invoke(texture);
		}

		/// <summary>
		/// Saves flow map to storage (available only in editor).
		/// </summary>
		public async void SaveFlowMap()
		{
#if UNITY_EDITOR
			if (flowMap == null)
			{
				Debug.LogError("Flow Map is not created! Create Flow Map first and then save it.");
				return;
			}

			// Saving to texture to PNG
			var texture = new Texture2D(flowMap.width, flowMap.height, TextureFormat.RGB24, false);
			RenderTexture.active = flowMap;
			texture.ReadPixels(new Rect(0, 0, flowMap.width, flowMap.height), 0, 0);
			texture.Apply();
			RenderTexture.active = null;

			Directory.CreateDirectory(WaterFlowMapsDirectoryPath);

			// Saving texture to storage
			var path = GetAvailableFilePath(WaterFlowMapPath);
			var bytes = texture.EncodeToPNG();
			await Task.Run(() =>
			{
				File.WriteAllBytes(path, bytes);
			});

			AssetDatabase.Refresh();
			EditorSceneManager.MarkSceneDirty(gameObject.scene);

			// Loading texture from storage to have same reference
			texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
			FlowMapCreated?.Invoke(texture);

			EditorUtility.DisplayDialog("Flow Map Saved Successfully!", $"Flow Map saved successfully to: '{path}'", "OK");


#else
			await Task.Yield(); // To avoid async method warning
#endif
		}

		/// <summary>
		/// Writes initial heights of mesh vertices to the texture.
		/// </summary>
		/// <param name="vertices">Mesh vertices.</param>
		/// <param name="triangles">Mesh triangles.</param>
		private void WriteVerticesToTexture(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles)
		{
			var minX = vertices.OrderBy(vertex => vertex.x).FirstOrDefault().x;
			var maxX = vertices.OrderBy(vertex => vertex.x).Reverse().FirstOrDefault().x;

			var minY = vertices.OrderBy(vertex => vertex.y).FirstOrDefault().y;
			var maxY = vertices.OrderBy(vertex => vertex.y).Reverse().FirstOrDefault().y;

			var minZ = vertices.OrderBy(vertex => vertex.z).FirstOrDefault().z;
			var maxZ = vertices.OrderBy(vertex => vertex.z).Reverse().FirstOrDefault().z;

			var diffX = maxX - minX;
			var diffY = maxY - minY;
			var diffZ = maxZ - minZ;

			var edgeLength = Mathf.Max(diffX, diffZ);

			VerticesMinWorldPos = new Vector3(minX, minY, minZ);
			VerticesScaleFactorXZ = edgeLength;
			VerticesScaleFactorY = Mathf.Max(diffY, edgeLength);

			RenderTexture.active = WaterHeightInputTexture;
			GL.Clear(false, true, Color.black);

			GL.PushMatrix();
			GL.LoadOrtho();

			DrawVertexPositionsMaterial.SetPass(0);

			GL.Begin(GL.TRIANGLES);

			// Drawing initial vertices' height to input texture
			for (var i = 0; i < triangles.Count;)
			{
				var endIndex = i + 3;

				for (; i < endIndex; i++)
				{
					var vertex = vertices[triangles[i]];

					// Calculating vertex position and height on texture
					var x = (vertex.x - minX) / VerticesScaleFactorXZ;
					var y = (vertex.y - minY) / VerticesScaleFactorY;
					var z = (vertex.z - minZ) / VerticesScaleFactorXZ;

					GL.MultiTexCoord2(0, 0, y); // X component is unused, relative height is in Y component
					GL.Vertex3(x, z, 0.0f);
				}
			}

			GL.End();
			GL.PopMatrix();

			RenderTexture.active = null;

			WaterBoundsChanged?.Invoke(VerticesScaleFactorXZ, VerticesScaleFactorY, VerticesMinWorldPos);
		}

		public void AddWaterBase()
		{
			gameObject.AddComponent<WaterBase>();

#if UNITY_EDITOR
			EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		}

		public void AddWaterDynamics()
		{
			gameObject.AddComponent<WaterDynamics>();

#if UNITY_EDITOR
			EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		}

		public void AddWaterInteractions()
		{
			gameObject.AddComponent<WaterInteractions>();

#if UNITY_EDITOR
			EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		}

		private string GetAvailableFilePath(string path)
		{
			// Finding available file name
			var index = 0;
			while (File.Exists(string.Format(path, index)))
			{
				index++;
			}

			return string.Format(path, index);
		}
    }
}