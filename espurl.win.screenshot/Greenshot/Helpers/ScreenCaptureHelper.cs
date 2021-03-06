﻿/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2011  Thomas Braun, Jens Klingen, Robin Krom
 * 
 * For more information see: http://getgreenshot.org/
 * The Greenshot project is hosted on Sourceforge: http://sourceforge.net/projects/greenshot/
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

using Greenshot;
using Greenshot.Configuration;
using Greenshot.Plugin;
using GreenshotPlugin.UnmanagedHelpers;
using GreenshotPlugin.Core;
using IniFile;

namespace Greenshot.Helpers {
	/// <summary>
	/// Description of ScreenCaptureHelper.
	/// </summary>
	public class ScreenCaptureHelper {
		private static log4net.ILog LOG = log4net.LogManager.GetLogger(typeof(ScreenCaptureHelper));
		private static CoreConfiguration conf = IniConfig.GetIniSection<CoreConfiguration>();
		private const int MAX_FRAMES = 500;
		private IntPtr hWndDesktop = IntPtr.Zero;
		private IntPtr hDCDesktop = IntPtr.Zero;
		private IntPtr hDCDest = IntPtr.Zero;
		private IntPtr hDIBSection = IntPtr.Zero;
		private IntPtr hOldObject = IntPtr.Zero;
		private int framesPerSecond;
		private Thread backgroundTask;
		private bool stop = false;
		private AVIWriter aviWriter;
		private WindowDetails recordingWindow;
		private Rectangle recordingRectangle;
		public bool RecordMouse = false;
		private Size recordingSize;
		private IntPtr bits0 = IntPtr.Zero; //pointer to the raw bits that make up the bitmap.
		private Bitmap GDIBitmap;

		public ScreenCaptureHelper(Rectangle recordingRectangle) {
			this.recordingRectangle = recordingRectangle;
		}
		public ScreenCaptureHelper(WindowDetails recordingWindow) {
			this.recordingWindow = recordingWindow;
		}

		/// <summary>
		/// Helper method to create an exception that might explain what is wrong while capturing
		/// </summary>
		/// <param name="method">string with current method</param>
		/// <param name="captureBounds">Rectangle of what we want to capture</param>
		/// <returns></returns>
		private static Exception CreateCaptureException(string method, Size size) {
			Exception exceptionToThrow = User32.CreateWin32Exception(method);
			if (size != Size.Empty) {
				exceptionToThrow.Data.Add("Height", size.Height);
				exceptionToThrow.Data.Add("Width", size.Width);
			}
			return exceptionToThrow;
		}
		
		public void Start(int framesPerSecond) {
			string filename;
			if (recordingWindow != null) {
				string windowTitle = Regex.Replace(recordingWindow.Text, @"[^\x20\d\w]", "");
				if (string.IsNullOrEmpty(windowTitle)) {
					windowTitle = "greenshot-recording";
				}
				filename = Path.Combine(conf.OutputFilePath, windowTitle + ".avi");
				
			} else {
				filename = Path.Combine(conf.OutputFilePath, "greenshot-recording.avi");
			}
			if (File.Exists(filename)) {
				try {
					File.Delete(filename);
				} catch {}
			}
			LOG.InfoFormat("Capturing to {0}", filename);
						
			if (recordingWindow != null) {
				LOG.InfoFormat("Starting recording Window '{0}', {1}", recordingWindow.Text, recordingWindow.ClientRectangle);
				recordingSize = recordingWindow.ClientRectangle.Size;
			} else {
				LOG.InfoFormat("Starting recording rectangle {0}", recordingRectangle);
				recordingSize = recordingRectangle.Size;
			}
			if (recordingSize.Width % 8 > 0) {
				LOG.InfoFormat("Correcting width to be factor 8, {0} => {1}", recordingSize.Width, recordingSize.Width + (8-(recordingSize.Width % 8)));
				recordingSize = new Size(recordingSize.Width + (8-(recordingSize.Width % 8)), recordingSize.Height);
			}
			if (recordingSize.Height % 8 > 0) {
				LOG.InfoFormat("Correcting Height to be factor 8, {0} => {1}", recordingSize.Height, recordingSize.Height + (8-(recordingSize.Height % 8)));
				recordingSize = new Size(recordingSize.Width, recordingSize.Height + (8-(recordingSize.Height % 8)));
			}
			this.framesPerSecond = framesPerSecond;
			// "P/Invoke" Solution for capturing the screen
			hWndDesktop = User32.GetDesktopWindow();
			// get te hDC of the target window
			hDCDesktop = User32.GetWindowDC(hWndDesktop);
			// Make sure the last error is set to 0
			Win32.SetLastError(0);

			// create a device context we can copy to
			hDCDest = GDI32.CreateCompatibleDC(hDCDesktop);
			// Check if the device context is there, if not throw an error with as much info as possible!
			if (hDCDest == IntPtr.Zero) {
				// Get Exception before the error is lost
				Exception exceptionToThrow = CreateCaptureException("CreateCompatibleDC", recordingSize);
				// Cleanup
				User32.ReleaseDC(hWndDesktop, hDCDesktop);
				// throw exception
				throw exceptionToThrow;
			}

			// Create BitmapInfoHeader for CreateDIBSection
			BitmapInfoHeader bitmapInfoHeader = new BitmapInfoHeader(recordingSize.Width, recordingSize.Height, 32);

			// Make sure the last error is set to 0
			Win32.SetLastError(0);

			// create a bitmap we can copy it to, using GetDeviceCaps to get the width/height
			hDIBSection = GDI32.CreateDIBSection(hDCDesktop, ref bitmapInfoHeader, BitmapInfoHeader.DIB_RGB_COLORS, out bits0, IntPtr.Zero, 0);

			if (hDIBSection == IntPtr.Zero) {
				// Get Exception before the error is lost
				Exception exceptionToThrow = CreateCaptureException("CreateDIBSection", recordingSize);
				exceptionToThrow.Data.Add("hdcDest", hDCDest.ToInt32());
				exceptionToThrow.Data.Add("hdcSrc", hDCDesktop.ToInt32());
				
				// clean up
				GDI32.DeleteDC(hDCDest);
				User32.ReleaseDC(hWndDesktop, hDCDesktop);

				// Throw so people can report the problem
				throw exceptionToThrow;
			}
			// Create a GDI Bitmap so we can use GDI and GDI+ operations on the same memory
			GDIBitmap = new Bitmap(recordingSize.Width, recordingSize.Height, 32, PixelFormat.Format32bppArgb, bits0);
			// select the bitmap object and store the old handle
			hOldObject = GDI32.SelectObject(hDCDest, hDIBSection);
			stop = false;
			
			aviWriter = new AVIWriter();
			// Comment the following 2 lines to make the user select it's own codec
			//aviWriter.Codec = "msvc";
			//aviWriter.Quality = 99;

			aviWriter.FrameRate = framesPerSecond;
			aviWriter.Open(filename, recordingSize.Width, recordingSize.Height);
			
			// Start update check in the background
			backgroundTask = new Thread (new ThreadStart(CaptureFrame));
			backgroundTask.IsBackground = true;
			backgroundTask.Start();
		}
		
