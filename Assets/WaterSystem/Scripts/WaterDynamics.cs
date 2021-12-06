using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace WaterSystem
{
	/// <summary>
	/// Class used for controlling dynamic of the water like waves and foam waves.
	/// </summary>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(WaterBase))]
	public class WaterDynamics : MonoBehaviour
	{
		[Serializable]
		private class WaveDataEditor
		{
			public bool Enabled;
			public Vector2 Direction;
			public float Steepness;
			public float Length;
		}

		private struct WaveData
		{
			public int Enabled;
			public Vector2 Direction;
			public float Steepness;
			public float Length;
		}

		private struct FoamWaveData
		{
			public Vector2 Pos;
			public float Amount;
			public float Speed;
			public float Min;
			public float Max;
			public Vector2 UvMin;
		}


		[Header("Dynamic Waves")]
		[SerializeField] private bool dynamicWaves = true;
		[SerializeField] private float dynamicWaveAmplitude = 0.35f;

		[SerializeField, FormerlySerializedAs("wavesDataEditor")]
		private List<WaveDataEditor> waves = new List<WaveDataEditor>()
		{
			new WaveDataEditor(){Enabled = true, Direction = new Vector2(1f, 1f), Steepness = 0.4f, Length = 20f},
			new WaveDataEditor(){Enabled = true, Direction = new Vector2(0f, 1f), Steepness = 0.6f, Length = 15f}
		};

		[Header("Foam")]
		[SerializeField] private bool dynamicFoam = true;
		[SerializeField] private Texture2D dynamicFoamTexture;
		[SerializeField] private Vector2 dynamicFoamTiling = Vector2.one * 0.5f;
		[SerializeField] private Vector2 dynamicFoamOffset;
		[SerializeField, Range(0f, 1f)] private float dynamicFoamStrength = 0.75f;

		[Header("Foam Waves")]
		[SerializeField] private bool foamWaves = true;
		[SerializeField] private Texture foamWavesTexture;
		[SerializeField] private int maxFoamWavesCount = 20;
		[SerializeField] private Vector2 foamWaveIntervalRange = new Vector2(0.5f, 2.0f);

		private WaterBase waterBase;

		private ComputeBuffer wavesBuffer;
		private ComputeBuffer foamWavesBuffer;

		private float nextFoamWaveTime;

		private readonly List<WaveData> wavesData = new List<WaveData>();
		private readonly List<FoamWaveData> foamWavesData = new List<FoamWaveData>();


		private const string WaterFoamPath = "Assets/WaterSystem/Textures/Foam.png";
		private const string WaterFoamWavesPath = "Assets/WaterSystem/Textures/FoamWaves.png";


		private WaterBase WaterBase => waterBase ?? (waterBase = GetComponent<WaterBase>());


		private void Reset()
		{
#if UNITY_EDITOR
			dynamicFoamTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterFoamPath);
			foamWavesTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterFoamWavesPath);
#endif

			CopyWavesDataEditorList();
			InitializeWavesBuffer();
			SetMaterialProperties();
		}

		private void OnValidate()
		{
			if (!gameObject.activeInHierarchy)
				return;

			CopyWavesDataEditorList();
			InitializeWavesBuffer();
			SetMaterialProperties();
		}

		private void OnEnable()
		{
#if UNITY_EDITOR
			UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += OnSceneSaved;
			EditorApplication.projectChanged += OnProjectChanged;
			FileModificationProcessor.ProjectSaved += OnProjectChanged;
#endif

			CopyWavesDataEditorList();
			InitializeWavesBuffer();

			if (Application.isPlaying)
				InitializeFoamWavesBuffer();

			SetMaterialProperties();

			WaterBase.WaterSurfaceMaterial.DisableKeyword("_WAVES_DEBUG");
		}

		private void Update()
		{
			if (!Application.isPlaying)
				return;

			UpdateWaves();
			UpdateFoamWaves();
		}

		private void OnDisable()
		{
#if UNITY_EDITOR
			UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= OnSceneSaved;
			EditorApplication.projectChanged -= OnProjectChanged;
			FileModificationProcessor.ProjectSaved -= OnProjectChanged;
#endif

			wavesBuffer?.Release();
			foamWavesBuffer?.Release();
		}

		private void OnSceneSaved(Scene scene)
		{
			// When scene is saved compute buffer has to be disposed and initialized again
			// otherwise it is only disposed without initialization by Unity
			CopyWavesDataEditorList();
			InitializeWavesBuffer();
			SetMaterialProperties();
		}

		private void OnProjectChanged()
		{
			// When project has been changed (e.g. asset created/deleted, etc.) compute buffer has to be disposed and initialized again
			// otherwise it is only disposed without initialization by Unity
			CopyWavesDataEditorList();
			InitializeWavesBuffer();
			SetMaterialProperties();
		}

		private void CopyWavesDataEditorList()
		{
			wavesData.Clear();

			foreach (var wave in waves)
			{
				wavesData.Add(new WaveData()
				{
					Enabled = wave.Enabled ? 1 : 0,
					Direction = wave.Direction,
					Length = wave.Length,
					Steepness = wave.Steepness
				});
			}
		}

		private void InitializeWavesBuffer()
		{
			wavesBuffer?.Release();

			if (WaterBase.WaterSurfaceMaterial == null)
				return;

			WaterBase.WaterSurfaceMaterial.SetInt(WaterShaderProperties.DynamicWavesCount, wavesData.Count);

			if (wavesData.Count <= 0)
				return;

			// Compute buffer which includes waves data
			wavesBuffer = new ComputeBuffer(wavesData.Count, sizeof(float) * 5);
			wavesBuffer.SetData(wavesData);

			WaterBase.WaterSurfaceMaterial.SetBuffer(WaterShaderProperties.DynamicWavesData, wavesBuffer);

			if (Application.isPlaying)
				WaterBase.AttachWavesComputeBuffer(WaterShaderProperties.DynamicWavesData, WaterShaderProperties.DynamicWavesKeyword, wavesBuffer);
		}

		private void InitializeFoamWavesBuffer()
		{
			if (maxFoamWavesCount <= 0) 
				return;

			foamWavesBuffer = new ComputeBuffer(maxFoamWavesCount, sizeof(float) * 8);
			foamWavesBuffer.SetData(foamWavesData);
			WaterBase.WaterSurfaceMaterial.SetBuffer(WaterShaderProperties.FoamWavesData, foamWavesBuffer);
		}

		private void UpdateWaves()
		{
			WaterBase.WavesComputeShader.SetFloat(WaterShaderProperties.DynamicWaveAmplitude, dynamicWaveAmplitude);
			WaterBase.WavesComputeShader.SetInt(WaterShaderProperties.DynamicWavesCount, dynamicWaves ? wavesData.Count : 0);
		}

		private void UpdateFoamWaves()
		{
			for (var i = foamWavesData.Count - 1; i >= 0; i--)
			{
				var data = foamWavesData[i];
				if (data.Amount >= data.Max)
				{
					foamWavesData.RemoveAt(i);
					continue;
				}

				data.Amount += data.Speed * Time.deltaTime;
				foamWavesData[i] = data;
			}

			EvaluateFoamWaves();

			WaterBase.WaterSurfaceMaterial.SetInt(WaterShaderProperties.FoamWavesCount, foamWavesData.Count);
			foamWavesBuffer?.SetData(foamWavesData);
		}

		private void EvaluateFoamWaves()
		{
			if (foamWavesData.Count >= maxFoamWavesCount || Time.time < nextFoamWaveTime) 
				return;

			var pos = Camera.main.transform.position;
			pos += Camera.main.transform.forward * Random.Range(-20f, 20f);
			pos += Camera.main.transform.right * Random.Range(-20f, 20f);

			var uvMinX = Random.Range(0, 2) == 0 ? 0f : 0.5f;
			var uvMinY = Random.Range(0, 2) == 0 ? 0f : 0.5f;

			var minFadeIn = Random.Range(0f, 0.15f);
			var maxFadeOut = Random.Range(minFadeIn + 0.5f, Mathf.Max(minFadeIn + 1.0f, minFadeIn + 3.5f));

			foamWavesData.Add(new FoamWaveData
			{
				Pos = pos,
				Amount = 0f,
				Speed = Random.Range(0.1f, 3f),
				Min = minFadeIn,
				Max = maxFadeOut,
				UvMin = new Vector2(uvMinX, uvMinY)
			});

			nextFoamWaveTime = Time.time + Random.Range(foamWaveIntervalRange.x, foamWaveIntervalRange.y);
		}

		private void SetMaterialProperties()
		{
			if (dynamicWaves)
				WaterBase.WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.DynamicWavesKeyword);
			else
				WaterBase.WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.DynamicWavesKeyword);

			WaterBase.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.DynamicWaveAmplitude, dynamicWaveAmplitude);


			if (dynamicFoam)
				WaterBase.WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.DynamicFoamKeyword);
			else
				WaterBase.WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.DynamicFoamKeyword);

			WaterBase.WaterSurfaceMaterial.SetTexture(WaterShaderProperties.DynamicFoamTex, dynamicFoamTexture);
			WaterBase.WaterSurfaceMaterial.SetVector(WaterShaderProperties.DynamicFoamTexTilingOffset,
				new Vector4(dynamicFoamTiling.x, dynamicFoamTiling.y, dynamicFoamOffset.x, dynamicFoamOffset.y));
			WaterBase.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.DynamicFoamStrength, dynamicFoamStrength);


			if (foamWaves)
				WaterBase.WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.FoamWavesKeyword);
			else
				WaterBase.WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.FoamWavesKeyword);

			WaterBase.WaterSurfaceMaterial.SetTexture(WaterShaderProperties.FoamWaveTex, foamWavesTexture);
		}
	}
}