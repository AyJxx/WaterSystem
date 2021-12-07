// Copyright (c) Adam Jůva.
// Licensed under the MIT License.
// Credit for this technique goes to Jacques Kerner - https://www.gamedeveloper.com/programming/water-interaction-model-for-boats-in-video-games

using System;
using UnityEngine;

namespace WaterSystem.WaterObjects
{
	/// <summary>
	/// Calculates forces by which water affects object in the water.
	/// </summary>
	public class MeshPhysics
	{
		private Rigidbody boatRigidBody;
		private MeshTriangles boatMeshTriangles;

		private PressureDragForceSettings boatPressureDragForceSettings;
		private SlammingForceSettings boatSlammingForceSettings;

		private const float RhoWater = 1027.0f;
		private const float WaterViscosity = 0.000001f;


		public MeshPhysics(Rigidbody rigidBody, MeshTriangles meshTriangles, PressureDragForceSettings pressureDragForceSettings, 
			SlammingForceSettings slammingForceSettings)
		{
			boatRigidBody = rigidBody;
			boatMeshTriangles = meshTriangles;

			boatPressureDragForceSettings = pressureDragForceSettings;
			boatSlammingForceSettings = slammingForceSettings;
			
		}

		public Vector3 CalculateBuoyancyForce(Triangle triangle, WaterBase waterBase)
		{
			var waterHeight = waterBase.GetWaterHeight(triangle.Center);

			var buoyancyForce = RhoWater * Physics.gravity.y * triangle.Normal
			                    * Mathf.Max(0f, -triangle.GetDistanceToWaterSurface(waterHeight)) * triangle.Area;
			buoyancyForce.x = 0.0f;
			buoyancyForce.z = 0.0f;

			return GetValidatedForce(buoyancyForce);
		}

		public float CalculateFrictionResistanceCoefficient(float speed, float length)
		{
			var Rn = (speed * length) / WaterViscosity;
			var CF = 0.075f / Mathf.Pow(Mathf.Log10(Rn) - 2f, 2);

			return CF;
		}

		public Vector3 CalculateViscousWaterResistanceForce(Triangle triangle, float frictionResistance)
		{
			var vii = triangle.Velocity - triangle.Velocity * Vector3.Dot(triangle.VelocityNormalized, triangle.Normal);
			var vfi = triangle.Velocity.magnitude * -vii.normalized;

			var viscousWaterResistance = 0.5f * RhoWater * frictionResistance * triangle.Area * vfi.magnitude * vfi;

			return GetValidatedForce(viscousWaterResistance);
		}

		public Vector3 CalculatePressureDragForce(Triangle triangle)
		{
			var velocity = triangle.Velocity.magnitude;
			var velocityReference = velocity;
			velocity /= velocityReference; // TODO:

			Vector3 force;

			if (triangle.CosTheta > 0f)
			{
				force = -(boatPressureDragForceSettings.LinearPressureDragCoefficient * velocity 
				          + boatPressureDragForceSettings.QuadraticPressureDragCoefficient * (velocity * velocity)) 
				        * triangle.Area * Mathf.Pow(triangle.CosTheta, boatPressureDragForceSettings.PressureFalloffPower) * triangle.Normal;
			}
			else
			{
				force = (boatPressureDragForceSettings.LinearSuctionDragCoefficient * velocity 
				         + boatPressureDragForceSettings.QuadraticSuctionDragCoefficient * (velocity * velocity)) 
				        * triangle.Area * Mathf.Pow(triangle.CosTheta, boatPressureDragForceSettings.SuctionFalloffPower) * triangle.Normal;
			}

			return GetValidatedForce(force);
		}

