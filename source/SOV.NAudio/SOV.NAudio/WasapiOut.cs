using NAudio.CoreAudioApi;
using NAud = NAudio;

namespace SOV.NAudio
{
	public class WasapiOut : NAud.Wave.WasapiOut
	{
		public WasapiOut(MMDevice device, AudioClientShareMode shareMode, bool useEventSync, int latency = 100)
			: base(device, shareMode, useEventSync, latency)
		{
		}
	}
}