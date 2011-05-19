using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using Cloo;
using System.Runtime.InteropServices;
using System.Globalization;

namespace RestFract.Generators.OpenCL
{
  public class OpenCLCalculatorFactory : ICalculatorFactory
  {
    [StructLayout(LayoutKind.Sequential)]
    struct float2
    {
      public float x;
      public float y;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PrProcessLayer
    {
      public float2 c_old2x;
      public float2 c_oldx;
      public float2 c_x;
      public float2 c_resx;
      public float c_calc;
      public float c_cmean;
      public float c_cvarsx;
      public float c_cvariance;
      public int c_active;
      public int c_isin;
      public int c_n;
      public int c_resn;
    };

    class OpenCLCalculator : ICalculator
    {
      ComputeContext _context;
      ComputeProgram _prg;
      ComputeKernel _krnl;
      float _param;
      long _count;
      List<Tuple<int, int, Complex, Complex>> points;
      Queue<Tuple<int, int, List<ProcessLayer>>> output;
      List<ProcessLayer> _ld;

      float2[] inx;
      float2[] inc;
      PrProcessLayer[][] opl;

      ComputeBuffer<float2> x;
      ComputeBuffer<float2> c;
      ComputeBuffer<PrProcessLayer>[] outp;

      public OpenCLCalculator(ComputeContext context, ComputeProgram prg, ComputeKernel krnl)
      {
        _context = context;
        _prg = prg;
        _krnl = krnl;
      }

      unsafe public void InitData(List<ProcessLayer> LayerData, double param, long count)
      {
        _param = (float)param;
        _ld = LayerData;
        _count = count;
        points = new List<Tuple<int, int, Complex, Complex>>();
        long n = count;
        inx = new float2[n];
        inc = new float2[n];
        opl = new PrProcessLayer[_ld.Count][];
        for (int i = 0; i < _ld.Count; i++) opl[i] = new PrProcessLayer[n];
        x = new ComputeBuffer<float2>(_context, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.AllocateHostPointer, n);
        c = new ComputeBuffer<float2>(_context, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.AllocateHostPointer, n);
        outp = new ComputeBuffer<PrProcessLayer>[_ld.Count];
        for (int i = 0; i < _ld.Count; i++) outp[i] = new ComputeBuffer<PrProcessLayer>(_context, ComputeMemoryFlags.WriteOnly, n);
      }

      public void AddPoint(int px, int py, Complex x, Complex c)
      {
        points.Add(Tuple.Create(px, py, x, c));
      }

      public bool GetPoint(out int px, out int py, out List<ProcessLayer> LayerData)
      {
        if (output.Count>0) {
          var t = output.Dequeue();
          px = t.Item1;
          py = t.Item2;
          LayerData = t.Item3;
          return true;
        } else {
          px = 0;
          py = 0;
          LayerData = null;
          return false;
        }
      }

      unsafe public void EndSend()
      {
        for (int i = 0; i < points.Count; i++)
        {
          inx[i].x = (float)points[i].Item3.Real;
          inx[i].y = (float)points[i].Item3.Imaginary;
          inc[i].x = (float)points[i].Item4.Real;
          inc[i].y = (float)points[i].Item4.Imaginary;
        }

        _krnl.SetMemoryArgument(0, x);
        _krnl.SetMemoryArgument(1, c);
        for (int i = 0; i < _ld.Count; i++)
        {
          _krnl.SetMemoryArgument(2 + i, outp[i]);
        }
        
        ComputeCommandQueue command = new ComputeCommandQueue(_context, _context.Devices[0], ComputeCommandQueueFlags.None);
        command.WriteToBuffer(inx, x, false, null);
        command.WriteToBuffer(inc, c, false, null);

        command.Execute(_krnl, null, new long[] { points.Count }, null, null);

        for (int i = 0; i < _ld.Count; i++)
          command.ReadFromBuffer(outp[i], ref opl[i], false, null);

        command.Finish();

        output = new Queue<Tuple<int, int, List<ProcessLayer>>>();

        for (int i = 0; i < points.Count; i++)
        {
          List<ProcessLayer> pl = new List<ProcessLayer>();
          for (int ii = 0; ii < _ld.Count; ii++)
          {
            ProcessLayer p = _ld[ii].Clone();
            p.c_active = opl[ii][i].c_active != 0;
            p.c_calc = opl[ii][i].c_calc;
            p.c_cmean = opl[ii][i].c_cmean;
            p.c_cvariance = opl[ii][i].c_cvariance;
            p.c_cvarsx = opl[ii][i].c_cvarsx;
            p.c_isin = opl[ii][i].c_isin != 0;
            p.c_n = opl[ii][i].c_n;
            p.c_old2x = new Complex(opl[ii][i].c_old2x.x,opl[ii][i].c_old2x.y);
            p.c_oldx = new Complex(opl[ii][i].c_oldx.x,opl[ii][i].c_oldx.y);
            p.c_resn = opl[ii][i].c_resn;
            p.c_resx = new Complex(opl[ii][i].c_resx.x,opl[ii][i].c_resx.y);
            p.c_x = new Complex(opl[ii][i].c_x.x,opl[ii][i].c_x.y);
            pl.Add(p);
          }
          output.Enqueue(Tuple.Create(points[i].Item1, points[i].Item2, pl));
        }
      }

