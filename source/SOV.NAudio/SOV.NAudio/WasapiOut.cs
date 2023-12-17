using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Reflection;
using NAud = NAudio;

namespace SOV.NAudio
{
	public class WasapiOut : NAud.Wave.WasapiOut, IWaveFormat
	{
		public bool ResamplerUsed => dmoResamplerNeeded;

		public bool SharedMode => shareMode == AudioClientShareMode.Shared;

		public WaveFormat WaveFormat
		{
			get
			{
				if (SharedMode)
					return audioClient.MixFormat;
				var format = InternalWaveFormat.ToStandardWaveFormat(true);
				if (sourceWaveFormat.Encoding == WaveFormatEncoding.DSD)
				{
					var myFieldInfo = format.GetType().GetField("waveFormatTag", BindingFlags.NonPublic | BindingFlags.Instance);
					myFieldInfo.SetValue(format, WaveFormatEncoding.DoP);
				}
				return format;
			}
		}

		public WasapiOut(MMDevice device, AudioClientShareMode shareMode, bool useEventSync = true, int latency = 100)
			: base(device, shareMode, useEventSync, latency)
		{
		}
	}
}