using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.CodeDom;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Reflection;
using System.IO;

namespace RestFract.Generators
{
  public interface IComplexEval
  {
    Complex eval(Complex x, Complex c, double n, double p);
  }

  public class SimpleCalculatorFactory : ICalculatorFactory
  {
    static double absval(double x) { return (x >= 0) ? (x) : (-x); }
    static double norm(Complex c) { return c.Real * c.Real + c.Imaginary * c.Imaginary; }

    static Complex Fractal_Mandel(Complex x, Complex c)
    {
      double sx = x.Real;
      double sy = x.Imaginary;
      return new Complex(sx * sx - sy * sy + c.Real, 2 * sx * sy + c.Imaginary);
    }

    static Complex Fractal_Mandel_n(Complex x, Complex c, double n)
    {
      return Complex.Pow(x, n) + c;
    }

    static Complex Fractal_BurningShip(Complex x, Complex c)
    {
      double sx = x.Real;
      double sy = x.Imaginary;
      return new Complex(sx * sx - sy * sy + c.Real, 2 * absval(sx * sy) + c.Imaginary);
    }

    static Complex Fractal_BurningShip_n(Complex x, Complex c, double n)
    {
      return Complex.Pow(new Complex(absval(x.Real), absval(x.Imaginary)), n) + c;
    }

    private class SimpleCalculator : ICalculator
    {
      private List<ProcessLayer> _ld;
      private double _param;
      private Queue<Tuple<int, int, Complex, Complex>> _l;
      private FractalType fractaltype;
      private IComplexEval code;
      private ProcessLayer deflayer;

      public SimpleCalculator(FractalType fractaltype, string code, ProcessLayer deflayer)
      {
        this.fractaltype = fractaltype;
        if (fractaltype == FractalType.FRACTAL_TYPE_CONVERGENT || fractaltype == FractalType.FRACTAL_TYPE_DIVERGENT)
        {
          Microsoft.CSharp.CSharpCodeProvider cp = new Microsoft.CSharp.CSharpCodeProvider();
          System.CodeDom.Compiler.CompilerParameters cpar = new System.CodeDom.Compiler.CompilerParameters();
          cpar.GenerateInMemory = true;
          cpar.GenerateExecutable = false;
          cpar.ReferencedAssemblies.Add("system.dll");
          cpar.ReferencedAssemblies.Add(System.Reflection.Assembly.GetExecutingAssembly().Location);
          cpar.ReferencedAssemblies.Add("system.core.dll");
          cpar.ReferencedAssemblies.Add("system.numerics.dll");
          string src = "using System;using System.Numerics;using RestFract;using RestFract.Generators;class evalclass:IComplexEval {" +
                       "public evalclass(){} public Complex eval(Complex x, Complex c, double n, double p)" +
                       "{ return " + code + "; } }";
          System.CodeDom.Compiler.CompilerResults cr = cp.CompileAssemblyFromSource(cpar, src);
          if (cr.Errors.Count == 0 && cr.CompiledAssembly != null)
          {
            Type ObjType = cr.CompiledAssembly.GetType("evalclass");
            try
            {
              if (ObjType != null)
              {
                this.code = (IComplexEval)Activator.CreateInstance(ObjType);
              }
            }
            catch (Exception e)
            {
              this.code = null;
              throw new NotImplementedException("Could not compile code",e);
            }
          }
          else
          {
            this.code = null;
            throw new NotImplementedException("Could not compile code: "+cr.Errors[0]);
          }
        }
        this.deflayer = deflayer;
      }

      public void InitData(List<ProcessLayer> LayerData, double param, long count)
      {
        _ld = LayerData;
        _param = param;
        _l = new Queue<Tuple<int, int, Complex, Complex>>();
      }

      public void AddPoint(int px, int py, Complex x, Complex c)
      {
        _l.Enqueue(Tuple.Create(px, py, x, c));
      }

      public bool GetPoint(out int px, out int py, out List<ProcessLayer> LayerData)
      {
        if (_l.Count == 0) { px = 0; py = 0; LayerData = null; return false; }
        var t = _l.Dequeue();
        px = t.Item1;
        py = t.Item2;
        Complex x = t.Item3;
        Complex c = t.Item4;
        LayerData = _ld;

        CalcFractal(x, c, LayerData, _param);
        return true;
      }

