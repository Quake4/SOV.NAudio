using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

// ReSharper disable once CheckNamespace
namespace NAudio.Wave
{
	/// <summary>
	/// Support for playback using Wasapi
	/// </summary>
	public class WasapiOut : IWavePlayer//, IWavePosition
    {
        protected AudioClient audioClient;
        private readonly MMDevice mmDevice;
        protected readonly AudioClientShareMode shareMode;
        private AudioRenderClient renderClient;
        private IWaveProvider sourceProvider;
        protected WaveFormat sourceWaveFormat;
        private int latencyMilliseconds;
        private int bufferFrameCount;
        private int bytesPerFrame;
        private readonly bool isUsingEventSync;
        private EventWaitHandle frameEventWaitHandle;
        private byte[] readBuffer;
        private volatile PlaybackState playbackState;
        private Thread playThread;
        private readonly SynchronizationContext syncContext;
        protected bool dmoResamplerNeeded;
		WaveFormatExtensible internalWaveFormat;
		WasapiFrameConverter.FrameConverter frameConverter;
		protected readonly IDictionary<WaveFormatEncoding, int[]> sampleRate;

		/// <summary>
		/// Playback Stopped
		/// </summary>
		public event EventHandler<StoppedEventArgs> PlaybackStopped;

        /// <summary>
        /// WASAPI Out shared mode, default
        /// </summary>
        public WasapiOut() :
            this(GetDefaultAudioEndpoint(), AudioClientShareMode.Shared, true, 200)
        {

        }

        /// <summary>
        /// WASAPI Out using default audio endpoint
        /// </summary>
        /// <param name="shareMode">ShareMode - shared or exclusive</param>
        /// <param name="latency">Desired latency in milliseconds</param>
        public WasapiOut(AudioClientShareMode shareMode, int latency) :
            this(GetDefaultAudioEndpoint(), shareMode, true, latency)
        {

        }

        /// <summary>
        /// WASAPI Out using default audio endpoint
        /// </summary>
        /// <param name="shareMode">ShareMode - shared or exclusive</param>
        /// <param name="useEventSync">true if sync is done with event. false use sleep.</param>
        /// <param name="latency">Desired latency in milliseconds</param>
        public WasapiOut(AudioClientShareMode shareMode, bool useEventSync, int latency) :
            this(GetDefaultAudioEndpoint(), shareMode, useEventSync, latency)
        {

        }

        /// <summary>
        /// Creates a new WASAPI Output
        /// </summary>
        /// <param name="device">Device to use</param>
        /// <param name="shareMode"></param>
        /// <param name="useEventSync">true if sync is done with event. false use sleep.</param>
        /// <param name="latency">Desired latency in milliseconds</param>
        public WasapiOut(MMDevice device, AudioClientShareMode shareMode, bool useEventSync, int latency, IDictionary<WaveFormatEncoding, int[]> samplerate = null)
        {
			sampleRate = samplerate;
			audioClient = device.AudioClient;
            mmDevice = device;
            this.shareMode = shareMode;
            isUsingEventSync = useEventSync;
            latencyMilliseconds = latency;
            syncContext = SynchronizationContext.Current;
			// allow the user to query the default format for shared mode streams
			InternalWaveFormat = new WaveFormatExtensible(audioClient.MixFormat.SampleRate, audioClient.MixFormat.BitsPerSample,
				audioClient.MixFormat.Channels, float32: audioClient.MixFormat.Encoding == WaveFormatEncoding.IeeeFloat);
        }