      public void EndGet(bool final)
      {
        points = new List<Tuple<int, int, Complex, Complex>>();
      }
    }

    private ComputeContext _context;

    public OpenCLCalculatorFactory(ComputeContext context)
    {
      _context = context;
    }

    public ICalculator GenFractalCalc(List<ProcessLayer> LayerData, FractalType fractaltype, string code, ProcessLayer deflayer)
    {
      string macros = @"
#pragma OPENCL EXTENSION cl_amd_printf : enable

inline float ABS(float a) {
  return a>0?a:-a;
}

inline float ARGC(float2 a) {
  return atan2(a.y,a.x);
}

inline float NORM(float2 a) {
  return a.x*a.x+a.y*a.y;
}

inline float ABSC(float2 a) {
  return sqrt(NORM(a));
}

inline float2 MULC(float2 a, float2 b) {
  return (float2)( a.x*b.x-a.y*b.y, a.y*b.x+a.x*b.y  );
}

inline float2 DIVC(float2 a, float2 b) {
  return (float2)( (a.x*b.x+a.y*b.y)/(b.x*b.x+b.y*b.y), (a.y*b.x-a.x*b.y)/(b.x*b.x+b.y*b.y)  );
}

inline float2 lnc(float2 c) {
  float r = ABSC(c);
  float a = ARGC(c);
  return (float2)(log(r),a);
}

inline float2 arctanc(float2 c) {
  float2 io = (float2)(0.0f,1.0f);
  float2 two = (float2)(2.0f,0.0f);
  float2 one = (float2)(1.0f,0.0f);
  
  return (float2)(MULC(DIVC(io,two),lnc(one - MULC(io,c))-lnc(one + MULC(io,c))));
}

inline float2 powc(float2 c, float p) {
  if (NORM(c)==0) {
    return (float2)(0.0f,0.0f);
  } else {
    float r = pow(ABSC(c),p);
    float a = ARGC(c)*p;
    return (float2)(r*cos(a),r*sin(a));
  }
}

struct ProcessLayer {
     float2 c_old2x;
     float2 c_oldx;
     float2 c_x;
     float2 c_resx;
     float c_calc;
     float c_cmean;
     float c_cvarsx;
     float c_cvariance;
     int c_active;
     int c_isin;
     int c_n;
     int c_resn;
};

kernel void FractalCalc (
    global  read_only float2* in_x,
    global  read_only float2* in_c,
";

      StringBuilder kernel = new StringBuilder(macros);

      for (int i=0; i< LayerData.Count; i++) {
        kernel.Append("    global write_only struct ProcessLayer* out_p" + i);
        kernel.Append(i+1==LayerData.Count ? "\n){" : ",\n");
      }

      bool hastriangle = false;
      bool fractdiv = true;
      SeqType modesused = 0;

      foreach (var it in LayerData)
      {
        if (it.c_checktype.HasFlag(SeqCheck.MPL_CHECK_TRIANGLE)) hastriangle = true;
        if (it.c_checktype.HasFlag(SeqCheck.MPL_CHECK_TRIANGLE_SMOOTH)) hastriangle = true;
        modesused |= it.c_seqtype;
      }

      if (modesused.HasFlag(SeqType.MPL_SEQ_STDDEV)) modesused |= SeqType.MPL_SEQ_VARIANCE;
      if (modesused.HasFlag(SeqType.MPL_SEQ_VARIANCE)) modesused |= SeqType.MPL_SEQ_VARSX;
      if (modesused.HasFlag(SeqType.MPL_SEQ_VARSX)) modesused |= SeqType.MPL_SEQ_MEAN;

      kernel.Append("float2 sumx = (float2)(0.0f,0.0f);");
      kernel.Append("float2 meanx = (float2)(0.0f,0.0f);");
      kernel.Append("float2 varsx = (float2)(0.0f,0.0f);");
      kernel.Append("float2 variacex = (float2)(0.0f,0.0f);");
      kernel.Append("float2 sdx = (float2)(0.0f,0.0f);");
      kernel.Append("float2 minx = (float2)(0.0f,0.0f);");
      kernel.Append("float2 maxx = (float2)(0.0f,0.0f);");
      kernel.Append("float2 deltax = (float2)(0.0f,0.0f);");
      kernel.Append("float2 deltac = (float2)(0.0f,0.0f);");

      kernel.Append("float delta = 0.0f;");
      kernel.Append("float newxnorm = 0.0f;");
      kernel.Append("float lowbound = 0.0f;");
      kernel.Append("float newd = 0.0f;");

      kernel.Append("int end = 0;");
      kernel.Append("int n = 0;");
      kernel.Append("float2 newx = (float2)(0.0f,0.0f);");

      kernel.Append("int index = get_global_id(0);");
      kernel.Append("float2 x = in_x[index];");
      kernel.Append("float2 c = in_c[index];");

      for (int i = 0; i < LayerData.Count; i++)
      {
        kernel.Append("struct ProcessLayer p"+i+";");
        kernel.Append("p"+i+".c_active = 1;");
        kernel.Append("p"+i+".c_isin = 0;");
        kernel.Append("p"+i+".c_x = x;");
        kernel.Append("p"+i+".c_oldx = x;");
        kernel.Append("p"+i+".c_old2x = x;");
        kernel.Append("p"+i+".c_calc = 0;");
        kernel.Append("p"+i+".c_cmean = 0;");
        kernel.Append("p"+i+".c_cvarsx = 0;");
        kernel.Append("p"+i+".c_cvariance = 0;");
      }

      kernel.Append("struct ProcessLayer* p = 0;");

      if (hastriangle)
      {
        if (fractaltype == FractalType.FRACTAL_TYPE_MANDEL)
        {
          kernel.Append("float trinorm = ABSC(c);");
          // trinorm = c.Magnitude;
        }
        else
        {
          kernel.Append("float trinorm = NORM(c);");
          // trinorm = c.Norm;
        }
      }

      kernel.Append("while (!end) {");
      // while (!end)
      kernel.Append("n++;");
      // n++;

      switch (fractaltype)
      {
        case FractalType.FRACTAL_TYPE_MANDEL:
          kernel.Append("newx = (float2)(x.x*x.x - x.y*x.y,2*x.x*x.y) + c;");
          //kernel.Append(@"printf(""%f %f - "",newx.x,newx.y);");
          //double sx = x.Real;
          //double sy = x.Imaginary;
          //return new Complex(sx * sx - sy * sy + c.Real, 2 * sx * sy + c.Imaginary);
          break;
        case FractalType.FRACTAL_TYPE_MANDEL_N:
          kernel.Append("newx = powc(x,pr) + c;");
          // return Complex.Pow(x, param) + c;
          break;
        case FractalType.FRACTAL_TYPE_BURNINGSHIP:
          kernel.Append("newx = (float2)(x.x*x.x-x.y*x.y,2*ABS(x.x*x.y)) + c;");
          //  double sx = x.Real;
          //  double sy = x.Imaginary;
          //  return new Complex(sx * sx - sy * sy + c.Real, 2 * absval(sx * sy) + c.Imaginary);
          break;
        case FractalType.FRACTAL_TYPE_BURNINGSHIP_N:
          kernel.Append("newx = powc((ABS(x.x),ABS(x.y)),pr) + c;");
          // return Complex.Pow(new Complex(absval(x.Real), absval(x.Imaginary)), n) + c;
          break;
        case FractalType.FRACTAL_TYPE_DIVERGENT:
          kernel.Append("newx = " + code + ";");
          //  newx = code.eval(x, c, n, param);
          break;
        case FractalType.FRACTAL_TYPE_CONVERGENT:
          kernel.Append("newx = " + code + ";");
          fractdiv = false;
          break;
        default:
          throw new NotSupportedException("Unknown FractalType");
      }
      if (modesused.HasFlag(SeqType.MPL_SEQ_SUM))
      {
        kernel.Append("sumx += newx;");
        //sumx+=newx;
      }
      if (modesused.HasFlag(SeqType.MPL_SEQ_MEAN))
      {
        kernel.Append("deltax = newx-meanx;");
        kernel.Append("meanx += deltax/(float)n;");
        /*Complex delta = newx-meanx;
        meanx = meanx+delta/(double)n;*/
        if (modesused.HasFlag(SeqType.MPL_SEQ_VARSX))
        {
          kernel.Append("varsx += MULC(deltax,(newx-meanx));");
          //varsx = varsx + delta*(newx-meanx);
          if (modesused.HasFlag(SeqType.MPL_SEQ_VARIANCE))
          {
            kernel.Append("if (n!=1) {");
            // if (n!=1) {
            kernel.Append("variacex = varsx / (float)((float)n-(float)1.0f);");
            //variacex = varsx/((double)n-(double)1);
            if (modesused.HasFlag(SeqType.MPL_SEQ_STDDEV))
            {
              kernel.Append("sdx = powc(variacex,0.5f);");
              //sdx = Complex.Sqrt(variacex);
            }
            kernel.Append("}");
          }
        }
      }
      if (modesused.HasFlag(SeqType.MPL_SEQ_MIN))
      {
        kernel.Append("if (n==1) minx = newx; else {");
        kernel.Append("if (NORM(newx)<NORM(minx)) { minx = newx; } }");
        //if (n==1) minx=newx; else if (Complex.Abs(newx)<Complex.Abs(minx)) minx=newx;
      }
      if (modesused.HasFlag(SeqType.MPL_SEQ_MAX))
      {
        kernel.Append("if (n==1) maxx = newx; else {");
        kernel.Append("if (NORM(newx)>NORM(maxx)) { maxx = newx; } }");
        //if (n==1) maxx=newx; else if (Complex.Abs(newx)>Complex.Abs(maxx)) maxx=newx;
      }
      if (modesused.HasFlag(SeqType.MPL_SEQ_DELTA))
      {
        kernel.Append("deltax = newx - x");
        //deltax = newx-x;
      }

      for (int i=0; i< LayerData.Count; i++) 
      {
        var p = LayerData[i];
        kernel.Append("p = &p"+i+";");
        kernel.Append("if (p->c_active) {");
        //if (p.c_active) {
        kernel.Append("p->c_n = n;");
        //p.c_n = n;
        kernel.Append("p->c_old2x = p->c_oldx;");
        kernel.Append("p->c_oldx = p->c_x;");
        //p.c_old2x = p.c_oldx;
        //p.c_oldx = p.c_x;
        switch (p.c_seqtype)
        {
          case SeqType.MPL_SEQ_NORMAL: kernel.Append("p->c_x = newx;"); break; // p.c_x = newx; break;
          case SeqType.MPL_SEQ_SUM: kernel.Append("p->c_x = sumx;"); break; //  p.c_x = sumx; break;
          case SeqType.MPL_SEQ_MEAN: kernel.Append("p->c_x = meanx;"); break;// p.c_x = meanx; break;          
          case SeqType.MPL_SEQ_VARSX: kernel.Append("p->c_x = varsx;"); break;
          case SeqType.MPL_SEQ_VARIANCE: kernel.Append("p->c_x = variacex;"); break; // p.c_x = variacex; break;
          case SeqType.MPL_SEQ_STDDEV: kernel.Append("p->c_x = sdx;"); break; // p.c_x = sdx; break;
          case SeqType.MPL_SEQ_MIN: kernel.Append("p->c_x = minx;"); break; //  p.c_x = minx; break;
          case SeqType.MPL_SEQ_MAX: kernel.Append("p->c_x = maxx;"); break; //  p.c_x = maxx; break;
          case SeqType.MPL_SEQ_DELTA: kernel.Append("p->c_x = deltax;"); break; //  p.c_x = deltax; break;
          default: kernel.Append("p->c_x = newx;"); break; // p.c_x = newx; break;
        }
        kernel.Append("newd = 0;");
        //double newd = 0;

        switch (p.c_checktype)
        {
          case SeqCheck.MPL_CHECK_SMOOTH:
            if (fractdiv)
            {
              kernel.Append("newd = exp(-ABSC(p->c_x));");
              //newd = Math.Exp(-Complex.Abs(p.c_x));
            }
            else
            {
              kernel.Append("newd = exp(-ABSC(p->c_x-p->c_oldx));");
              //newd = Math.Exp(-Complex.Abs(p.c_x-p.c_oldx));
            }
            break;
          case SeqCheck.MPL_CHECK_REAL:
            kernel.Append("newd = p->c_x.x;");
            //newd = p.c_x.Real;
            break;
          case SeqCheck.MPL_CHECK_IMAG:
            kernel.Append("newd = p->c_x.y;");
            //newd = p.c_x.Imaginary;
            break;
          case SeqCheck.MPL_CHECK_ARG:
            kernel.Append("newd = atan2(p->c_x.y,p->c_x.x);");
            //newd = p.c_x.Phase;
            break;
          case SeqCheck.MPL_CHECK_ABS:
            kernel.Append("newd = ABSC(p->c_x);");
            //newd = p.c_x.Magnitude;
            break;
          case SeqCheck.MPL_CHECK_CURVATURE:
            kernel.Append("if (isnotequal(p.c_oldx,p.c_old2x)) { newd = ABSC(atanc(DIVC(p->c_x-p->c_oldx,p->c_oldx-p->c_old2x))); } else newd = 0;");
            //if ((p.c_oldx!=p.c_old2x)) newd=Complex.Abs(Complex.Atan((p.c_x-p.c_oldx) / (p.c_oldx-p.c_old2x))); else newd=0; }
            break;
          case SeqCheck.MPL_CHECK_TRIANGLE:
            if (fractaltype == FractalType.FRACTAL_TYPE_MANDEL)
            {
              kernel.Append("newxnorm = NORM(p->c_oldx);");
              //double newxnorm = p.c_oldx.Norm();     
              kernel.Append("lowbound = ABS(newxnorm-trinorm);");
              //double lowbound = absval(newxnorm-trinorm);
              kernel.Append("if ((newxnorm+trinorm-lowbound)==0) newd = 0; else newd = (ABSC(p->c_x)-lowbound)/(newxnorm+trinorm-lowbound);");
              //if ((newxnorm+trinorm-lowbound)==0) newd=0; else
              //  newd = (p.c_x.Magnitude-lowbound)/(newxnorm+trinorm-lowbound);
            }
            else
            {
              kernel.Append("newxnorm = ABSC(p->c_x);");
              //double newxnorm = p.c_x.Magnitude;
              kernel.Append("lowbound = ABS(newxnorm-trinorm);");
              //double lowbound = absval(newxnorm-trinorm);
              kernel.Append("if ((newxnorm+trinorm-lowbound)==0) newd = 0; else newd = (ABSC(p->c_x-c)-lowbound)/(newxnorm+trinorm-lowbound);");
              //if ((newxnorm+trinorm-lowbound)==0) newd=0; else
              //  newd = ((Complex.Abs(p.c_x-c)-lowbound)/(newxnorm+trinorm-lowbound));
            }
            break;
          case SeqCheck.MPL_CHECK_TRIANGLE_SMOOTH:
            if (fractaltype == FractalType.FRACTAL_TYPE_MANDEL)
            {
              kernel.Append("newxnorm = NORM(p->c_oldx);");
              //double newxnorm = p.c_oldx.Norm();     
              kernel.Append("lowbound = ABS(newxnorm-trinorm);");
              //double lowbound = absval(newxnorm-trinorm);
              kernel.Append("if ((newxnorm+trinorm-lowbound)==0) newd = 0; else newd = (ABSC(p->c_x)-lowbound)/(newxnorm+trinorm-lowbound);");
              //if ((newxnorm+trinorm-lowbound)==0) newd=0; else
              //  newd = (p.c_x.Magnitude-lowbound)/(newxnorm+trinorm-lowbound);
            }
            else
            {
              kernel.Append("newxnorm = ABSC(p->c_x);");
              //double newxnorm = p.c_x.Magnitude;
              kernel.Append("lowbound = ABS(newxnorm-trinorm);");
              //double lowbound = absval(newxnorm-trinorm);
              kernel.Append("if ((newxnorm+trinorm-lowbound)==0) newd = 0; else newd = (ABSC(p->c_x-c)-lowbound)/(newxnorm+trinorm-lowbound);");
              //if ((newxnorm+trinorm-lowbound)==0) newd=0; else
              //  newd = ((Complex.Abs(p.c_x-c)-lowbound)/(newxnorm+trinorm-lowbound));
            }
            break;
          case SeqCheck.MPL_CHECK_ORBIT_TRAP:
            switch (p.c_orbittraptype)
            {
              case OrbitTrap.MPL_ORBIT_TRAP_POINT:
                kernel.Append("newd = ABSC(p->c_x - p->c_pointA);");
                //newd = Complex.Abs(p.c_x - p.c_pointA);
                break;
              case OrbitTrap.MPL_ORBIT_TRAP_LINE:
                if ((p.c_pointA.Real) == 1)
                {
                  kernel.Append("newd = ABS(p->c_x.x);");
                  //newd = Math.Abs(p.c_x.Real);
                }
                else
                {
                  kernel.Append("newd = ABS(p->c_x.y);");
                  //newd = Math.Abs(p.c_x.Imaginary);
                }
                break;
              case OrbitTrap.MPL_ORBIT_TRAP_GAUSS:
                {
                  kernel.Append("newd = ABSC((round(p->c_x.x),round(p->c_x.y)) - p->c_x);");
                  //Complex gauss = new Complex(Math.Round(p.c_x.Real),Math.Round(p.c_x.Imaginary));
                  //newd = Complex.Abs(gauss - p.c_x);
                }
                break;
            }
            break;
        }
        switch (p.c_checkseqtype)
        {
          case SeqType.MPL_SEQ_NORMAL: kernel.Append("p->c_calc = newd;"); break;
          case SeqType.MPL_SEQ_SUM: kernel.Append("p->c_calc += newd;"); break; // p.c_calc += newd; break;
          case SeqType.MPL_SEQ_MEAN: kernel.Append("p->c_calc += newd;"); break; // p.c_calc += newd; break;
          case SeqType.MPL_SEQ_VARSX:
            {
              kernel.Append("delta = newd - p->c_cmean;");
              //double delta = newd - p.c_cmean;
              kernel.Append("p->c_cmean = p->c_cmean + delta / p->c_n;");
              //p.c_cmean = p.c_cmean+delta/p.c_n;
              kernel.Append("p->c_calc += delta * (newd - p->c_cmean);");
              //p.c_calc += delta*(newd-p.c_cmean);
            }
            break;
          case SeqType.MPL_SEQ_VARIANCE:
            {
              kernel.Append("delta = newd - p->c_cmean;");
              //double delta = newd - p.c_cmean;
              kernel.Append("p->c_cmean = p->c_cmean + delta / p->c_n;");
              //p.c_cmean = p.c_cmean+delta/p.c_n;
              kernel.Append("p->c_cvarsx += delta * (newd - p->c_cmean);");
              //p.c_cvarsx = p.c_cvarsx + delta*(newd-p.c_cmean);
              kernel.Append("if (p->c_n!=1) { p->c_calc = p->c_cvarsx/(p->c_n-1.0f); }");
              /*if (p.c_n!=1) {
                p.c_calc = p.c_cvarsx/(p.c_n-1.0);
              }*/
            }
            break;
          case SeqType.MPL_SEQ_STDDEV:
            {
              kernel.Append("delta = newd - p->c_cmean;");
              //double delta = newd - p.c_cmean;
              kernel.Append("p->c_cmean = p->c_cmean + delta / p->c_n;");
              //p.c_cmean = p.c_cmean+delta/p.c_n;
              kernel.Append("p->c_cvarsx += delta * (newd - p->c_cmean);");
              //p.c_cvarsx = p.c_cvarsx + delta*(newd-p.c_cmean);
              kernel.Append("if (p->c_n!=1) { p->c_cvariance = p->c_cvarsx/((float)p->c_n-1.0f);");
              /*if (p.c_n!=1) {
                p.c_cvariance = p.c_cvarsx/(p.c_n-1.0);
              }*/
              kernel.Append("p->c_calc = sqrt(p->c_cvariance);");
              //p.c_calc = Math.Sqrt(p.c_cvariance);
              kernel.Append("}");
            }
            break;
          case SeqType.MPL_SEQ_MIN:
            kernel.Append("if (p->c_n==1) p->c_calc = newd; else if (p->c_calc>newd) { p->c_calc = newd; p->c_resx = p->c_x; p->c_resn = p->c_n; };");
            //if (p.c_n==1) p.c_calc=newd; else if (p.c_calc>newd) { p.c_calc = newd; p.c_resx = p.c_x; p.c_resn = p.c_n; } 
            break;
          case SeqType.MPL_SEQ_MAX:
            kernel.Append("if (p->c_n==1) p->c_calc = newd; else if (p->c_calc<newd) { p->c_calc = newd; p->c_resx = p->c_x; p->c_resn = p->c_n; };");
            // if (p.c_n==1) p.c_calc=newd; else if (p.c_calc<newd) { p.c_calc = newd; p.c_resx = p.c_x; p.c_resn = p.c_n; }
            break;
          case SeqType.MPL_SEQ_DELTA:
            kernel.Append("p->c_calc = newd-p->c_calc;");
            //p.c_calc = newd-p.c_calc; 
            break;
          default:
            kernel.Append("p->c_calc = newd;");
            //p.c_calc = newd; 
            break;
        }

        if (p.c_convchktype == ConvCheck.MPL_CONVCHK_REAL)
        {
          kernel.AppendFormat(CultureInfo.InvariantCulture,"if (p->c_x.x*p->c_x.x " + (fractdiv ? ">" : "<") + " {0:E}f) p->c_active = 0;", p.c_bailout);
          /*double ddd = p.c_x.Real*p.c_x.Real;
          if ((fractdiv) && ( ddd>p.c_bailout)) p.c_active = false;
          if (!(fractdiv) && ( ddd<p.c_bailout)) p.c_active = false;*/
        }
        else if (p.c_convchktype == ConvCheck.MPL_CONVCHK_IMAG)
        {
          kernel.AppendFormat(CultureInfo.InvariantCulture, "if (p->c_x.y*p->c_x.y " + (fractdiv ? ">" : "<") + " {0:E}f) p->c_active = 0;", p.c_bailout);
          /*double ddd = p.c_x.Imaginary*p.c_x.Imaginary;
          if ((fractdiv) && ( ddd>p.c_bailout)) p.c_active = false;
          if (!(fractdiv) && ( ddd<p.c_bailout)) p.c_active = false;*/
        }
        else if (p.c_convchktype == ConvCheck.MPL_CONVCHK_OR)
        {
          kernel.AppendFormat(CultureInfo.InvariantCulture, "if ((p->c_x.y*p->c_x.y " + (fractdiv ? ">" : "<") + " {0:E}f) || (p->c_x.x*p->c_x.x " + (fractdiv ? ">" : "<") + " {0:E}f)) p->c_active = 0;", p.c_bailout);
          /*if ((fractdiv) && ((p.c_x.Real*p.c_x.Real>p.c_bailout) || (p.c_x.Imaginary*p.c_x.Imaginary>p.c_bailout))) p.c_active = false;
          if (!(fractdiv) && ((p.c_x.Real*p.c_x.Real<p.c_bailout) || (p.c_x.Imaginary*p.c_x.Imaginary<p.c_bailout))) p.c_active = false;*/
        }
        else if (p.c_convchktype == ConvCheck.MPL_CONVCHK_AND)
        {
          kernel.AppendFormat(CultureInfo.InvariantCulture, "if ((p->c_x.y*p->c_x.y " + (fractdiv ? ">" : "<") + " {0:E}f) && (p->c_x.x*p->c_x.x " + (fractdiv ? ">" : "<") + " {0:E}f)) p->c_active = 0;", p.c_bailout);
          /*if ((fractdiv) && ((p.c_x.Real*p.c_x.Real>p.c_bailout) && (p.c_x.Imaginary*p.c_x.Imaginary>p.c_bailout))) p.c_active = false;
          if (!(fractdiv) && ((p.c_x.Real*p.c_x.Real<p.c_bailout) && (p.c_x.Imaginary*p.c_x.Imaginary<p.c_bailout))) p.c_active = false;*/
        }
        else if (p.c_convchktype == ConvCheck.MPL_CONVCHK_MANH)
        {
          kernel.AppendFormat(CultureInfo.InvariantCulture, "if ( ((ABS(p->c_x.y)+ABS(p->c_x.x))*((ABS(p->c_x.y)+ABS(p->c_x.x))) " + (fractdiv ? ">" : "<") + " {0:G}f)) p->c_active = 0;", p.c_bailout);
          /*double ddd = (absval(p.c_x.Imaginary)+absval(p.c_x.Real))*(absval(p.c_x.Imaginary)+absval(p.c_x.Real));
           if ((fractdiv) && ( ddd>p.c_bailout)) p.c_active = false;
          if (!(fractdiv) && ( ddd<p.c_bailout)) p.c_active = false;*/
        }
        else if (p.c_convchktype == ConvCheck.MPL_CONVCHK_MANR)
        {
          kernel.AppendFormat(CultureInfo.InvariantCulture, "if ( ((p->c_x.y+p->c_x.x)*(p->c_x.y+p->c_x.x)) " + (fractdiv ? ">" : "<") + " {0:E}f)) p->c_active = 0;", p.c_bailout);
          /*double ddd = (p.c_x.Real+p.c_x.Imaginary)*(p.c_x.Real+p.c_x.Imaginary);
           if ((fractdiv) && ( ddd>p.c_bailout)) p.c_active = false;
          if (!(fractdiv) && ( ddd<p.c_bailout)) p.c_active = false; */
        }
        else
        {
          kernel.AppendFormat(CultureInfo.InvariantCulture, "if (NORM(p->c_x) " + (fractdiv ? ">" : "<") + " {0:E}f) p->c_active = 0;", p.c_bailout);
          /*double ddd = p.c_x.Norm();
           if ((fractdiv) && ( ddd>p.c_bailout)) p.c_active = false;
          if (!(fractdiv) && ( ddd<p.c_bailout)) p.c_active = false;*/
        }
        kernel.AppendFormat(CultureInfo.InvariantCulture, "if (p->c_n>{0}) {{ p->c_active = 0; p->c_isin = 1; }}", p.c_nlimit);
        //if (p.c_n>p.c_nlimit) { p.c_active = false; p.c_isin = true; }
        if (p.c_checktype == SeqCheck.MPL_CHECK_TRIANGLE_SMOOTH)
        {
          throw new NotImplementedException("Smooth triangle algorithm is unavailable in this CalculatorFactory");
          /*if (p.c_active == false) 
            if (!p.c_isin) {
              p.c_oldx = p.c_x;
              p.c_x = Fractal_Mandel(p.c_x,c);
              p.c_n++;
              double newxnorm = p.c_oldx.Norm();
              double lowbound = absval(newxnorm-trinorm);
              if ((newxnorm+trinorm-lowbound)==0) newd=0; else
                newd = (p.c_x.Magnitude-lowbound)/(newxnorm+trinorm-lowbound);
              p.c_calc += newd;
              double oldsum = p.c_calc/(p.c_n+1);
              double il2=1/Math.Log(2);
              double lp=Math.Log(Math.Log(p.c_bailout));
              double f=il2*lp-il2*Math.Log(Math.Log(Complex.Abs(p.c_x)))+2;
              double az2 = p.c_x.Norm();
              p.c_oldx = p.c_x;
              p.c_x = Fractal_Mandel(p.c_oldx,c);
              lowbound = absval(az2-trinorm);
              if ((az2+trinorm-lowbound)!=0) p.c_calc+=(Complex.Abs(p.c_x)-lowbound)/(az2+trinorm-lowbound);
              p.c_n++;
              p.c_calc = p.c_calc/(p.c_n+1);
              p.c_calc = oldsum+(p.c_calc-oldsum)*(f-1);
            } else {
              p.c_calc /= p.c_n+1;
            }*/
        }
        else if (p.c_checkseqtype == SeqType.MPL_SEQ_MEAN)
        {
          kernel.Append("if (p->c_active == 0) p->c_calc /= (float)p->c_n+1.0f;");
          //if (p.c_active == false) p.c_calc /= p.c_n+1;
        }
        if (p == deflayer)
        {
          kernel.Append("if (p->c_active == 0) end = 1;");
          /*if (!deflayer.c_active) end = true; */
        }
        kernel.Append("}");
        
      }
      kernel.Append("x = newx; }");
      for (int i = 0; i < LayerData.Count; i++)
      {
        kernel.Append("out_p"+i+"[index] = p"+i+";");
        //kernel.Append("out_p" + i + "[index].c_calc = 52.0f;");
      }
      kernel.Append("}");

      //System.Console.WriteLine(kernel.Replace(";", ";\n").Replace("}","}\n"));
      //kernel.Clear();
      //kernel.Append(@"kernel void VectorAdd(global  read_only float* a,global  read_only float* b,global write_only float* c ){int index = get_global_id(0);c[index] = a[index] + b[index];}");
      
      ComputeProgram prg = new ComputeProgram(_context, kernel.Replace(";", ";\n").Replace("}","}\n").ToString());
      try
      {
        prg.Build(null, null, null, IntPtr.Zero);
      }
      catch (ComputeException e)
      {
        throw new Exception("Error while building: " + prg.GetBuildLog(_context.Devices[0]), e);
      }
      ComputeKernel krnl = prg.CreateKernel("FractalCalc");

      return new OpenCLCalculator(_context,prg,krnl);
    }
  }
}
