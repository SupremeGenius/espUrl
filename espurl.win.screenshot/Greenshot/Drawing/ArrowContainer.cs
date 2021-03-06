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
using System.Drawing.Drawing2D;

using Greenshot.Drawing.Fields;
using Greenshot.Helpers;
using Greenshot.Plugin.Drawing;

namespace Greenshot.Drawing
{
   /// <summary>
   /// Description of LineContainer.
   /// </summary>
   [Serializable()]
   public class ArrowContainer : LineContainer
   {
      public enum ArrowHeadCombination { NONE, START_POINT, END_POINT, BOTH };

      private static readonly AdjustableArrowCap ARROW_CAP = new AdjustableArrowCap(4, 6);

      public ArrowContainer(Surface parent)
         : base(parent)
      {
         AddField(GetType(), FieldType.ARROWHEADS, 2);
         AddField(GetType(), FieldType.LINE_COLOR, Color.Red);
         AddField(GetType(), FieldType.FILL_COLOR, Color.Transparent);
         AddField(GetType(), FieldType.SHADOW, false);
         AddField(GetType(), FieldType.ARROWHEADS, Greenshot.Drawing.ArrowContainer.ArrowHeadCombination.END_POINT);
      }

      public override void Draw(Graphics g, RenderMode rm)
      {
         int lineThickness = GetFieldValueAsInt(FieldType.LINE_THICKNESS);
         bool shadow = GetFieldValueAsBool(FieldType.SHADOW);

         if (lineThickness > 0)
         {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            Color lineColor = GetFieldValueAsColor(FieldType.LINE_COLOR);
            ArrowHeadCombination heads = (ArrowHeadCombination)GetFieldValue(FieldType.ARROWHEADS);
            if (shadow)
            {
               //draw shadow first
               int basealpha = 100;
               int alpha = basealpha;
               int steps = 5;
               int currentStep = 1;
               while (currentStep <= steps)
               {
                  using (Pen shadowCapPen = new Pen(Color.FromArgb(alpha, 100, 100, 100)))
                  {
                     shadowCapPen.Width = lineThickness;
                     SetArrowHeads(heads, shadowCapPen);

                     g.DrawLine(shadowCapPen,
                        this.Left + currentStep,
                        this.Top + currentStep,
                        this.Left + currentStep + this.Width,
                        this.Top + currentStep + this.Height);

                     currentStep++;
                     alpha = alpha - (basealpha / steps);
                  }
               }

            }
            using (Pen pen = new Pen(lineColor))
            {
               pen.Width = lineThickness;

               if (pen.Width > 0)
               {
                  SetArrowHeads(heads, pen);
                  g.DrawLine(pen, this.Left, this.Top, this.Left + this.Width, this.Top + this.Height);
               }
            }
         }
      }

      private void SetArrowHeads(ArrowHeadCombination heads, Pen pen)
      {
         if (heads == ArrowHeadCombination.BOTH || heads == ArrowHeadCombination.START_POINT)
         {
            pen.CustomStartCap = ARROW_CAP;
         }
         if (heads == ArrowHeadCombination.BOTH || heads == ArrowHeadCombination.END_POINT)
         {
            pen.CustomEndCap = ARROW_CAP;
         }
      }

      public override Rectangle DrawingBounds
      {
         get
         {
            int lineThickness = GetFieldValueAsInt(FieldType.LINE_THICKNESS);
            if (lineThickness > 0)
            {
               using (Pen pen = new Pen(Color.White))
               {
                  pen.Width = lineThickness;
                  SetArrowHeads((ArrowHeadCombination)GetFieldValue(FieldType.ARROWHEADS), pen);
                  GraphicsPath path = new GraphicsPath();
                  path.AddLine(this.Left, this.Top, this.Left + this.Width, this.Top + this.Height);
                  Rectangle drawingBounds = Rectangle.Round(path.GetBounds(new Matrix(), pen));
                  drawingBounds.Inflate(2, 2);
                  return drawingBounds;
               }
            }
            else
            {
               return Rectangle.Empty;
            }
         }
      }

      public override bool ClickableAt(int x, int y)
      {
         int lineThickness = GetFieldValueAsInt(FieldType.LINE_THICKNESS) + 10;
         if (lineThickness > 0)
         {
            using (Pen pen = new Pen(Color.White))
            {
               pen.Width = lineThickness;
               SetArrowHeads((ArrowHeadCombination)GetFieldValue(FieldType.ARROWHEADS), pen);
               GraphicsPath path = new GraphicsPath();
               path.AddLine(this.Left, this.Top, this.Left + this.Width, this.Top + this.Height);
               return path.IsOutlineVisible(x, y, pen);
            }
         }
         else
         {
            return false;
         }
      }
   }
}
