using NAudio.Wave;

namespace SOV.NAudio
{
	/// <summary>
	/// Represents the interface to a device that can play a WaveFile
	/// </summary>
	public interface IWaveFormat
	{
		/// <summary>
		/// Gets the WaveFormat of this WaveProvider.
		/// </summary>
		/// <value>The wave format.</value>
		WaveFormat WaveFormat { get; }
	}
}