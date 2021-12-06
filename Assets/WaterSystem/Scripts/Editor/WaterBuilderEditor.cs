// Copyright (c) Adam Jůva.
// Licensed under the MIT License.

using UnityEditor;
using UnityEngine;

namespace WaterSystem.Editor
{
	[CustomEditor(typeof(WaterBuilder))]
	public class WaterBuilderEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			DrawWaterMeshEditorOptions();
			DrawWaterFlowEditorOptions();
			DrawWaterBaseEditorOptions();
		}

		private void DrawWaterMeshEditorOptions()
		{
			var style = new GUIStyle()
			{
				normal = new GUIStyleState() { textColor = Color.white },
				fontStyle = FontStyle.Bold
			};

			GUILayout.Space(10);
			GUILayout.Label("Water Mesh Editor", style);

			if (GUILayout.Button("Add Mesh Point"))
			{
				((WaterBuilder)target).CreateMeshPoint();
			}

			if (GUILayout.Button("Create Water Mesh"))
			{
				_ = ((WaterBuilder)target).CreateWaterMesh();
			}

			if (GUILayout.Button("Save Water Mesh"))
			{
				((WaterBuilder)target).SaveWaterMesh();
			}
		}

		private void DrawWaterFlowEditorOptions()
		{
			var style = new GUIStyle()
			{
				normal = new GUIStyleState() { textColor = Color.white },
				fontStyle = FontStyle.Bold
			};

			GUILayout.Space(10);
			GUILayout.Label("Water Flow Editor", style);

			if (GUILayout.Button("Add Flow Vector"))
			{
				((WaterBuilder)target).CreateFlowVector();
			}

			if (GUILayout.Button("Create Flow Map"))
			{
				((WaterBuilder)target).CreateFlowMap();
			}

			if (GUILayout.Button("Save Flow Map"))
			{
				((WaterBuilder)target).SaveFlowMap();
			}
		}

		private void DrawWaterBaseEditorOptions()
		{
			var style = new GUIStyle()
			{
				normal = new GUIStyleState() { textColor = Color.white },
				fontStyle = FontStyle.Bold
			};

			GUILayout.Space(10);
			GUILayout.Label("Water Visual Editor", style);

			if (GUILayout.Button("Add Water Base"))
			{
				((WaterBuilder)target).AddWaterBase();
			}

			if (GUILayout.Button("Add Water Dynamics"))
			{
				((WaterBuilder)target).AddWaterDynamics();
			}

			if (GUILayout.Button("Add Water Interactions"))
			{
				((WaterBuilder)target).AddWaterInteractions();
			}
		}
	}
}