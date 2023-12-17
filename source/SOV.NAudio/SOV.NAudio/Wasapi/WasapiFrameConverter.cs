/*

Copyright © 2023 Oleg Samsonov aka Quake4. All rights reserved.
https://github.com/Quake4/SOV.NAudio

This Source Code Form is subject to the terms of the Mozilla
Public License, v. 2.0. If a copy of the MPL was not distributed
with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

*/
using NAudio.Wave.Asio;
using System;

namespace NAudio.Wave
{
	internal static class WasapiFrameConverter
	{
		public delegate void FrameConverter(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames);

		public static FrameConverter SelectFrameConverter(WaveFormat input, WaveFormat output)
		{
			if (input.SampleRate != output.SampleRate)
				return null;

			if (output.Encoding == WaveFormatEncoding.Pcm)
			{
				switch (input.Encoding)
				{
					case WaveFormatEncoding.Pcm:
						switch (input.BitsPerSample)
						{
							case 16:
								switch (output.BitsPerSample)
								{
									case 16:
										return Converter16To16Generic;
									case 24:
										//convertor = Converter16To24Generic;
										break;
									case 32:
										return Converter16To32Generic;
									default:
										break;
								}
								break;
							case 24:
								switch (output.BitsPerSample)
								{
									case 16:
										return Converter24To16Generic;
									case 32:
										return Converter24To32Generic;
									default:
										break;
								}
								break;
							case 32:
								switch (output.BitsPerSample)
								{
									case 16:
										return Converter32To16Generic;
									case 24:
										//convertor = Converter32To24Generic;
										break;
									case 32:
										return Converter32To32Generic;
									default:
										break;
								}
								break;
							default:
								break;
						}
						break;

					case WaveFormatEncoding.IeeeFloat:
						switch (output.BitsPerSample)
						{
							case 16:
								return ConverterFloatTo16Generic;
							case 24:
								//convertor = ConverterFloatTo24Generic;
								break;
							case 32:
								return ConverterFloatTo32Generic;
							default:
								break;
						}
						break;

					default:
						break;
				}
			}

			return null;
		}

		#region PCM

