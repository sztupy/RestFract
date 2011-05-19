using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using RestFract.Output;
using RestFract.Generators;
using RestFract.Callbacks;

namespace RestFract
{
  public enum MandelType
  {
    MANDEL_TYPE_MANDEL = 1, // minden pixelnel a pixel koordinataval kezd, es a konstans is a pixel koordinataja lesz
    MANDEL_TYPE_JULIA = 2, // minden pixelnel a pixel koordinatavalkezd, a konstans adott
    MANDEL_TYPE_INVJULIA = 4 // minden pixelnel adott koordinataval kezd, a konstans erteke a pixel koordinataja lesz
  }

  [Flags]
  public enum FractalType
  {
    // fraktal tipusa
    FRACTAL_TYPE_MANDEL = 1,
    FRACTAL_TYPE_MANDEL_N = 2,
    FRACTAL_TYPE_BURNINGSHIP = 4,
    FRACTAL_TYPE_BURNINGSHIP_N = 8,
    FRACTAL_TYPE_NEWTON = 16,

    FRACTAL_TYPE_DIVERGENT = 32768,
    FRACTAL_TYPE_CONVERGENT = 65536,
  }

  abstract public class Mandel
  {
    public Mandel()
    {
      c_cent = 0;
      c_radius = 2;
      c_saveCent = 0;
      c_saveRadius = 2;
      c_type = MandelType.MANDEL_TYPE_MANDEL;
      c_julia = 0;
      c_timeused = 0;
      c_LayerData = new List<ProcessLayer>();
      c_fractaltype = FractalType.FRACTAL_TYPE_MANDEL;
      c_param = 0;
      c_lineprocess = true;
      c_processnum = 1;
      c_function = "";
      c_haschanged = true;
      c_calc = null;
      c_LayerDataHash = 0;
      c_factory = null;
    }

    public Mandel(ICalculatorFactory factory) : this() {
      c_factory = factory;
    }

    virtual public void Draw(IMandelOutput Output, int c_width, int c_height)
    {
      int starttime = System.Environment.TickCount;
      Output.InitDraw();
      double ival = c_radius / (c_width / 2);
      Complex c_begin = new Complex(c_cent.Real - c_radius, c_cent.Imaginary - ival * (c_height / 2));
      
      if (c_haschanged || (c_LayerDataHash != c_LayerData.GetHashCode()) || (c_calc == null))
      {
        foreach (var it in c_LayerData) if (it.c_default)
        {
          c_calc = c_factory.GenFractalCalc(c_LayerData, c_fractaltype, c_function, it);
          break;
        }
        c_haschanged = false;
        c_LayerDataHash = c_LayerData.GetHashCode();
      }

      c_calc.InitData(c_LayerData, c_param,c_lineprocess?c_width:c_width*c_height);

    if (c_lineprocess) { // soronkenti feldologozas
      // elkuldjuk a szamokat
      for (int y=0; y<c_height; y++) {
        for (int x=0; x<c_width; x++) {
          Complex pos = new Complex(c_begin.Real+x*ival,c_begin.Imaginary+y*ival);
          if (c_type==MandelType.MANDEL_TYPE_MANDEL) {
            c_calc.AddPoint(x,y,pos,pos);
          } else if (c_type==MandelType.MANDEL_TYPE_INVJULIA) { 
            c_calc.AddPoint(x,y,c_julia,pos);
          } else {
            c_calc.AddPoint(x,y,pos,c_julia);
          }
        }
        c_calc.EndSend();
        for (int x=0; x<c_width; x++) {
          int px,py;
          List<ProcessLayer> pl;
          c_calc.GetPoint(out px, out py, out pl);
          for (int i = 0; i < c_LayerData.Count; i++)
            c_LayerData[i].LoadFrom(pl[i]);
          PutPoint(Output, 1, px, py);
          if (c_callback != null) c_callback.SetPoint(x, y);
        }
        c_calc.EndGet(y==c_height-1);
        Output.NextLine(1, y+1);
        if (c_callback != null) c_callback.SetLine(y+1);
      }
    } else { // globalis feldolgozas -- sok adat eseten hamar fagyashoz vezethet (szekvencialis programnal 2000x2000-es meret felett, parhuzamos esetben mar 400x400 meret korul is gondjaink lesznek, legalabbis adatcsatornas modszer alkalmazasa eseten
      // elkuldjuk a szamokat
      for (int y=0; y<c_height; y++) {
        for (int x=0; x<c_width; x++) {
          Complex pos = new Complex(c_begin.Real+x*ival,c_begin.Imaginary+y*ival);
          if (c_type == MandelType.MANDEL_TYPE_MANDEL)
          {
            c_calc.AddPoint(x, y, pos, pos);
          }
          else if (c_type == MandelType.MANDEL_TYPE_INVJULIA)
          {
            c_calc.AddPoint(x, y, c_julia, pos);
          }
          else
          {
            c_calc.AddPoint(x, y, pos, c_julia);
          }
        }
      }
      c_calc.EndSend();
      // fogadjuk a szamokat  
      for (int y=0; y<c_height; y++) {
        for (int x=0; x<c_width; x++) {
          int px, py;
          List<ProcessLayer> pl;
          c_calc.GetPoint(out px, out py, out pl);
          for (int i = 0; i < c_LayerData.Count; i++)
            c_LayerData[i].LoadFrom(pl[i]);
          PutPoint(Output, 1, px, py);
          if (c_callback != null) c_callback.SetPoint(x, y);
        }
        Output.NextLine(0, y+1);
        if (c_callback != null) c_callback.SetLine(y + 1);
      }
      c_calc.EndGet(true);
    }  
      
      Output.EndDraw();

      int endtime = System.Environment.TickCount;
      c_timeused = ((double)(endtime - starttime)) / 1000.0;
    }

