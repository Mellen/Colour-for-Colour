using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

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
        object lockObject = new object();

        public ColourForColour(IWpfTextView view)
        {
            _view = view;
            _layer = view.GetAdornmentLayer("ColourForColour");

            _view.MouseHover += OnMouseHover;
            _view.LayoutChanged += OnLayoutChanged;
        }

        /// <summary>
        /// On layout change figure out where all the coulours are
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

        /// <summary>
        /// Show the relevant colour swatch for the position of the mouse
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnMouseHover(object sender, MouseHoverEventArgs args)
        {
            ShowColourSwatch(args.Position, args.TextPosition, args.View);
        }

        /// <summary>
        /// creates a colour swatch image
        /// </summary>
        /// <param name="colourPos"></param>
        /// <param name="charSpan"></param>
        /// <returns></returns>
        private Image CreateSwatchImage(Tuple<int, int, Color> colourPos, SnapshotSpan charSpan)
        {
            IWpfTextViewLineCollection textViewLines = _view.TextViewLines;
            Geometry g = textViewLines.GetMarkerGeometry(charSpan);
            
            Rect r = new Rect(5, 5, 30, 30);
            RectangleGeometry rg = new RectangleGeometry(r);
            Brush brush = new SolidColorBrush(colourPos.Item3);
            brush.Freeze();
            Brush penBrush = new SolidColorBrush(colourPos.Item3);
            penBrush.Freeze();
            Pen pen = new Pen(penBrush, 0.5);
            pen.Freeze();
            GeometryDrawing drawingfront = new GeometryDrawing(brush, pen, rg);
            drawingfront.Freeze();

            Brush penBrushBorder = new SolidColorBrush(Color.FromRgb(0xad, 0xad, 0xad));
            penBrushBorder.Freeze();
            Pen penBorder = new Pen(penBrushBorder, 0.5);
            penBorder.Freeze();


            Rect rWhite = new Rect(0, 0, 20, 40);
            RectangleGeometry rgWhite = new RectangleGeometry(rWhite);
            Brush brushWhite = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            brushWhite.Freeze();
            GeometryDrawing drawingleft = new GeometryDrawing(brushWhite, penBorder, rgWhite);
            drawingleft.Freeze();

            Rect rBlack = new Rect(20, 0, 20, 40);
            RectangleGeometry rgBlack = new RectangleGeometry(rBlack);
            Brush brushBlack = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            brushBlack.Freeze();
            GeometryDrawing drawingright = new GeometryDrawing(brushBlack, penBorder, rgBlack);
            drawingright.Freeze();


            DrawingGroup drawing = new DrawingGroup();
            drawing.Children.Add(drawingleft);
            drawing.Children.Add(drawingright);
            drawing.Children.Add(drawingfront);
            drawing.Freeze();

            DrawingImage drawingImage = new DrawingImage(drawing);

            drawingImage.Freeze();

            Image image = new Image();
            image.Source = drawingImage;

            Canvas.SetLeft(image, g.Bounds.Left - g.Bounds.Width);
            Canvas.SetTop(image, g.Bounds.Top - 40);

            return image;
        }

        /// <summary>
        /// If the text position is within a colour then show that could on the screen
        /// </summary>
        /// <param name="position"></param>
        /// <param name="textPosition"></param>
        /// <param name="textView"></param>
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

                    Image image = CreateSwatchImage(colourPos, charSpan);

                    _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, charSpan, null, image, null);
                    Thread t = new Thread(p =>
                    {
                        Thread.Sleep(3500);
                        lock (lockObject)
                        {
                            Application.Current.Dispatcher.Invoke(new Action(() =>
                            {
                                _layer.RemoveAdornmentsByVisualSpan(charSpan);
                            }), new object[]{});
                        }
                    });
                    t.Start();
                }
            }
        }

        /// <summary>
        /// take a hex string and turn it into a 4 byte tuple
        /// </summary>
        /// <param name="colour"></param>
        /// <returns></returns>
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

       

    }
}
