using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;

namespace RestFract.Color
{
   
  public class DictGradientMap : IGradientMap
  {
    public DictGradientMap()
    {
      c_points = new SortedDictionary<double, ColorValue>();
      c_type = GradientType.GRADIENT_MAP_RGB;
      c_points[0] = new ColorValue(true, 0.0, 0.0, 0.0);
      c_points[1] = new ColorValue(true, 1.0, 1.0, 1.0);
      c_shift = 0;
    }

    public DictGradientMap(ColorValue col)
    {
      c_points = new SortedDictionary<double, ColorValue>();
      c_type = GradientType.GRADIENT_MAP_RGB;
      c_points[0] = col;
      c_points[1] = col;
      c_shift = 0;
    }

    public DictGradientMap(ColorValue start, ColorValue end)
    {
      c_points = new SortedDictionary<double, ColorValue>();
      c_type = GradientType.GRADIENT_MAP_RGB;
      c_points[0] = start;
      c_points[1] = end;
      c_shift = 0;
    }

    public DictGradientMap(string filename, bool type)
    {
      c_points = new SortedDictionary<double, ColorValue>();
      c_type = GradientType.GRADIENT_MAP_RGB;
      LoadFromFile(filename, type);
      c_shift = 0;
    }

    public virtual void setPoint(double pos, ColorValue point)
    {
      if (pos < 0) pos = 0;
      if (pos > 1) pos = 1;
      c_points[pos] = point;
    }

    public virtual ColorValue getPoint(double pos, bool cyclic = false)
    {
      ColorValue value;
      if (!cyclic)
      {
        if (pos <= 0) pos = 0;
        if (pos >= 1) pos = 1;
      }
      pos += c_shift;
      while (pos < 0) pos += 1;
      while (pos > 1) pos -= 1;
      if (pos <= 0)
      {
        return c_points[0];
      }
      if (pos >= 1)
      {
        return c_points[1];
      }
      if (c_points.TryGetValue(pos, out value))
      {
        return value;
      }
      ColorValue start = c_points[0], end = c_points[1];
      double sc = 0, ec = 1;
      foreach (var iter in c_points)
      {
        if ((iter.Key) <= pos) { sc = iter.Key; start = iter.Value; };
      }
      foreach (var iter in c_points.Reverse())
      {
        if (iter.Key >= pos) { ec = iter.Key; end = iter.Value; };
      }

      double bound = (pos - sc) / (ec - sc);
      if (c_type == GradientType.GRADIENT_MAP_RGB)
      {
        return new ColorValue(true,
                  start.Red + (end.Red - start.Red) * bound,
                  start.Green + (end.Green - start.Green) * bound,
                  start.Blue + (end.Blue - start.Blue) * bound,
                  start.Alpha + (end.Alpha - start.Alpha) * bound);
      }
      else if (c_type == GradientType.GRADIENT_MAP_HSV)
      {
        return new ColorValue(false,
                  start.Hue + (end.Hue - start.Hue) * bound,
                  start.Saturation + (end.Saturation - start.Saturation) * bound,
                  start.Value + (end.Value - start.Value) * bound,
                  start.Alpha + (end.Alpha - start.Alpha) * bound);
      }
      else if (c_type == GradientType.GRADIENT_MAP_HSVBACK)
      {
        return new ColorValue(false,
                (start.Hue + 1) + (end.Hue - (start.Hue + 1)) * bound,
                start.Saturation + (end.Saturation - start.Saturation) * bound,
                start.Value + (end.Value - start.Value) * bound,
                start.Alpha + (end.Alpha - start.Alpha) * bound);
      }
      else
      {
        if (Math.Abs(start.Hue - end.Hue) > 0.5)
        {
          return new ColorValue(false,
                  (start.Hue + 1) + (end.Hue - (start.Hue + 1)) * bound,
                  start.Saturation + (end.Saturation - start.Saturation) * bound,
                  start.Value + (end.Value - start.Value) * bound,
                  start.Alpha + (end.Alpha - start.Alpha) * bound);
        }
        else
        {
          return new ColorValue(false,
                  start.Hue + (end.Hue - start.Hue) * bound,
                  start.Saturation + (end.Saturation - start.Saturation) * bound,
                  start.Value + (end.Value - start.Value) * bound,
                  start.Alpha + (end.Alpha - start.Alpha) * bound);
        }
      }

    }

