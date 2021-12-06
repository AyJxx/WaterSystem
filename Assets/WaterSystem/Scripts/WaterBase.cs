// Copyright (c) Adam Jůva.
// Licensed under the MIT License.

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace WaterSystem
{
	/// <summary>
	/// <see cref="WaterBase"/> is main class which exposes functionality of the water to outside world
	/// and also is responsible for functionality of <see cref="WaterDynamics"/> and <see cref="WaterInteractions"/>.
	/// </summary>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(WaterBuilder))]
	public class WaterBase : MonoBehaviour
	{
		public enum Tessellation
		{
			EdgeFactor, EdgeLength
		}

		[Tooltip("Compute shader for waves calculations.")]
		[SerializeField] private ComputeShader wavesComputeShader;

		[Header("Water")]
		[SerializeField] private float waterTiling = 10f;
		[SerializeField] private Color waterColor1 = new Color(165f / 255f, 255f / 255f, 244f / 255f);
		[SerializeField] private Color waterColor2 = new Color(0f / 255f, 72f / 255f, 23f / 255f);
		[SerializeField, Range(0f, 1f)] private float waterOpacity = 0.75f;
		[SerializeField] private Color underwaterColor = new Color(2f / 255f, 46f / 255f, 8f / 255f);
		[SerializeField, Range(0f, 5f)] private float underwaterDensity = 0.5f;
		[SerializeField, Range(0f, 1f)] private float refractionStrength = 0.25f;

		[Header("Normals")]
		[SerializeField, Range(0f, 1f)] private float smoothness = 0.975f;
		[SerializeField] private Color specularColor = Color.gray;
		[SerializeField] private Texture2D normalMap;
		[SerializeField, Range(0f, 1f)] private float normalScale = 0.35f;

		[Header("Flow")]
		[SerializeField] private Texture2D flowMap;
		[SerializeField] private float flowGridResolution = 5f;
		[SerializeField] private float flowSpeed = 0.5f;

		[Header("Foam")]
		[SerializeField] private bool staticFoam = true;
		[SerializeField] private Texture2D staticFoamTexture;
		[SerializeField] private Vector2 staticFoamTiling = Vector2.one * 2f;
		[SerializeField] private Vector2 staticFoamOffset;
		[SerializeField, Range(0f, 1f)] private float staticFoamStrength = 0.75f;

		[Space(10)]
		[SerializeField] private bool edgeFoam = true;
		[SerializeField] private Texture2D edgeFoamTexture;
		[SerializeField] private Vector2 edgeFoamTiling = Vector2.one * 3f;
		[SerializeField] private Vector2 edgeFoamOffset;
		[SerializeField, Range(0f, 1f)] private float edgeFoamStrength = 0.75f;

		[Header("Caustics")]
		[SerializeField] private bool caustics = false;
		[SerializeField] private Texture2D causticsTexture;
		[SerializeField] private Vector2 causticsScale1 = Vector2.one * 0.25f;
		[SerializeField] private Vector2 causticsSpeed1 = Vector2.one * 0.15f;
		[SerializeField] private Vector2 causticsScale2 = Vector2.one * 0.15f;
		[SerializeField] private Vector2 causticsSpeed2 = Vector2.one * -0.25f;
		[SerializeField, Range(0f, 100f)] private float causticsStrength = 25f;
		[SerializeField] private float causticsDepth = 3f;

		[Header("Edge Blending")]
		[SerializeField, Range(1f, 100f)] private float shoreBlendFactor = 2f;
		[SerializeField] private bool edgeFading = true;
		[SerializeField, Range(0f, 100f)] private float edgeFadingX = 0f;
		[SerializeField, Range(0f, 100f)] private float edgeFadingY = 0f;

		[Header("Tessellation")]
		[SerializeField] private Tessellation tessellation;
		[SerializeField] private float tessellationEdgeFactor = 5.0f;
		[SerializeField] private float tessellationEdgeLength = 1.0f;

		[Space(20)]
		[SerializeField] private bool specularHighlights = true;
		[SerializeField] private bool environmentReflections = true;
		[SerializeField] private bool receiveShadows;

		[Space(20)]
		[SerializeField] private CullMode cullMode = CullMode.Back;

		private WaterBuilder waterBuilder;

		private bool isInitialized;

		private int computeWavesKernel;

		private int waterHeightTextureDimensionsSize;
		private Color[] waterHeightData;
		private bool waterHeightTextureUpdated;


		private const string WaterWavesComputeShaderPath = "Assets/WaterSystem/Shaders/WaterWaves.compute";
		private const string WaterNormalMapPath = "Assets/WaterSystem/Textures/NormalMap.jpg";
		private const string WaterFoamPath = "Assets/WaterSystem/Textures/Foam.png";
		private const string WaterFoamEdgePath = "Assets/WaterSystem/Textures/FoamEdge.jpg";
		private const string WaterCausticsPath = "Assets/WaterSystem/Textures/Caustics.png";


		public Material WaterSurfaceMaterial => WaterBuilder.WaterSurfaceMaterial;

		/// <summary>
		/// Includes current heights (including waves) of the water for individual vertices.
		/// </summary>
		public RenderTexture WaterHeightMap { get; private set; }

		public ComputeShader WavesComputeShader => wavesComputeShader;

		public WaterBuilder WaterBuilder => waterBuilder ?? (waterBuilder = GetComponent<WaterBuilder>());


		public void Reset()
		{
#if UNITY_EDITOR
			wavesComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(WaterWavesComputeShaderPath);
			normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNormalMapPath);
			staticFoamTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterFoamPath);
			edgeFoamTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterFoamEdgePath);
			causticsTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterCausticsPath);