		public Vector3 CalculateSlammingForce(SlammingForce slammingData, Triangle triangle)
		{
			if (triangle.CosTheta < 0.0f)
				return Vector3.zero;

			var V = slammingData.CurrentUnderwaterArea * slammingData.Velocity;
			var V_last = slammingData.LastUnderwaterArea * slammingData.LastVelocity;

			var acceleration = ((V - V_last) / (slammingData.Area * Time.fixedDeltaTime)).magnitude;
			if (boatSlammingForceSettings.SlammingForceConstantAcceleration)
				acceleration = boatSlammingForceSettings.SlammingForceMaxAcceleration;

			var stoppingForce = boatRigidBody.mass * triangle.Velocity * ((2 * slammingData.CurrentUnderwaterArea) / boatMeshTriangles.TotalMeshArea);

			var slammingForce = Mathf.Pow(Mathf.Clamp01(acceleration / boatSlammingForceSettings.SlammingForceMaxAcceleration), 
				                    boatSlammingForceSettings.SlammingForceRampPower) 
			                    * triangle.CosTheta * stoppingForce;
			slammingForce *= -1f;

			return GetValidatedForce(slammingForce);
		}

		private Vector3 GetValidatedForce(Vector3 force)
		{
			return float.IsNaN(force.x * force.y * force.z) ? Vector3.zero : force;
		}
	}

	[Serializable]
	public class PressureDragForceSettings
	{
		[SerializeField] private float linearPressureDragCoefficient = 5.0f;
		[SerializeField] private float quadraticPressureDragCoefficient = 100.0f;
		[SerializeField] private float pressureFalloffPower = 1.0f;

		[SerializeField] private float linearSuctionDragCoefficient = 5.0f;
		[SerializeField] private float quadraticSuctionDragCoefficient = 100.0f;
		[SerializeField] private float suctionFalloffPower = 1.0f;


		public float LinearPressureDragCoefficient => linearPressureDragCoefficient;
		public float QuadraticPressureDragCoefficient => quadraticPressureDragCoefficient;
		public float PressureFalloffPower => pressureFalloffPower;

		public float LinearSuctionDragCoefficient => linearSuctionDragCoefficient;
		public float QuadraticSuctionDragCoefficient => quadraticSuctionDragCoefficient;
		public float SuctionFalloffPower => suctionFalloffPower;
	}

	[Serializable]
	public class SlammingForceSettings
	{
		[SerializeField] private bool slammingForceConstantAcceleration = false;
		[SerializeField] private float slammingForceRampPower = 0.5f;
		[SerializeField] private float slammingForceMaxAcceleration = 1000.0f;


		public bool SlammingForceConstantAcceleration => slammingForceConstantAcceleration;
		public float SlammingForceRampPower => slammingForceRampPower;
		public float SlammingForceMaxAcceleration => slammingForceMaxAcceleration;
	}

	public struct SlammingForce
	{
		public float Area;
		public float CurrentUnderwaterArea;
		public float LastUnderwaterArea;
		public Vector3 Velocity;
		public Vector3 LastVelocity;
		public Vector3 TriangleCenter;

		private int lastFrame;

		public void Initialize(Vector3 vertex1, Vector3 vertex2, Vector3 vertex3)
		{
			TriangleCenter = (vertex1 + vertex2 + vertex3) / 3f;
			CurrentUnderwaterArea = Area;
		}

		public void Initialize(Vector3 vertex1, Vector3 vertex2, Vector3 vertex3, float currentUnderwaterArea)
		{
			TriangleCenter = (vertex1 + vertex2 + vertex3) / 3f;
			CurrentUnderwaterArea = currentUnderwaterArea;
		}

		public void UpdateVelocity(Vector3 totalVelocity, Vector3 totalAngularVelocity, Vector3 worldCenterOfMass, int frame)
		{
			if (frame == lastFrame)
				return;
			lastFrame = frame;

			LastVelocity = Velocity;

			var centerToTriangleCenter = TriangleCenter - worldCenterOfMass;
			Velocity = totalVelocity + Vector3.Cross(totalAngularVelocity, centerToTriangleCenter);
		}

		public void UpdateArea()
		{
			LastUnderwaterArea = CurrentUnderwaterArea;
		}
	}
}
