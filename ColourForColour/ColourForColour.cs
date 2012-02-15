using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;

namespace ColourForColour
{
    ///<summary>
    ///ColourForColour places red boxes behind all the "A"s in the editor window
    ///</summary>
    public class ColourForColour
    {
        IAdornmentLayer _layer;
        IWpfTextView _view;
        List<Tuple<int, int, Color>> _colourPositions;


        public ColourForColour(IWpfTextView view)
        {
            _view = view;
            _layer = view.GetAdornmentLayer("ColourForColour");

            _view.MouseHover += OnMouseHover;
            _view.LayoutChanged += OnLayoutChanged;
        }

        /// <summary>
        /// On layout change add the adornment to any reformatted lines
        /// </summary>
        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            /*
             * #(([0-9A-F]{6})|([0-9A-F]{8})|([0-9A-F]{3}))[\"<;]
             * starts with #
             * has 6 xor 8 xor 3 characters which match a hexidecimal digit
             * ends with ", < or ;
             */
            _colourPositions = new List<Tuple<int, int, Color>>();
            var matches = Regex.Matches(_view.TextSnapshot.GetText(), "#(([0-9A-F]{6})|([0-9A-F]{8})|([0-9A-F]{3}))[\"<;]", RegexOptions.IgnoreCase);
            foreach(var m in matches)
            {
                var match = m as Match;
                var mgrp = match.Groups[1] as Group;
                var colourbytes = BytesFromColourString(mgrp.Value);
                var colour = Color.FromArgb(colourbytes.Item1, colourbytes.Item2, colourbytes.Item3, colourbytes.Item4);
                _colourPositions.Add(new Tuple<int,int,Color>(mgrp.Index, mgrp.Index + mgrp.Length, colour));
            }
        }

        private void OnMouseHover(object sender, MouseHoverEventArgs args)
        {
            ShowColourSwatch(args.Position, args.TextPosition, args.View);
        }

