using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using RestFract.Color;
using RestFract.Output;
using RestFract.Generators;

namespace RestFract
{
  [Flags]
  public enum LayerType
  {
    //mely teruletekre vonatkozik a szinezes
    LAYER_TYPE_INSIDE = 1,
    LAYER_TYPE_OUTSIDE = 2,
    LAYER_TYPE_BOTH = 3
  }

  [Flags]
  public enum LayerExtra
  {
    LAYER_EXTRA_NORMAL = 1,
    LAYER_EXTRA_INC = 2,
    LAYER_EXTRA_DEC = 4,
    LAYER_EXTRA_LOG = 8
  }

  [Flags]
  public enum DataUsed
  {
    // mely adat alapjan dolgozik a reteg
    LAYER_DATAUSED_ITER = 1,
    LAYER_DATAUSED_VALUE = 2,
    LAYER_DATAUSED_X_REAL = 4,
    LAYER_DATAUSED_X_IMAG = 8,
    LAYER_DATAUSED_X_ARG = 16,
    LAYER_DATAUSED_X_ABS = 32,
    LAYER_DATAUSED_RES_REAL = 64,
    LAYER_DATAUSED_RES_IMAG = 128,
    LAYER_DATAUSED_RES_ARG = 256,
    LAYER_DATAUSED_RES_ABS = 512,
    LAYER_DATAUSED_RES_N = 1024
  }

  [Flags]
  public enum Interp
  {
    //milyen interpolacio tortenik a maximum es a minimum ertek kozott
    LAYER_INTERP_LINEAR = 1,
    LAYER_INTERP_LOG = 2,
    LAYER_INTERP_EXP = 4
  }

  public class ColoredMandel : Mandel
  {
    public ColoredMandel()
      : base()
    {
      c_layers = new List<ColorLayer>();
      c_inscol = new ColorValue(true, 0, 0, 0);
      c_outcol = new ColorValue(true, 1, 1, 1);
    }

    public ColoredMandel(ICalculatorFactory factory) : base(factory) {
      c_layers = new List<ColorLayer>();
      c_inscol = new ColorValue(true, 0, 0, 0);
      c_outcol = new ColorValue(true, 1, 1, 1);
    }