    abstract public void PutPoint(IMandelOutput Output, int flags, int x, int y);

    virtual public void setBounds(Complex center, double radius) { c_cent = center; c_radius = radius; } // intervallum beallitasa  
    public Complex Center
    {
      get { return c_cent; }
    }
    public double Radius
    {
      get { return c_radius; }
    }

    public ICalculatorFactory Factory
    {
      get { return c_factory; }
      set { c_factory = value; c_haschanged = true; c_calc = null; }
    }

    public MandelType Type
    {
      get { return c_type; }
      set
      {
        c_haschanged = true;
        if ((c_type == MandelType.MANDEL_TYPE_MANDEL) && (value != MandelType.MANDEL_TYPE_MANDEL))
        {
          c_saveCent = c_cent;
          c_saveRadius = c_radius;

          c_julia = c_cent;

          c_cent = new Complex(0, 0);
          c_radius = 2;
        }
        if ((c_type != MandelType.MANDEL_TYPE_MANDEL) && (value == MandelType.MANDEL_TYPE_MANDEL))
        {
          c_cent = c_saveCent;
          c_radius = c_saveRadius;
        }
        c_type = value;
      }
    }

    public virtual Complex JuliaCenter
    {
      set { c_julia = value; }
      get { return c_julia; }
    }

    virtual public double getLastTimeUsed() { return c_timeused; }

    public virtual bool ProcessType
    {
      get { return c_lineprocess; }
      set { c_lineprocess = value; }
    }

    public virtual FractalType Fractal
    {
      set { c_fractaltype = value; c_haschanged = true; }
      get { return c_fractaltype; }
    }

    public virtual double FractalParam
    {
      set { c_param = value; }
      get { return c_param; }
    }

    public virtual ICalculationCallback Callback
    {
      set { c_callback = value; }
      get { return c_callback; }
    }

    virtual public bool setProcessNum(int processnum) { return false; }
    virtual public int getProcessNum() { return c_processnum; }

    virtual public bool setFunction(string function)
    {
      c_function = function;
      c_haschanged = true;
      return true;
    }
    virtual public string getFunction() { return c_function; }



    private Complex c_cent; // kepernyo kozepe
    private double c_radius; // sugara

    private Complex c_saveCent; // elmentett koordinatak (Mandel->Julia valtashoz)
    double c_saveRadius;

    private MandelType c_type; // Mandel vagy Julia mod

    private Complex c_julia; // Julia kozeppontja

    double c_timeused; // felhasznalt ido

    protected List<ProcessLayer> c_LayerData;
    private int c_LayerDataHash;

    private FractalType c_fractaltype;
    private double c_param;

    private ICalculator c_calc;
    private ICalculatorFactory c_factory;

    private ICalculationCallback c_callback;

    private bool c_lineprocess; // soronkent dolgozzon, vagy egybe az egeszet (tobb processzor eseten)
    private int c_processnum; // processzorok szama
    string c_function; // egyeb fuggveny definicioja

    protected bool c_haschanged; // tortent-e nagyobb valtozas ami szuksgesse teszi a kliensek ujrageneralasat
//    int c_oldprecision; // processzorszamvaltasnal a regi processzorok szama
  }
}