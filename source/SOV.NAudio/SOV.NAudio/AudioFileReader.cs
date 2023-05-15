using NAudio.Wave;
using System;

namespace SOV.NAudio
{
	/// <summary>
	/// AudioFileReader simplifies opening an audio file in NAudio
	/// Simply pass in the filename, and it will attempt to open the
	/// file and set up a conversion path that turns into PCM IEEE float.
	/// ACM codecs will be used for conversion.
	/// It provides a volume property and implements both WaveStream and
	/// ISampleProvider, making it possibly the only stage in your audio
	/// pipeline necessary for simple playback scenarios
	/// </summary>
	public class AudioFileReader : WaveStream
	{
		protected WaveStream readerStream; // the waveStream which we will use for all positioning

		/// <summary>
		/// Initializes a new instance of AudioFileReader
		/// </summary>
		/// <param name="fileName">The file to open</param>
		public AudioFileReader(string fileName)
		{
			FileName = fileName;
			CreateReaderStream(fileName);
		}

		/// <summary>
		/// Creates the reader stream, supporting all filetypes in the core NAudio library,
		/// and ensuring we are in PCM format
		/// </summary>
		/// <param name="fileName">File Name</param>
		protected void CreateReaderStream(string fileName)
		{
			if (fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
			{
				readerStream = new WaveFileReader(fileName);
				if (readerStream.WaveFormat.Encoding != WaveFormatEncoding.Pcm && readerStream.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
				{
					throw new Exception($"File not supported {fileName} in encoding {readerStream.WaveFormat.Encoding}");
					//readerStream = WaveFormatConversionStream.CreatePcmStream(readerStream);
					//readerStream = new BlockAlignReductionStream(readerStream);
				}
			}
			else if (fileName.EndsWith(".aiff", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".aif", StringComparison.OrdinalIgnoreCase))
				readerStream = new AiffFileReader(fileName);
			else
				// fall back to media foundation reader, see if that can play it
				readerStream = new MediaFoundationReader(fileName);
		}
		/// <summary>
		/// File Name
		/// </summary>
		public string FileName { get; }

		/// <summary>
		/// WaveFormat of this stream
		/// </summary>
		public override WaveFormat WaveFormat => readerStream.WaveFormat;

		public override long Length => readerStream.Length;

		public override long Position { get => readerStream.Position; set => readerStream.Position = value; }

		public override int Read(byte[] buffer, int offset, int count)
		{
			return readerStream.Read(buffer, offset, count);
		}

		/// <summary>
		/// Disposes this AudioFileReader
		/// </summary>
		/// <param name="disposing">True if called from Dispose</param>
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