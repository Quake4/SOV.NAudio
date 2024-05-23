/*

Copyright © 2023 Oleg Samsonov aka Quake4. All rights reserved.
https://github.com/Quake4/SOV.NAudio

This Source Code Form is subject to the terms of the Mozilla
Public License, v. 2.0. If a copy of the MPL was not distributed
with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

*/
using System;
using System.Runtime.InteropServices;

namespace SOV.NAudio
{
	/// <summary>
	/// https://learn.microsoft.com/en-us/windows/win32/procthread/multimedia-class-scheduler-service
	/// </summary>
	public class MMCSS
	{
		[DllImport("avrt.dll")]
		static extern IntPtr AvSetMmThreadCharacteristics(string taskName, out int taskIndex);

		[DllImport("avrt.dll")]
		static extern bool AvRevertMmThreadCharacteristics(int taskIndex);

		[DllImport("avrt.dll")]
		static extern bool AvSetMmThreadPriority(IntPtr handle, ePriority priority);

		public enum ePriority
		{
			Low = -1,
			Normal = 0,
			High = 1,
			Critical = 2
		}

		static IntPtr _ptr;
		static int _taskIndex = -1;

		public static bool Set()
		{
			if (_taskIndex != -1)
				throw new Exception("Call Unset before.");
			try
			{
				_ptr = AvSetMmThreadCharacteristics("Pro Audio", out _taskIndex);
				return _ptr != IntPtr.Zero;
			}
			catch { }
			return false;
		}

		public static bool Priority(ePriority priority)
		{
			if (_taskIndex == -1)
				throw new Exception("Call Set before.");
			try
			{
				return AvSetMmThreadPriority(_ptr, priority);
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
					_ptr = IntPtr.Zero;
					return result;
				}
			}
			catch { };
			return false;
		}
	}
}