		private void CaptureFrame() {
			int MSBETWEENCAPTURES = 1000/framesPerSecond;
			int msToNextCapture = MSBETWEENCAPTURES;
			while (!stop)  {
				DateTime nextCapture = DateTime.Now.AddMilliseconds(msToNextCapture);
				Point captureLocation;
				if (recordingWindow != null) {
					captureLocation = recordingWindow.Location;
				} else {
					captureLocation = new Point(recordingRectangle.X,  recordingRectangle.Y);
				}
				// "Capture"
				GDI32.BitBlt(hDCDest, 0, 0, recordingSize.Width, recordingSize.Height, hDCDesktop, captureLocation.X,  captureLocation.Y, CopyPixelOperation.SourceCopy | CopyPixelOperation.CaptureBlt);

				// Mouse
				if (RecordMouse) {
					CursorInfo cursorInfo = new CursorInfo(); 
					cursorInfo.cbSize = Marshal.SizeOf(cursorInfo);
					Point mouseLocation = Cursor.Position;
					mouseLocation.Offset(-captureLocation.X, -captureLocation.Y);
					if (User32.GetCursorInfo(out cursorInfo)) {
						User32.DrawIcon(hDCDest, mouseLocation.X, mouseLocation.Y, cursorInfo.hCursor);
					}
				}
				// add to avi
				try {
					aviWriter.AddLowLevelFrame(bits0);
				} catch (Exception) {
					LOG.Error("Error adding frame to avi, stopping capturing.");
					break;
				}
				int sleeptime = (int)(nextCapture.Subtract(DateTime.Now).Ticks / TimeSpan.TicksPerMillisecond);
				if (sleeptime > 0) {
					Thread.Sleep(sleeptime);
					msToNextCapture = MSBETWEENCAPTURES;
				} else {
					// Compensating
					msToNextCapture = Math.Max(0, MSBETWEENCAPTURES - sleeptime);
				}
			}
			Cleanup();
		}
		
		public void Stop() {
			stop = true;
			backgroundTask.Join();
			Cleanup();
		}
		/// <summary>
		///  Free resources
		/// </summary>
		private void Cleanup() {
			if (hOldObject != IntPtr.Zero && hDCDest != IntPtr.Zero) {
				// restore selection (old handle)
				GDI32.SelectObject(hDCDest, hOldObject);
				GDI32.DeleteDC(hDCDest);
			}
			if (hDCDesktop != IntPtr.Zero) {
				User32.ReleaseDC(hWndDesktop, hDCDesktop);
			}
			if (hDIBSection != IntPtr.Zero) {
				// free up the Bitmap object
				GDI32.DeleteObject(hDIBSection);
			}
			if (aviWriter != null) {
				aviWriter.Dispose();
				aviWriter = null;
			}
		}
	}
}