    public override void PutPoint(IMandelOutput Output, int flags, int x, int y)
    {
      ColorValue c;
      bool inside = false;
      foreach (var it in c_layers)
      {
        if ((it.c_calcdata.c_default) && (it.c_calcdata.c_isin)) inside = true;
      }
      int n = 0;
      if (inside) Output.PutPoint(n, flags, x, y, c_inscol); else Output.PutPoint(n, flags, x, y, c_outcol);
      foreach (var it in c_layers)
      {
        n++;

        if (((inside) && it.c_type.HasFlag(LayerType.LAYER_TYPE_INSIDE)) || ((!inside) && it.c_type.HasFlag(LayerType.LAYER_TYPE_OUTSIDE)))
        {
          double bv = -1;
          double min = 0;
          double max = 0;

          switch (it.c_dataused)
          {
            case DataUsed.LAYER_DATAUSED_ITER:
              bv = it.c_calcdata.c_n;
              min = 0;
              max = it.c_calcdata.c_nlimit;
              break;
            case DataUsed.LAYER_DATAUSED_VALUE:
              bv = it.c_calcdata.c_calc;
              min = it.c_param1;
              max = it.c_param2;
              break;
            case DataUsed.LAYER_DATAUSED_X_REAL:
              bv = it.c_calcdata.c_x.Real;
              min = it.c_param1;
              max = it.c_param2;
              break;
            case DataUsed.LAYER_DATAUSED_X_IMAG:
              bv = it.c_calcdata.c_x.Imaginary;
              min = it.c_param1;
              max = it.c_param2;
              break;
            case DataUsed.LAYER_DATAUSED_X_ARG:
              bv = it.c_calcdata.c_x.Phase;
              min = 0;
              max = Math.PI;
              break;
            case DataUsed.LAYER_DATAUSED_X_ABS:
              bv = Complex.Abs(it.c_calcdata.c_x);
              min = it.c_param1;
              max = it.c_param2;
              break;
            case DataUsed.LAYER_DATAUSED_RES_REAL:
              bv = it.c_calcdata.c_resx.Real;
              min = it.c_param1;
              max = it.c_param2;
              break;
            case DataUsed.LAYER_DATAUSED_RES_IMAG:
              bv = it.c_calcdata.c_resx.Imaginary;
              min = it.c_param1;
              max = it.c_param2;
              break;
            case DataUsed.LAYER_DATAUSED_RES_ARG:
              bv = it.c_calcdata.c_resx.Phase;
              min = 0;
              max = Math.PI;
              break;
            case DataUsed.LAYER_DATAUSED_RES_ABS:
              bv = Complex.Abs(it.c_calcdata.c_resx);
              min = it.c_param1;
              max = it.c_param2;
              break;
            case DataUsed.LAYER_DATAUSED_RES_N:
              bv = it.c_calcdata.c_resn;
              min = 0;
              max = it.c_calcdata.c_nlimit;
              break;
          }
          if (it.c_cycle == 1)
          {
            if (min < max)
            {
              if (bv < min) bv = min;
              if (bv > max) bv = max;
            }
            else
            {
              if (bv > min) bv = min;
              if (bv < max) bv = max;
            }
          }
          double value = 0;
          if (bv == min) value = 0;
          else
            if (bv == max) value = 1;
            else
              if (min != 0)
              {
                if (it.c_interp == Interp.LAYER_INTERP_LOG)
                {
                  value = (Math.Log(bv) - Math.Log(min)) / (Math.Log(max) - Math.Log(min)) * it.c_cycle;
                }
                else if (it.c_interp == Interp.LAYER_INTERP_EXP)
                {
                  value = (Math.Exp(bv) - Math.Exp(min)) / (Math.Exp(max) - Math.Exp(min)) * it.c_cycle;
                }
                else
                {
                  value = (bv - min) / (max - min) * it.c_cycle;
                }
              }
              else
              {
                value = (bv - min) / (max - min) * it.c_cycle;
              }
          if (!inside)
          {
            double newalpha = it.c_alpha;
            double newvalue = it.c_value;
            double newsaturation = it.c_saturation;
            double iter = it.c_calcdata.c_n;
            if (newalpha > 0)
            {
              if (it.c_alphaextr.HasFlag(LayerExtra.LAYER_EXTRA_INC))
              {
                if (it.c_alphaextr.HasFlag(LayerExtra.LAYER_EXTRA_LOG)) newalpha = Math.Log(iter) / Math.Log(((double)it.c_calcdata.c_nlimit)) * newalpha;
                else newalpha = (iter / ((double)it.c_calcdata.c_nlimit)) * (newalpha);
              }
              else
                if (it.c_alphaextr.HasFlag(LayerExtra.LAYER_EXTRA_DEC))
                {
                  if (it.c_alphaextr.HasFlag(LayerExtra.LAYER_EXTRA_LOG)) newalpha = newalpha - Math.Log(iter) / Math.Log(((double)it.c_calcdata.c_nlimit)) * newalpha;
                  else newalpha = newalpha - (iter / ((double)it.c_calcdata.c_nlimit)) * newalpha;
                }
            }

            if (newvalue > 0)
            {
              if (it.c_valueextr.HasFlag(LayerExtra.LAYER_EXTRA_INC))
              {
                if (it.c_valueextr.HasFlag(LayerExtra.LAYER_EXTRA_LOG)) newvalue = Math.Log(iter) / Math.Log(((double)it.c_calcdata.c_nlimit)) * newvalue;
                else newvalue = (iter / ((double)it.c_calcdata.c_nlimit)) * (newvalue);
              }
              else
                if (it.c_valueextr.HasFlag(LayerExtra.LAYER_EXTRA_DEC))
                {
                  if (it.c_valueextr.HasFlag(LayerExtra.LAYER_EXTRA_LOG)) newvalue = newvalue - Math.Log(iter) / Math.Log(((double)it.c_calcdata.c_nlimit)) * newvalue;
                  else newvalue = newvalue - (iter / ((double)it.c_calcdata.c_nlimit)) * newvalue;
                }
            }

            if (newsaturation > 0)
            {
              if (it.c_saturationextr.HasFlag(LayerExtra.LAYER_EXTRA_INC))
              {
                if (it.c_saturationextr.HasFlag(LayerExtra.LAYER_EXTRA_LOG)) newsaturation = Math.Log(iter) / Math.Log(((double)it.c_calcdata.c_nlimit)) * newsaturation;
                else newsaturation = (iter / ((double)it.c_calcdata.c_nlimit)) * (newsaturation);
              }
              else
                if (it.c_saturationextr.HasFlag(LayerExtra.LAYER_EXTRA_DEC))
                {
                  if (it.c_saturationextr.HasFlag(LayerExtra.LAYER_EXTRA_LOG)) newsaturation = newsaturation - Math.Log(iter) / Math.Log(((double)it.c_calcdata.c_nlimit)) * newsaturation;
                  else newsaturation = newsaturation - (iter / ((double)it.c_calcdata.c_nlimit)) * newsaturation;
                }
            }
            c = it.c_gr.getPoint(value, it.c_cycle != 1);
            c.Blend(newalpha, newsaturation, newvalue);
            Output.PutPoint(n, flags, x, y, c);
          }
          else
          {
            c = it.c_gr.getPoint(value, it.c_cycle != 1);
            c.Blend(it.c_alpha, it.c_value, it.c_saturation);
            Output.PutPoint(n, flags, x, y, c);
          }
        }
      }

    }

    public virtual ColorValue InsideColor
    {
      get { return c_inscol; }
      set { c_inscol = value; }
    }

    public virtual ColorValue OutsideColor
    {
      get { return c_outcol; }
      set { c_outcol = value; }
    }

    public virtual void AddLayer(ProcessLayer process, ColorLayer layer)
    {
      c_haschanged = true;
      ProcessLayer p = null;
      foreach (var it in c_layers)
      {
        if (process.Similar(it.c_calcdata)) p = it.c_calcdata;
      }
      if (p != null)
      {
        layer.c_calcdata = p;
        c_layers.Add(layer);
      }
      else
      {
        p = process.Clone();
        c_LayerData.Add(p);
        var xx = layer;
        xx.c_calcdata = p;
        c_layers.Add(xx);
      }
    }

    public virtual void ClearLayers() { c_layers.Clear(); }
    public virtual void DeleteLayer(int a)
    {
      c_layers.RemoveAt(a);
    }
    public virtual void ChangeLayer(int a, ProcessLayer process, ColorLayer layer) { }
    public virtual void SwapLayers(int a, int b)
    {
      var x = c_layers[a];
      c_layers[a] = c_layers[b];
      c_layers[b] = x;
    }
    public virtual void setDefaultLayer(int a)
    {
      foreach (var it in c_layers)
      {
        it.c_calcdata.c_default = false;
      }
      c_layers[a].c_calcdata.c_default = true;
    }

    public virtual ColorLayer GetLayer(int a)
    {
      return c_layers[a];
    }

    public virtual List<ColorLayer> getLayers() { return c_layers; }

    private ColorValue c_inscol;
    private ColorValue c_outcol;

    private List<ColorLayer> c_layers;
  }
}