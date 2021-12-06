// Copyright (c) Adam Jůva.
// Licensed under the MIT License.

using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Random = UnityEngine.Random;

namespace WaterSystem
{
	/// <summary>
	/// Class used for controlling interactions with the water.
	/// </summary>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(WaterBase))]
	public class WaterInteractions : MonoBehaviour
	{
		private class ColliderData
		{
			public Collider Collider;
			public Vector3 LastPosition;
			public Vector3 LastImpactPosition;
			public int ImpactCount;
		};

		private struct WaveData
		{
			public Vector3 WaveHitPos;
			public float WaveStrength;
			public float WaveSpread;
			public float WaveFrequency;
		};


		[Header("Collisions")]
		[SerializeField] private LayerMask ignoreMask;
		[SerializeField] private string[] ignoreTags;
		[SerializeField] private float colliderDeltaPositionLimit = 0.5f;
		[SerializeField] private float maxColliderSpeed = 2.5f;
		[SerializeField] private ParticleSystem[] waterSplashes;

		[Header("Interactive Waves")]
		[SerializeField] private bool interactiveWaves = true;
		[SerializeField] private int maxWavesCount = 1024;
		[SerializeField] private float interactiveWaveAmplitude = 0.2f;
		[SerializeField] private float interactiveWaveFrequency = 0.5f;
		[SerializeField] private float interactiveWaveSpeed = 1.0f;
		[SerializeField, Range(0.5f, 0.99f)] private float waveDamping = 0.97f;
		[SerializeField] private float waveStrength = 2.0f;
		[SerializeField] private float waveSpreadSpeed = 2.0f;
		[SerializeField] private float waveFirstImpactMultiplier = 3.0f;

		[Header("Foam")]
		[SerializeField] private bool interactionFoam = true;
		[SerializeField] private Texture2D interactionFoamTexture;
		[SerializeField] private Vector2 interactionFoamTiling = Vector2.one;
		[SerializeField] private Vector2 interactionFoamOffset;
		[SerializeField, Range(0f, 1f)] private float interactionFoamStrength = 1.00f;

		private WaterBase waterBase;

		private ComputeBuffer wavesBuffer;

		private readonly HashSet<string> ignoredObjectTags = new HashSet<string>();

		/// <summary>
		/// Buffer which includes colliders which are currently in collision with the water
		/// </summary>
		private readonly Dictionary<Collider, ColliderData> currentColliders = new Dictionary<Collider, ColliderData>();

		private readonly List<WaveData> wavesData = new List<WaveData>(); // Waves data for compute buffer


		private const float MinFpsFactor =  1.0f / 144.0f; // 144 FPS
		private const float MaxFpsFactor = 1.0f / 30.0f; // 30 FPS
		private const float FpsFactorDifference = MaxFpsFactor - MinFpsFactor; // Used to compensate friction for different FPS

		private const string WaterFoamInteractionPath = "Assets/WaterSystem/Textures/FoamInteraction.jpg";


		private WaterBase WaterBase => waterBase ?? (waterBase = GetComponent<WaterBase>());


		private void Reset()
		{
#if UNITY_EDITOR
			interactionFoamTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterFoamInteractionPath);
#endif

			SetMaterialProperties();
		}

		private void OnValidate()
		{
			SetMaterialProperties();
		}

		private void Start()
		{
			InitializeWavesBuffer();
			SetMaterialProperties();

			foreach (var ignoreTag in ignoreTags)
			{
				ignoredObjectTags.Add(ignoreTag);
			}
		}

		private void Update()
		{
			UpdateWaves();
		}

		private void OnDestroy()
		{
			wavesBuffer?.Release();
		}

		private void OnTriggerEnter(Collider other)
		{
			// Check if object should be ignored
			if ((1 << other.gameObject.layer & ignoreMask) > 0)
				return;

			if (ignoredObjectTags.Contains(other.transform.tag))
				return;

			AddCollider(other);
		}

		private void OnTriggerExit(Collider other)
		{
			RemoveCollider(other);
		}

		private void InitializeWavesBuffer()
		{
			if (maxWavesCount <= 0)
				return;

			// Compute buffer which includes ripple waves data
			wavesBuffer = new ComputeBuffer(maxWavesCount, sizeof(float) * 6);
			WaterBase.WaterSurfaceMaterial.SetBuffer(WaterShaderProperties.InteractionWavesData, wavesBuffer);
			WaterBase.AttachWavesComputeBuffer(WaterShaderProperties.InteractionWavesData, WaterShaderProperties.InteractionWavesKeyword, wavesBuffer);
		}

		private void UpdateWaves()
		{
			WaterBase.WavesComputeShader.SetFloat(WaterShaderProperties.InteractionWaveSpeed, interactiveWaveSpeed);
			WaterBase.WavesComputeShader.SetFloat(WaterShaderProperties.InteractionWaveFrequency, interactiveWaveFrequency);
			WaterBase.WavesComputeShader.SetFloat(WaterShaderProperties.InteractionWaveAmplitude, interactiveWaveAmplitude);
			WaterBase.WavesComputeShader.SetInt(WaterShaderProperties.InteractionWavesCount, interactiveWaves ? wavesData.Count : 0);

			var minDamping = waveDamping - 0.03f;
			var maxDamping = Mathf.Min(waveDamping + 0.02f, 0.99f);

			var multiplier = (Time.deltaTime - MinFpsFactor) / FpsFactorDifference;
			var damping = Mathf.Lerp(maxDamping, minDamping, multiplier);

			for (var i = wavesData.Count - 1; i >= 0; i--)
			{
				var wave = wavesData[i];
				if (wave.WaveStrength < 0.001f)
				{
					wavesData.RemoveAt(i);
					continue;
				}

				wave.WaveStrength *= damping;
				wave.WaveSpread += waveSpreadSpeed * Time.deltaTime;
				wavesData[i] = wave;
			}

			EvaluateCollidersInWater();

			WaterBase.WaterSurfaceMaterial.SetInt(WaterShaderProperties.InteractionWavesCount, wavesData.Count);
			wavesBuffer?.SetData(wavesData);
		}