        static MMDevice GetDefaultAudioEndpoint()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                throw new NotSupportedException("WASAPI supported only on Windows Vista and above");
            }
            var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
        }

        private void PlayThread()
        {
            ResamplerDmoStream resamplerDmoStream = null;
            IWaveProvider playbackProvider = sourceProvider;
            Exception exception = null;
            try
            {
                if (dmoResamplerNeeded)
                {
                    resamplerDmoStream = new ResamplerDmoStream(sourceProvider, OutputWaveFormat, ResamplerDmoStream.MaxQuality);
                    playbackProvider = resamplerDmoStream;
                }
                // fill a whole buffer
                bufferFrameCount = audioClient.BufferSize;
                bytesPerFrame = OutputWaveFormat.Channels * OutputWaveFormat.BitsPerSample / 8;
                readBuffer = BufferHelpers.Ensure(readBuffer, bufferFrameCount * Math.Max(OutputWaveFormat.BlockAlign,
					(sourceWaveFormat.Encoding == WaveFormatEncoding.DSD ? 2 : 1) * sourceWaveFormat.BlockAlign));
                if (FillBuffer(playbackProvider, bufferFrameCount))
                {
                    // played a zero length stream - exit immediately
                    return;
                }
                // to calculate buffer duration but does always seem to match latency
                // var bufferDurationMilliseconds = (bufferFrameCount * 1000) /OutputWaveFormat.SampleRate;
                // Create WaitHandle for sync
                var waitHandles = new WaitHandle[] { frameEventWaitHandle };

                audioClient.Start();

                while (playbackState != PlaybackState.Stopped)
                {
                    // If using Event Sync, Wait for notification from AudioClient or Sleep half latency
                    if (isUsingEventSync)
                    {
                        WaitHandle.WaitAny(waitHandles, 3 * latencyMilliseconds, false);
                    }
                    else
                    {
                        Thread.Sleep(latencyMilliseconds / 2);
                    }

                    // If still playing
                    if (playbackState == PlaybackState.Playing)
                    {
                        // See how much buffer space is available.
                        int numFramesPadding;
                        if (isUsingEventSync)
                        {
                            // In exclusive mode, always ask the max = bufferFrameCount = audioClient.BufferSize
                            numFramesPadding = (shareMode == AudioClientShareMode.Shared) ? audioClient.CurrentPadding : 0;
                        }
                        else
                        {
                            numFramesPadding = audioClient.CurrentPadding;
                        }
                        int numFramesAvailable = bufferFrameCount - numFramesPadding;
                        if (numFramesAvailable > 10) // see https://naudio.codeplex.com/workitem/16363
                        {
                            if (FillBuffer(playbackProvider, numFramesAvailable))
                            {
                                // reached the end
                                break;
                            }
                        }
                    }
                }
                if (playbackState == PlaybackState.Playing)
                {
                    // we got here by reaching the end of the input file, so
                    // let's make sure the last buffer has time to play
                    // (otherwise the user requested stop, so we'll just stop
                    // immediately
                    Thread.Sleep(isUsingEventSync ? latencyMilliseconds : latencyMilliseconds / 2);
                }
                audioClient.Stop();
                // set if we got here by reaching the end
                playbackState = PlaybackState.Stopped;
                audioClient.Reset();
            }
            catch (Exception e)
            {
                exception = e;
            }
            finally
            {
                if (resamplerDmoStream != null)
                {
                    resamplerDmoStream.Dispose();
                }
                RaisePlaybackStopped(exception);
            }
        }

        private void RaisePlaybackStopped(Exception e)
        {
            var handler = PlaybackStopped;
            if (handler != null)
            {
                if (syncContext == null)
                {
                    handler(this, new StoppedEventArgs(e));
                }
                else
                {
                    syncContext.Post(state => handler(this, new StoppedEventArgs(e)), null);
                }
            }
        }

        /// <summary>
        /// returns true if reached the end
        /// </summary>
        private bool FillBuffer(IWaveProvider playbackProvider, int frameCount)
        {
            var readLength = frameCount * bytesPerFrame;
			var sourceReadLength = readLength;
			var sourceBlockAligh = sourceWaveFormat.BlockAlign;
			if (frameConverter != null)
			{
				if (sourceWaveFormat.Encoding == WaveFormatEncoding.DSD)
					sourceBlockAligh = sourceWaveFormat.Channels * 2;
				sourceReadLength = frameCount * sourceBlockAligh;
			}

			int read = playbackProvider.Read(readBuffer, 0, sourceReadLength);
			if (read == 0) return true;

            var buffer = renderClient.GetBuffer(frameCount);
			if (frameConverter != null)
			{
				var frames = read / sourceBlockAligh;
				unsafe
				{
					fixed (void* pBuffer = readBuffer)
						frameConverter(new IntPtr(pBuffer), sourceWaveFormat.Channels, buffer, OutputWaveFormat.Channels, frames);
					read = frames * OutputWaveFormat.BlockAlign;
				}
			}
			else
				Marshal.Copy(readBuffer, 0, buffer, read);
            if (this.isUsingEventSync && this.shareMode == AudioClientShareMode.Exclusive)
            {
                if (read < readLength)
                {
                    // need to silence the end of the buffer as we have to pass frameCount
                    byte silence = sourceWaveFormat.Encoding == WaveFormatEncoding.DSD ? (byte)0x69 : (byte)0;
                    unsafe
                    {
                        byte* pByte = (byte*)buffer;
                        while(read < readLength)
                            pByte[read++] = silence;
                    }
                }

                renderClient.ReleaseBuffer(frameCount, AudioClientBufferFlags.None);
            }
            else
            {
                int actualFrameCount = read / bytesPerFrame;
                /*if (actualFrameCount != frameCount)
                {
                    Debug.WriteLine(String.Format("WASAPI wanted {0} frames, supplied {1}", frameCount, actualFrameCount ));
                }*/
                renderClient.ReleaseBuffer(actualFrameCount, AudioClientBufferFlags.None);
            }
            return false;
        }

        private WaveFormatExtensible GetFallbackFormat(WaveFormat source, bool dop = false)
        {
            var deviceChannels = audioClient.MixFormat.Channels; // almost certain to be stereo

			// we are in exclusive mode
			// First priority is to try the sample rate you provided.
			var sampleRatesToTry = new List<int>(10);

			var channelCountsToTry = new List<int>(3) { source.Channels };
			if (!channelCountsToTry.Contains(deviceChannels)) channelCountsToTry.Add(deviceChannels);
			if (!channelCountsToTry.Contains(2)) channelCountsToTry.Add(2);

			var bitDepthsToTry = new List<int>(4);

			int[] sr_values = null;
			int samplerate = source.SampleRate;
			var formatEncoding = WaveFormatEncoding.PCM;

			if (dop)
			{
				formatEncoding = WaveFormatEncoding.DoP;
				samplerate = source.SampleRate / 16;
				sampleRatesToTry.Add(samplerate);
				bitDepthsToTry.Add(32);
				bitDepthsToTry.Add(24);

				if (sampleRate != null && sampleRate.ContainsKey(formatEncoding))
					sr_values = sampleRate[formatEncoding];
			}
			else
			{
				var sampleRatesToTryLower = new List<int>();
				// And if we've not already got 44.1 and 48kHz in the list, let's try them too
				var baseSampleRate = source.SampleRate % 44100 == 0 ? 44100 : 48000;
				for (int i = 1; i < (1 << 4); i = i << 1)
				{
					var sr = i * baseSampleRate;
					if (sr < source.SampleRate)
						sampleRatesToTryLower.Add(sr);
					else
						sampleRatesToTry.Add(sr);
				}
				// Add lower as reverse
				sampleRatesToTryLower.Reverse();
				sampleRatesToTry.AddRange(sampleRatesToTryLower);
				// Last priority is to use the sample rate the device wants
				//if (!sampleRatesToTry.Contains(deviceSampleRate)) sampleRatesToTry.Add(deviceSampleRate);

				bitDepthsToTry.Add(source.BitsPerSample);
				if (!bitDepthsToTry.Contains(32)) bitDepthsToTry.Add(32);
				if (!bitDepthsToTry.Contains(24)) bitDepthsToTry.Add(24);
				if (!bitDepthsToTry.Contains(16)) bitDepthsToTry.Add(16);

				if (sampleRate != null && sampleRate.ContainsKey(WaveFormatEncoding.PCM))
					sr_values = sampleRate[WaveFormatEncoding.PCM];
			}

			foreach (var sampleRate in sampleRatesToTry)
            {
				// check sample rate
				if (sr_values != null && !sr_values.Contains(sampleRate)) continue;
                foreach (var channelCount in channelCountsToTry)
                {
                    foreach (var bitDepth in bitDepthsToTry)
                    {
						var format = new WaveFormatExtensible(sampleRate, bitDepth, channelCount);
						if (audioClient.IsFormatSupported(shareMode, format))
							return format;
						// 24bit as 32bit
						if (bitDepth == 32 && (source.BitsPerSample == 24 || source.BitsPerSample == 32))
						{
							format = new WaveFormatExtensible(sampleRate, 24, channelCount, 1);
							if (audioClient.IsFormatSupported(shareMode, format))
								return format;
						}
					}
				}
            }

            throw new NotSupportedException($"Desired {formatEncoding} sample rate '{samplerate}' dosn't supported or disabled.");
        }

		public WaveFormatExtensible InternalWaveFormat
		{
			get { return internalWaveFormat; }
			protected set { internalWaveFormat = value; OutputWaveFormat = value.ToStandardWaveFormat(); }
		}

		/// <summary>
		/// Gets a <see cref="Wave.WaveFormat"/> instance indicating the format the hardware is using.
		/// </summary>
		public WaveFormat OutputWaveFormat { get; protected set; }

