using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Collections.Generic;
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
					format.SetEncoding(WaveFormatEncoding.DoP);
				return format;
			}
		}

		public WasapiOut(MMDevice device, AudioClientShareMode shareMode, IDictionary<WaveFormatEncoding, int[]> samplerate, bool useEventSync = true, int latency = 100)
			: base(device, shareMode, useEventSync, latency, samplerate)
		{
		}
	}
}