// Copyright (c) Adam Jůva.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace WaterSystem
{
#if UNITY_EDITOR
	public class FileModificationProcessor : UnityEditor.AssetModificationProcessor
	{
		public static event Action ProjectSaved;


		private static string[] OnWillSaveAssets(string[] paths)
		{
			PropagateCallback();
			return paths;
		}

		private static async void PropagateCallback()
		{
			// Delay is needed here, because event has to be invoked after assets are saved and not before
			await Task.Delay(1);

			ProjectSaved?.Invoke();
		}
	}
#endif
}