#region IWavePlayer Members

        /// <summary>
        /// Begin Playback
        /// </summary>
        public void Play()
        {
            if (playbackState != PlaybackState.Playing)
            {
                if (playbackState == PlaybackState.Stopped)
                {
                    playThread = new Thread(PlayThread)
                    {
                        Priority = ThreadPriority.Highest
                    };
                    playbackState = PlaybackState.Playing;
                    playThread.Start();                    
                }
                else
                {
                    playbackState = PlaybackState.Playing;
                }                
            }
        }

        /// <summary>
        /// Stop playback and flush buffers
        /// </summary>
        public void Stop()
        {
            if (playbackState != PlaybackState.Stopped)
            {
                playbackState = PlaybackState.Stopped;
                playThread.Join();
                playThread = null;
            }
        }

        /// <summary>
        /// Stop playback without flushing buffers
        /// </summary>
        public void Pause()
        {
            if (playbackState == PlaybackState.Playing)
            {
                playbackState = PlaybackState.Paused;
            }            
        }

        /// <summary>
        /// Initialize for playing the specified wave stream
        /// </summary>
        /// <param name="waveProvider">IWaveProvider to play</param>
        public void Init(IWaveProvider waveProvider)
        {
            if (sourceProvider != null && sourceWaveFormat.ToString() == waveProvider.WaveFormat.ToString())
            {
                sourceProvider = waveProvider;
                return;
            }

            long latencyRefTimes = latencyMilliseconds * 10000L;
            WaveFormat prevOutputWaveFormat = OutputWaveFormat;
			sourceProvider = null;
			sourceWaveFormat = null;

			// allow auto sample rate conversion - works for shared mode
			var flags = AudioClientStreamFlags.AutoConvertPcm | AudioClientStreamFlags.SrcDefaultQuality;

            if (shareMode == AudioClientShareMode.Exclusive)
            {
                flags = AudioClientStreamFlags.None;
				if (waveProvider.WaveFormat.Encoding == WaveFormatEncoding.DSD)
				{
					InternalWaveFormat = GetFallbackFormat(waveProvider.WaveFormat, true);
					frameConverter = WasapiFrameConverter.SelectFrameConverter(waveProvider.WaveFormat, OutputWaveFormat);
					dmoResamplerNeeded = false;
				}
				else
				{
					bool skip_sr = false;
					if (sampleRate != null && sampleRate.ContainsKey(WaveFormatEncoding.PCM))
						if (sampleRate[WaveFormatEncoding.PCM] != null && !sampleRate[WaveFormatEncoding.PCM].Contains(waveProvider.WaveFormat.SampleRate))
							skip_sr = true;
					var format = new WaveFormatExtensible(waveProvider.WaveFormat.SampleRate, waveProvider.WaveFormat.BitsPerSample,
						waveProvider.WaveFormat.Channels, float32: waveProvider.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat);
					if (skip_sr || format.ToStandardWaveFormat().Encoding != OutputWaveFormat.Encoding ||
						!audioClient.IsFormatSupported(shareMode, format/*, out WaveFormatExtensible closestSampleRateFormat*/))
					{
						// Use closesSampleRateFormat (in sharedMode, it equals usualy to the audioClient.MixFormat)
						// See documentation : http://msdn.microsoft.com/en-us/library/ms678737(VS.85).aspx 
						// They say : "In shared mode, the audio engine always supports the mix format"
						// The MixFormat is more likely to be a WaveFormatExtensible.
						//if (closestSampleRateFormat == null)
						//{
							InternalWaveFormat = GetFallbackFormat(waveProvider.WaveFormat);
						//}
						//else
						//{
						//    OutputWaveFormat = closestSampleRateFormat.ToStandardWaveFormat();
						//}
						dmoResamplerNeeded = false;
						var outWF = OutputWaveFormat;
						frameConverter = WasapiFrameConverter.SelectFrameConverter(waveProvider.WaveFormat, outWF);
						if (frameConverter == null && outWF.ToString() != waveProvider.WaveFormat.ToString())
						{
							try
							{
								// just check that we can make it.
								using (new ResamplerDmoStream(waveProvider, outWF, ResamplerDmoStream.MaxQuality))
								{
								}
							}
							catch (Exception)
							{
								// On Windows 10 some poorly coded drivers return a bad format in to closestSampleRateFormat
								// In that case, try and fallback as if it provided no closest (e.g. force trying the mix format)
								InternalWaveFormat = GetFallbackFormat(waveProvider.WaveFormat);
								using (new ResamplerDmoStream(waveProvider, outWF, ResamplerDmoStream.MaxQuality))
								{
								}
							}
							dmoResamplerNeeded = true;
						}
					}
					else
					{
						InternalWaveFormat = format;
						frameConverter = null;
						dmoResamplerNeeded = false;
					}
				}
			}

			// If using EventSync, setup is specific with shareMode
			if (isUsingEventSync)
			{
				if (audioClient.IsInitialized)
				{
					audioClient.Dispose();
					audioClient = mmDevice.AudioClient;
				}

				// Init Shared or Exclusive
				if (shareMode == AudioClientShareMode.Shared)
				{
					// With EventCallBack and Shared, both latencies must be set to 0 (update - not sure this is true anymore)
					// 
					audioClient.Initialize(shareMode, AudioClientStreamFlags.EventCallback | flags, latencyRefTimes, 0,
						InternalWaveFormat, Guid.Empty);

					// Windows 10 returns 0 from stream latency, resulting in maxing out CPU usage later
					var streamLatency = audioClient.StreamLatency;
					if (streamLatency != 0)
					{
						// Get back the effective latency from AudioClient
						latencyMilliseconds = (int)(streamLatency / 10000);
					}
				}
				else
				{
					try
					{
						// With EventCallBack and Exclusive, both latencies must equals
						audioClient.Initialize(shareMode, AudioClientStreamFlags.EventCallback | flags, latencyRefTimes, latencyRefTimes,
											InternalWaveFormat, Guid.Empty);
					}
					catch (COMException ex)
					{
						// Starting with Windows 7, Initialize can return AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED for a render device.
						// We should to initialize again.
						if (ex.ErrorCode != AudioClientErrorCode.BufferSizeNotAligned)
							throw;

						// Calculate the new latency.
						long newLatencyRefTimes = (long)(10000000.0 /
							(double)this.InternalWaveFormat.SampleRate *
							(double)this.audioClient.BufferSize + 0.5);

						this.audioClient.Dispose();
						this.audioClient = this.mmDevice.AudioClient;
						this.audioClient.Initialize(this.shareMode, AudioClientStreamFlags.EventCallback | flags,
											newLatencyRefTimes, newLatencyRefTimes, InternalWaveFormat, Guid.Empty);
					}
				}

				// Create the Wait Event Handle
				frameEventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
				audioClient.SetEventHandle(frameEventWaitHandle.SafeWaitHandle.DangerousGetHandle());
			}
			else if (prevOutputWaveFormat.ToString() != OutputWaveFormat.ToString() || !audioClient.IsInitialized)
            {
				if (audioClient.IsInitialized)
				{
					audioClient.Dispose();
					audioClient = mmDevice.AudioClient;
				}

                // Normal setup for both sharedMode
                audioClient.Initialize(shareMode, flags, latencyRefTimes, 0, InternalWaveFormat, Guid.Empty);
            }

			sourceProvider = waveProvider;
			sourceWaveFormat = waveProvider.WaveFormat;

			// Get the RenderClient
			renderClient = audioClient.AudioRenderClient;
        }

        /// <summary>
        /// Playback State
        /// </summary>
        public PlaybackState PlaybackState
        {
            get { return playbackState; }
        }

#endregion

#region IDisposable Members

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            if (audioClient != null)
            {
                Stop();

                audioClient.Dispose();
                audioClient = null;
                if (renderClient != null)
                {
                    renderClient.Dispose();
                    renderClient = null;
                }
            }
        }

#endregion
    }
}
