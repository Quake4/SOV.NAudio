/*

Copyright © 2023 Oleg Samsonov aka Quake4. All rights reserved.
https://github.com/Quake4/SOV.NAudio

This Source Code Form is subject to the terms of the Mozilla
Public License, v. 2.0. If a copy of the MPL was not distributed
with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

*/
using NAudio.Wave;
using System.Collections.Generic;
using NAud = NAudio;

namespace SOV.NAudio
{
	public class AsioOut : NAud.Wave.AsioOut, IWaveFormat
	{
		public bool ResamplerUsed => dmoResamplerUsed;

		public WaveFormat WaveFormat => OutputWaveFormat;

		public AsioOut(string driverName, IDictionary<WaveFormatEncoding, int[]> samplerate)
			: base(driverName, samplerate)
		{
			MMCSS.Set();
		}

		public override void Dispose()
		{
			MMCSS.Unset();
			base.Dispose();
		}
	}
}