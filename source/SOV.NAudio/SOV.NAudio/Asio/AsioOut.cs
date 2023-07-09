﻿using System;
using NAudio.Wave.Asio;
using System.Threading;

namespace NAudio.Wave
{
    /// <summary>
    /// ASIO Out Player. New implementation using an internal C# binding.
    /// 
    /// This implementation is only supporting Short16Bit and Float32Bit formats and is optimized 
    /// for 2 outputs channels .
    /// SampleRate is supported only if AsioDriver is supporting it
    ///     
    /// This implementation is probably the first AsioDriver binding fully implemented in C#!
    /// 
    /// Original Contributor: Mark Heath 
    /// New Contributor to C# binding : Alexandre Mutel - email: alexandre_mutel at yahoo.fr
    /// </summary>
    public class AsioOut : IWavePlayer
    {
        private AsioDriverExt driver;
        private IWaveProvider sourceStream;
        private WaveFormat sourceWaveFormat;
        private PlaybackState playbackState;
        private int nbSamples;
        private byte[] waveBuffer;
        private AsioSampleConvertor.SampleConvertor convertor;
        private string driverName;

        private readonly SynchronizationContext syncContext;
        private bool isInitialized;
		private bool isSendStop;

        /// <summary>
        /// Playback Stopped
        /// </summary>
        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        /// <summary>
        /// When recording, fires whenever recorded audio is available
        /// </summary>
        public event EventHandler<AsioAudioAvailableEventArgs> AudioAvailable;

