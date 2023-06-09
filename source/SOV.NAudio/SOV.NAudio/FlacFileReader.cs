using NAudio.Flac;

namespace SOV.NAudio
{
	public class FlacFileReader : FlacReader
	{
		public FlacFileReader(string filename)
			: base(System.IO.File.OpenRead(filename), FlacPreScanMethodMode.Async)
		{
		}
	}
}