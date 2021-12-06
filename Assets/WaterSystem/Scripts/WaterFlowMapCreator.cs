// Copyright (c) Adam Jůva.
// Licensed under the MIT License.

using System.Linq;
using UnityEngine;

namespace WaterSystem
{
	/// <summary>
	/// Class responsible for water flow texture.
	/// </summary>
	public class WaterFlowMapCreator
	{
		private readonly MeshFilter waterMeshFilter;
		private readonly Material drawFlowMapMaterial;


		private static readonly int FlowCoordinateID = Shader.PropertyToID("_FlowCoordinate");
		private static readonly int FlowDirectionID = Shader.PropertyToID("_FlowDirection");
		private static readonly int FlowRadiusID = Shader.PropertyToID("_FlowRadius");
		private static readonly int FlowSpeedID = Shader.PropertyToID("_FlowSpeed");
		private static readonly int FlowBlendingID = Shader.PropertyToID("_FlowBlending");
		private static readonly int GlobalFlowID = Shader.PropertyToID("_GlobalFlow");


		public WaterFlowMapCreator(MeshFilter waterMeshFilter, Material drawFlowMapMaterial)
		{
			this.waterMeshFilter = waterMeshFilter;
			this.drawFlowMapMaterial = drawFlowMapMaterial;
		}

		/// <summary>
		/// Creates flow map texture.
		/// </summary>
		/// <param name="flowVectorsTransform">Transform which includes flow vectors.</param>
		/// <param name="flowMapWidth">Used for flow map width and height.</param>
		/// <returns>Flow map render texture.</returns>
		public RenderTexture CreateFlowMap(Transform flowVectorsTransform, int flowMapWidth)
		{
			// Mapping flow position and radius to calculated UVs
			var vertices = waterMeshFilter.sharedMesh.vertices;

			var minX = vertices.OrderBy(vertex => vertex.x).FirstOrDefault().x;
			var maxX = vertices.OrderBy(vertex => vertex.x).Reverse().FirstOrDefault().x;
			var minZ = vertices.OrderBy(vertex => vertex.z).FirstOrDefault().z;
			var maxZ = vertices.OrderBy(vertex => vertex.z).Reverse().FirstOrDefault().z;
			var diffX = maxX - minX;
			var diffZ = maxZ - minZ;

			var edgeLength = Mathf.Max(diffX, diffZ);

			var flowMap = new RenderTexture(flowMapWidth, flowMapWidth, 0, RenderTextureFormat.ARGB32);

			// Sort flow vector transforms, so global flow is on the first index to create proper flow map
			var flowVectors = flowVectorsTransform.GetComponentsInChildren<WaterFlow>();
			var sortedFlowVectors = flowVectors.OrderBy(flowVector => !flowVector.IsGlobalFlow);

			// If no flow vector is marked as global flow, making one drawing pass to flow map with 0 speed and enabled global flow
			// to avoid having black areas on the flow map
			var globalFlowContained = flowVectors.FirstOrDefault(flowVector => flowVector.IsGlobalFlow);
			if (!globalFlowContained)
			{
				drawFlowMapMaterial.SetFloat(FlowSpeedID, 0.0f);
				drawFlowMapMaterial.SetInt(GlobalFlowID, 1);

				WriteToTexture(flowMap);
			}

			foreach (var flowVector in sortedFlowVectors)
			{
				var xCoord = (flowVector.transform.localPosition.x - minX) / edgeLength;
				var yCoord = (flowVector.transform.localPosition.z - minZ) / edgeLength;
				var flowCoordinate = new Vector2(xCoord, yCoord);
				var flowRadius = Mathf.Clamp01(flowVector.FlowRadius / edgeLength);

				drawFlowMapMaterial.SetVector(FlowCoordinateID, flowCoordinate);
				drawFlowMapMaterial.SetVector(FlowDirectionID, new Vector4(flowVector.transform.forward.x, flowVector.transform.forward.z));
				drawFlowMapMaterial.SetFloat(FlowRadiusID, flowRadius);
				drawFlowMapMaterial.SetFloat(FlowSpeedID, flowVector.FlowSpeed);
				drawFlowMapMaterial.SetFloat(FlowBlendingID, flowVector.FlowBlending);
				drawFlowMapMaterial.SetInt(GlobalFlowID, flowVector.IsGlobalFlow ? 1 : 0);

				WriteToTexture(flowMap);
			}

			return flowMap;
		}

		private void WriteToTexture(RenderTexture flowMap)
		{
			var temp = RenderTexture.GetTemporary(flowMap.width, flowMap.height, 0, RenderTextureFormat.ARGBFloat);
			Graphics.Blit(flowMap, temp);
			Graphics.Blit(temp, flowMap, drawFlowMapMaterial);
			RenderTexture.ReleaseTemporary(temp);
		}
	}
}
