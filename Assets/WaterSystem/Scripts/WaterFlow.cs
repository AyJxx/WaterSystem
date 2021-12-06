// Copyright (c) Adam Jůva.
// Licensed under the MIT License.

using System;
using UnityEditor;
using UnityEngine;

namespace WaterSystem
{
	/// <summary>
	/// Class responsible for adjusting flow of the water.
	/// </summary>
	[ExecuteInEditMode]
	public class WaterFlow : MonoBehaviour
	{
		[Tooltip("True to make this flow direction global and other directions will blend with it.")]
		[SerializeField] private bool isGlobalFlow;

		[SerializeField, Range(0, 100)] private float flowRadius = 1.0f;
		[SerializeField, Range(0, 1)] private float flowSpeed = 0.25f;
		[SerializeField, Range(0, 1)] private float flowBlending = 0.5f;

		private bool lastGlobalFlowState;

		private const float FlowBlendingFactor = 5.0f;


		public bool IsGlobalFlow
		{
			get => isGlobalFlow;
			set
			{
				isGlobalFlow = value;
				lastGlobalFlowState = value;
			} 
		}

		public float FlowRadius => flowRadius;
		public float FlowSpeed => flowSpeed;
		public float FlowBlending => Mathf.Lerp(FlowBlendingFactor, 1.0f, flowBlending);


		private Action<WaterFlow> GlobalFlowChanged;


		private void OnValidate()
		{
			if (isGlobalFlow && !lastGlobalFlowState)
			{
				GlobalFlowChanged?.Invoke(this);
			}

			lastGlobalFlowState = isGlobalFlow;
		}

#if UNITY_EDITOR
		private void OnDrawGizmos()
		{
			Handles.color = Color.yellow;
			Handles.DrawWireDisc(transform.position, Vector3.up, flowRadius);
			Handles.ArrowHandleCap(0, transform.position, transform.rotation, 1, EventType.Repaint);
		}
#endif

		public void Initialize(Action<WaterFlow> globalFlowChanged)
		{
			GlobalFlowChanged = globalFlowChanged;
		}
	}
}