      public void EndSend()
      {
      }

      public void EndGet(bool final)
      {
      }

      private void CalcFractal(Complex x, Complex c, List<ProcessLayer> LayerData, double param)
      {
        bool fractdiv = true;
        bool hastriangle = false;
        double trinorm = 0;

        Complex sumx = 0, meanx = 0, varsx = 0, variacex = 0, sdx = 0, minx = 0, maxx = 0, deltax = 0;
        SeqType modesused = 0;

        foreach (var it in LayerData)
        {
          it.c_active = true;
          it.c_isin = false;
          if (it.c_default) deflayer = it;
          if (it.c_checktype.HasFlag(SeqCheck.MPL_CHECK_TRIANGLE)) hastriangle = true;
          if (it.c_checktype.HasFlag(SeqCheck.MPL_CHECK_TRIANGLE_SMOOTH)) hastriangle = true;
          modesused |= it.c_seqtype;
          it.c_x = x;
          it.c_oldx = x;
          it.c_old2x = x;
          it.c_calc = 0;
          it.c_cmean = 0;
          it.c_cvarsx = 0;
          it.c_cvariance = 0;
        }

        if (modesused.HasFlag(SeqType.MPL_SEQ_STDDEV)) modesused |= SeqType.MPL_SEQ_VARIANCE;
        if (modesused.HasFlag(SeqType.MPL_SEQ_VARIANCE)) modesused |= SeqType.MPL_SEQ_VARSX;
        if (modesused.HasFlag(SeqType.MPL_SEQ_VARSX)) modesused |= SeqType.MPL_SEQ_MEAN;

        if (hastriangle)
        {
          if (fractaltype == FractalType.FRACTAL_TYPE_MANDEL)
          {
            trinorm = Complex.Abs(c);
          }
          else
          {
            trinorm = norm(c);
          }
        }

        if (deflayer != null)
        {
          int n = 0;
          bool end = false;
          Complex newx = x;
          while (!end)
          {
            n++;
            switch (fractaltype)
            {
              case FractalType.FRACTAL_TYPE_MANDEL:
                newx = Fractal_Mandel(x, c);
                break;
              case FractalType.FRACTAL_TYPE_MANDEL_N:
                newx = Fractal_Mandel_n(x, c, param);
                break;
              case FractalType.FRACTAL_TYPE_BURNINGSHIP:
                newx = Fractal_BurningShip(x, c);
                break;
              case FractalType.FRACTAL_TYPE_BURNINGSHIP_N:
                newx = Fractal_BurningShip_n(x, c, param);
                break;
              case FractalType.FRACTAL_TYPE_DIVERGENT:
                if (code != null)
                {
                  newx = code.eval(x, c, n, param);
                }
                break;
              case FractalType.FRACTAL_TYPE_CONVERGENT:
                fractdiv = false;
                if (code != null)
                {
                  newx = code.eval(x, c, n, param);
                }
                break;
              default:
                newx = x;
                break;
            }
            if (modesused.HasFlag(SeqType.MPL_SEQ_SUM)) sumx += newx;
            if (modesused.HasFlag(SeqType.MPL_SEQ_MEAN))
            {
              Complex delta = newx - meanx;
              meanx = meanx + delta / (double)n;
              if (modesused.HasFlag(SeqType.MPL_SEQ_VARSX))
              {
                varsx = varsx + delta * (newx - meanx);
                if (modesused.HasFlag(SeqType.MPL_SEQ_VARIANCE))
                {
                  if (n != 1)
                  {
                    variacex = varsx / ((double)n - (double)1);
                    if (modesused.HasFlag(SeqType.MPL_SEQ_STDDEV))
                    {
                      sdx = Complex.Sqrt(variacex);
                    }
                  }
                }
              }
            }
            if (modesused.HasFlag(SeqType.MPL_SEQ_MIN)) if (n == 1) minx = newx; else if (Complex.Abs(newx) < Complex.Abs(minx)) minx = newx;
            if (modesused.HasFlag(SeqType.MPL_SEQ_MAX)) if (n == 1) maxx = newx; else if (Complex.Abs(newx) > Complex.Abs(maxx)) maxx = newx;
            if (modesused.HasFlag(SeqType.MPL_SEQ_DELTA)) deltax = newx - x;
            foreach (var p in LayerData)
            {
              if (p.c_active)
              {
                p.c_n = n;
                p.c_old2x = p.c_oldx;
                p.c_oldx = p.c_x;
                switch (p.c_seqtype)
                {
                  case SeqType.MPL_SEQ_NORMAL: p.c_x = newx; break;
                  case SeqType.MPL_SEQ_SUM: p.c_x = sumx; break;
                  case SeqType.MPL_SEQ_MEAN: p.c_x = meanx; break;
                  case SeqType.MPL_SEQ_VARSX: p.c_x = varsx; break;
                  case SeqType.MPL_SEQ_VARIANCE: p.c_x = variacex; break;
                  case SeqType.MPL_SEQ_STDDEV: p.c_x = sdx; break;
                  case SeqType.MPL_SEQ_MIN: p.c_x = minx; break;
                  case SeqType.MPL_SEQ_MAX: p.c_x = maxx; break;
                  case SeqType.MPL_SEQ_DELTA: p.c_x = deltax; break;
                  default: p.c_x = newx; break;
                }
                double newd = 0;
                switch (p.c_checktype)
                {
                  case SeqCheck.MPL_CHECK_SMOOTH:
                    if (fractdiv)
                    {
                      newd = Math.Exp(-Complex.Abs(p.c_x));
                    }
                    else
                    {
                      newd = Math.Exp(-Complex.Abs(p.c_x - p.c_oldx));
                    }
                    break;
                  case SeqCheck.MPL_CHECK_REAL:
                    newd = p.c_x.Real;
                    break;
                  case SeqCheck.MPL_CHECK_IMAG:
                    newd = p.c_x.Imaginary;
                    break;
                  case SeqCheck.MPL_CHECK_ARG:
                    newd = p.c_x.Phase;
                    break;
                  case SeqCheck.MPL_CHECK_ABS:
                    newd = p.c_x.Magnitude;
                    break;
                  case SeqCheck.MPL_CHECK_CURVATURE:
                    {
                      if ((p.c_oldx != p.c_old2x))
                        newd = Complex.Abs(Complex.Atan((p.c_x - p.c_oldx) / (p.c_oldx - p.c_old2x)));
                      else newd = 0;
                    }
                    break;
                  case SeqCheck.MPL_CHECK_TRIANGLE:
                    if (fractaltype == FractalType.FRACTAL_TYPE_MANDEL)
                    {
                      double newxnorm = norm(p.c_oldx);
                      double lowbound = absval(newxnorm - trinorm);
                      if ((newxnorm + trinorm - lowbound) == 0) newd = 0;
                      else
                        newd = (p.c_x.Magnitude - lowbound) / (newxnorm + trinorm - lowbound);
                    }
                    else
                    {
                      double newxnorm = p.c_x.Magnitude;
                      double lowbound = absval(newxnorm - trinorm);
                      if ((newxnorm + trinorm - lowbound) == 0) newd = 0;
                      else
                        newd = ((Complex.Abs(p.c_x - c) - lowbound) / (newxnorm + trinorm - lowbound));
                    }
                    break;
                  case SeqCheck.MPL_CHECK_TRIANGLE_SMOOTH:
                    if (fractaltype == FractalType.FRACTAL_TYPE_MANDEL)
                    {
                      double newxnorm = norm(p.c_oldx);
                      double lowbound = absval(newxnorm - trinorm);
                      if ((newxnorm + trinorm - lowbound) == 0) newd = 0;
                      else
                        newd = (Complex.Abs(p.c_x) - lowbound) / (newxnorm + trinorm - lowbound);
                    }
                    else
                    {
                      double newxnorm = p.c_x.Magnitude;
                      double lowbound = absval(newxnorm - trinorm);
                      if ((newxnorm + trinorm - lowbound) == 0) newd = 0;
                      else
                        newd = ((Complex.Abs(p.c_x - c) - lowbound) / (newxnorm + trinorm - lowbound));
                    }
                    break;
                  case SeqCheck.MPL_CHECK_ORBIT_TRAP:
                    switch (p.c_orbittraptype)
                    {
                      case OrbitTrap.MPL_ORBIT_TRAP_POINT:
                        newd = Complex.Abs(p.c_x - p.c_pointA);
                        break;
                      case OrbitTrap.MPL_ORBIT_TRAP_LINE:
                        if ((p.c_pointA.Real) == 1)
                        {
                          newd = absval(p.c_x.Real);
                        }
                        else
                        {
                          newd = absval(p.c_x.Imaginary);
                        }
                        break;
                      case OrbitTrap.MPL_ORBIT_TRAP_GAUSS:
                        {
                          Complex gauss = new Complex(Math.Round(p.c_x.Real), Math.Round(p.c_x.Imaginary));
                          newd = Complex.Abs(gauss - p.c_x);
                        }
                        break;
                    }
                    break;
                }
                switch (p.c_checkseqtype)
                {
                  case SeqType.MPL_SEQ_NORMAL: p.c_calc = newd; break;
                  case SeqType.MPL_SEQ_SUM: p.c_calc += newd; break;
                  case SeqType.MPL_SEQ_MEAN: p.c_calc += newd; break;
                  case SeqType.MPL_SEQ_VARSX:
                    {
                      double delta = newd - p.c_cmean;
                      p.c_cmean = p.c_cmean + delta / p.c_n;
                      p.c_calc += delta * (newd - p.c_cmean);
                    }
                    break;
                  case SeqType.MPL_SEQ_VARIANCE:
                    {
                      double delta = newd - p.c_cmean;
                      p.c_cmean = p.c_cmean + delta / p.c_n;
                      p.c_cvarsx = p.c_cvarsx + delta * (newd - p.c_cmean);
                      if (p.c_n != 1)
                      {
                        p.c_calc = p.c_cvarsx / (p.c_n - 1.0);
                      }
                    }
                    break;
                  case SeqType.MPL_SEQ_STDDEV:
                    {
                      double delta = newd - p.c_cmean;
                      p.c_cmean = p.c_cmean + delta / p.c_n;
                      p.c_cvarsx = p.c_cvarsx + delta * (newd - p.c_cmean);
                      if (p.c_n != 1)
                      {
                        p.c_cvariance = p.c_cvarsx / (p.c_n - 1.0);
                      }
                      p.c_calc = Math.Sqrt(p.c_cvariance);
                    }
                    break;
                  case SeqType.MPL_SEQ_MIN: if (p.c_n == 1) p.c_calc = newd; else if (p.c_calc > newd) { p.c_calc = newd; p.c_resx = p.c_x; p.c_resn = p.c_n; } break;
                  case SeqType.MPL_SEQ_MAX: if (p.c_n == 1) p.c_calc = newd; else if (p.c_calc < newd) { p.c_calc = newd; p.c_resx = p.c_x; p.c_resn = p.c_n; } break;
                  case SeqType.MPL_SEQ_DELTA: p.c_calc = newd - p.c_calc; break;
                  default: p.c_calc = newd; break;
                }
                if (p.c_convchktype == ConvCheck.MPL_CONVCHK_REAL)
                {
                  double ddd = p.c_x.Real * p.c_x.Real;
                  if ((fractdiv) && (ddd > p.c_bailout)) p.c_active = false;
                  if (!(fractdiv) && (ddd < p.c_bailout)) p.c_active = false;
                }
                else if (p.c_convchktype == ConvCheck.MPL_CONVCHK_IMAG)
                {
                  double ddd = p.c_x.Imaginary * p.c_x.Imaginary;
                  if ((fractdiv) && (ddd > p.c_bailout)) p.c_active = false;
                  if (!(fractdiv) && (ddd < p.c_bailout)) p.c_active = false;
                }
                else if (p.c_convchktype == ConvCheck.MPL_CONVCHK_OR)
                {
                  if ((fractdiv) && ((p.c_x.Real * p.c_x.Real > p.c_bailout) || (p.c_x.Imaginary * p.c_x.Imaginary > p.c_bailout))) p.c_active = false;
                  if (!(fractdiv) && ((p.c_x.Real * p.c_x.Real < p.c_bailout) || (p.c_x.Imaginary * p.c_x.Imaginary < p.c_bailout))) p.c_active = false;
                }
                else if (p.c_convchktype == ConvCheck.MPL_CONVCHK_AND)
                {
                  if ((fractdiv) && ((p.c_x.Real * p.c_x.Real > p.c_bailout) && (p.c_x.Imaginary * p.c_x.Imaginary > p.c_bailout))) p.c_active = false;
                  if (!(fractdiv) && ((p.c_x.Real * p.c_x.Real < p.c_bailout) && (p.c_x.Imaginary * p.c_x.Imaginary < p.c_bailout))) p.c_active = false;
                }
                else if (p.c_convchktype == ConvCheck.MPL_CONVCHK_MANH)
                {
                  double ddd = (absval(p.c_x.Imaginary) + absval(p.c_x.Real)) * (absval(p.c_x.Imaginary) + absval(p.c_x.Real));
                  if ((fractdiv) && (ddd > p.c_bailout)) p.c_active = false;
                  if (!(fractdiv) && (ddd < p.c_bailout)) p.c_active = false;
                }
                else if (p.c_convchktype == ConvCheck.MPL_CONVCHK_MANR)
                {
                  double ddd = (p.c_x.Real + p.c_x.Imaginary) * (p.c_x.Real + p.c_x.Imaginary);
                  if ((fractdiv) && (ddd > p.c_bailout)) p.c_active = false;
                  if (!(fractdiv) && (ddd < p.c_bailout)) p.c_active = false;
                }
                else
                {
                  double ddd = norm(p.c_x);
                  if ((fractdiv) && (ddd > p.c_bailout)) p.c_active = false;
                  if (!(fractdiv) && (ddd < p.c_bailout)) p.c_active = false;
                }
                if (p.c_n > p.c_nlimit) { p.c_active = false; p.c_isin = true; }
                if (p.c_active == false)
                {
                  if (p.c_checktype == SeqCheck.MPL_CHECK_TRIANGLE_SMOOTH)
                  {
                    if (!p.c_isin)
                    {
                      p.c_oldx = p.c_x;
                      p.c_x = Fractal_Mandel(p.c_x, c);
                      p.c_n++;
                      double newxnorm = norm(p.c_oldx);
                      double lowbound = absval(newxnorm - trinorm);
                      if ((newxnorm + trinorm - lowbound) == 0) newd = 0;
                      else
                        newd = (p.c_x.Magnitude - lowbound) / (newxnorm + trinorm - lowbound);
                      p.c_calc += newd;
                      double oldsum = p.c_calc / (p.c_n + 1);
                      double il2 = 1 / Math.Log(2);
                      double lp = Math.Log(Math.Log(p.c_bailout));
                      double f = il2 * lp - il2 * Math.Log(Math.Log(Complex.Abs(p.c_x))) + 2;
                      double az2 = norm(p.c_x);
                      p.c_oldx = p.c_x;
                      p.c_x = Fractal_Mandel(p.c_oldx, c);
                      lowbound = absval(az2 - trinorm);
                      if ((az2 + trinorm - lowbound) != 0) p.c_calc += (Complex.Abs(p.c_x) - lowbound) / (az2 + trinorm - lowbound);
                      p.c_n++;
                      p.c_calc = p.c_calc / (p.c_n + 1);
                      p.c_calc = oldsum + (p.c_calc - oldsum) * (f - 1);
                    }
                    else
                    {
                      p.c_calc /= p.c_n + 1;
                    }
                  }
                  else if (p.c_checkseqtype == SeqType.MPL_SEQ_MEAN)
                  {
                    p.c_calc /= p.c_n + 1;
                  }
                }
              }
            }
            x = newx;
            if (!deflayer.c_active)
            {
              end = true;
            }
          }
        }
        else
        {
          throw new NotImplementedException("No default layer found");
        }
      }
    }

    public ICalculator GenFractalCalc(List<ProcessLayer> LayerData, FractalType fractaltype, string code, ProcessLayer deflayer)
    {
      return new SimpleCalculator(fractaltype, code, deflayer);
    }
  }
}
