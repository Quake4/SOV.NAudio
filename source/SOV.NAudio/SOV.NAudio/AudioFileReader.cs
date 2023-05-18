using NAudio.Wave;
using System;

namespace SOV.NAudio
{
	public class AudioFileReader : WaveStream
	{
		public static string[] Files = new string[] { ".flac", ".mp3", ".m4a", ".mp4", ".aiff", ".aif", ".wav" };

		protected WaveStream readerStream;

		public AudioFileReader(string fileName)
		{
			FileName = fileName;
			CreateReaderStream(fileName);
		}

		protected void CreateReaderStream(string fileName)
		{
			if (fileName.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
				readerStream = new FlacFileReader(fileName);
			else if (fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
			{
				readerStream = new WaveFileReader(fileName);
				if (readerStream.WaveFormat.Encoding != WaveFormatEncoding.Pcm && readerStream.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
					throw new Exception($"File not supported {fileName} in encoding {readerStream.WaveFormat.Encoding}");
			}
			else if (fileName.EndsWith(".aiff", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".aif", StringComparison.OrdinalIgnoreCase))
				readerStream = new AiffFileReader(fileName);
			else
				// fall back to media foundation reader, see if that can play it
				readerStream = new MediaFoundationReader(fileName);
		}

		public string FileName { get; }

		public override WaveFormat WaveFormat => readerStream.WaveFormat;

		public override long Length => readerStream.Length;

		public override long Position { get => readerStream.Position; set => readerStream.Position = value; }

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (readerStream == null)
				return 0;
			return readerStream.Read(buffer, offset, count);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && readerStream != null)
			{
				readerStream.Dispose();
				readerStream = null;
			}
			base.Dispose(disposing);
		}
	}
}