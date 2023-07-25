/*

Copyright © 2023 Oleg Samsonov aka Quake4. All rights reserved.
https://github.com/Quake4/SOV.NAudio

This Source Code Form is subject to the terms of the Mozilla
Public License, v. 2.0. If a copy of the MPL was not distributed
with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

*/
namespace SOV.NAudio
{
	public class TrackInfo
	{
		public int Number;
		public string Title;
		public string Performer;
	}

	public class FileInfo
	{
		public int Year;
		public string Title;
		public string Artist;
	}

	public interface IMetaInfo
	{
		FileInfo FileInfo { get; }
		TrackInfo TrackInfo { get; }
		int TrackCount { get; }
	}
}