    public virtual void ClearPoint(double pos)
    {
      if ((pos > 0) && (pos < 1)) { c_points.Remove(pos); }
    }
    public virtual SortedDictionary<double, ColorValue> getPoints() { return c_points; }

    public virtual void Reverse()
    {
      SortedDictionary<double, ColorValue> p2 = new SortedDictionary<double,ColorValue>(c_points);
      c_points[1] = p2[0];
      c_points[0] = p2[1];
      foreach (var iter in p2)
      {
        if ((iter.Key != 0) && (iter.Key != 1)) c_points[1 - iter.Key] = iter.Value;
      }
    }

    public virtual GradientType MappingType
    {
      get { return c_type; }
      set { c_type = value; }
    }

    public virtual bool LoadFromFile(string filename, bool type = false)
    {
      try
      {
        using (FileStream fs = new FileStream(filename, FileMode.Open))
        {
          using (StreamReader f = new StreamReader(fs))
          {
            string s;
            s = f.ReadLine();
            s = f.ReadLine();
            int numlines = Convert.ToInt32(f.ReadLine());
            double[] a = new double[13];
            double[] save = new double[4];
            c_points.Clear();
            c_points[0] = new ColorValue(true, 0, 0, 0, 0);
            c_points[1] = new ColorValue(true, 1, 0, 0, 0);
            for (int i = 0; i < numlines; i++)
            {
              string[] ss = f.ReadLine().Split(' ');

              for (int i2 = 0; i2 < 13; i2++) { double.TryParse(ss[i2], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out a[i2]); };
              //std::cout << std::endl;
              if ((i != 0) && (type))
              {
                if ((save[0] != a[3]) || (save[1] != a[4]) || (save[2] != a[5]) || (save[3] != a[7]))
                {
                  c_points[a[0] - double.Epsilon] = new ColorValue(true, save[0], save[1], save[2], save[3]);
                }
              }
              c_points[a[0]] = new ColorValue(true, a[3], a[4], a[5], a[6]);
              if (type) c_points[a[1]] = new ColorValue(true, a[3] + (a[7] - a[3]) / 2, a[4] + (a[8] - a[4]) / 2, a[5] + (a[9] - a[5]) / 2, a[6] + (a[10] - a[6]) / 2);
              save[0] = a[7];
              save[1] = a[8];
              save[2] = a[9];
              save[3] = a[10];
            }
            c_points[1] = new ColorValue(true, a[7], a[8], a[9], a[10]);
          }
        }
        return true;
      }
      catch (FileNotFoundException)
      {
        return false;
      }
    }

    public virtual void SaveToFile(string filename)
    {
      using (FileStream fs = new FileStream(filename, FileMode.Create))
      {
        using (StreamWriter f = new StreamWriter(fs))
        {
          f.WriteLine("GIMP Gradient");
          f.WriteLine("Name: " + filename);
          f.WriteLine(c_points.Count);
          double[] a = new double[11];
          a[0] = 0;
          a[3] = c_points[0].Red;
          a[4] = c_points[0].Green;
          a[5] = c_points[0].Blue;
          a[6] = c_points[0].Alpha;
          foreach (var p in c_points)
          {
            if (p.Key == 0) continue;
            a[7] = p.Value.Red;
            a[8] = p.Value.Green;
            a[9] = p.Value.Blue;
            a[10] = p.Value.Alpha;
            a[2] = p.Key;
            a[1] = a[0] + (a[2] - a[0]) / 2;

            foreach (double d in a)
            {
              f.Write(d);
              f.Write(" ");
            }
            f.WriteLine("0 0");

            a[0] = a[2];
            a[3] = a[7];
            a[4] = a[8];
            a[5] = a[9];
            a[6] = a[10];
          }
        }
      }
    }

    public virtual double Shift
    {
      get { return c_shift; }
      set { c_shift = value; }
    }

    private double c_shift;
    private GradientType c_type;
    private SortedDictionary<double, ColorValue> c_points;
  }
}

