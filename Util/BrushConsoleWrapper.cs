namespace Util
{
    public class Brush
    {
        public Brush() { }
    }
    public struct Color
    {
        public byte R, G, B;
        public string Name;
        public Color(byte r, byte g, byte b, string name) { R = r; G = g; B = b; Name = name; }
    }

    public static class Brushes
    {
        public static Color Black = new Color(0, 0, 0, "Black");
        public static Color White = new Color(255, 255, 255, "White");
        public static Color LimeGreen = new Color(50, 205, 50, "LimeGreen");
        public static Color Orange = new Color(255, 165, 0, "Amber");
        public static Color Blue = new Color(0, 0, 255, "Blue");
        public static Color Black_low = new Color(0, 0, 0, "Low intensity Black");
        public static Color White_low = new Color(127, 127, 127, "Low intensity White");
        public static Color LimeGreen_low = new Color(25, 103, 25, "Low intensity LimeGreen");
        public static Color Orange_low = new Color(127, 83, 0, "Low intensity Amber");
        public static Color Blue_low = new Color(0, 0, 127, "Low intensity Blue");

    }

    public static class ColorExtensions
    {
        public static Color MakeDim(this Color color)
        {
            if (color.Equals(Brushes.Black)) return Brushes.Black_low;
            if (color.Equals(Brushes.White)) return Brushes.White_low;
            if (color.Equals(Brushes.LimeGreen)) return Brushes.LimeGreen_low;
            if (color.Equals(Brushes.Orange)) return Brushes.Orange_low;
            if (color.Equals(Brushes.Blue)) return Brushes.Blue_low;
            return color;
        }
    }
}
