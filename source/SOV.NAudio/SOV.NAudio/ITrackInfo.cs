namespace SOV.NAudio
{
	public class TrackInfo
	{
		public int Number;
		public string Title;
		public string Performer;
	}

	public interface ITrackInfo
	{
		TrackInfo TrackInfo { get; }
		int TrackCount { get; }
	}
}