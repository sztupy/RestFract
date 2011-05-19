using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RestFract.Color
{
  public struct ColorValue
  {
    public ColorValue(bool type, double rh, double gs, double bv)
    {
      c_a = 1;
      if (rh < 0) rh = 0;
      if (gs < 0) gs = 0;
      if (bv < 0) bv = 0;
      if (rh > 1) rh = 1;
      if (gs > 1) gs = 1;
      if (bv > 1) bv = 1;
      if (type)
      {
        c_r = rh;
        c_g = gs;
        c_b = bv;
        RGBtoHSV(c_r, c_g, c_b, out c_h, out c_s, out c_v);
      }
      else
      {
        c_h = rh;
        c_s = gs;
        c_v = bv;
        HSVtoRGB(c_h, c_s, c_v, out c_r, out c_g, out c_b);
      }
    }

    public ColorValue(bool type, double rh, double gs, double bv, double a)
    {
      if (!type)
      {
        while (rh < 0) rh += 1;
        while (rh > 1) rh -= 1;
      }
      if (rh < 0) rh = 0;
      if (gs < 0) gs = 0;
      if (bv < 0) bv = 0;
      if (rh > 1) rh = 1;
      if (gs > 1) gs = 1;
      if (bv > 1) bv = 1;
      if (a < 0) a = 0;
      if (a > 1) a = 1;
      c_a = a;
      if (type)
      {
        c_r = rh;
        c_g = gs;
        c_b = bv;
        RGBtoHSV(c_r, c_g, c_b, out c_h, out c_s, out c_v);
      }
      else
      {
        c_h = rh;
        c_s = gs;
        c_v = bv;
        HSVtoRGB(c_h, c_s, c_v, out c_r, out c_g, out c_b);
      }
    }

    public static void HSVtoRGB(double h, double s, double v, out double r, out double g, out double b)
    {
      int i;
      double f, p, q, t, hTemp;
      h = h * 360;
      if (h >= 360) h -= 360;
      if (s == 0.0 || h == -1.0) // s==0? Totally unsaturated = grey so R,G and B all equal value
      {
        r = g = b = v;
        return;
      }
      hTemp = h / 60.0f;
      i = (int)Math.Floor(hTemp);                 // which sector
      f = hTemp - i;                      // how far through sector
      p = v * (1 - s);
      q = v * (1 - s * f);
      t = v * (1 - s * (1 - f));
      switch (i)
      {
        case 0: { r = v; g = t; b = p; break; }
        case 1: { r = q; g = v; b = p; break; }
        case 2: { r = p; g = v; b = t; break; }
        case 3: { r = p; g = q; b = v; break; }
        case 4: { r = t; g = p; b = v; break; }
        default: { r = v; g = p; b = q; break; }
      }
      if (r > 1) r = 1; if (r < 0) r = 0;
      if (g > 1) g = 1; if (g < 0) g = 0;
      if (b > 1) b = 1; if (b < 0) b = 0;

    }

    public static void RGBtoHSV(double r, double g, double b, out double h, out double s, out double v)
    {
      double min, max, delta;

      min = r; if (b < min) min = b; if (g < min) min = g;
      max = r; if (b > max) max = b; if (g > max) max = g;
      v = max;				// v

      delta = max - min;

      if ((max != 0) && (delta != 0))
      {
        s = delta / max;		// s
      }
      else
      {
        // r = g = b = 0		// s = 0, v is undefined
        s = 0;
        h = 0;
        return;
      }

      if (r == max)
        h = (g - b) / delta;		// between yellow & magenta
      else if (g == max)
        h = 2 + (b - r) / delta;	// between cyan & yellow
      else
        h = 4 + (r - g) / delta;	// between magenta & cyan

      h *= 60;				// degrees
      if (h < 0)
        h += 360;
      h /= 360;

      if (h > 1) h = 1; if (h < 0) h = 0;
      if (s > 1) s = 1; if (s < 0) s = 0;
      if (v > 1) v = 1; if (v < 0) v = 0;

    }

    public void setRGB(double r, double g, double b)
    {
      if (r < 0) r = 0;
      if (g < 0) g = 0;
      if (b < 0) b = 0;
      if (r > 1) r = 1;
      if (g > 1) g = 1;
      if (b > 1) b = 1;
      c_r = r;
      c_g = g;
      c_b = b;
      RGBtoHSV(c_r, c_g, c_b, out c_h, out c_s, out c_v);
    }
    public void setHSV(double h, double s, double v)
    {
      while (h < 0) h += 1;
      while (h > 1) h -= 1;
      if (s < 0) s = 0;
      if (v < 0) v = 0;
      if (s > 1) s = 1;
      if (v > 1) v = 1;
      c_h = h;
      c_s = s;
      c_v = v;
      HSVtoRGB(c_h, c_s, c_v, out c_r, out c_g, out c_b);
    }

    public void Blend(ColorValue sec, double alpha = 1, double saturation = 1, double value = 1)
    {
      if ((alpha <= 0) || (sec.c_a <= 0)) return;
      if (alpha < 0) alpha = 0;
      if (alpha > 1) alpha = 1;
      if (saturation < 0) saturation = 0;
      if (saturation > 1) saturation = 1;
      if (value < 0) value = 0;
      if (value > 1) value = 1;

      ColorValue sec2 = sec;
      if (saturation != 1)
      {
        sec2.Saturation = sec2.Saturation * saturation;
      }
      if (value != 1)
      {
        sec2.Value = sec2.Value * value;
      }
      if (alpha != 1)
      {
        if ((alpha != 0) && (sec2.c_a != 0))
        {
          c_r = c_r + (sec2.c_r - c_r) * sec2.c_a * alpha;
          c_g = c_g + (sec2.c_g - c_g) * sec2.c_a * alpha;
          c_b = c_b + (sec2.c_b - c_b) * sec2.c_a * alpha;
          RGBtoHSV(c_r, c_g, c_b, out c_h, out c_s, out c_v);
        }
      }
      else if (sec2.c_a != 1)
      {
        if ((alpha != 0) && (sec2.c_a != 0))
        {
          c_r = c_r + (sec2.c_r - c_r) * sec2.c_a;
          c_g = c_g + (sec2.c_g - c_g) * sec2.c_a;
          c_b = c_b + (sec2.c_b - c_b) * sec2.c_a;
          RGBtoHSV(c_r, c_g, c_b, out c_h, out c_s, out c_v);
        }
      }
      else
      {
        c_a = sec2.c_a;
        c_r = sec2.c_r;
        c_g = sec2.c_g;
        c_b = sec2.c_b;
        c_h = sec2.c_h;
        c_s = sec2.c_s;
        c_v = sec2.c_v;
      }
    }

    public void Blend(double alpha = 1, double saturation = 1, double value = 1)
    {
      if (alpha < 0) alpha = 0;
      if (alpha > 1) alpha = 1;
      if (saturation < 0) saturation = 0;
      if (saturation > 1) saturation = 1;
      if (value < 0) value = 0;
      if (value > 1) value = 1;

      if (saturation != 1)
      {
        Saturation = Saturation * saturation;
      }
      if (value != 1)
      {
        Value = Value * value;
      }
      if (alpha != 1)
      {
        Alpha = Alpha * alpha;
      }
    }

    public double Blue
    {
      get { return c_b; }
      set { c_b = value; if (c_b < 0) c_b = 0; if (c_b > 1) c_b = 1; RGBtoHSV(c_r, c_g, c_b, out c_h, out c_s, out c_v); }
    }
    public double Red
    {
      get { return c_r; }
      set { c_r = value; if (c_r < 0) c_r = 0; if (c_r > 1) c_r = 1; RGBtoHSV(c_r, c_g, c_b, out c_h, out c_s, out c_v); }
    }
    public double Green
    {
      get { return c_g; }
      set { c_g = value; if (c_g < 0) c_g = 0; if (c_g > 1) c_g = 1; RGBtoHSV(c_r, c_g, c_b, out c_h, out c_s, out c_v); }
    }
    public double Alpha
    {
      get { return c_a; }
      set { c_a = value; if (c_a < 0) c_a = 0; if (c_a > 1) c_a = 1; }
    }
    public double Hue
    {
      get { return c_h; }
      set { c_h = value; if (c_h < 0) c_h = 0; if (c_h > 1) c_h = 1; HSVtoRGB(c_h, c_s, c_v, out c_r, out c_g, out c_b); }
    }
    public double Saturation
    {
      get { return c_s; }
      set { c_s = value; if (c_s < 0) c_s = 0; if (c_s > 1) c_s = 1; HSVtoRGB(c_h, c_s, c_v, out c_r, out c_g, out c_b); }
    }
    public double Value
    {
      get { return c_v; }
      set { c_v = value; if (c_v < 0) c_v = 0; if (c_v > 1) c_v = 1; HSVtoRGB(c_h, c_s, c_v, out c_r, out c_g, out c_b); }
    }

    private
      double c_r, c_g, c_b, c_a, c_h, c_s, c_v;
  }
}