#endif

			SetMaterialProperties();
		}

		private void OnValidate()
		{
			SetMaterialProperties();
		}

		private void Awake()
		{
			if (!Application.isPlaying)
				return;

			Initialize();

			WaterBuilder.WaterBoundsChanged += OnWaterBoundsChanged;
		}

		private void OnEnable()
		{
#if UNITY_EDITOR
			UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += OnSceneSaved;
			EditorApplication.projectChanged += OnProjectChanged;
			FileModificationProcessor.ProjectSaved += OnProjectChanged;
#endif

			WaterBuilder.FlowMapCreated += OnFlowMapCreated;

			SetMaterialProperties();
		}

		private void Update()
		{
			if (Camera.main != null)
				WaterSurfaceMaterial.SetMatrix(WaterShaderProperties.ViewToWorldMatrix, Camera.main.cameraToWorldMatrix);

			if (!Application.isPlaying)
				return;

			UpdateWavesComputeShader();
		}

		private void LateUpdate()
		{
			if (!Application.isPlaying)
				return;

			waterHeightTextureUpdated = false;
		}

		private void OnDisable()
		{
#if UNITY_EDITOR
			UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= OnSceneSaved;
			EditorApplication.projectChanged -= OnProjectChanged;
			FileModificationProcessor.ProjectSaved -= OnProjectChanged;
#endif

			WaterBuilder.FlowMapCreated -= OnFlowMapCreated;
		}

		private void OnDestroy()
		{
			if (!Application.isPlaying)
				return;

			if (WaterHeightMap != null)
				WaterHeightMap.Release();

			WaterBuilder.WaterBoundsChanged -= OnWaterBoundsChanged;
		}

		private void OnTriggerEnter(Collider other)
		{
			// Check if object is IWaterEntity
			var waterEntity = other.GetComponentInParent<IWaterEntity>();
			if (waterEntity != null)
			{
				// Inject water physics
				waterEntity.CurrentWaterBase = this;
			}
		}

		private void OnTriggerExit(Collider other)
		{
			// Check if object has WaterObjectController component and if is injected by this water physics instance
			var waterEntity = other.GetComponentInParent<IWaterEntity>();
			if (waterEntity != null && waterEntity.CurrentWaterBase == this)
			{
				// Remove injection
				waterEntity.CurrentWaterBase = null;
			}
		}

		public void AttachWavesComputeBuffer(int bufferID, string wavesKeyword, ComputeBuffer wavesBuffer)
		{
			WavesComputeShader.SetBuffer(computeWavesKernel, bufferID, wavesBuffer);
			WavesComputeShader.EnableKeyword(wavesKeyword);
		}

		/// <summary>
		/// Requests new data from water height texture.
		/// </summary>
		public void RequestWaterHeightTextureUpdate()
		{
			if (waterHeightTextureUpdated)
				return;

			waterHeightTextureUpdated = true;

			AsyncGPUReadback.Request(WaterHeightMap, 0, TextureFormat.RGBAFloat, OnWaterHeightTextureProcessed);
		}

		/// <summary>
		/// Gets water height for provided world position.
		/// </summary>
		/// <param name="worldPos">World position.</param>
		/// <returns>Water height in world space.</returns>
		public float GetWaterHeight(Vector3 worldPos)
		{
			if (waterHeightData == null)
				return float.MinValue;

			worldPos.x = (worldPos.x - WaterBuilder.VerticesMinWorldPos.x) / WaterBuilder.VerticesScaleFactorXZ;
			worldPos.z = (worldPos.z - WaterBuilder.VerticesMinWorldPos.z) / WaterBuilder.VerticesScaleFactorXZ;

			var x = Mathf.FloorToInt(worldPos.x * (waterHeightTextureDimensionsSize - 1));
			var z = Mathf.FloorToInt(worldPos.z * (waterHeightTextureDimensionsSize - 1));
			var index = x + (z * waterHeightTextureDimensionsSize);

			if (index < 0 || index >= waterHeightData.Length)
				return float.MinValue;

			var waterHeight = waterHeightData[index].g;
			waterHeight = waterHeight * 2 - 1;
			waterHeight *= WaterBuilder.VerticesScaleFactorY;

			// Adding current Y position of the transform in case that transform is not on 0
			waterHeight += transform.position.y;

			return waterHeight;
		}

		/// <summary>
		/// Initializes all needed data.
		/// </summary>
		private void Initialize()
		{
			if (!Application.isPlaying || isInitialized)
				return;

			// Water height map
			var outputTextureSize = WaterBuilder.WaterHeightInputTexture.width;
			WaterHeightMap = new RenderTexture(outputTextureSize, outputTextureSize, 0, RenderTextureFormat.ARGBFloat)
			{
				enableRandomWrite = true,
				filterMode =  FilterMode.Bilinear
			};
			WaterHeightMap.Create();

			waterHeightData = new Color[outputTextureSize * outputTextureSize];
			waterHeightTextureDimensionsSize = WaterHeightMap.width;

			WaterSurfaceMaterial.SetTexture(WaterShaderProperties.WaterHeightMap, WaterHeightMap);
			WaterSurfaceMaterial.SetFloat(WaterShaderProperties.WaterHeightMapScaleFactorXZ, WaterBuilder.VerticesScaleFactorXZ);
			WaterSurfaceMaterial.SetFloat(WaterShaderProperties.WaterHeightMapScaleFactorY, WaterBuilder.VerticesScaleFactorY);
			WaterSurfaceMaterial.SetVector(WaterShaderProperties.MinWorldPos, WaterBuilder.VerticesMinWorldPos);

			// Waves compute shader
			computeWavesKernel = wavesComputeShader.FindKernel("CSComputeWaves");

			wavesComputeShader.SetTexture(computeWavesKernel, WaterShaderProperties.WaterHeightInputTexture, WaterBuilder.WaterHeightInputTexture);
			wavesComputeShader.SetTexture(computeWavesKernel, WaterShaderProperties.WaterHeightMap, WaterHeightMap);

			wavesComputeShader.SetInt(WaterShaderProperties.TextureResolution, WaterHeightMap.width);
			wavesComputeShader.SetFloat(WaterShaderProperties.WaterHeightMapScaleFactorXZ, WaterBuilder.VerticesScaleFactorXZ);
			wavesComputeShader.SetFloat(WaterShaderProperties.WaterHeightMapScaleFactorY, WaterBuilder.VerticesScaleFactorY);
			wavesComputeShader.SetVector(WaterShaderProperties.MinWorldPos, WaterBuilder.VerticesMinWorldPos);

			isInitialized = true;
		}

		private void OnWaterHeightTextureProcessed(AsyncGPUReadbackRequest request)
		{
			if (request.hasError)
				return;

			request.GetData<Color>().CopyTo(waterHeightData);
		}

		private void UpdateWavesComputeShader()
		{
			if (!isInitialized)
				return;

			WavesComputeShader.SetFloat(WaterShaderProperties.Time, Time.time);
			WavesComputeShader.Dispatch(computeWavesKernel, WaterHeightMap.width / 8, WaterHeightMap.height / 8, 1);
		}

		private void OnWaterBoundsChanged(float scaleFactorXZ, float scaleFactorY, Vector3 minWorldPos)
		{
			WavesComputeShader.SetFloat(WaterShaderProperties.WaterHeightMapScaleFactorXZ, scaleFactorXZ);
			WavesComputeShader.SetFloat(WaterShaderProperties.WaterHeightMapScaleFactorY, scaleFactorY);
			WavesComputeShader.SetVector(WaterShaderProperties.MinWorldPos, minWorldPos);

			WaterSurfaceMaterial.SetFloat(WaterShaderProperties.WaterHeightMapScaleFactorXZ, scaleFactorXZ);
			WaterSurfaceMaterial.SetFloat(WaterShaderProperties.WaterHeightMapScaleFactorY, scaleFactorY);
			WaterSurfaceMaterial.SetVector(WaterShaderProperties.MinWorldPos, minWorldPos);
		}

		private void OnFlowMapCreated(Texture2D flowMap)
		{
			this.flowMap = flowMap;
			WaterBuilder.WaterSurfaceMaterial.SetTexture(WaterShaderProperties.FlowMap, flowMap);
		}

		private void OnSceneSaved(Scene scene)
		{
			SetMaterialProperties();
		}

		private void OnProjectChanged()
		{
			SetMaterialProperties();
		}

		private void SetMaterialProperties()
		{
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.Tiling, waterTiling);
			WaterBuilder.WaterSurfaceMaterial.SetColor(WaterShaderProperties.WaterColor1, waterColor1);
			WaterBuilder.WaterSurfaceMaterial.SetColor(WaterShaderProperties.WaterColor2, waterColor2);
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.WaterOpacity, waterOpacity);
			WaterBuilder.WaterSurfaceMaterial.SetColor(WaterShaderProperties.UnderwaterColor, underwaterColor);
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.UnderwaterDensity, underwaterDensity);
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.RefractionStrength, refractionStrength);


			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.Smoothness, smoothness);
			WaterBuilder.WaterSurfaceMaterial.SetColor(WaterShaderProperties.SpecColor, specularColor);
			WaterBuilder.WaterSurfaceMaterial.SetTexture(WaterShaderProperties.NormalMap, normalMap);
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.NormalScale, normalScale);


			WaterBuilder.WaterSurfaceMaterial.SetTexture(WaterShaderProperties.FlowMap, flowMap);
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.GridResolution, flowGridResolution);
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.FlowSpeed, flowSpeed);


			if (staticFoam)
				WaterBuilder.WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.StaticFoamKeyword);
			else
				WaterBuilder.WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.StaticFoamKeyword);
			WaterBuilder.WaterSurfaceMaterial.SetTexture(WaterShaderProperties.StaticFoamTex, staticFoamTexture);
			WaterBuilder.WaterSurfaceMaterial.SetVector(WaterShaderProperties.StaticFoamTexTilingOffset,
				new Vector4(staticFoamTiling.x, staticFoamTiling.y, staticFoamOffset.x, staticFoamOffset.y));
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.StaticFoamStrength, staticFoamStrength);

			if (edgeFoam)
				WaterBuilder.WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.EdgeFoamKeyword);
			else
				WaterBuilder.WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.EdgeFoamKeyword);
			WaterBuilder.WaterSurfaceMaterial.SetTexture(WaterShaderProperties.EdgeFoamTex, edgeFoamTexture);
			WaterBuilder.WaterSurfaceMaterial.SetVector(WaterShaderProperties.EdgeFoamTexTilingOffset,
				new Vector4(edgeFoamTiling.x, edgeFoamTiling.y, edgeFoamOffset.x, edgeFoamOffset.y));
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.EdgeFoamStrength, edgeFoamStrength);


			if (caustics)
				WaterBuilder.WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.CausticsKeyword);
			else
				WaterBuilder.WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.CausticsKeyword);
			WaterBuilder.WaterSurfaceMaterial.SetTexture(WaterShaderProperties.CausticsTex, causticsTexture);
			WaterBuilder.WaterSurfaceMaterial.SetVector(WaterShaderProperties.CausticsScaleSpeed1,
				new Vector4(causticsScale1.x, causticsScale1.y, causticsSpeed1.x, causticsSpeed1.y));
			WaterBuilder.WaterSurfaceMaterial.SetVector(WaterShaderProperties.CausticsScaleSpeed2,
				new Vector4(causticsScale2.x, causticsScale2.y, causticsSpeed2.x, causticsSpeed2.y));
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.CausticsStrength, causticsStrength);
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.CausticsDepth, causticsDepth);


			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.ShoreBlendFactor, shoreBlendFactor);

			if (edgeFading)
				WaterBuilder.WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.EdgeFadingKeyword);
			else
				WaterBuilder.WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.EdgeFadingKeyword);
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.EdgeFadingX, edgeFadingX);
			WaterBuilder.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.EdgeFadingY, edgeFadingY);


			if (tessellation == Tessellation.EdgeFactor)
				WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.TessellationEdgeFactorKeyword);
			else
				WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.TessellationEdgeFactorKeyword);

			if (tessellation == Tessellation.EdgeLength)
				WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.TessellationEdgeLengthKeyword);
			else
				WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.TessellationEdgeLengthKeyword);

			WaterSurfaceMaterial.SetFloat(WaterShaderProperties.TessellationEdgeFactor, tessellationEdgeFactor);
			WaterSurfaceMaterial.SetFloat(WaterShaderProperties.TessellationEdgeLength, tessellationEdgeLength);


			if (specularHighlights)
				WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.SpecularHighlightsOffKeyword);
			else
				WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.SpecularHighlightsOffKeyword);

			if (environmentReflections)
				WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.EnvironmentReflectionsOffKeyword);
			else
				WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.EnvironmentReflectionsOffKeyword);

			if (receiveShadows)
				WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.ReceiveShadowsOffKeyword);
			else
				WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.ReceiveShadowsOffKeyword);

			WaterSurfaceMaterial.SetFloat(WaterShaderProperties.CullMode, (int)cullMode);
		}
	}
}