        private void ShowColourSwatch(int position, IMappingPoint textPosition, ITextView textView)
        {
            _layer.RemoveAllAdornments();
            SnapshotPoint? snapPoint = textPosition.GetPoint(textPosition.AnchorBuffer, PositionAffinity.Predecessor);
            if (snapPoint.HasValue)
            {
                SnapshotSpan charSpan = textView.GetTextElementSpan(snapPoint.Value);
                var colourPos = _colourPositions.Find(cp => (cp.Item1 <= charSpan.Start) && (cp.Item2 >= charSpan.Start));
                if(colourPos != null)
                {
                    IWpfTextViewLineCollection textViewLines = _view.TextViewLines;
                    Geometry g = textViewLines.GetMarkerGeometry(charSpan);
                    Rect r = new Rect(0,0, 30, 30);
                    RectangleGeometry rg = new RectangleGeometry(r);
                    Brush brush = new SolidColorBrush(colourPos.Item3);
                    brush.Freeze();
                    Brush penBrush = new SolidColorBrush(colourPos.Item3);
                    penBrush.Freeze();
                    Pen pen = new Pen(penBrush, 0.5);
                    pen.Freeze();
                    GeometryDrawing drawing = new GeometryDrawing(brush, pen, rg);
                    drawing.Freeze();
                    DrawingImage drawingImage = new DrawingImage(drawing);
                    drawingImage.Freeze();

                    Image image = new Image();
                    image.Source = drawingImage;

                    //Align the image with the top of the bounds of the text geometry
                    Canvas.SetLeft(image, g.Bounds.Left - g.Bounds.Width);
                    Canvas.SetTop(image, g.Bounds.Top - (g.Bounds.Height*2));

                    _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, charSpan, null, image, null); 
                    _layer.Elements.
                }
            }
        }


        private Tuple<byte, byte, byte, byte> BytesFromColourString(string colour)
        {
            string alpha;
            string red;
            string green;
            string blue;

            if (colour.Length == 8)
            {
                alpha = colour.Substring(0, 2);
                red = colour.Substring(2, 2);
                green = colour.Substring(4, 2);
                blue = colour.Substring(6, 2);
            }
            else if (colour.Length == 6)
            {
                red = colour.Substring(0, 2);
                green = colour.Substring(2, 2);
                blue = colour.Substring(4, 2);
                alpha = "FF";
            }
            else if (colour.Length == 3)
            {
                red = colour.Substring(0, 1) + colour.Substring(0, 1);
                green = colour.Substring(1, 1) + colour.Substring(1, 1);
                blue = colour.Substring(2, 1) + colour.Substring(2, 1);
                alpha = "FF";
            }
            else
            {
                throw new ArgumentException(String.Format("The colour string may be 8, 6 or 3 characters long, the one passed in is {0}", colour.Length));
            }
            return new Tuple<byte, byte, byte, byte>( Convert.ToByte(alpha, 16)
                                                    , Convert.ToByte(red, 16)
                                                    , Convert.ToByte(green, 16)
                                                    , Convert.ToByte(blue, 16));
        }

        /// <summary>
        /// Within the given line add the scarlet box behind the a
        /// </summary>
        private void CreateVisuals(ITextViewLine line)
        {
            //grab a reference to the lines in the current TextView 
            IWpfTextViewLineCollection textViewLines = _view.TextViewLines;
            int start = line.Start;
            int end = line.End;

            var matches = Regex.Matches(_view.TextSnapshot.GetText(), "#(([0-9A-F]{6})|([0-9A-F]{8})|([0-9A-F]{3}))[\"<;]", RegexOptions.IgnoreCase);

            _layer.RemoveAllAdornments();

            foreach (var match in matches)
            {
                int startIndex = ((Match)match).Groups[1].Index;
                int endIndex = ((Match)match).Groups[1].Index + ((Match)match).Groups[1].Length;
                string colour = ((Match)match).Groups[1].Value;
                SnapshotSpan span = new SnapshotSpan(_view.TextSnapshot, Span.FromBounds(startIndex, endIndex));
                Tuple<byte, byte, byte, byte> colourbytes = BytesFromColourString(colour);
                Color c = Color.FromArgb(colourbytes.Item1, colourbytes.Item2, colourbytes.Item3, colourbytes.Item4);
                Geometry g = textViewLines.GetMarkerGeometry(span);
                if (g != null)
                {
                    Brush brush = new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x00, 0xff));
                    brush.Freeze();
                    Brush penBrush = new SolidColorBrush(c);
                    penBrush.Freeze();
                    Pen pen = new Pen(penBrush, 0.5);
                    pen.Freeze();
                    GeometryDrawing drawing = new GeometryDrawing(brush, pen, g);
                    drawing.Freeze();
                    DrawingImage drawingImage = new DrawingImage(drawing);
                    drawingImage.Freeze();

                    Image image = new Image();
                    image.Source = drawingImage;

                    //Align the image with the top of the bounds of the text geometry
                    Canvas.SetLeft(image, g.Bounds.Left);
                    Canvas.SetTop(image, g.Bounds.Top);

                    _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, image, null);
                }
            }
            
               /* if (t[i] == 'a')
                {
                    SnapshotSpan span = new SnapshotSpan(_view.TextSnapshot, Span.FromBounds(i, i + 1));
                    Geometry g = textViewLines.GetMarkerGeometry(span);
                    if (g != null)
                    {
                        GeometryDrawing drawing = new GeometryDrawing(_brush, _pen, g);
                        drawing.Freeze();

                        DrawingImage drawingImage = new DrawingImage(drawing);
                        drawingImage.Freeze();

                        Image image = new Image();
                        image.Source = drawingImage;

                        //Align the image with the top of the bounds of the text geometry
                        Canvas.SetLeft(image, g.Bounds.Left);
                        Canvas.SetTop(image, g.Bounds.Top);

                        _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, image, null);
                    }
                }*/
        }

    }
}
