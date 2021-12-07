// Copyright (c) Adam Jůva.
// Licensed under the MIT License.
// Credit for this technique goes to Jacques Kerner - https://www.gamedeveloper.com/programming/water-interaction-model-for-boats-in-video-games

using System.Collections.Generic;
using UnityEngine;

namespace WaterSystem.WaterObjects
{
	/// <summary>
	/// Calculates triangles of the mesh which are under water.
	/// </summary>
	public class MeshTriangles
	{
		private readonly Rigidbody rigidBody;
		private readonly MeshFilter meshFilter;

		private Vector3[] verticesLocalSpace;
		private Vector3[] verticesGlobalSpace;
		private int[] triangles;

		private readonly List<Vertex> triangleVertices = new List<Vertex>(3);

		
		public Triangle[] UnderwaterTriangles { get; }
		public int UnderwaterTrianglesCount { get; private set; }

		public SlammingForce[] SlammingForces { get; }
		public float TotalMeshArea { get; private set; }


		public MeshTriangles(Rigidbody rigidBody, MeshFilter meshFilter)
		{
			this.rigidBody = rigidBody;
			this.meshFilter = meshFilter;

			verticesGlobalSpace = new Vector3[meshFilter.sharedMesh.vertexCount];
			verticesLocalSpace = meshFilter.sharedMesh.vertices;
			triangles = meshFilter.sharedMesh.triangles;

			UnderwaterTriangles = new Triangle[triangles.Length];

			SlammingForces = new SlammingForce[triangles.Length / 3];
			for (var i = 0; i < SlammingForces.Length; i++)
			{
				var index = i * 3;
				var vertex1 = meshFilter.transform.TransformPoint(verticesLocalSpace[triangles[index]]);
				var vertex2 = meshFilter.transform.TransformPoint(verticesLocalSpace[triangles[index + 1]]);
				var vertex3 = meshFilter.transform.TransformPoint(verticesLocalSpace[triangles[index + 2]]);

				var a = Vector3.Distance(vertex1, vertex2);
				var c = Vector3.Distance(vertex1, vertex3);
				var triangleArea = (a * c * Mathf.Sin(Vector3.Angle(vertex2 - vertex1, vertex3 - vertex1) * Mathf.Deg2Rad)) / 2.0f;
				SlammingForces[i].Area = triangleArea;

				TotalMeshArea += triangleArea;
			}
		}

		public void CalculateUnderwaterTriangles(WaterBase waterBase)
		{
			for (var i = 0; i < SlammingForces.Length; i++)
			{
				SlammingForces[i].UpdateArea();
			}

			for (var i = 0; i < verticesGlobalSpace.Length; i++)
			{
				verticesGlobalSpace[i] = meshFilter.transform.TransformPoint(verticesLocalSpace[i]);
			}

			UnderwaterTrianglesCount = 0;

			for (var i = 0; i < triangles.Length; i += 3)
			{
				var index1 = triangles[i];
				var index2 = triangles[i + 1];
				var index3 = triangles[i + 2];

				var vertex1 = verticesGlobalSpace[index1];
				var vertex2 = verticesGlobalSpace[index2];
				var vertex3 = verticesGlobalSpace[index3];

				var waterHeight1 = waterBase.GetWaterHeight(vertex1);
				var waterHeight2 = waterBase.GetWaterHeight(vertex2);
				var waterHeight3 = waterBase.GetWaterHeight(vertex3);

				if (vertex1.y >= waterHeight1 && vertex2.y >= waterHeight2 && vertex3.y >= waterHeight3) // All above water
				{
					SlammingForces[i / 3].Initialize(vertex1, vertex2, vertex3, 0.0f);
					continue;
				}

				if (vertex1.y < waterHeight1 && vertex2.y < waterHeight2 && vertex3.y < waterHeight3) // All underwater
				{
					SlammingForces[i / 3].Initialize(vertex1, vertex2, vertex3);

					UnderwaterTriangles[UnderwaterTrianglesCount++].Initialize(i / 3, vertex1, vertex2, vertex3, rigidBody.velocity, rigidBody.angularVelocity, rigidBody.worldCenterOfMass);
					continue;
				}

				triangleVertices.Clear();
				triangleVertices.Add(new Vertex { Position = vertex1, Index = 0, WaterHeightDifferenceY = vertex1.y - waterHeight1 });
				triangleVertices.Add(new Vertex { Position = vertex2, Index = 1, WaterHeightDifferenceY = vertex2.y - waterHeight2 });
				triangleVertices.Add(new Vertex { Position = vertex3, Index = 2, WaterHeightDifferenceY = vertex3.y - waterHeight3 });
				triangleVertices.Sort((first, second) => first.WaterHeightDifferenceY.CompareTo(second.WaterHeightDifferenceY));

				if (triangleVertices[0].WaterHeightDifferenceY >= 0 && triangleVertices[1].WaterHeightDifferenceY < 0 && triangleVertices[2].WaterHeightDifferenceY < 0) // H is above water
				{
					AddTwoUnderwaterTriangles(UnderwaterTriangles, triangleVertices, i / 3);
				}
				else if (triangleVertices[0].WaterHeightDifferenceY >= 0 && triangleVertices[1].WaterHeightDifferenceY >= 0 && triangleVertices[2].WaterHeightDifferenceY < 0) // H and M are above water
				{
					AddOneUnderwaterTriangle(UnderwaterTriangles, triangleVertices, i / 3);
				}
			}
		}