		private void EvaluateCollidersInWater()
		{
			foreach (var currentCollider in currentColliders)
			{
				var colliderData = currentCollider.Value;

				if (!colliderData.Collider.gameObject.activeInHierarchy)
					continue;

				var colliderPosition = colliderData.Collider.transform.position;
				var impactPosition = new Vector3(colliderData.Collider.transform.position.x, transform.position.y, colliderData.Collider.transform.position.z);
				var intensityMultiplier = colliderData.ImpactCount < 2 ? waveFirstImpactMultiplier : 1.0f;

				var colliderVelocity = colliderPosition - colliderData.LastPosition;
				var colliderSpeed = colliderVelocity.magnitude / Time.deltaTime;
				var impactDeltaPosition = (impactPosition - colliderData.LastImpactPosition).magnitude;

				// TODO: Check bounds under water
				if (colliderData.ImpactCount < 2 || 
					(impactDeltaPosition > colliderDeltaPositionLimit && colliderData.Collider.bounds.min.y <= WaterBase.GetWaterHeight(colliderPosition)))
				{
					CreateWave(impactPosition, colliderVelocity.normalized, colliderSpeed, intensityMultiplier);

					colliderData.LastImpactPosition = impactPosition;
				}

				colliderData.LastPosition = colliderPosition;
				colliderData.ImpactCount++;
			}
		}

		private void CreateWave(Vector3 hitPos, Vector3 direction, float colliderSpeed, float intensityMultiplier)
		{
			var hitIntensityRatio = Mathf.Clamp01(colliderSpeed / maxColliderSpeed);

			SetWaveData(hitPos + direction * 0.5f, waveStrength * hitIntensityRatio * intensityMultiplier, 0.05f,
				Mathf.Lerp(5f, 1.0f, hitIntensityRatio));

			// Splash
			if (waterSplashes.Length > 0)
			{
				const float heightOffset = 0.5f;
				var waterSplash = waterSplashes[Random.Range(0, waterSplashes.Length)];

				waterSplash.transform.position = new Vector3(hitPos.x, WaterBase.GetWaterHeight(hitPos) - heightOffset, hitPos.z);
				waterSplash.transform.position -= direction * 1.35f;
				waterSplash.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
				waterSplash.Emit(20);
			}
		}

		private void SetWaveData(Vector3 hitPos, float strength, float spread, float frequency)
		{
			if (wavesData.Count >= maxWavesCount)
				return;

			hitPos = transform.InverseTransformPoint(hitPos);
			hitPos.y = 0.0f;

			wavesData.Add(new WaveData
			{
				WaveFrequency = frequency,
				WaveSpread = spread,
				WaveStrength = strength,
				WaveHitPos = hitPos
			});
		}

		private void AddCollider(Collider collider)
		{
			var colliderPosY = Mathf.Max(transform.position.y, collider.transform.position.y);

			var colliderData = 
				new ColliderData
				{
					Collider = collider,
					LastPosition = new Vector3(collider.transform.position.x, colliderPosY, collider.transform.position.z),
					LastImpactPosition = new Vector3(collider.transform.position.x, transform.position.y, collider.transform.position.z),
					ImpactCount = 0
				};

			currentColliders[collider] = colliderData;
		}

		private void RemoveCollider(Collider collider)
		{
			currentColliders.Remove(collider);
		}

		private void SetMaterialProperties()
		{
			if (interactiveWaves)
				WaterBase.WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.InteractionWavesKeyword);
			else
				WaterBase.WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.InteractionWavesKeyword);

			WaterBase.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.InteractionWaveAmplitude, interactiveWaveAmplitude);
			WaterBase.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.InteractionWaveFrequency, interactiveWaveFrequency);
			WaterBase.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.InteractionWaveSpeed, interactiveWaveSpeed);


			if (interactionFoam)
				WaterBase.WaterSurfaceMaterial.EnableKeyword(WaterShaderProperties.InteractionFoamKeyword);
			else
				WaterBase.WaterSurfaceMaterial.DisableKeyword(WaterShaderProperties.InteractionFoamKeyword);

			WaterBase.WaterSurfaceMaterial.SetTexture(WaterShaderProperties.InteractionFoamTex, interactionFoamTexture);
			WaterBase.WaterSurfaceMaterial.SetVector(WaterShaderProperties.InteractionFoamTexTilingOffset,
				new Vector4(interactionFoamTiling.x, interactionFoamTiling.y, interactionFoamOffset.x, interactionFoamOffset.y));
			WaterBase.WaterSurfaceMaterial.SetFloat(WaterShaderProperties.InteractionFoamStrength, interactionFoamStrength);
		}
	}
}