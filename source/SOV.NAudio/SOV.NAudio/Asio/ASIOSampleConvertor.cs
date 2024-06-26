﻿using System;
using System.Runtime.CompilerServices;

namespace NAudio.Wave.Asio
{
    /// <summary>
    /// This class stores convertors for different interleaved WaveFormat to ASIOSampleType separate channel
    /// format.
    /// </summary>
    internal class AsioSampleConvertor
    {
        public delegate void SampleConvertor(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples);

        /// <summary>
        /// Selects the sample convertor based on the input WaveFormat and the output ASIOSampleTtype.
        /// </summary>
        /// <param name="waveFormat">The wave format.</param>
        /// <param name="asioType">The type.</param>
        /// <returns></returns>
        public static SampleConvertor SelectSampleConvertor(WaveFormat waveFormat, AsioSampleType asioType)
        {
            SampleConvertor convertor = null;
            bool is2Channels = waveFormat.Channels == 2;

			var exception = $"Not a supported conversion {asioType} for {waveFormat}.";

			// TODO : IMPLEMENTS OTHER CONVERTOR TYPES
			switch (asioType)
			{
				case AsioSampleType.DSDInt8MSB1:
					switch (waveFormat.BitsPerSample)
					{
						case 1:
							convertor = ConvertorDsdToByteGeneric;
							break;
					}
					break;
				case AsioSampleType.Int32LSB:
                    switch (waveFormat.BitsPerSample)
                    {
						case 1:
							convertor = ConvertorDsdToDop32;
							break;
						case 16:
                            convertor = ConvertorShortToIntGeneric;
                            break;
						case 24:
                            convertor = Convertor24ToIntGeneric;
                            break;
                        case 32:
                            if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                                convertor = (is2Channels) ? (SampleConvertor)ConvertorFloatToInt2Channels : (SampleConvertor)ConvertorFloatToIntGeneric;
                            else
                                convertor = (is2Channels) ? (SampleConvertor)ConvertorIntToInt2Channels : (SampleConvertor)ConvertorIntToIntGeneric;
                            break;
                    }
                    break;
                case AsioSampleType.Int16LSB:
                    switch (waveFormat.BitsPerSample)
                    {
                        case 16:
                            convertor = (is2Channels) ? (SampleConvertor)ConvertorShortToShort2Channels : (SampleConvertor)ConvertorShortToShortGeneric;
                            break;
                        case 32:
                            if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                                convertor = (is2Channels) ? (SampleConvertor)ConvertorFloatToShort2Channels : (SampleConvertor)ConvertorFloatToShortGeneric;
                            else
                                convertor = (is2Channels) ? (SampleConvertor)ConvertorIntToShort2Channels : (SampleConvertor)ConvertorIntToShortGeneric;
                            break;
                    }
                    break;
                case AsioSampleType.Int24LSB:
                    switch (waveFormat.BitsPerSample)
                    {
						case 1:
							convertor = ConvertorDsdToDop24;
							break;
						case 16:
							convertor = ConvertorShortTo24Generic;
							break;
						case 24:
							convertor = Convertor24To24Generic;
							break;
						case 32:
                            if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                                convertor = ConverterFloatTo24LSBGeneric;
                            else
								convertor = ConvertorIntTo24Generic;
                            break;
                    }
                    break;
                case AsioSampleType.Float32LSB:
                    switch (waveFormat.BitsPerSample)
                    {
                        case 16:
                            throw new ArgumentException(exception);
                        case 32:
                            if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                                convertor = ConverterFloatToFloatGeneric;
                            else
                                convertor = ConvertorIntToFloatGeneric;
                            break;
                    }
                    break;

                default:
                    throw new ArgumentException(
                        String.Format("ASIO Buffer Type {0} is not yet supported.",
                                      Enum.GetName(typeof(AsioSampleType), asioType)));
            }

            if (convertor is null)
                throw new ArgumentException($"Converter {asioType} not found for {waveFormat}.");

            return convertor;
        }

