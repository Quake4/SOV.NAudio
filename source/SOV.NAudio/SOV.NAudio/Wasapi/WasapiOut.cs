﻿using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Utils;
using System;
using System.Collections.Generic;
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
        private WaveFormat sourceWaveFormat;
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
        public WasapiOut(MMDevice device, AudioClientShareMode shareMode, bool useEventSync, int latency)
        {
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
                readBuffer = BufferHelpers.Ensure(readBuffer, bufferFrameCount * bytesPerFrame);
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
            int read = playbackProvider.Read(readBuffer, 0, readLength);
            if (read == 0) return true;

            var buffer = renderClient.GetBuffer(frameCount);
			/*if (bytesPerFrame % 3 == 0) // 24bit need padding to 32
			{
				unsafe
				{
					byte* pByte = (byte*)buffer;
					fixed (byte* pSource = readBuffer)
					{
						var pPtr = pSource;
						var count = read / 8;// bytesPerFrame;
						while (count-- > 0)
						{
							*pByte++ = *(pPtr + 3);
							*pByte++ = *(pPtr + 2);
							*pByte++ = *(pPtr + 1);
							*pByte++ = *(pPtr + 0);
							//pPtr += 3;

							//*pByte++ = 0;
							*pByte++ = 0;
							*pByte++ = 0;
							*pByte++ = 0;

							pPtr += 8;
						}
					}
				}
			}
			else*/
				Marshal.Copy(readBuffer, 0, buffer, read);
            if (this.isUsingEventSync && this.shareMode == AudioClientShareMode.Exclusive)
            {
                if (read < readLength)
                {
                    // need to zero the end of the buffer as we have to
                    // pass frameCount
                    unsafe
                    {
                        byte* pByte = (byte*)buffer;
                        while(read < readLength)
                        {
                            pByte[read++] = 0;
                        }
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

        private WaveFormatExtensible GetFallbackFormat()
        {
            //var deviceSampleRate = audioClient.MixFormat.SampleRate;
            var deviceChannels = audioClient.MixFormat.Channels; // almost certain to be stereo

			// we are in exclusive mode
			// First priority is to try the sample rate you provided.
			var sampleRatesToTry = new List<int>();
			var sampleRatesToTryLower = new List<int>();
			// And if we've not already got 44.1 and 48kHz in the list, let's try them too
			var baseSampleRate = sourceWaveFormat.SampleRate % 44100 == 0 ? 44100 : 48000;
			for (int i = 1; i < (1 << 4); i = i << 1)
			{
				var sr = i * baseSampleRate;
				if (sr < sourceWaveFormat.SampleRate)
					sampleRatesToTryLower.Add(sr);
				else
					sampleRatesToTry.Add(sr);
			}
			// Add lower as reverse
			sampleRatesToTryLower.Reverse();
			sampleRatesToTry.AddRange(sampleRatesToTryLower);
			// Last priority is to use the sample rate the device wants
			//if (!sampleRatesToTry.Contains(deviceSampleRate)) sampleRatesToTry.Add(deviceSampleRate);

			var channelCountsToTry = new List<int>() { sourceWaveFormat.Channels };
            if (!channelCountsToTry.Contains(deviceChannels)) channelCountsToTry.Add(deviceChannels);
            if (!channelCountsToTry.Contains(2)) channelCountsToTry.Add(2);

            var bitDepthsToTry = new List<int>() { sourceWaveFormat.BitsPerSample };
            if (!bitDepthsToTry.Contains(32)) bitDepthsToTry.Add(32);
            if (!bitDepthsToTry.Contains(24)) bitDepthsToTry.Add(24);
            if (!bitDepthsToTry.Contains(16)) bitDepthsToTry.Add(16);

            foreach (var sampleRate in sampleRatesToTry)
            {
                foreach (var channelCount in channelCountsToTry)
                {
                    foreach (var bitDepth in bitDepthsToTry)
                    {
						var format = new WaveFormatExtensible(sampleRate, bitDepth, channelCount);
						if (audioClient.IsFormatSupported(shareMode, format))
							return format;
						// 24bit as 32bit
						if (bitDepth == 24 && (sourceWaveFormat.BitsPerSample == 24 || sourceWaveFormat.BitsPerSample == 32))
						{
							format = new WaveFormatExtensible(sampleRate, bitDepth, channelCount, 1);
							if (audioClient.IsFormatSupported(shareMode, format))
								return format;
						}
					}
				}
            }
            throw new NotSupportedException("Can't find a supported format to use");
        }

        /// <summary>
        /// Gets the current position in bytes from the wave output device.
        /// (n.b. this is not the same thing as the position within your reader
        /// stream)
        /// </summary>
        /// <returns>Position in bytes</returns>
        /*public long GetPosition()
        {
            ulong pos;
            switch (playbackState)
            {
                case PlaybackState.Stopped:
                    return 0;
                case PlaybackState.Playing:
                    pos = audioClient.AudioClockClient.AdjustedPosition;
                    break;
                default: // PlaybackState.Paused
                    audioClient.AudioClockClient.GetPosition(out pos, out _);
                    break;
            }
            return ((long)pos * OutputWaveFormat.AverageBytesPerSecond) / (long)audioClient.AudioClockClient.Frequency;
        }*/

		public WaveFormatExtensible InternalWaveFormat { get; protected set; }

		/// <summary>
		/// Gets a <see cref="Wave.WaveFormat"/> instance indicating the format the hardware is using.
		/// </summary>
		public WaveFormat OutputWaveFormat => InternalWaveFormat.ToStandardWaveFormat();

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
            if (sourceProvider != null && sourceWaveFormat == waveProvider.WaveFormat && sourceWaveFormat.ToString() == waveProvider.WaveFormat.ToString())
            {
                sourceProvider = waveProvider;
                return;
            }

            long latencyRefTimes = latencyMilliseconds * 10000L;
            WaveFormat prevOutputWaveFormat = InternalWaveFormat.ToStandardWaveFormat();
            //OutputWaveFormat = waveProvider.WaveFormat;

			//if (OutputWaveFormat.Encoding == WaveFormatEncoding.DSD)
			//	OutputWaveFormat = new WaveFormat(OutputWaveFormat.SampleRate / 16, 24, OutputWaveFormat.Channels);

            // allow auto sample rate conversion - works for shared mode
            var flags = AudioClientStreamFlags.AutoConvertPcm | AudioClientStreamFlags.SrcDefaultQuality;
            sourceProvider = waveProvider;
            sourceWaveFormat = waveProvider.WaveFormat;

            if (shareMode == AudioClientShareMode.Exclusive)
            {
                flags = AudioClientStreamFlags.None;
				var format = new WaveFormatExtensible(waveProvider.WaveFormat.SampleRate, waveProvider.WaveFormat.BitsPerSample,
					waveProvider.WaveFormat.Channels, float32: waveProvider.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat);
				if (!audioClient.IsFormatSupported(shareMode, format/*, out WaveFormatExtensible closestSampleRateFormat*/) ||
					format.ToStandardWaveFormat().Encoding != OutputWaveFormat.Encoding)
                {
                    // Use closesSampleRateFormat (in sharedMode, it equals usualy to the audioClient.MixFormat)
                    // See documentation : http://msdn.microsoft.com/en-us/library/ms678737(VS.85).aspx 
                    // They say : "In shared mode, the audio engine always supports the mix format"
                    // The MixFormat is more likely to be a WaveFormatExtensible.
                    //if (closestSampleRateFormat == null)
                    //{
                        InternalWaveFormat = GetFallbackFormat();
                    //}
                    //else
                    //{
                    //    OutputWaveFormat = closestSampleRateFormat.ToStandardWaveFormat();
                    //}

                    try
                    {
                        // just check that we can make it.
                        using (new ResamplerDmoStream(waveProvider, OutputWaveFormat, ResamplerDmoStream.MaxQuality))
                        {
                        }
                    }
                    catch (Exception)
                    {
						// On Windows 10 some poorly coded drivers return a bad format in to closestSampleRateFormat
						// In that case, try and fallback as if it provided no closest (e.g. force trying the mix format)
						InternalWaveFormat = GetFallbackFormat();
                        using (new ResamplerDmoStream(waveProvider, OutputWaveFormat, ResamplerDmoStream.MaxQuality))
                        {
                        }
                    }
                    dmoResamplerNeeded = true;
                }
                else
                {
                    dmoResamplerNeeded = false;
					InternalWaveFormat = format;
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