		internal static void Converter16To16Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
				short* input = (short*)inputInterleavedBuffer;
				short* output = (short*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						var value = *input++;
						output[0] = value;
						output[1] = value;
						output += outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						*output++ = *input++;
						*output++ = *input++;
					}
				// generic
				else
				{
					var max = Math.Max(inputChannels, outputChannels);
					var min = Math.Min(inputChannels, outputChannels);
					for (int i = 0; i < frames; i++)
						for (int j = 0; j < max; j++)
						{
							if (j < min)
								*output++ = *input++;
							else if (j >= outputChannels)
								input++;
							if (j >= inputChannels)
								*output++ = 0;
						}
				}
			}
		}

		internal static void Converter16To24Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
		}

		internal static void Converter16To32Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
				// Use a trick (short instead of int to avoid any convertion from 16Bit to 32Bit)
				short* input = (short*)inputInterleavedBuffer;
				short* output = (short*)outputInterleavedBuffer;
				output++;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						var value = *input++;
						output[0] = value;
						output[2] = value;
						output += 2 * outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						*output = *input++;
						output += 2;
						*output = *input++;
						output += 2;
					}
				// generic
				else
				{
					var max = Math.Max(inputChannels, outputChannels);
					var min = Math.Min(inputChannels, outputChannels);
					for (int i = 0; i < frames; i++)
						for (int j = 0; j < max; j++)
						{
							if (j < min)
							{
								*output = *input++;
								output += 2;
							}
							else if (j >= outputChannels)
								input++;
							if (j >= inputChannels)
							{
								*output++ = 0;
								*output++ = 0;
							}
						}
				}
			}
		}

		internal static void Converter24To16Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
				byte* input = (byte*)inputInterleavedBuffer;
				short* output = (short*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						var value = AsioSampleConvertor.roundedShift16((*input++ << 8) | (*input++ << 16) | (*input++ << 24));
						output[0] = value;
						output[1] = value;
						output += outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						*output++ = AsioSampleConvertor.roundedShift16((*input++ << 8) | (*input++ << 16) | (*input++ << 24));
						*output++ = AsioSampleConvertor.roundedShift16((*input++ << 8) | (*input++ << 16) | (*input++ << 24));
					}
				// generic
				else
				{
					var max = Math.Max(inputChannels, outputChannels);
					var min = Math.Min(inputChannels, outputChannels);
					for (int i = 0; i < frames; i++)
						for (int j = 0; j < max; j++)
						{
							if (j < min)
								*output++ = AsioSampleConvertor.roundedShift16((*input++ << 8) | (*input++ << 16) | (*input++ << 24));
							else if (j >= outputChannels)
								input += 3;
							if (j >= inputChannels)
								*output++ = 0;
						}
				}
			}
		}

		internal static void Converter24To32Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
				byte* input = (byte*)inputInterleavedBuffer;
				byte* output = (byte*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						output[0] = 0;
						output[4] = 0;

						var value = *input++;
						output[1] = value;
						output[5] = value;

						value = *input++;
						output[2] = value;
						output[6] = value;

						value = *input++;
						output[3] = value;
						output[7] = value;

						output += 4 * outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						*output++ = 0;
						*output++ = *input++;
						*output++ = *input++;
						*output++ = *input++;

						*output++ = 0;
						*output++ = *input++;
						*output++ = *input++;
						*output++ = *input++;
					}
				// generic
				else
				{
					var max = Math.Max(inputChannels, outputChannels);
					var min = Math.Min(inputChannels, outputChannels);
					for (int i = 0; i < frames; i++)
						for (int j = 0; j < max; j++)
						{
							if (j < min)
							{
								*output++ = 0;
								*output++ = *input++;
								*output++ = *input++;
								*output++ = *input++;
							}
							else if (j >= outputChannels)
								input += 3;
							if (j >= inputChannels)
							{
								*(int*)output = 0;
								output += 4;
							}
						}
				}
			}
		}

		internal static void Converter32To16Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
				// Use a trick (short instead of int to avoid any convertion from 16Bit to 32Bit)
				int* input = (int*)inputInterleavedBuffer;
				short* output = (short*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						var value = AsioSampleConvertor.roundedShift16(*input++);
						output[0] = value;
						output[1] = value;
						output += outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						*output++ = AsioSampleConvertor.roundedShift16(*input++);
						*output++ = AsioSampleConvertor.roundedShift16(*input++);
					}
				// generic
				else
				{
					var max = Math.Max(inputChannels, outputChannels);
					var min = Math.Min(inputChannels, outputChannels);
					for (int i = 0; i < frames; i++)
						for (int j = 0; j < max; j++)
						{
							if (j < min)
								*output++ = AsioSampleConvertor.roundedShift16(*input++);
							else if (j >= outputChannels)
								input++;
							if (j >= inputChannels)
								*output++ = 0;
						}
				}
			}
		}

		internal static void Converter32To24Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
		}

		internal static void Converter32To32Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
				// Use a trick (short instead of int to avoid any convertion from 16Bit to 32Bit)
				int* input = (int*)inputInterleavedBuffer;
				int* output = (int*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						var value = *input++;
						output[0] = value;
						output[1] = value;
						output += outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						*output++ = *input++;
						*output++ = *input++;
					}
				// generic
				else
				{
					var max = Math.Max(inputChannels, outputChannels);
					var min = Math.Min(inputChannels, outputChannels);
					for (int i = 0; i < frames; i++)
						for (int j = 0; j < max; j++)
						{
							if (j < min)
								*output++ = *input++;
							else if (j >= outputChannels)
								input++;
							if (j >= inputChannels)
								*output++ = 0;
						}
				}
			}
		}

		#endregion

		#region Float

		internal static void ConverterFloatTo16Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
				// Use a trick (short instead of int to avoid any convertion from 16Bit to 32Bit)
				float* input = (float*)inputInterleavedBuffer;
				short* output = (short*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						var value = AsioSampleConvertor.clampToShort(*input++);
						output[0] = value;
						output[1] = value;
						output += outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						*output++ = AsioSampleConvertor.clampToShort(*input++);
						*output++ = AsioSampleConvertor.clampToShort(*input++);
					}
				// generic
				else
				{
					var max = Math.Max(inputChannels, outputChannels);
					var min = Math.Min(inputChannels, outputChannels);
					for (int i = 0; i < frames; i++)
						for (int j = 0; j < max; j++)
						{
							if (j < min)
								*output++ = AsioSampleConvertor.clampToShort(*input++);
							else if (j >= outputChannels)
								input++;
							if (j >= inputChannels)
								*output++ = 0;
						}
				}
			}
		}

		internal static void ConverterFloatTo24Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
		}

		internal static void ConverterFloatTo32Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
				// Use a trick (short instead of int to avoid any convertion from 16Bit to 32Bit)
				float* input = (float*)inputInterleavedBuffer;
				int* output = (int*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						var value = AsioSampleConvertor.clampToInt(*input++);
						output[0] = value;
						output[1] = value;
						output += outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						*output++ = AsioSampleConvertor.clampToInt(*input++);
						*output++ = AsioSampleConvertor.clampToInt(*input++);
					}
				// generic
				else
				{
					var max = Math.Max(inputChannels, outputChannels);
					var min = Math.Min(inputChannels, outputChannels);
					for (int i = 0; i < frames; i++)
						for (int j = 0; j < max; j++)
						{
							if (j < min)
								*output++ = AsioSampleConvertor.clampToInt(*input++);
							else if (j >= outputChannels)
								input++;
							if (j >= inputChannels)
								*output++ = 0;
						}
				}
			}
		}

		#endregion
	}
}