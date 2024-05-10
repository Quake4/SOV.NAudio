/*

Copyright © 2023 Oleg Samsonov aka Quake4. All rights reserved.
https://github.com/Quake4/SOV.NAudio

This Source Code Form is subject to the terms of the Mozilla
Public License, v. 2.0. If a copy of the MPL was not distributed
with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

*/
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Collections.Generic;
using NAud = NAudio;

namespace SOV.NAudio
{
	public class WasapiOut : NAud.Wave.WasapiOut, IWaveFormat
	{
		public bool ResamplerUsed => dmoResamplerNeeded;

		public bool SharedMode => shareMode == AudioClientShareMode.Shared;

		public WaveFormat WaveFormat
		{
			get
			{
				if (SharedMode)
					return audioClient.MixFormat;
				var format = InternalWaveFormat.ToStandardWaveFormat(true);
				if (sourceWaveFormat.Encoding == WaveFormatEncoding.DSD)
					format.SetEncoding(WaveFormatEncoding.DoP);
				return format;
			}
		}

		public WasapiOut(MMDevice device, AudioClientShareMode shareMode, IDictionary<WaveFormatEncoding, int[]> samplerate, bool useEventSync = true, int latency = 100)
			: base(device, shareMode, useEventSync, latency, samplerate)
		{
			if (MMCSS.Set())
				MMCSS.Priority(MMCSS.ePriority.Critical);
		}

		public override void Dispose()
		{
			MMCSS.Unset();
			base.Dispose();
		}
	}
}