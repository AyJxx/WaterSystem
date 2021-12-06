using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WaterSystem
{
	/// <summary>
	/// Class responsible for creating water mesh.
	/// </summary>
	public class WaterMeshCreator
	{
		private readonly MeshFilter waterMeshFilter;

		private bool isProcessing;


		public WaterMeshCreator(MeshFilter waterMeshFilter)
		{
			this.waterMeshFilter = waterMeshFilter;
		}

		/// <summary>
		/// Creates water mesh.
		/// </summary>
		/// <param name="waterMeshPointsTransform">Transform which includes mesh points.</param>
		/// <param name="triangulationDensity">Density of the triangulation.</param>
		/// <returns>Water mesh.</returns>
		public async Task<Mesh> CreateWaterMesh(Transform waterMeshPointsTransform, float triangulationDensity)
		{
			if (waterMeshPointsTransform == null || waterMeshPointsTransform.childCount < 3)
			{
				Debug.LogError("Mesh Points transform must be created and contains at least 3 Points before creating water mesh.");
				return null;
			}

			if (isProcessing)
				return null;

			isProcessing = true;

			var waterMesh = new Mesh();

			var vertices = CalculateVertices(waterMeshPointsTransform);
			var triangles = CalculateTriangles(vertices.Count);

			var finalVertexPositions = new List<Vector3>();
			var finalTriangles = new List<int>();
			var startingPoints = new List<Vector3>();

			for (var i = 0; i < triangles.Count; i += 3)
			{
				var index1 = triangles[i];
				var index2 = triangles[i + 1];
				var index3 = triangles[i + 2];

				// Triangulation counts with counter clockwise supplied vertices
				var iteration = i / 3;
				await Triangulation(triangulationDensity, iteration, finalVertexPositions, finalTriangles, vertices[index1], vertices[index3], vertices[index2], startingPoints);
			}

			var finalUVs = CalculateUVs(finalVertexPositions);

			if (waterMesh == null)
				waterMesh = new Mesh() { name = "Water" };
			else
				waterMesh.Clear();

			waterMesh.SetVertices(finalVertexPositions);
			waterMesh.SetTriangles(finalTriangles, 0);
			waterMesh.SetUVs(0, finalUVs);
			waterMesh.RecalculateBounds();
			waterMesh.RecalculateNormals();
			waterMesh.RecalculateTangents();

			isProcessing = false;

			return waterMesh;
		}

		private List<Vector3> CalculateVertices(Transform waterMeshPointsTransform)
		{
			var vertices = new List<Vector3>();
			var verticesTmp = new List<Vector3>();

			var maxDistance = float.MinValue;
			var maxIndex = 0;
			for (var i = 0; i < waterMeshPointsTransform.childCount; i++)
			{
				verticesTmp.Add(waterMeshPointsTransform.GetChild(i).localPosition);

				var nextIndex = (i + 1) % waterMeshPointsTransform.childCount;
				var sqrDist = (waterMeshPointsTransform.GetChild(nextIndex).localPosition - waterMeshPointsTransform.GetChild(i).localPosition).sqrMagnitude;
				if (sqrDist > maxDistance)
				{
					maxIndex = i;
					maxDistance = sqrDist;
				}
			}

			// Shifting vertices, so first vertex is always one which has largest distance to his next vertex
			var startIndex = maxIndex - 1 >= 0 ? maxIndex - 1 : verticesTmp.Count - 1;
			//startIndex = maxIndex;

			for (var i = 0; i < verticesTmp.Count; i++)
			{
				var index = (startIndex + i) % verticesTmp.Count;
				vertices.Add(verticesTmp[index]);
			}

			return vertices;
		}

		private List<int> CalculateTriangles(int verticesCount)
		{
			var indicesCount = 3 * (verticesCount - 2);

			// These are indices, all triangles start from first vertex
			var v1 = 0;
			var v2 = 2;
			var v3 = 1;

			var triangles = new List<int>();
			for (var i = 0; i < indicesCount; i += 3)
			{
				triangles.Add(v1);
				triangles.Add(v2++);
				triangles.Add(v3++);
			}

			return triangles;
		}

		private List<Vector2> CalculateUVs(IReadOnlyList<Vector3> vertices)
		{
			var uvs = new List<Vector2>();

			var minX = vertices.OrderBy(vertex => vertex.x).FirstOrDefault().x;
			var maxX = vertices.OrderBy(vertex => vertex.x).Reverse().FirstOrDefault().x;
			var minZ = vertices.OrderBy(vertex => vertex.z).FirstOrDefault().z;
			var maxZ = vertices.OrderBy(vertex => vertex.z).Reverse().FirstOrDefault().z;
			var diffX = maxX - minX;
			var diffZ = maxZ - minZ;

			// UVs need to be calculated in square shape to avoid stretching of the texture and other calculations
			var edgeLength = Mathf.Max(diffX, diffZ);

			foreach (var vertex in vertices)
			{
				var uv = new Vector2(1.0f, 1.0f);
				var x = (vertex.x - minX) / edgeLength;
				var z = (vertex.z - minZ) / edgeLength;
				uvs.Add(new Vector2(uv.x * x, uv.y * z));

				//Debug.Log($"Vertex: {vertex}, UV: {waterUVs[waterUVs.Count - 1]}");
			}

			return uvs;
		}

		/// <summary>
		/// Triangulates supplied triangle (all supplied vertices are in local space).
		/// </summary>
		private Task Triangulation(float triangulationDensity, int iteration, List<Vector3> vertexPositions, List<int> triangles, 
			Vector3 p0, Vector3 p1, Vector3 p2, List<Vector3> startingPoints)
		{
			return Task.Run(() =>
			{
				// Caching where to start indexing new vertices for current triangle
				var startVertexIndex = vertexPositions.Count;

				var triangleCenter = (p0 + p1 + p2) / 3f;
				var triangleNormal = Vector3.Cross((p2 - p0).normalized, (p1 - p0).normalized).normalized;

				// Swapping (shifting) vertices of triangle so they are always ordered the same way
				if (iteration > 0)
				{
					var p0Cached = p0;
					var p1Cached = p1;
					var p2Cached = p2;

					p0 = p2Cached;
					p1 = p0Cached;
					p2 = p1Cached;
				}

				// First pass - projecting vectors to opposite side
				var projectedPrimaryLines = new List<Line>();
				if (iteration == 0)
				{
					var fromVectorDir = (p2 - p1).normalized;
					var fromVectorLength = (p2 - p1).magnitude;
					var projectedVector = p0 - p1;

					var density = 100.0f - triangulationDensity + 1f;
					var stepThreshold = (fromVectorLength - 0.1f) * density * 0.01f;
					for (var step = stepThreshold; step < fromVectorLength; step += stepThreshold)
					{
						var startPoint = p1 + fromVectorDir * step;
						var endPoint = startPoint + projectedVector;
						var finalEndPoint = ProjectPointOnPlane(GetIntersectionPoint(p0, p2, endPoint, startPoint), triangleNormal, triangleCenter);

						//Debug.DrawRay(transform.TransformPoint(startPoint), transform.TransformDirection(endPoint - startPoint), Color.magenta, 5f);
						//Debug.DrawRay(transform.TransformPoint(finalEndPoint), Vector3.up, Color.blue, 5f);

						projectedPrimaryLines.Add(new Line { Start = finalEndPoint, End = startPoint });
						startingPoints.Add(finalEndPoint);
					}
				}
				else
				{
					var startingPointsCached = new Vector3[startingPoints.Count];

					if (iteration == 1)
					{
						startingPoints.Reverse();
					}

					startingPoints.CopyTo(startingPointsCached);
					startingPoints.Clear();

					var projectedVector = p0 - p2;

					foreach (var startPoint in startingPointsCached)
					{
						var endPoint = startPoint + projectedVector;
						var finalEndPoint = ProjectPointOnPlane(GetIntersectionPoint(p0, p1, endPoint, startPoint), triangleNormal, triangleCenter);

						//Debug.DrawRay(transform.TransformPoint(startPoint), transform.TransformDirection(endPoint - startPoint), Color.magenta, 5f);
						//Debug.DrawRay(transform.TransformPoint(finalEndPoint), Vector3.up, Color.blue, 5f);

						projectedPrimaryLines.Add(new Line { Start = finalEndPoint, End = startPoint });
						startingPoints.Add(finalEndPoint);
					}
				}

				// Second pass - projecting vectors from intersection points of first pass to remaining opposite side
				var projectedSecondaryLines = new List<Line>();
				if (iteration == 0)
				{
					var projectedVector = p1 - p2;

					for (var i = 0; i < projectedPrimaryLines.Count; i++)
					{
						var line = projectedPrimaryLines[i];
						var endPoint = line.Start + projectedVector;
						var finalEndPoint = ProjectPointOnPlane(GetIntersectionPoint(p0, p1, line.Start, endPoint), triangleNormal, triangleCenter);

						projectedSecondaryLines.Add(new Line { Start = finalEndPoint, End = line.Start });

						//Debug.DrawRay(transform.TransformPoint(line.Start), transform.TransformDirection(projectedVector), Color.cyan, 5f);
						//Debug.DrawRay(transform.TransformPoint(finalEndPoint), Vector3.up, Color.red, 5f);
					}
				}
				else
				{
					var projectedVector = p2 - p1;

					for (var i = 0; i < projectedPrimaryLines.Count; i++)
					{
						var line = projectedPrimaryLines[i];
						var endPoint = line.Start + projectedVector;
						var finalEndPoint = ProjectPointOnPlane(GetIntersectionPoint(p0, p2, line.Start, endPoint), triangleNormal, triangleCenter);

						projectedSecondaryLines.Add(new Line { Start = finalEndPoint, End = line.Start }); // prohodit start a end

						//Debug.DrawRay(transform.TransformPoint(line.Start), transform.TransformDirection(projectedVector), Color.cyan, 5f);
						//Debug.DrawRay(transform.TransformPoint(finalEndPoint), Vector3.up, Color.red, 5f);
					}
				}

				// Adding vertices to a collection
				var verticesHeightCount = projectedPrimaryLines.Count + 1; // Height of vertices from bottom to top (doesn't include first vertex) - p1->p2 or p0->p2
				var verticesWidthCount = projectedSecondaryLines.Count + 2; // Width of vertices on bottom (includes first vertex) - p0->p1

				vertexPositions.Add(p0);
				vertexPositions.AddRange(iteration == 0
					? projectedSecondaryLines.Select(line => line.Start)
					: projectedSecondaryLines.Select(line => line.End));
				vertexPositions.Add(p1);

				// Third pass - remaining intersections of primary and secondary lines inside of the triangle
				if (iteration == 0)
				{
					for (var i = 0; i < projectedPrimaryLines.Count; i++)
					{
						var primaryLine = projectedPrimaryLines[i];
						vertexPositions.Add(primaryLine.Start);

						for (var j = i + 1; j < projectedSecondaryLines.Count; j++)
						{
							var secondaryLine = projectedSecondaryLines[j];
							var finalPoint = ProjectPointOnPlane(GetIntersectionPoint(primaryLine.Start, primaryLine.End, secondaryLine.Start, secondaryLine.End), triangleNormal, triangleCenter);

							vertexPositions.Add(finalPoint);
						}

						vertexPositions.Add(primaryLine.End);
					}
				}
				else
				{
					for (var i = 0; i < projectedSecondaryLines.Count; i++)
					{
						var secondaryLine = projectedSecondaryLines[i];
						vertexPositions.Add(secondaryLine.Start);

						for (var j = 0; j < projectedPrimaryLines.Count - 1 - i; j++)
						{
							var secondaryLineIncremented = projectedSecondaryLines[i + j + 1];
							var primaryLine = projectedPrimaryLines[j];
							var finalPoint = ProjectPointOnPlane(GetIntersectionPoint(primaryLine.Start, primaryLine.End, secondaryLineIncremented.Start, secondaryLineIncremented.End), triangleNormal, triangleCenter);

							vertexPositions.Add(finalPoint);
						}

						vertexPositions.Add(projectedPrimaryLines[projectedPrimaryLines.Count - 1 - i].End);
					}
				}

				vertexPositions.Add(p2);
				// Vertices are filled completelly here for supplied triangle

				//for (var i = startVertexIndex; i < vertices.Count; i++)
				//{
				//	Debug.DrawRay(transform.TransformPoint(vertices[i]), Vector3.up, Color.blue, 5f);
				//	await Task.Delay(100);
				//}

				// Building triangles
				// From left to right
				// From bottom to top
				for (int y = 0, index = startVertexIndex; y < verticesHeightCount; y++, verticesWidthCount--)
				{
					var incrementIndex = true;
					for (var x = 0; x < verticesWidthCount; index++)
					{
						if (x == verticesWidthCount - 1)
						{
							index++;
							break;
						}

						if (incrementIndex)
						{
							triangles.Add(index);
							triangles.Add(index + verticesWidthCount);
							triangles.Add(index + 1);
							x++;
							incrementIndex = false;
						}
						else
						{
							triangles.Add(index);
							triangles.Add(index + verticesWidthCount - 1);
							triangles.Add(index + verticesWidthCount);
							index--;
							incrementIndex = true;
						}

						//
						//if (x == 0)
						//{
						//	triangles.Add(index);
						//	triangles.Add(index + verticesWidthCount);
						//	triangles.Add(index + 1);
						//	x++;
						//}
						//else if (x == 1)
						//{
						//	triangles.Add(index);
						//	triangles.Add(index + verticesWidthCount - 1);
						//	triangles.Add(index + 1);
						//	x++;
						//}
						//else
						//{
						//	if (!incrementIndex)
						//	{
						//		triangles.Add(index);
						//		triangles.Add(index + verticesWidthCount - 2);
						//		triangles.Add(index + verticesWidthCount - 1);
						//		index--; // Not incrementing index in this iteration as it is needed to start from this vertex again on next triangle
						//		incrementIndex = true;
						//	}
						//	else if (x != verticesWidthCount - 1)
						//	{
						//		triangles.Add(index);
						//		triangles.Add(index + verticesWidthCount - 1);
						//		triangles.Add(index + 1);
						//		x++;
						//		incrementIndex = false;
						//	}
						//	else if (x == verticesWidthCount - 1)
						//		x++;
						//}
					}
				}
			});
		}

		public static Vector3 GetIntersectionPoint(Vector3 aStart, Vector3 aEnd, Vector3 bStart, Vector3 bEnd, bool debug = false)
		{
			float? xExclusive = null, zExclusive = null;
			var onlyFirstLineHasExclusive = false;

			// General equation of a line
			// First line
			var uA = aEnd - aStart;
			float tA, cA;
			if (!Mathf.Approximately(uA.x, 0.0f) && !Mathf.Approximately(uA.z, 0.0f))
			{
				tA = uA.x > uA.z ? Mathf.Min(uA.x, uA.z) / Mathf.Max(uA.x, uA.z) : Mathf.Max(uA.x, uA.z) / Mathf.Min(uA.x, uA.z);
				cA = -tA * aStart.x + aStart.z;

				if (debug)
					Debug.Log($"y = {tA}x + {cA}");
			}
			else if (Mathf.Approximately(uA.x, 0.0f))
			{
				tA = aStart.x;
				cA = 0.0f;
				xExclusive = tA;
				onlyFirstLineHasExclusive = true;

				if (debug)
					Debug.Log($"x = {tA}");
			}
			else
			{
				tA = aStart.z;
				cA = 0.0f;
				zExclusive = tA;
				onlyFirstLineHasExclusive = true;

				if (debug)
					Debug.Log($"y = {tA}");
			}

			// General equation of a line
			// Second line
			var uB = bEnd - bStart;
			float tB, cB;
			if (!Mathf.Approximately(uB.x, 0.0f) && !Mathf.Approximately(uB.z, 0.0f))
			{
				tB = uB.x > uB.z ? Mathf.Min(uB.x, uB.z) / Mathf.Max(uB.x, uB.z) : Mathf.Max(uB.x, uB.z) / Mathf.Min(uB.x, uB.z);
				cB = -tB * bStart.x + bStart.z;

				if (debug)
					Debug.Log($"y = {tB}x + {cB}");
			}
			else if (Mathf.Approximately(uB.x, 0.0f))
			{
				tB = bStart.x;
				cB = 0.0f;
				xExclusive = tB;
				onlyFirstLineHasExclusive = false;

				if (debug)
					Debug.Log($"x = {tB}");
			}
			else
			{
				tB = bStart.z;
				cB = 0.0f;
				zExclusive = tB;
				onlyFirstLineHasExclusive = false;

				if (debug)
					Debug.Log($"y = {tB}");
			}

			// Lines intersection
			Vector3 intersectionPoint;
			if (xExclusive.HasValue && zExclusive.HasValue)
			{
				intersectionPoint = new Vector3(xExclusive.Value, 0.0f, zExclusive.Value);
			}
			else if (xExclusive.HasValue)
			{
				var x = xExclusive.Value;
				//var z = Mathf.Approximately(xExclusive.Value, tB) ? tA * xExclusive.Value + cA : tB * xExclusive.Value + cB;
				var z = !onlyFirstLineHasExclusive ? tA * xExclusive.Value + cA : tB * xExclusive.Value + cB;
				intersectionPoint = new Vector3(x, 0.0f, z);
			}
			else if (zExclusive.HasValue)
			{
				var x = !onlyFirstLineHasExclusive ? (zExclusive.Value - cA) / tA : (zExclusive.Value - cB) / tB;
				//var x = Mathf.Approximately(zExclusive.Value, tB) ? (zExclusive.Value - cA) / tA : (zExclusive.Value - cB) / tB;
				var z = zExclusive.Value;
				intersectionPoint = new Vector3(x, 0.0f, z);
			}
			else
			{
				// tBx + cB = tAx + cA
				var xDiff = tA - tB;
				var x = (-cA + cB) / xDiff;
				var z = tB * x + cB;
				intersectionPoint = new Vector3(x, 0.0f, z);
			}

			if (debug)
				Debug.Log($"Intersection point: {intersectionPoint}");

			return intersectionPoint;
		}

		public static Vector3 ProjectPointOnPlane(Vector3 point, Vector3 normal, Vector3 center, bool debug = false)
		{
			var projectedPoint = point;

			// General equation of the plane
			// Y coordinate has to be projected on the triangle plane, because lines intersection is performed on XZ plane
			// normal.x * center.x + normal.y * center.y + normal.z * center.z + d = 0
			// normal.y * center.y = - normal.x * center.x - normal.z * center.z - d
			// center.y = (- normal.x * center.x - normal.z * center.z - d) / normal.y
			var d = -(normal.x * center.x) - (normal.y * center.y) - (normal.z * center.z);
			projectedPoint.y = (-normal.x * projectedPoint.x - normal.z * projectedPoint.z - d) / normal.y;

			if (debug)
				Debug.Log($"Projected point: {projectedPoint}");

			return projectedPoint;
		}


		private struct Line
		{
			public Vector3 Start;
			public Vector3 End;
		}
	}
}
