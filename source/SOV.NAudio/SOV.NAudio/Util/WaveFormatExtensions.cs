/*

Copyright © 2023 Oleg Samsonov aka Quake4. All rights reserved.
https://github.com/Quake4/SOV.NAudio

This Source Code Form is subject to the terms of the Mozilla
Public License, v. 2.0. If a copy of the MPL was not distributed
with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

*/
using System.Reflection;

namespace NAudio.Wave
{
	public static class WaveFormatExtensions
	{
		private static FieldInfo _fiEncoding;

		public static WaveFormat SetEncoding(this WaveFormat format, WaveFormatEncoding encoding)
		{
			if (_fiEncoding == null)
				_fiEncoding = typeof(WaveFormat).GetField("waveFormatTag", BindingFlags.NonPublic | BindingFlags.Instance);
			_fiEncoding.SetValue(format, encoding);
			return format;
		}
	}
}