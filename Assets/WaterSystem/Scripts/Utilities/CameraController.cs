using UnityEngine;

namespace WaterSystem.Utilities
{
	public class CameraController : MonoBehaviour
	{
		[SerializeField] private Transform target;
		[SerializeField] private float rotationSpeed = 2.0f;

		private float pitch;
		private float yaw;


		private void Update()
		{
			pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
			pitch = Mathf.Clamp(pitch, -90f, 90f);

			yaw += Input.GetAxis("Mouse X") * rotationSpeed;
			yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;

			transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.right);
		}

		private void LateUpdate()
		{
			if (!target)
				return;

			transform.position = target.position;
		}
	}
}