using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RestFract.Color;

namespace RestFract
{
  public struct ColorLayer
  {
    public ColorLayer(LayerType type,
        DataUsed dataused,
        Interp interp,
        ProcessLayer calcdata,
        IGradientMap gr,
        double param1 = 0,
        double param2 = 0,
        double cycle = 1,
        double alpha = 1,
        double saturation = 1,
        double value = 1,
        LayerExtra alphaextr = LayerExtra.LAYER_EXTRA_NORMAL,
        LayerExtra saturationextr = LayerExtra.LAYER_EXTRA_NORMAL,
        LayerExtra valueextr = LayerExtra.LAYER_EXTRA_NORMAL)
    {
      c_type = type;
      c_dataused = dataused;
      c_interp = interp;
      c_valueextr = valueextr;
      c_saturationextr = saturationextr;
      c_alphaextr = alphaextr;
      c_param1 = param1;
      c_param2 = param2;
      c_value = value;
      c_saturation = saturation;
      c_alpha = alpha;
      c_cycle = cycle;
      c_calcdata = calcdata;
      c_gr = gr;
    }

    public LayerType c_type;
    public DataUsed c_dataused;
    public Interp c_interp;
    public LayerExtra c_valueextr, c_saturationextr, c_alphaextr;
    public double c_param1, c_param2;
    public double c_value, c_saturation, c_alpha, c_cycle;
    public ProcessLayer c_calcdata;
    public IGradientMap c_gr;
  }
}