		public static void ConvertorDsdToDop24(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
		{
			// to stereo
			if (asioOutputBuffers.Length == 2)
			{
				unsafe
				{
					byte* inputSamples = (byte*)inputInterleavedBuffer;
					byte* leftSamples = (byte*)asioOutputBuffers[0];
					byte* rightSamples = (byte*)asioOutputBuffers[1];

					for (int i = 0; i < nbSamples / 2; i++)
					{
						*leftSamples++ = inputSamples[0 + nbChannels * 1];
						*leftSamples++ = inputSamples[0 + nbChannels * 0];
						*leftSamples++ = 0x05;

						*rightSamples++ = inputSamples[1 + nbChannels * 1];
						*rightSamples++ = inputSamples[1 + nbChannels * 0];
						*rightSamples++ = 0x05;

						*leftSamples++ = inputSamples[0 + nbChannels * 3];
						*leftSamples++ = inputSamples[0 + nbChannels * 2];
						*leftSamples++ = 0xFA;

						*rightSamples++ = inputSamples[1 + nbChannels * 3];
						*rightSamples++ = inputSamples[1 + nbChannels * 2];
						*rightSamples++ = 0xFA;

						// Go to next sample
						inputSamples += 4 * nbChannels;
					}
				}
			}
		}

		public static void ConvertorDsdToDop32(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
		{
			// to stereo
			if (asioOutputBuffers.Length == 2)
			{
				unsafe
				{
					byte* inputSamples = (byte*)inputInterleavedBuffer;
					byte* leftSamples = (byte*)asioOutputBuffers[0];
					byte* rightSamples = (byte*)asioOutputBuffers[1];

					for (int i = 0; i < nbSamples / 2; i++)
					{
						*leftSamples++ = 0x69;
						*leftSamples++ = inputSamples[0 + nbChannels * 1];
						*leftSamples++ = inputSamples[0 + nbChannels * 0];
						*leftSamples++ = 0x05;

						*rightSamples++ = 0x69;
						*rightSamples++ = inputSamples[1 + nbChannels * 1];
						*rightSamples++ = inputSamples[1 + nbChannels * 0];
						*rightSamples++ = 0x05;

						*leftSamples++ = 0x69;
						*leftSamples++ = inputSamples[0 + nbChannels * 3];
						*leftSamples++ = inputSamples[0 + nbChannels * 2];
						*leftSamples++ = 0xFA;

						*rightSamples++ = 0x69;
						*rightSamples++ = inputSamples[1 + nbChannels * 3];
						*rightSamples++ = inputSamples[1 + nbChannels * 2];
						*rightSamples++ = 0xFA;

						// Go to next sample
						inputSamples += 4 * nbChannels;
					}
				}
			}
		}

		public static void ConvertorDsdToByteGeneric(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
		{
			// to stereo
			if (asioOutputBuffers.Length == 2)
			{
				unsafe
				{
					byte* inputSamples = (byte*)inputInterleavedBuffer;
					byte* leftSamples = (byte*)asioOutputBuffers[0];
					byte* rightSamples = (byte*)asioOutputBuffers[1];

					var samples = nbSamples / 8;
					while (samples-- > 0)
					{
						*leftSamples++ = inputSamples[0];
						*rightSamples++ = inputSamples[1];

						// Go to next sample
						inputSamples += nbChannels;
					}
				}
			}
		}

		public static void Convertor24ToIntGeneric(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
			unsafe
			{
				int channels = asioOutputBuffers.Length;
				byte* inputSamples = (byte*)inputInterleavedBuffer;
				int*[] samples = new int*[channels];
				for (int i = 0; i < channels; i++)
					samples[i] = (int*)asioOutputBuffers[i];

				// optimized mono to stereo
				if (nbChannels == 1 && channels >= 2)
					for (int i = 0; i < nbSamples; i++)
					{
						int value = (*inputSamples++ << 8) | (*inputSamples++ << 16) | (*inputSamples++ << 24);
						*samples[0]++ = value;
						*samples[1]++ = value;
					}
				// optimized stereo to stereo
				else if (nbChannels == 2 && channels == 2)
					for (int i = 0; i < nbSamples; i++)
					{
						*samples[0]++ = (*inputSamples++ << 8) | (*inputSamples++ << 16) | (*inputSamples++ << 24);
						*samples[1]++ = (*inputSamples++ << 8) | (*inputSamples++ << 16) | (*inputSamples++ << 24);
					}
				// generic
				else
					for (int i = 0; i < nbSamples; i++)
						for (int j = 0; j < Math.Max(nbChannels, channels); j++)
						{
							if (j < Math.Min(nbChannels, channels))
								*samples[j]++ = (*inputSamples++ << 8) | (*inputSamples++ << 16) | (*inputSamples++ << 24);
							else if (j >= channels)
								inputSamples += 3;
							if (j >= nbChannels)
								*samples[j]++ = 0;
						}
			}
        }

		public static void ConvertorShortTo24Generic(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
		{
			unsafe
			{
				int channels = asioOutputBuffers.Length;
				byte* inputSamples = (byte*)inputInterleavedBuffer;
				byte*[] samples = new byte*[channels];
				for (int i = 0; i < channels; i++)
					samples[i] = (byte*)asioOutputBuffers[i];

				byte value;
				// optimized mono to stereo
				if (nbChannels == 1 && channels >= 2)
					for (int i = 0; i < nbSamples; i++)
					{
						*samples[0]++ = 0;
						*samples[1]++ = 0;

						value = *inputSamples++;
						*samples[0]++ = value;
						*samples[1]++ = value;

						value = *inputSamples++;
						*samples[0]++ = value;
						*samples[1]++ = value;
					}
				// optimized stereo to stereo
				else if (nbChannels == 2 && channels == 2)
					for (int i = 0; i < nbSamples; i++)
					{
						*samples[0]++ = 0;
						*samples[0]++ = *inputSamples++;
						*samples[0]++ = *inputSamples++;

						*samples[1]++ = 0;
						*samples[1]++ = *inputSamples++;
						*samples[1]++ = *inputSamples++;
					}
				// generic
				else
					for (int i = 0; i < nbSamples; i++)
						for (int j = 0; j < Math.Max(nbChannels, channels); j++)
						{
							if (j < Math.Min(nbChannels, channels))
							{
								*samples[j]++ = 0;
								*samples[j]++ = *inputSamples++;
								*samples[j]++ = *inputSamples++;
							}
							else if (j >= channels)
								inputSamples += 2;
							if (j >= nbChannels)
							{
								*samples[j]++ = 0;
								*samples[j]++ = 0;
								*samples[j]++ = 0;
							}
						}
			}
		}

		public static void Convertor24To24Generic(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
		{
			unsafe
			{
				int channels = asioOutputBuffers.Length;
				byte* inputSamples = (byte*)inputInterleavedBuffer;
				byte*[] samples = new byte*[channels];
				for (int i = 0; i < channels; i++)
					samples[i] = (byte*)asioOutputBuffers[i];

				byte value;
				// optimized mono to stereo
				if (nbChannels == 1 && channels >= 2)
					for (int i = 0; i < nbSamples; i++)
					{
						value = *inputSamples++;
						*samples[0]++ = value;
						*samples[1]++ = value;

						value = *inputSamples++;
						*samples[0]++ = value;
						*samples[1]++ = value;

						value = *inputSamples++;
						*samples[0]++ = value;
						*samples[1]++ = value;
					}
				// optimized stereo to stereo
				else if (nbChannels == 2 && channels == 2)
					for (int i = 0; i < nbSamples; i++)
					{
						*samples[0]++ = *inputSamples++;
						*samples[0]++ = *inputSamples++;
						*samples[0]++ = *inputSamples++;

						*samples[1]++ = *inputSamples++;
						*samples[1]++ = *inputSamples++;
						*samples[1]++ = *inputSamples++;
					}
				// generic
				else
					for (int i = 0; i < nbSamples; i++)
						for (int j = 0; j < Math.Max(nbChannels, channels); j++)
						{
							if (j < Math.Min(nbChannels, channels))
							{
								*samples[j]++ = *inputSamples++;
								*samples[j]++ = *inputSamples++;
								*samples[j]++ = *inputSamples++;
							}
							else if (j >= channels)
								inputSamples += 3;
							if (j >= nbChannels)
							{
								*samples[j]++ = 0;
								*samples[j]++ = 0;
								*samples[j]++ = 0;
							}
						}
			}
		}

		public static void ConvertorIntTo24Generic(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
		{
			unsafe
			{
				int channels = asioOutputBuffers.Length;
				int* inputSamples = (int*)inputInterleavedBuffer;
				byte*[] samples = new byte*[channels];
				for (int i = 0; i < channels; i++)
					samples[i] = (byte*)asioOutputBuffers[i];

				int value;
				// optimized mono to stereo
				if (nbChannels == 1 && channels >= 2)
				{
					byte byteValue;
					for (int i = 0; i < nbSamples; i++)
					{
						value = *inputSamples++ >> 8;

						byteValue = (byte)(value);
						*samples[0]++ = byteValue;
						*samples[1]++ = byteValue;

						byteValue = (byte)(value >> 8);
						*samples[0]++ = byteValue;
						*samples[1]++ = byteValue;

						byteValue = (byte)(value >> 16);
						*samples[0]++ = byteValue;
						*samples[1]++ = byteValue;
					}
				}
				// optimized stereo to stereo
				else if (nbChannels == 2 && channels == 2)
					for (int i = 0; i < nbSamples; i++)
					{
						value = *inputSamples++ >> 8;

						*samples[0]++ = (byte)(value);
						*samples[0]++ = (byte)(value >> 8);
						*samples[0]++ = (byte)(value >> 16);

						value = *inputSamples++ >> 8;

						*samples[1]++ = (byte)(value);
						*samples[1]++ = (byte)(value >> 8);
						*samples[1]++ = (byte)(value >> 16);
					}
				// generic
				else
					for (int i = 0; i < nbSamples; i++)
						for (int j = 0; j < Math.Max(nbChannels, channels); j++)
						{
							if (j < Math.Min(nbChannels, channels))
							{
								value = *inputSamples++ >> 8;

								*samples[j]++ = (byte)(value);
								*samples[j]++ = (byte)(value >> 8);
								*samples[j]++ = (byte)(value >> 16);
							}
							else if (j >= channels)
								inputSamples++;
							if (j >= nbChannels)
							{
								*samples[j]++ = 0;
								*samples[j]++ = 0;
								*samples[j]++ = 0;
							}
						}
			}
		}


		/// <summary>
		/// Generic convertor for SHORT
		/// </summary>
		public static void ConvertorShortToIntGeneric(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
				int channels = asioOutputBuffers.Length;
				short* inputSamples = (short*)inputInterleavedBuffer;
				// Use a trick (short instead of int to avoid any convertion from 16Bit to 32Bit)
                short*[] samples = new short*[channels];
                for (int i = 0; i < channels; i++)
                {
                    samples[i] = (short*)asioOutputBuffers[i];
                    // Point to upper 16 bits of the 32Bits.
                    samples[i]++;
                }

				short value;
				// optimized mono to stereo
				if (nbChannels == 1 && channels >= 2)
					for (int i = 0; i < nbSamples; i++)
					{
						value = *inputSamples++;
						*samples[0] = value;
						samples[0] += 2;
						*samples[1] = value;
						samples[1] += 2;
					}
				// optimized stereo to stereo
				else if (nbChannels == 2 && channels == 2)
					for (int i = 0; i < nbSamples; i++)
					{
						*samples[0] = *inputSamples++;
						samples[0] += 2;
						*samples[1] = *inputSamples++;
						samples[1] += 2;
					}
				// generic
				else
					for (int i = 0; i < nbSamples; i++)
						for (int j = 0; j < nbChannels; j++)
						{
							value = *inputSamples++;
							if (j < channels)
							{
								*samples[j] = value;
								samples[j] += 2;
							}
						}
            }
        }

        /// <summary>
        /// Optimized convertor for 2 channels FLOAT
        /// </summary>
        public static void ConvertorFloatToInt2Channels(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                float* inputSamples = (float*)inputInterleavedBuffer;
                int* leftSamples = (int*)asioOutputBuffers[0];
                int* rightSamples = (int*)asioOutputBuffers[1];

                for (int i = 0; i < nbSamples; i++)
                {
                    *leftSamples++ = clampToInt(inputSamples[0]);
                    *rightSamples++ = clampToInt(inputSamples[1]);
                    inputSamples += 2;
                }
            }
        }

        /// <summary>
        /// Generic convertor Float to INT
        /// </summary>
        public static void ConvertorFloatToIntGeneric(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                float* inputSamples = (float*)inputInterleavedBuffer;
                int*[] samples = new int*[nbChannels];
                for (int i = 0; i < nbChannels; i++)
                {
                    samples[i] = (int*)asioOutputBuffers[i];
                }

                for (int i = 0; i < nbSamples; i++)
                {
                    for (int j = 0; j < nbChannels; j++)
                    {
                        *samples[j]++ = clampToInt(*inputSamples++);
                    }
                }
            }
        }

        /// <summary>
        /// Optimized convertor for 2 channels INT to INT
        /// </summary>
        public static void ConvertorIntToInt2Channels(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                int* inputSamples = (int*)inputInterleavedBuffer;
                int* leftSamples = (int*)asioOutputBuffers[0];
                int* rightSamples = (int*)asioOutputBuffers[1];

                for (int i = 0; i < nbSamples; i++)
                {
                    *leftSamples++ = inputSamples[0];
                    *rightSamples++ = inputSamples[1];
                    inputSamples += 2;
                }
            }
        }

        /// <summary>
        /// Generic convertor INT to INT
        /// </summary>
        public static void ConvertorIntToIntGeneric(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                int* inputSamples = (int*)inputInterleavedBuffer;
                int*[] samples = new int*[nbChannels];
                for (int i = 0; i < nbChannels; i++)
                {
                    samples[i] = (int*)asioOutputBuffers[i];
                }

                for (int i = 0; i < nbSamples; i++)
                {
                    for (int j = 0; j < nbChannels; j++)
                    {
                        *samples[j]++ = *inputSamples++;
                    }
                }
            }
        }

        /// <summary>
        /// Optimized convertor for 2 channels INT to SHORT
        /// </summary>
        public static void ConvertorIntToShort2Channels(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                int* inputSamples = (int*)inputInterleavedBuffer;
                short* leftSamples = (short*)asioOutputBuffers[0];
                short* rightSamples = (short*)asioOutputBuffers[1];

                for (int i = 0; i < nbSamples; i++)
                {
                    *leftSamples++ = (short)(*inputSamples++ >> 16);
                    *rightSamples++ = (short)(*inputSamples++ >> 16);
                }
            }
        }

