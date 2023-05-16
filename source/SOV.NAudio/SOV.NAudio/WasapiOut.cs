using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAud = NAudio;

namespace SOV.NAudio
{
	public class WasapiOut : NAud.Wave.WasapiOut, IWaveFormat
	{
		public bool ResamplerUsed => dmoResamplerNeeded;

		public bool SharedMode => shareMode == AudioClientShareMode.Shared;

		public WaveFormat WaveFormat { get { return SharedMode ? audioClient.MixFormat : base.OutputWaveFormat; } }

		public WasapiOut(MMDevice device, AudioClientShareMode shareMode, bool useEventSync = false, int latency = 100)
			: base(device, shareMode, useEventSync, latency)
		{
		}
	}
}