		private void AddTwoUnderwaterTriangles(Triangle[] triangles, List<Vertex> triangleVertices, int triangleIndex)
		{
			var H = triangleVertices[2].Position;
			var M = Vector3.zero;
			var L = Vector3.zero;

			var H_height = triangleVertices[2].WaterHeightDifferenceY;
			var M_height = 0f;
			var L_height = 0f;

			var M_index = triangleVertices[2].Index - 1 < 0 ? 2 : triangleVertices[2].Index - 1;

			if (triangleVertices[1].Index == M_index)
			{
				M = triangleVertices[1].Position;
				M_height = triangleVertices[1].WaterHeightDifferenceY;

				L = triangleVertices[0].Position;
				L_height = triangleVertices[0].WaterHeightDifferenceY;
			}
			else
			{
				M = triangleVertices[0].Position;
				M_height = triangleVertices[0].WaterHeightDifferenceY;

				L = triangleVertices[1].Position;
				L_height = triangleVertices[1].WaterHeightDifferenceY;
			}

			var M_t = -M_height / (H_height - M_height);
			var I_M = M + (H - M) * M_t;

			var L_t = -L_height / (H_height - L_height);
			var I_L = L + (H - L) * L_t;

			triangles[UnderwaterTrianglesCount++].Initialize(triangleIndex / 3, M, I_M, I_L, rigidBody.velocity,
				rigidBody.angularVelocity, rigidBody.worldCenterOfMass);
			triangles[UnderwaterTrianglesCount++].Initialize(triangleIndex / 3, L, M, I_L, rigidBody.velocity,
				rigidBody.angularVelocity, rigidBody.worldCenterOfMass);

			var underwaterTrianglesArea = triangles[UnderwaterTrianglesCount - 2].Area + triangles[UnderwaterTrianglesCount - 1].Area;
			SlammingForces[triangleIndex / 3].Initialize(triangleVertices[0].Position, triangleVertices[1].Position,
				triangleVertices[2].Position, underwaterTrianglesArea);
		}

		private void AddOneUnderwaterTriangle(Triangle[] triangles, List<Vertex> triangleVertices, int triangleIndex)
		{
			var L = triangleVertices[0].Position;
			var M = Vector3.zero;
			var H = Vector3.zero;

			var L_height = triangleVertices[0].WaterHeightDifferenceY;
			var M_height = 0f;
			var H_height = 0f;

			var H_index = triangleVertices[0].Index - 1 < 0 ? 2 : triangleVertices[0].Index - 1;

			if (triangleVertices[1].Index == H_index)
			{
				H = triangleVertices[1].Position;
				H_height = triangleVertices[1].WaterHeightDifferenceY;

				M = triangleVertices[2].Position;
				M_height = triangleVertices[2].WaterHeightDifferenceY;
			}
			else
			{
				H = triangleVertices[2].Position;
				H_height = triangleVertices[2].WaterHeightDifferenceY;

				M = triangleVertices[1].Position;
				M_height = triangleVertices[1].WaterHeightDifferenceY;
			}

			var H_t = -L_height / (H_height - L_height);
			var I_H = L + (H - L) * H_t;

			var M_t = -L_height / (M_height - L_height);
			var I_M = L + (M - L) * M_t;

			triangles[UnderwaterTrianglesCount++].Initialize(triangleIndex / 3, L, I_H, I_M, rigidBody.velocity,
				rigidBody.angularVelocity, rigidBody.worldCenterOfMass);

			var underwaterTrianglesArea = triangles[UnderwaterTrianglesCount - 1].Area;
			SlammingForces[triangleIndex / 3].Initialize(triangleVertices[0].Position, triangleVertices[1].Position,
				triangleVertices[2].Position, underwaterTrianglesArea);
		}
	}

	public struct Vertex
	{
		public Vector3 Position;
		public int Index;
		public float WaterHeightDifferenceY;
	}

	public struct Triangle
	{
		public Vector3 Vertex1;
		public Vector3 Vertex2;
		public Vector3 Vertex3;

		public Vector3 Center { get; private set; }
		public Vector3 Normal { get; private set; }
		public Vector3 Velocity { get; private set; }
		public Vector3 VelocityNormalized { get; private set; }
		public float Area { get; private set; }
		public float CosTheta { get; private set; }
		public int TriangleIndex { get; private set; }

		public void Initialize(int triangleIndex, Vector3 vertex1, Vector3 vertex2, Vector3 vertex3, Vector3 velocity, Vector3 angularVelocity, Vector3 worldCenterOfMass)
		{
			TriangleIndex = triangleIndex;

			Vertex1 = vertex1;
			Vertex2 = vertex2;
			Vertex3 = vertex3;

			Center = (Vertex1 + Vertex2 + Vertex3) / 3.0f;

			Normal = Vector3.Cross((Vertex2 - Vertex1), (Vertex3 - Vertex1)).normalized;

			var a = Vector3.Distance(Vertex1, Vertex2);
			var c = Vector3.Distance(Vertex1, Vertex3);
			Area = (a * c * Mathf.Sin(Vector3.Angle(Vertex2 - Vertex1, Vertex3 - Vertex1) * Mathf.Deg2Rad)) / 2.0f;

			var dirToTriangleCenter = Center - worldCenterOfMass;
			Velocity = velocity + Vector3.Cross(angularVelocity, dirToTriangleCenter);
			VelocityNormalized = Velocity.normalized;

			CosTheta = Vector3.Dot(VelocityNormalized, Normal);
		}

		public float GetDistanceToWaterSurface(float waterSurfaceHeight)
		{
			return Center.y - waterSurfaceHeight;
		}
	}
}