        /// <summary>
        /// Generic convertor INT to SHORT
        /// </summary>
        public static void ConvertorIntToShortGeneric(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                int* inputSamples = (int*)inputInterleavedBuffer;
                int*[] samples = new int*[nbChannels];
                for (int i = 0; i < nbChannels; i++)
                {
                    samples[i] = (int*)asioOutputBuffers[i];
                }

                for (int i = 0; i < nbSamples; i++)
                {
                    for (int j = 0; j < nbChannels; j++)
                    {
                        *samples[j]++ = *inputSamples++ >> 16;
                    }
                }
            }
        }

        /// <summary>
        /// Generic convertor INT to FLOAT
        /// </summary>
        public static void ConvertorIntToFloatGeneric(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                int* inputSamples = (int*)inputInterleavedBuffer;
                float*[] samples = new float*[nbChannels];
                for (int i = 0; i < nbChannels; i++)
                {
                    samples[i] = (float*)asioOutputBuffers[i];
                }

                for (int i = 0; i < nbSamples; i++)
                {
                    for (int j = 0; j < nbChannels; j++)
                    {
                        *samples[j]++ = *inputSamples++ / (1 << (32 - 1));
                    }
                }
            }
        }

        /// <summary>
        /// Optimized convertor for 2 channels SHORT
        /// </summary>
        public static void ConvertorShortToShort2Channels(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                short* inputSamples = (short*)inputInterleavedBuffer;
                // Use a trick (short instead of int to avoid any convertion from 16Bit to 32Bit)
                short* leftSamples = (short*)asioOutputBuffers[0];
                short* rightSamples = (short*)asioOutputBuffers[1];

                // Point to upper 16 bits of the 32Bits.
                for (int i = 0; i < nbSamples; i++)
                {
                    *leftSamples++ = inputSamples[0];
                    *rightSamples++ = inputSamples[1];
                    // Go to next sample
                    inputSamples += 2;
                }
            }
        }

        /// <summary>
        /// Generic convertor for SHORT
        /// </summary>
        public static void ConvertorShortToShortGeneric(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                short* inputSamples = (short*)inputInterleavedBuffer;
                // Use a trick (short instead of int to avoid any convertion from 16Bit to 32Bit)
                short*[] samples = new short*[nbChannels];
                for (int i = 0; i < nbChannels; i++)
                {
                    samples[i] = (short*)asioOutputBuffers[i];
                }

                for (int i = 0; i < nbSamples; i++)
                {
                    for (int j = 0; j < nbChannels; j++)
                    {
                        *(samples[j]++) = *inputSamples++;
                    }
                }
            }
        }

        /// <summary>
        /// Optimized convertor for 2 channels FLOAT
        /// </summary>
        public static void ConvertorFloatToShort2Channels(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                float* inputSamples = (float*)inputInterleavedBuffer;
                // Use a trick (short instead of int to avoid any convertion from 16Bit to 32Bit)
                short* leftSamples = (short*)asioOutputBuffers[0];
                short* rightSamples = (short*)asioOutputBuffers[1];

                for (int i = 0; i < nbSamples; i++)
                {
                    *leftSamples++ = clampToShort(inputSamples[0]);
                    *rightSamples++ = clampToShort(inputSamples[1]);
                    inputSamples += 2;
                }
            }
        }

        /// <summary>
        /// Generic convertor SHORT
        /// </summary>
        public static void ConvertorFloatToShortGeneric(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                float* inputSamples = (float*)inputInterleavedBuffer;
                // Use a trick (short instead of int to avoid any convertion from 16Bit to 32Bit)
                short*[] samples = new short*[nbChannels];
                for (int i = 0; i < nbChannels; i++)
                {
                    samples[i] = (short*)asioOutputBuffers[i];
                }

                for (int i = 0; i < nbSamples; i++)
                {
                    for (int j = 0; j < nbChannels; j++)
                    {
                        *(samples[j]++) = clampToShort(*inputSamples++);
                    }
                }
            }
        }

        /// <summary>
        /// Generic converter 24 LSB
        /// </summary>
        public static void ConverterFloatTo24LSBGeneric(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                float* inputSamples = (float*)inputInterleavedBuffer;
                
                byte*[] samples = new byte*[nbChannels];
                for (int i = 0; i < nbChannels; i++)
                {
                    samples[i] = (byte*)asioOutputBuffers[i];
                }

                for (int i = 0; i < nbSamples; i++)
                {
                    for (int j = 0; j < nbChannels; j++)
                    {
                        int sample24 = clampTo24Bit(*inputSamples++);
                        *(samples[j]++) = (byte)(sample24);
                        *(samples[j]++) = (byte)(sample24 >> 8);
                        *(samples[j]++) = (byte)(sample24 >> 16);
                    }
                }
            }
        }

        /// <summary>
        /// Generic convertor for float
        /// </summary>
        public static void ConverterFloatToFloatGeneric(IntPtr inputInterleavedBuffer, IntPtr[] asioOutputBuffers, int nbChannels, int nbSamples)
        {
            unsafe
            {
                float* inputSamples = (float*)inputInterleavedBuffer;
                float*[] samples = new float*[nbChannels];
                for (int i = 0; i < nbChannels; i++)
                {
                    samples[i] = (float*)asioOutputBuffers[i];
                }

                for (int i = 0; i < nbSamples; i++)
                {
                    for (int j = 0; j < nbChannels; j++)
                    {
                        *(samples[j]++) = *inputSamples++;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int clampTo24Bit(double sampleValue)
        {
            sampleValue = (sampleValue < -1.0) ? -1.0 : (sampleValue > 1.0) ? 1.0 : sampleValue;
            return (int)(sampleValue * 8388607.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int clampToInt(double sampleValue)
        {
            sampleValue = (sampleValue < -1.0) ? -1.0 : (sampleValue > 1.0) ? 1.0 : sampleValue;
            return (int)(sampleValue * 2147483647.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short clampToShort(double sampleValue)
        {
            sampleValue = (sampleValue < -1.0) ? -1.0 : (sampleValue > 1.0) ? 1.0 : sampleValue;
            return (short)(sampleValue * 32767.0);
        }
	}
}