        /// <summary>
        /// Occurs when the driver settings are changed by the user, e.g. in the control panel.
        /// </summary>
        public event EventHandler DriverResetRequest;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsioOut"/> class with the first 
        /// available ASIO Driver.
        /// </summary>
        public AsioOut()
            : this(0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsioOut"/> class with the driver name.
        /// </summary>
        /// <param name="driverName">Name of the device.</param>
        public AsioOut(string driverName)
        {
            syncContext = SynchronizationContext.Current;
            InitFromName(driverName);
        }

        /// <summary>
        /// Opens an ASIO output device
        /// </summary>
        /// <param name="driverIndex">Device number (zero based)</param>
        public AsioOut(int driverIndex)
        {
            syncContext = SynchronizationContext.Current; 
            String[] names = GetDriverNames();
            if (names.Length == 0)
            {
                throw new ArgumentException("There is no ASIO Driver installed on your system");
            }
            if (driverIndex < 0 || driverIndex > names.Length)
            {
                throw new ArgumentException(String.Format("Invalid device number. Must be in the range [0,{0}]", names.Length));
            }
            InitFromName(names[driverIndex]);
        }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// <see cref="AsioOut"/> is reclaimed by garbage collection.
        /// </summary>
        ~AsioOut()
        {
            Dispose();
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            if (driver != null)
            {
                if (playbackState != PlaybackState.Stopped)
                {
                    driver.Stop();
                }
                driver.ResetRequestCallback = null;
                driver.ReleaseDriver();
                driver = null;
            }
        }

        /// <summary>
        /// Gets the names of the installed ASIO Driver.
        /// </summary>
        /// <returns>an array of driver names</returns>
        public static string[] GetDriverNames()
        {
            return AsioDriver.GetAsioDriverNames();
        }

        /// <summary>
        /// Determines whether ASIO is supported.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if ASIO is supported; otherwise, <c>false</c>.
        /// </returns>
        public static bool isSupported()
        {
            return GetDriverNames().Length > 0;
        }

        /// <summary>
        /// Determines whether this driver supports the specified sample rate.
        /// </summary>
        /// <param name="sampleRate">The samplerate to check.</param>
        /// <returns>
        ///   <c>true</c> if the specified sample rate is supported otherwise, <c>false</c>.
        /// </returns>
        public bool IsSampleRateSupported(int sampleRate)
        {
            return driver.IsSampleRateSupported(sampleRate);
        }

        /// <summary>
        /// Inits the driver from the asio driver name.
        /// </summary>
        /// <param name="driverName">Name of the driver.</param>
        private void InitFromName(string driverName)
        {
            this.driverName = driverName;

            // Get the basic driver
            AsioDriver basicDriver = AsioDriver.GetAsioDriverByName(driverName);

            try
            {
                // Instantiate the extended driver
                driver = new AsioDriverExt(basicDriver);
            }
            catch
            {
                ReleaseDriver(basicDriver);
                throw;
            }
            driver.ResetRequestCallback = OnDriverResetRequest;
            this.ChannelOffset = 0;
        }



        private void OnDriverResetRequest()
        {
            DriverResetRequest?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Release driver
        /// </summary>
        private void ReleaseDriver(AsioDriver driver)
        {
            driver.DisposeBuffers();
            driver.ReleaseComAsioDriver();
        }

        /// <summary>
        /// Shows the control panel
        /// </summary>
        public void ShowControlPanel()
        {
            driver.ShowControlPanel();
        }

        /// <summary>
        /// Starts playback
        /// </summary>
        public void Play()
        {
            if (playbackState != PlaybackState.Playing)
            {
                playbackState = PlaybackState.Playing;
                driver.Start();
				isSendStop = false;
			}
        }

        /// <summary>
        /// Stops playback
        /// </summary>
        public void Stop()
        {
            playbackState = PlaybackState.Stopped;
			driver.Stop();
			isSendStop = false;
            RaisePlaybackStopped(null);
        }

        /// <summary>
        /// Pauses playback
        /// </summary>
        public void Pause()
        {
            playbackState = PlaybackState.Paused;
            driver.Stop();
        }

        /// <summary>
        /// Initialises to play
        /// </summary>
        /// <param name="waveProvider">Source wave provider</param>
        public void Init(IWaveProvider waveProvider)
        {
            InitRecordAndPlayback(waveProvider, 0, -1);
        }

        /// <summary>
        /// Initialises to play, with optional recording
        /// </summary>
        /// <param name="waveProvider">Source wave provider - set to null for record only</param>
        /// <param name="recordChannels">Number of channels to record</param>
        /// <param name="recordOnlySampleRate">Specify sample rate here if only recording, ignored otherwise</param>
        public void InitRecordAndPlayback(IWaveProvider waveProvider, int recordChannels, int recordOnlySampleRate)
        {
            if (waveProvider != null && isInitialized && waveProvider.WaveFormat.ToString() == sourceWaveFormat.ToString() && waveProvider.WaveFormat == sourceWaveFormat) {
                sourceStream = waveProvider;
                return;
            }

			var previousWaveFormat = sourceWaveFormat;
			var outChannels = NumberOfOutputChannels = waveProvider.WaveFormat.Channels;
			int desiredSampleRate = waveProvider != null ? waveProvider.WaveFormat.SampleRate : recordOnlySampleRate;
			int bitsPerSample = waveProvider.WaveFormat.BitsPerSample;
			if (!driver.IsSampleRateSupported(desiredSampleRate))
				throw new ArgumentException($"Desired SampleRate {desiredSampleRate} is not supported.");

			if (waveProvider != null)
            {
                sourceStream = waveProvider;
                sourceWaveFormat = waveProvider.WaveFormat;

				// check dsd native
				try
				{
					if (previousWaveFormat == null ||
						previousWaveFormat.Encoding == WaveFormatEncoding.DSD && waveProvider.WaveFormat.Encoding != WaveFormatEncoding.DSD ||
						previousWaveFormat.Encoding != WaveFormatEncoding.DSD && waveProvider.WaveFormat.Encoding == WaveFormatEncoding.DSD)
					{
						var format = new AsioIoFormat { FormatType = waveProvider.WaveFormat.BitsPerSample == 1 ? AsioIoFormatType.DSDFormat : AsioIoFormatType.PCMFormat };
						driver.Driver.Future((int)AsioFeature.kAsioSetIoFormat, ref format);
						driver.BuildCapabilities();
						if (isInitialized)
						{
							driver.DisposeBuffers();
							isInitialized = false;
						}
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"DSD Native isn't found!" + ex.Message);
					if (waveProvider.WaveFormat.Encoding == WaveFormatEncoding.DSD)
						desiredSampleRate = waveProvider.WaveFormat.SampleRate / 16;
				}

				// Select the correct sample convertor from WaveFormat -> ASIOFormat
				var asioSampleType = driver.Capabilities.OutputChannelInfos[0].type;
				outChannels = NumberOfOutputChannels > driver.Capabilities.NbOutputChannels ? driver.Capabilities.NbOutputChannels :
					NumberOfOutputChannels == 1 ? 2 : NumberOfOutputChannels;
				convertor = AsioSampleConvertor.SelectSampleConvertor(waveProvider.WaveFormat, asioSampleType);

				switch (asioSampleType)
				{
					case AsioSampleType.Float32LSB:
						OutputWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(desiredSampleRate, outChannels);
						break;
					case AsioSampleType.Int32LSB:
						OutputWaveFormat = new WaveFormat(desiredSampleRate, 32, outChannels);
						if (waveProvider.WaveFormat.Encoding == WaveFormatEncoding.DSD)
						{
							bitsPerSample = 16;
							OutputWaveFormat = WaveFormat.CreateCustomFormat(WaveFormatEncoding.DoP,
								OutputWaveFormat.SampleRate, outChannels, OutputWaveFormat.AverageBytesPerSecond, OutputWaveFormat.BlockAlign, OutputWaveFormat.BitsPerSample);
						}
                        break;
                    case AsioSampleType.Int16LSB:
						OutputWaveFormat = new WaveFormat(desiredSampleRate, 16, outChannels);
                        break;
                    case AsioSampleType.Int24LSB:
						OutputWaveFormat = new WaveFormat(desiredSampleRate, 24, outChannels);
						if (waveProvider.WaveFormat.Encoding == WaveFormatEncoding.DSD)
						{
							bitsPerSample = 16;
							OutputWaveFormat = WaveFormat.CreateCustomFormat(WaveFormatEncoding.DoP,
								OutputWaveFormat.SampleRate, outChannels, OutputWaveFormat.AverageBytesPerSecond, OutputWaveFormat.BlockAlign, OutputWaveFormat.BitsPerSample);
						}
						break;
					case AsioSampleType.DSDInt8MSB1:
						OutputWaveFormat = WaveFormat.CreateCustomFormat(waveProvider.WaveFormat.Encoding, desiredSampleRate, outChannels,
							waveProvider.WaveFormat.SampleRate * outChannels * waveProvider.WaveFormat.BitsPerSample / 8, waveProvider.WaveFormat.BlockAlign, waveProvider.WaveFormat.BitsPerSample);
						break;
					default:
                        throw new NotSupportedException($"{asioSampleType} not currently supported");
                }
            }
            else
                NumberOfOutputChannels = 0;

            if (driver.Capabilities.SampleRate != desiredSampleRate || !isInitialized)
            {
				if (isInitialized)
				{
					driver.DisposeBuffers();
					isInitialized = false;
				}
				driver.SetSampleRate(desiredSampleRate);
            }

            if (!isInitialized)
            {
                // Plug the callback
                driver.FillBufferCallback = driver_BufferUpdate;
                NumberOfInputChannels = recordChannels;

                // Used Prefered size of ASIO Buffer
                nbSamples = driver.CreateBuffers(outChannels, NumberOfInputChannels, false);
                driver.SetChannelOffset(ChannelOffset, InputChannelOffset); // will throw an exception if channel offset is too high
            }

            if (waveProvider != null)
                // make a buffer big enough to read enough from the sourceStream to fill the ASIO buffers
                waveBuffer = new byte[nbSamples * NumberOfOutputChannels * bitsPerSample / 8];

            isInitialized = true;
        }

        /// <summary>
        /// driver buffer update callback to fill the wave buffer.
        /// </summary>
        /// <param name="inputChannels">The input channels.</param>
        /// <param name="outputChannels">The output channels.</param>
        void driver_BufferUpdate(IntPtr[] inputChannels, IntPtr[] outputChannels)
        {
            if (this.NumberOfInputChannels > 0)
            {
                var audioAvailable = AudioAvailable;
                if (audioAvailable != null)
                {
                    var args = new AsioAudioAvailableEventArgs(inputChannels, outputChannels, nbSamples,
                                                               driver.Capabilities.InputChannelInfos[0].type);
                    audioAvailable(this, args);
                    if (args.WrittenToOutputBuffers)
                        return;
                }
            }

            if (this.NumberOfOutputChannels > 0)
            {
                int read = sourceStream.Read(waveBuffer, 0, waveBuffer.Length);
                if (read < waveBuffer.Length)
                {
					// we have reached the end of the input data - clear out the end
					if (OutputWaveFormat.Encoding == WaveFormatEncoding.DSD)
						for (var i = read; i < waveBuffer.Length; i++)
							waveBuffer[i] = (byte)0x69;
					else
						Array.Clear(waveBuffer, read, waveBuffer.Length - read);
                }

                // Call the convertor
                unsafe
                {
                    // TODO : check if it's better to lock the buffer at initialization?
                    fixed (void* pBuffer = &waveBuffer[0])
                    {
                        convertor(new IntPtr(pBuffer), outputChannels, NumberOfOutputChannels, nbSamples);
                    }
                }
				
                if (read == 0 && !isSendStop)
                {
					isSendStop = true;
					if (syncContext != null)
						syncContext.Post(s => Stop(), null);
					else
					{
						var thread = new Thread(() => Stop());
                        thread.SetApartmentState(ApartmentState.STA);
                        thread.Start();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the latency (in samples) of the playback driver
        /// </summary>
        public int PlaybackLatency
        {
            get
            {
                int latency, temp;
                driver.Driver.GetLatencies(out temp, out latency);
                return latency;
            }
        }

        /// <summary>
        /// Playback State
        /// </summary>
        public PlaybackState PlaybackState => playbackState;

        /// <summary>
        /// Driver Name
        /// </summary>
        public string DriverName => this.driverName;

        /// <summary>
        /// The number of output channels we are currently using for playback
        /// (Must be less than or equal to DriverOutputChannelCount)
        /// </summary>
        public int NumberOfOutputChannels { get; private set; }

        /// <summary>
        /// The number of input channels we are currently recording from
        /// (Must be less than or equal to DriverInputChannelCount)
        /// </summary>
        public int NumberOfInputChannels { get; private set; }

        /// <summary>
        /// The maximum number of input channels this ASIO driver supports
        /// </summary>
        public int DriverInputChannelCount => driver.Capabilities.NbInputChannels;

        /// <summary>
        /// The maximum number of output channels this ASIO driver supports
        /// </summary>
        public int DriverOutputChannelCount => driver.Capabilities.NbOutputChannels;

        /// <summary>
        /// The number of samples per channel, per buffer.
        /// </summary>
        public int FramesPerBuffer
        {
            get
            {
                if (!isInitialized)
                    throw new Exception("Not initialized yet. Call this after calling Init");

                return nbSamples;
            }
        }

        /// <summary>
        /// By default the first channel on the input WaveProvider is sent to the first ASIO output.
        /// This option sends it to the specified channel number.
        /// Warning: make sure you don't set it higher than the number of available output channels - 
        /// the number of source channels.
        /// n.b. Future NAudio may modify this
        /// </summary>
        public int ChannelOffset { get; set; }

        /// <summary>
        /// Input channel offset (used when recording), allowing you to choose to record from just one
        /// specific input rather than them all
        /// </summary>
        public int InputChannelOffset { get; set; }

        /// <inheritdoc/>
        public WaveFormat OutputWaveFormat { get; private set; }

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
        /// Get the input channel name
        /// </summary>
        /// <param name="channel">channel index (zero based)</param>
        /// <returns>channel name</returns>
        public string AsioInputChannelName(int channel)
        {
            return channel > DriverInputChannelCount ? "" : driver.Capabilities.InputChannelInfos[channel].name;
        }

        /// <summary>
        /// Get the output channel name
        /// </summary>
        /// <param name="channel">channel index (zero based)</param>
        /// <returns>channel name</returns>
        public string AsioOutputChannelName(int channel)
        {
            return channel > DriverOutputChannelCount ? "" : driver.Capabilities.OutputChannelInfos[channel].name;
        }
    }
}