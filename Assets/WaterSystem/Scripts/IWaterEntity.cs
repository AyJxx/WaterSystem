namespace WaterSystem
{
	/// <summary>
	/// Implement this interface for all objects which can interact with the water to have access to the water height, etc.
	/// </summary>
	public interface IWaterEntity
	{
		/// <summary>
		/// Water Base this object is currently colliding with.
		/// </summary>
		WaterBase CurrentWaterBase { get; set; }
	}
}
