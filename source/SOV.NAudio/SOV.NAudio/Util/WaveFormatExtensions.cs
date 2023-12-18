using System.Reflection;

namespace NAudio.Wave
{
	public static class WaveFormatExtensions
	{
		private static FieldInfo _fiEncoding;

		public static WaveFormat SetEncoding(this WaveFormat format, WaveFormatEncoding encoding)
		{
			if (_fiEncoding == null)
				_fiEncoding = format.GetType().GetField("waveFormatTag", BindingFlags.NonPublic | BindingFlags.Instance);
			_fiEncoding.SetValue(format, encoding);
			return format;
		}
	}
}