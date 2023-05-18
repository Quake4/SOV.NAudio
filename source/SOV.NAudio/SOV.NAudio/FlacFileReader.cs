using NAudio.Flac;

namespace SOV.NAudio
{
	public class FlacFileReader : FlacReader
	{
		public FlacFileReader(string filename)
			: base(filename)
		{
		}
	}
}