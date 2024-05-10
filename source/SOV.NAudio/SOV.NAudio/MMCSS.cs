/*

Copyright © 2023 Oleg Samsonov aka Quake4. All rights reserved.
https://github.com/Quake4/SOV.NAudio

This Source Code Form is subject to the terms of the Mozilla
Public License, v. 2.0. If a copy of the MPL was not distributed
with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

*/
using System.Runtime.InteropServices;

namespace SOV.NAudio
{
	/// <summary>
	/// https://learn.microsoft.com/en-us/windows/win32/procthread/multimedia-class-scheduler-service
	/// </summary>
	public class MMCSS
	{
		[DllImport("avrt.dll")]
		static extern bool AvSetMmThreadCharacteristics(string taskName, out int taskIndex);

		[DllImport("avrt.dll")]
		static extern bool AvRevertMmThreadCharacteristics(int taskIndex);

		static int _taskIndex = -1;

		public static bool Set()
		{
			if (_taskIndex != -1)
				throw new System.Exception("Call Unset before.");
			try
			{
				return AvSetMmThreadCharacteristics("Pro Audio", out _taskIndex);
			}
			catch { }
			return false;
		}

		public static bool Unset()
		{
			try
			{
				if (_taskIndex != -1)
				{
					var result = AvRevertMmThreadCharacteristics(_taskIndex);
					_taskIndex = -1;
					return result;
				}
			}
			catch { };
			return false;
		}
	}
}