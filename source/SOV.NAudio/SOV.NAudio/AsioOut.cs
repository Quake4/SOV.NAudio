using NAud = NAudio;

namespace SOV.NAudio
{
	public class AsioOut : NAud.Wave.AsioOut
	{
		public AsioOut(string driverName)
			: base(driverName)
		{
		}
	}
}