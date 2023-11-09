using NAudio.Wave;
using NAud = NAudio;

namespace SOV.NAudio
{
	public class AsioOut : NAud.Wave.AsioOut, IWaveFormat
	{
		public bool ResamplerUsed => dmoResamplerUsed;

		public WaveFormat WaveFormat => OutputWaveFormat;

		public AsioOut(string driverName)
			: base(driverName)
		{
		}
	}
}