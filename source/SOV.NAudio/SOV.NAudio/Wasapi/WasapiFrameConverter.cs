﻿/*

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
			if (input.SampleRate != output.SampleRate && input.Encoding != WaveFormatEncoding.DSD)
				return null;

			if (input.Encoding == WaveFormatEncoding.DSD)
			{
				switch (output.BitsPerSample)
				{
					case 24:
						return ConverterDSDTo24Generic;
					case 32:
						return ConverterDSDTo32Generic;
					default:
						throw new NotSupportedException($"Not a supported conversion {WaveFormatEncoding.DoP} for {input}.");
				}
			}
			else if (output.Encoding == WaveFormatEncoding.PCM)
			{
				switch (input.Encoding)
				{
					case WaveFormatEncoding.PCM:
						switch (input.BitsPerSample)
						{
							case 16:
								switch (output.BitsPerSample)
								{
									case 16:
										return Converter16To16Generic;
									case 24:
										return Converter16To24Generic;
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
									case 24:
										return Converter24To24Generic;
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
										return Converter32To24Generic;
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
								return ConverterFloatTo24Generic;
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

		#region DoP

		internal static void ConverterDSDTo24Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
				byte* input = (byte*)inputInterleavedBuffer;
				byte* output = (byte*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames / 2; i++)
					{
						//left
						*output++ = input[1];
						*output++ = input[0];
						*output++ = 0x05;

						//right
						*output++ = input[1];
						*output++ = input[0];
						*output++ = 0x05;

						//left
						*output++ = input[3];
						*output++ = input[2];
						*output++ = 0xFA;

						//right
						*output++ = input[3];
						*output++ = input[2];
						*output++ = 0xFA;

						output += 6 * (outputChannels - 2);
						input += 4;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames / 2; i++)
					{
						//left
						*output++ = input[2];
						*output++ = input[0];
						*output++ = 0x05;

						//right
						*output++ = input[3];
						*output++ = input[1];
						*output++ = 0x05;

						//left
						*output++ = input[6];
						*output++ = input[4];
						*output++ = 0xFA;

						//right
						*output++ = input[7];
						*output++ = input[5];
						*output++ = 0xFA;

						input += 4 * inputChannels;
					}
				// generic
				else
				{
					var max = Math.Max(inputChannels, outputChannels);
					var min = Math.Min(inputChannels, outputChannels);
					for (int i = 0; i < frames; i++)
					{
						for (int j = 0; j < max; j++)
						{
							if (j < min)
							{
								*output++ = input[inputChannels * 1 + j];
								*output++ = input[inputChannels * 0 + j];
								*output++ = (i & 1) > 0 ? (byte)0xFA : (byte)0x05;
							}
							if (j >= inputChannels)
							{
								*output++ = 0x69;
								*output++ = 0x69;
								*output++ = (i & 1) > 0 ? (byte)0xFA : (byte)0x05;
							}
						}
						input += 2 * inputChannels;
					}
				}
			}
		}

		internal static void ConverterDSDTo32Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
				byte* input = (byte*)inputInterleavedBuffer;
				byte* output = (byte*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames / 2; i++)
					{
						//left
						*output++ = 0x69;
						*output++ = input[1];
						*output++ = input[0];
						*output++ = 0x05;

						//right
						*output++ = 0x69;
						*output++ = input[1];
						*output++ = input[0];
						*output++ = 0x05;

						//left
						*output++ = 0x69;
						*output++ = input[3];
						*output++ = input[2];
						*output++ = 0xFA;

						//right
						*output++ = 0x69;
						*output++ = input[3];
						*output++ = input[2];
						*output++ = 0xFA;

						output += 8 * (outputChannels - 2);
						input += 4;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames / 2; i++)
					{
						//left
						//*output++ = (0x05 << 24) | (input[0] << 16) | (input[2] << 8) | 0x69;
						*output++ = 0x69;
						*output++ = input[2];
						*output++ = input[0];
						*output++ = 0x05;

						//right
						//*output++ = (0x05 << 24) | (input[1] << 16) | (input[3] << 8) | 0x69;
						*output++ = 0x69;
						*output++ = input[3];
						*output++ = input[1];
						*output++ = 0x05;

						//left
						//*output++ = (0xFA << 24) | (input[4] << 16) | (input[6] << 8) | 0x69;
						*output++ = 0x69;
						*output++ = input[6];
						*output++ = input[4];
						*output++ = 0xFA;

						//right
						//*output++ = (0xFA << 24) | (input[5] << 16) | (input[7] << 8) | 0x69;
						*output++ = 0x69;
						*output++ = input[7];
						*output++ = input[5];
						*output++ = 0xFA;

						input += 4 * inputChannels;
					}
				// generic
				else
				{
					var max = Math.Max(inputChannels, outputChannels);
					var min = Math.Min(inputChannels, outputChannels);
					for (int i = 0; i < frames; i++)
					{
						for (int j = 0; j < max; j++)
						{
							if (j < min)
							{
								*output++ = 0x69;
								*output++ = input[inputChannels * 1 + j];
								*output++ = input[inputChannels * 0 + j];
								*output++ = (i & 1) > 0 ? (byte)0xFA : (byte)0x05;
							}
							if (j >= inputChannels)
							{
								*output++ = 0x69;
								*output++ = 0x69;
								*output++ = 0x69;
								*output++ = (i & 1) > 0 ? (byte)0xFA : (byte)0x05;
							}
						}
						input += 2 * inputChannels;
					}
				}
			}
		}

		#endregion

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
			unsafe
			{
				byte* input = (byte*)inputInterleavedBuffer;
				byte* output = (byte*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						output[0] = 0;
						output[3] = 0;

						var value = *input++;
						output[1] = value;
						output[4] = value;

						value = *input++;
						output[2] = value;
						output[5] = value;

						output += 3 * outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						*output++ = 0;
						*output++ = *input++;
						*output++ = *input++;

						*output++ = 0;
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
							}
							else if (j >= outputChannels)
								input += 2;
							if (j >= inputChannels)
							{
								*(int*)output = 0;
								output += 3;
							}
						}
				}
			}
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
						short value = (short)(((*input++ << 8) | (*input++ << 16) | (*input++ << 24)) >> 16);
						output[0] = value;
						output[1] = value;
						output += outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						*output++ = (short)(((*input++ << 8) | (*input++ << 16) | (*input++ << 24)) >> 16);
						*output++ = (short)(((*input++ << 8) | (*input++ << 16) | (*input++ << 24)) >> 16);
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
								*output++ = (short)(((*input++ << 8) | (*input++ << 16) | (*input++ << 24)) >> 16);
							else if (j >= outputChannels)
								input += 3;
							if (j >= inputChannels)
								*output++ = 0;
						}
				}
			}
		}

		internal static void Converter24To24Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
				byte* input = (byte*)inputInterleavedBuffer;
				byte* output = (byte*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						var value = *input++;
						output[0] = value;
						output[3] = value;

						value = *input++;
						output[1] = value;
						output[4] = value;

						value = *input++;
						output[2] = value;
						output[5] = value;

						output += 3 * outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						*output++ = *input++;
						*output++ = *input++;
						*output++ = *input++;

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
								*output++ = *input++;
								*output++ = *input++;
								*output++ = *input++;
							}
							else if (j >= outputChannels)
								input += 3;
							if (j >= inputChannels)
							{
								*(int*)output = 0;
								output += 3;
							}
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
				int* input = (int*)inputInterleavedBuffer;
				short* output = (short*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						short value = (short)(*input++ >> 16);
						output[0] = value;
						output[1] = value;
						output += outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						*output++ = (short)(*input++ >> 16);
						*output++ = (short)(*input++ >> 16);
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
								*output++ = (short)(*input++ >> 16);
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
			unsafe
			{
				byte* input = (byte*)inputInterleavedBuffer;
				byte* output = (byte*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						input++;
						var value = *input++;
						output[0] = value;
						output[3] = value;

						value = *input++;
						output[1] = value;
						output[4] = value;

						value = *input++;
						output[2] = value;
						output[5] = value;

						output += 3 * outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						input++;
						*output++ = *input++;
						*output++ = *input++;
						*output++ = *input++;

						input++;
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
								input++;
								*output++ = *input++;
								*output++ = *input++;
								*output++ = *input++;
							}
							else if (j >= outputChannels)
								input += 4;
							if (j >= inputChannels)
							{
								*output++ = 0;
								*output++ = 0;
								*output++ = 0;
							}
						}
				}
			}
		}

		internal static void Converter32To32Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
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
			unsafe
			{
				float* input = (float*)inputInterleavedBuffer;
				byte* output = (byte*)outputInterleavedBuffer;

				// optimized mono to stereo
				if (inputChannels == 1 && outputChannels >= 2)
					for (int i = 0; i < frames; i++)
					{
						var value = AsioSampleConvertor.clampTo24Bit(*input++);

						output[0] = (byte)(value >> 0);
						output[3] = (byte)(value >> 0);

						output[1] = (byte)(value >> 8);
						output[4] = (byte)(value >> 8);

						output[2] = (byte)(value >> 16);
						output[5] = (byte)(value >> 16);

						output += 3 * outputChannels;
					}
				// optimized stereo to stereo
				else if (inputChannels == 2 && outputChannels == 2)
					for (int i = 0; i < frames; i++)
					{
						var value = AsioSampleConvertor.clampTo24Bit(*input++);
						*output++ = (byte)(value >> 0);
						*output++ = (byte)(value >> 8);
						*output++ = (byte)(value >> 16);

						value = AsioSampleConvertor.clampTo24Bit(*input++);
						*output++ = (byte)(value >> 0);
						*output++ = (byte)(value >> 8);
						*output++ = (byte)(value >> 16);
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
								var value = AsioSampleConvertor.clampTo24Bit(*input++);
								*output++ = (byte)(value >> 0);
								*output++ = (byte)(value >> 8);
								*output++ = (byte)(value >> 16);
							}
							else if (j >= outputChannels)
								input++;
							if (j >= inputChannels)
							{
								*(int*)output = 0;
								output += 3;
							}
						}
				}
			}
		}

		internal static void ConverterFloatTo32Generic(IntPtr inputInterleavedBuffer, int inputChannels, IntPtr outputInterleavedBuffer, int outputChannels, int frames)
		{
			unsafe
			{
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