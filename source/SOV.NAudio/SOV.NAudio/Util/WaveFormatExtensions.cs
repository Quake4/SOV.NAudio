using NAudio.Wave;
using System.Reflection;

namespace NAudio.Wave
{
	public static class WaveFormatExtensions
	{
		public static void SetEncoding(this WaveFormat format, WaveFormatEncoding encoding)
		{
			var myFieldInfo = format.GetType().GetField("waveFormatTag", BindingFlags.NonPublic | BindingFlags.Instance);
			myFieldInfo.SetValue(format, encoding);
		}
	}
}