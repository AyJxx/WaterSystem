// Copyright (c) Adam Jůva.
// Licensed under the MIT License.
// Credit for this technique goes to Jacques Kerner - https://www.gamedeveloper.com/programming/water-interaction-model-for-boats-in-video-games

using UnityEngine;

namespace WaterSystem.WaterObjects
{
	/// <summary>
	/// Simple controller with inputs for the boat.
	/// </summary>
	[RequireComponent(typeof(Rigidbody))]
	public class BoatController : MonoBehaviour, IWaterEntity
	{
		[SerializeField] private MeshFilter boatMeshFilter;
		[SerializeField] private MeshRenderer underwaterRenderer;

		[SerializeField] private Transform engine;
		[SerializeField] private float engineForceMultiplier = 600f;
		[SerializeField] private float engineTorqueMultiplier = 300f;

		[SerializeField] private Vector3 centerOfMass;

		[SerializeField] private PressureDragForceSettings boatPressureDragForceSettings;
		[SerializeField] private SlammingForceSettings boatSlammingForceSettings;

		private Rigidbody boatRigidBody;
		private MeshTriangles boatMeshTriangles;
		private MeshPhysics meshPhysics;

		private Vector3 engineForce;
		private Vector3 engineTorque;


		public WaterBase CurrentWaterBase { get; set; }


		private void OnValidate()
		{
			if (boatRigidBody)
				boatRigidBody.centerOfMass = centerOfMass;
		}

		private void Awake()
		{
			boatRigidBody = GetComponent<Rigidbody>();
			boatMeshTriangles = new MeshTriangles(boatRigidBody, boatMeshFilter);
			meshPhysics = new MeshPhysics(boatRigidBody, boatMeshTriangles, boatPressureDragForceSettings, boatSlammingForceSettings);
		}

		private void Update()
		{
			if (CurrentWaterBase == null)
				return;

			CurrentWaterBase.RequestWaterHeightTextureUpdate();

			boatMeshTriangles.CalculateUnderwaterTriangles(CurrentWaterBase);

			CalculateEngineForce();
		}

		private void FixedUpdate()
		{
			if (CurrentWaterBase == null)
				return;

			var cf = meshPhysics.CalculateFrictionResistanceCoefficient(boatRigidBody.velocity.magnitude, underwaterRenderer.bounds.size.z);

			var avgCenter = Vector3.zero;

			for (var i = 0; i < boatMeshTriangles.UnderwaterTrianglesCount; i++)
			{
				var triangle = boatMeshTriangles.UnderwaterTriangles[i];

				var force = Vector3.zero;
				force += meshPhysics.CalculateBuoyancyForce(triangle, CurrentWaterBase);
				force += meshPhysics.CalculateViscousWaterResistanceForce(triangle, cf);
				force += meshPhysics.CalculatePressureDragForce(triangle);

				var slammingForceIndex = triangle.TriangleIndex;
				var slammingForce = boatMeshTriangles.SlammingForces[slammingForceIndex];
				slammingForce.UpdateVelocity(boatRigidBody.velocity, boatRigidBody.angularVelocity, boatRigidBody.worldCenterOfMass, Time.frameCount);
				boatMeshTriangles.SlammingForces[slammingForceIndex] = slammingForce;

				force += meshPhysics.CalculateSlammingForce(slammingForce, triangle);

				boatRigidBody.AddForceAtPosition(force, triangle.Center);
				
				avgCenter += triangle.Center;
			}

			boatRigidBody.AddForceAtPosition(engineForce, engine.position);
			boatRigidBody.AddTorque(engineTorque);
		}

		private void CalculateEngineForce()
		{
			if (engine.position.y < CurrentWaterBase.GetWaterHeight(engine.position))
			{
				engineForce = Input.GetAxis("Vertical") * engine.forward * engineForceMultiplier;
				engineTorque = Input.GetAxis("Horizontal") * engine.up * engineTorqueMultiplier;
			}
			else
			{
				engineForce = Vector3.zero;
				engineTorque = Vector3.zero;
			}

			if (Input.GetKeyDown(KeyCode.Space))
				transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, 0f);
		}
	}
}