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
using System.Drawing;

using Greenshot.Drawing;
using Greenshot.Configuration;

namespace Greenshot.Memento {
	/// <summary>
	/// The SurfaceCropMemento makes it possible to undo-redo an surface crop
	/// </summary>
	public class SurfaceCropMemento : IMemento {
		private Image image;
		private Surface surface;
		private Rectangle cropRectangle;
		
		public SurfaceCropMemento(Surface surface, Rectangle cropRectangle) {
			this.surface = surface;
			this.image = surface.Image;
			this.cropRectangle = cropRectangle;
		}
		
		public void Dispose() {
			if (image != null) {
				image.Dispose();
				image = null;
			}
		}

		public bool Merge(IMemento otherMemento) {
			return false;
		}
		
		public LangKey ActionKey {
			get {
				//return LangKey.editor_crop;
				return LangKey.none;
			}
		}

		public IMemento Restore() {
			SurfaceCropMemento oldState = new SurfaceCropMemento( surface, cropRectangle);
			surface.UndoCrop(image, cropRectangle);
			surface.Invalidate();
			return oldState;
		}
	}
}
