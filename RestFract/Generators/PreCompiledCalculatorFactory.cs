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
  public class PreCompiledCalculatorFactory : ICalculatorFactory
  {
    public interface IPreCompiledCalculator {
      void CalcFractal(Complex x, Complex c, List<ProcessLayer> ld, double pr);
    }
                             
    private class PreCompiledCalculator : ICalculator
    {
      private IPreCompiledCalculator _pcr;
      private List<ProcessLayer> _ld;
      private double _param;
      private Queue<Tuple<int,int,Complex,Complex>> _l;

      public PreCompiledCalculator(IPreCompiledCalculator pcr)
      {
        _pcr = pcr;
      }

      public void InitData(List<ProcessLayer> LayerData, double param, long count)
      {
        _ld = LayerData;
        _param = param;
        _l = new Queue<Tuple<int,int,Complex,Complex>>();
      }

      public void AddPoint(int px, int py, Complex x, Complex c)
      {
        _l.Enqueue(Tuple.Create(px,py,x,c));
      }

      public bool GetPoint(out int px, out int py, out List<ProcessLayer> LayerData)
      {
        if (_l.Count == 0) { px = 0; py = 0; LayerData = null; return false; }
        var t = _l.Dequeue();
        px = t.Item1;
        py = t.Item2;
        Complex x = t.Item3;
        Complex c = t.Item4;
        foreach (var it in _ld)
        {
          it.c_active = true;
          it.c_isin = false;
          it.c_x = x;
          it.c_oldx = x;
          it.c_old2x = x;
          it.c_calc = 0;
          it.c_cmean = 0;
          it.c_cvarsx = 0;
          it.c_cvariance = 0;
        }
        LayerData = _ld;

        _pcr.CalcFractal(x, c, LayerData, _param);
        return true;
      }

      public void EndSend()
      {
      }

      public void EndGet(bool final)
      {
      }
    }

    public ICalculator GenFractalCalc(List<ProcessLayer> LayerData, FractalType fractaltype, string code, ProcessLayer deflayer)
    {
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

      CodeNamespace ns = new CodeNamespace();
      CodeTypeDeclaration cl = new CodeTypeDeclaration("FractRunner");
      cl.IsClass = true;
      cl.BaseTypes.Add(typeof(IPreCompiledCalculator));
      cl.TypeAttributes = System.Reflection.TypeAttributes.Public;
      ns.Types.Add(cl);

      CodeMemberMethod calc = new CodeMemberMethod();
      calc.Attributes = MemberAttributes.Public;
      calc.ReturnType = new CodeTypeReference(typeof(void));
      calc.Name = "CalcFractal";
      calc.Parameters.Add(new CodeParameterDeclarationExpression(typeof(Complex), "x"));
      calc.Parameters.Add(new CodeParameterDeclarationExpression(typeof(Complex), "c"));
      calc.Parameters.Add(new CodeParameterDeclarationExpression(typeof(List<ProcessLayer>), "ld"));
      calc.Parameters.Add(new CodeParameterDeclarationExpression(typeof(double), "pr"));
      cl.Members.Add(calc);


      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(Complex), "sumx", new CodePrimitiveExpression(0)));
      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(Complex), "meanx", new CodePrimitiveExpression(0)));
      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(Complex), "varsx", new CodePrimitiveExpression(0)));
      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(Complex), "variacex", new CodePrimitiveExpression(0)));
      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(Complex), "sdx", new CodePrimitiveExpression(0)));
      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(Complex), "minx", new CodePrimitiveExpression(0)));
      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(Complex), "maxx", new CodePrimitiveExpression(0)));
      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(Complex), "deltax", new CodePrimitiveExpression(0)));

      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(Complex), "deltac", new CodePrimitiveExpression(0)));

      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(double), "delta", new CodePrimitiveExpression(0)));
      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(double), "newxnorm", new CodePrimitiveExpression(0)));
      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(double), "lowbound", new CodePrimitiveExpression(0)));
      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(double), "newd", new CodePrimitiveExpression(0)));

      if (hastriangle)
      {
        if (fractaltype == FractalType.FRACTAL_TYPE_MANDEL)
        {
          // trinorm = c.Magnitude;
          calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(double), "trinorm",
              new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("c"), "Magnitude")
          ));
        }
        else
        {
          // trinorm = c.Norm;
          calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(double), "trinorm",
            new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("c"), "Real"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("c"), "Real")), CodeBinaryOperatorType.Add, new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("c"), "Imaginary"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("c"), "Imaginary")))
          ));
        }
      }

      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(bool), "end", new CodePrimitiveExpression(false)));
      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(int), "n", new CodePrimitiveExpression(0)));
      calc.Statements.Add(new CodeVariableDeclarationStatement(typeof(Complex), "newx", new CodeArgumentReferenceExpression("x")));

      // while (!end)
      CodeIterationStatement itr = new CodeIterationStatement(new CodeSnippetStatement(), new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("end"), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(false)), new CodeSnippetStatement());
      calc.Statements.Add(itr);

      // n++;
      itr.Statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("n"), new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("n"), CodeBinaryOperatorType.Add, new CodePrimitiveExpression(1))));

      switch (fractaltype)
      {
        case FractalType.FRACTAL_TYPE_MANDEL:
          itr.Statements.Add(
            new CodeAssignStatement(
              new CodeVariableReferenceExpression("newx"),
              new CodeObjectCreateExpression(typeof(Complex),
                new CodeBinaryOperatorExpression(
                  new CodeBinaryOperatorExpression(
                    new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("x"), "Real"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("x"), "Real")),
                    CodeBinaryOperatorType.Subtract,
                    new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("x"), "Imaginary"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("x"), "Imaginary"))),
                  CodeBinaryOperatorType.Add,
                  new CodePropertyReferenceExpression(new CodeArgumentReferenceExpression("c"), "Real")
                ),
                new CodeBinaryOperatorExpression(
                  new CodeBinaryOperatorExpression(
                    new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("x"), "Real"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("x"), "Imaginary")),
                    CodeBinaryOperatorType.Multiply,
                    new CodePrimitiveExpression(2)
                    ),
                  CodeBinaryOperatorType.Add,
                  new CodePropertyReferenceExpression(new CodeArgumentReferenceExpression("c"), "Imaginary")
                  )
              )));
          //double sx = x.Real;
          //double sy = x.Imaginary;
          //return new Complex(sx * sx - sy * sy + c.Real, 2 * sx * sy + c.Imaginary);
          break;
        case FractalType.FRACTAL_TYPE_MANDEL_N:
          itr.Statements.Add(new CodeAssignStatement(
              new CodeVariableReferenceExpression("newx"),
              new CodeBinaryOperatorExpression(
                new CodeMethodInvokeExpression(
                  new CodeTypeReferenceExpression(typeof(Complex)),
                  "Pow",
                  new CodeArgumentReferenceExpression("x"),
                  new CodeArgumentReferenceExpression("pr")
                ),
                CodeBinaryOperatorType.Add,
                new CodeArgumentReferenceExpression("c")
                )
              ));
          // return Complex.Pow(x, param) + c;
          break;
        case FractalType.FRACTAL_TYPE_BURNINGSHIP:
          itr.Statements.Add(
            new CodeAssignStatement(
              new CodeVariableReferenceExpression("newx"),
              new CodeObjectCreateExpression(typeof(Complex),
                new CodeBinaryOperatorExpression(
                  new CodeBinaryOperatorExpression(
                    new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("x"), "Real"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("x"), "Real")),
                    CodeBinaryOperatorType.Subtract,
                    new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("x"), "Imaginary"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("x"), "Imaginary"))),
                  CodeBinaryOperatorType.Add,
                  new CodePropertyReferenceExpression(new CodeArgumentReferenceExpression("c"), "Real")
                ),
                new CodeBinaryOperatorExpression(
                  new CodeBinaryOperatorExpression(
                    new CodeMethodInvokeExpression(
                      new CodeTypeReferenceExpression(typeof(Math)),
                      "Abs",
                      new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("x"), "Real"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("x"), "Imaginary"))
                    ),
                    CodeBinaryOperatorType.Multiply,
                    new CodePrimitiveExpression(2)
                    ),
                  CodeBinaryOperatorType.Add,
                  new CodePropertyReferenceExpression(new CodeArgumentReferenceExpression("c"), "Imaginary")
                  )
              )));
          //  double sx = x.Real;
          //  double sy = x.Imaginary;
          //  return new Complex(sx * sx - sy * sy + c.Real, 2 * absval(sx * sy) + c.Imaginary);
          break;
        case FractalType.FRACTAL_TYPE_BURNINGSHIP_N:
          itr.Statements.Add(new CodeAssignStatement(
              new CodeVariableReferenceExpression("newx"),
              new CodeBinaryOperatorExpression(
                new CodeMethodInvokeExpression(
                  new CodeTypeReferenceExpression(typeof(Complex)),
                  "Pow",
                  new CodeObjectCreateExpression(typeof(Complex),
                    new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Abs", new CodePropertyReferenceExpression(new CodeArgumentReferenceExpression("x"), "Real")),
                    new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Abs", new CodePropertyReferenceExpression(new CodeArgumentReferenceExpression("x"), "Imaginary"))
                  ),
                  new CodeArgumentReferenceExpression("pr")
                ),
                CodeBinaryOperatorType.Add,
                new CodeArgumentReferenceExpression("c")
                )
              ));
          // return Complex.Pow(new Complex(absval(x.Real), absval(x.Imaginary)), n) + c;
          break;
        case FractalType.FRACTAL_TYPE_DIVERGENT:
          itr.Statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newx"), new CodeSnippetExpression(code)));
          //  newx = code.eval(x, c, n, param);
          break;
        case FractalType.FRACTAL_TYPE_CONVERGENT:
          fractdiv = false;
          itr.Statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newx"), new CodeSnippetExpression(code)));
          break;
        default:
          throw new NotSupportedException("Unknown FractalType");
      }
      if (modesused.HasFlag(SeqType.MPL_SEQ_SUM))
      {
        itr.Statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("sumx"), new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("sumx"), CodeBinaryOperatorType.Add, new CodeVariableReferenceExpression("newx"))));
        //sumx+=newx;
      }
      if (modesused.HasFlag(SeqType.MPL_SEQ_MEAN))
      {
        itr.Statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("deltac"), new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newx"), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("meanx"))));
        itr.Statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("meanx"), new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("meanx"), CodeBinaryOperatorType.Add, new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("deltac"), CodeBinaryOperatorType.Divide, new CodeCastExpression(typeof(double), new CodeVariableReferenceExpression("n"))))));
        /*Complex delta = newx-meanx;
        meanx = meanx+delta/(double)n;*/
        if (modesused.HasFlag(SeqType.MPL_SEQ_VARSX))
        {
          itr.Statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("varsx"), new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("varsx"), CodeBinaryOperatorType.Add, new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("deltac"), CodeBinaryOperatorType.Multiply, new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newx"), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("meanx"))))));
          //varsx = varsx + delta*(newx-meanx);
          if (modesused.HasFlag(SeqType.MPL_SEQ_VARIANCE))
          {
            CodeConditionStatement ifst = new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("n"), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(1)));
            // if (n!=1) {
            ifst.FalseStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("variacex"), new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("varsx"), CodeBinaryOperatorType.Divide, new CodeBinaryOperatorExpression(new CodeCastExpression(typeof(double), new CodeVariableReferenceExpression("n")), CodeBinaryOperatorType.Subtract, new CodePrimitiveExpression(1.0)))));
            //variacex = varsx/((double)n-(double)1);
            if (modesused.HasFlag(SeqType.MPL_SEQ_STDDEV))
            {
              ifst.FalseStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("sdx"), new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Complex)), "Sqrt", new CodeVariableReferenceExpression("variacex"))));
              //sdx = Complex.Sqrt(variacex);
            }
            itr.Statements.Add(ifst);
          }
        }
      }
      if (modesused.HasFlag(SeqType.MPL_SEQ_MIN))
      {
        CodeConditionStatement ifst = new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("n"), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(1)));
        ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("minx"), new CodeVariableReferenceExpression("newx")));
        ifst.FalseStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Complex)), "Abs", new CodeVariableReferenceExpression("newx")), CodeBinaryOperatorType.LessThan, new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Complex)), "Abs", new CodeVariableReferenceExpression("minx"))), new CodeAssignStatement(new CodeVariableReferenceExpression("minx"), new CodeVariableReferenceExpression("newx"))));
        itr.Statements.Add(ifst);
        //if (n==1) minx=newx; else if (Complex.Abs(newx)<Complex.Abs(minx)) minx=newx;
      }
      if (modesused.HasFlag(SeqType.MPL_SEQ_MAX))
      {
        CodeConditionStatement ifst = new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("n"), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(1)));
        ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("maxx"), new CodeVariableReferenceExpression("newx")));
        ifst.FalseStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Complex)), "Abs", new CodeVariableReferenceExpression("newx")), CodeBinaryOperatorType.GreaterThan, new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Complex)), "Abs", new CodeVariableReferenceExpression("maxx"))), new CodeAssignStatement(new CodeVariableReferenceExpression("maxx"), new CodeVariableReferenceExpression("newx"))));
        itr.Statements.Add(ifst);
        //if (n==1) maxx=newx; else if (Complex.Abs(newx)>Complex.Abs(maxx)) maxx=newx;
      }
      if (modesused.HasFlag(SeqType.MPL_SEQ_DELTA))
      {
        itr.Statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("deltax"), new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newx"), CodeBinaryOperatorType.Subtract, new CodeArgumentReferenceExpression("x"))));
        //deltax = newx-x;
      }
      itr.Statements.Add(new CodeVariableDeclarationStatement(typeof(IEnumerator<ProcessLayer>), "pc", new CodeMethodInvokeExpression(new CodeArgumentReferenceExpression("ld"), "GetEnumerator")));
      itr.Statements.Add(new CodeVariableDeclarationStatement(typeof(ProcessLayer), "p"));
      foreach (var p in LayerData)
      {
        itr.Statements.Add(new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("pc"), "MoveNext"));
        itr.Statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("p"), new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("pc"), "Current")));
        CodeConditionStatement ifst = new CodeConditionStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_active"));
        //if (p.c_active) {
        ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_n"), new CodeVariableReferenceExpression("n")));
        //p.c_n = n;
        ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_old2x"), new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_oldx")));
        ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_oldx"), new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x")));
        //p.c_old2x = p.c_oldx;
        //p.c_oldx = p.c_x;
        switch (p.c_seqtype)
        {
          case SeqType.MPL_SEQ_NORMAL: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), new CodeVariableReferenceExpression("newx"))); break; // p.c_x = newx; break;
          case SeqType.MPL_SEQ_SUM: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), new CodeVariableReferenceExpression("sumx"))); break; //  p.c_x = sumx; break;
          case SeqType.MPL_SEQ_MEAN: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), new CodeVariableReferenceExpression("meanx"))); break; // p.c_x = meanx; break;          
          case SeqType.MPL_SEQ_VARSX: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), new CodeVariableReferenceExpression("varsx"))); break; // p.c_x = variacex; break;
          case SeqType.MPL_SEQ_VARIANCE: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), new CodeVariableReferenceExpression("variacex"))); break; // p.c_x = variacex; break;
          case SeqType.MPL_SEQ_STDDEV: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), new CodeVariableReferenceExpression("sdx"))); break; // p.c_x = sdx; break;
          case SeqType.MPL_SEQ_MIN: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), new CodeVariableReferenceExpression("minx"))); break; //  p.c_x = minx; break;
          case SeqType.MPL_SEQ_MAX: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), new CodeVariableReferenceExpression("maxx"))); break; //  p.c_x = maxx; break;
          case SeqType.MPL_SEQ_DELTA: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), new CodeVariableReferenceExpression("deltax"))); break; //  p.c_x = deltax; break;
          default: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), new CodeVariableReferenceExpression("newx"))); break; // p.c_x = newx; break;
        }
        ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodePrimitiveExpression(0)));
        //double newd = 0;

        switch (p.c_checktype)
        {
          case SeqCheck.MPL_CHECK_SMOOTH:
            if (fractdiv)
            {
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Exp", new CodeBinaryOperatorExpression(new CodePrimitiveExpression(0), CodeBinaryOperatorType.Subtract, new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Complex)), "Abs", new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"))))));
              //newd = Math.Exp(-Complex.Abs(p.c_x));
            }
            else
            {
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Exp", new CodeBinaryOperatorExpression(new CodePrimitiveExpression(0), CodeBinaryOperatorType.Subtract, new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Complex)), "Abs", new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), CodeBinaryOperatorType.Subtract, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_oldx")))))));
              //newd = Math.Exp(-Complex.Abs(p.c_x-p.c_oldx));
            }
            break;
          case SeqCheck.MPL_CHECK_REAL:
            ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Real")));
            //newd = p.c_x.Real;
            break;
          case SeqCheck.MPL_CHECK_IMAG:
            ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Imaginary")));
            //newd = p.c_x.Imaginary;
            break;
          case SeqCheck.MPL_CHECK_ARG:
            ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Phase")));
            //newd = p.c_x.Phase;
            break;
          case SeqCheck.MPL_CHECK_ABS:
            ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Magnitude")));
            //newd = p.c_x.Magnitude;
            break;
          case SeqCheck.MPL_CHECK_CURVATURE:
            ifst.TrueStatements.Add(new CodeConditionStatement(
              new CodeBinaryOperatorExpression(
                new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_oldx"),
                CodeBinaryOperatorType.ValueEquality,
                new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_old2x")),
                new CodeStatement[] { new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodePrimitiveExpression(0)) },
                new CodeStatement[] {
                          new CodeAssignStatement(new CodeVariableReferenceExpression("newd"),
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Complex)),"Abs",
                              new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Complex)),"Atan",
                              new CodeBinaryOperatorExpression(
                                new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x"),
                                  CodeBinaryOperatorType.Subtract,
                                  new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_oldx")),
                                  CodeBinaryOperatorType.Divide,
                                  new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_oldx"),
                                    CodeBinaryOperatorType.Subtract,
                                    new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_old2x"))
                              )))
                            )
                        }));
            //if ((p.c_oldx!=p.c_old2x)) newd=Complex.Abs(Complex.Atan((p.c_x-p.c_oldx) / (p.c_oldx-p.c_old2x))); else newd=0; }
            break;
          case SeqCheck.MPL_CHECK_TRIANGLE:
            if (fractaltype == FractalType.FRACTAL_TYPE_MANDEL)
            {
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newxnorm"), new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_oldx"), "Real"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_oldx"), "Real")), CodeBinaryOperatorType.Add, new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_oldx"), "Imaginary"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_oldx"), "Imaginary")))));
              //double newxnorm = p.c_oldx.Norm();                      
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("lowbound"), new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Abs", new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newxnorm"), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("trinorm")))));
              //double lowbound = absval(newxnorm-trinorm);
              ifst.TrueStatements.Add(
                new CodeConditionStatement(
                  new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newxnorm"), CodeBinaryOperatorType.Add, new CodeVariableReferenceExpression("trinorm")), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("lowbound")), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(0)),
                  new CodeStatement[] { new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodePrimitiveExpression(0)) },
                  new CodeStatement[] { new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Magnitude"), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("lowbound")), CodeBinaryOperatorType.Divide, new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newxnorm"), CodeBinaryOperatorType.Add, new CodeVariableReferenceExpression("trinorm")), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("lowbound")))) }
                ));
              //if ((newxnorm+trinorm-lowbound)==0) newd=0; else
              //  newd = (p.c_x.Magnitude-lowbound)/(newxnorm+trinorm-lowbound);
            }
            else
            {
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newxnorm"), new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Magnitude")));
              //double newxnorm = p.c_x.Magnitude;
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("lowbound"), new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Abs", new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newxnorm"), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("trinorm")))));
              //double lowbound = absval(newxnorm-trinorm);
              ifst.TrueStatements.Add(
                new CodeConditionStatement(
                  new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newxnorm"), CodeBinaryOperatorType.Add, new CodeVariableReferenceExpression("trinorm")), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("lowbound")), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(0)),
                  new CodeStatement[] { new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodePrimitiveExpression(0)) },
                  new CodeStatement[] { new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("c")), "Magnitude"), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("lowbound")), CodeBinaryOperatorType.Divide, new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newxnorm"), CodeBinaryOperatorType.Add, new CodeVariableReferenceExpression("trinorm")), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("lowbound")))) }
                ));
              //if ((newxnorm+trinorm-lowbound)==0) newd=0; else
              //  newd = ((Complex.Abs(p.c_x-c)-lowbound)/(newxnorm+trinorm-lowbound));
            }
            break;
          case SeqCheck.MPL_CHECK_TRIANGLE_SMOOTH:
            if (fractaltype == FractalType.FRACTAL_TYPE_MANDEL)
            {
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newxnorm"), new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_oldx"), "Real"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_oldx"), "Real")), CodeBinaryOperatorType.Add, new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_oldx"), "Imaginary"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_oldx"), "Imaginary")))));
              //double newxnorm = p.c_oldx.Norm();                      
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("lowbound"), new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Abs", new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newxnorm"), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("trinorm")))));
              //double lowbound = absval(newxnorm-trinorm);
              ifst.TrueStatements.Add(
                new CodeConditionStatement(
                  new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newxnorm"), CodeBinaryOperatorType.Add, new CodeVariableReferenceExpression("trinorm")), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("lowbound")), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(0)),
                  new CodeStatement[] { new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodePrimitiveExpression(0)) },
                  new CodeStatement[] { new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Magnitude"), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("lowbound")), CodeBinaryOperatorType.Divide, new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newxnorm"), CodeBinaryOperatorType.Add, new CodeVariableReferenceExpression("trinorm")), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("lowbound")))) }
                ));
              //if ((newxnorm+trinorm-lowbound)==0) newd=0; else
              //  newd = (p.c_x.Magnitude-lowbound)/(newxnorm+trinorm-lowbound);
            }
            else
            {
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newxnorm"), new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Magnitude")));
              //double newxnorm = p.c_x.Magnitude;
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("lowbound"), new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Abs", new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newxnorm"), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("trinorm")))));
              //double lowbound = absval(newxnorm-trinorm);
              ifst.TrueStatements.Add(
                new CodeConditionStatement(
                  new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newxnorm"), CodeBinaryOperatorType.Add, new CodeVariableReferenceExpression("trinorm")), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("lowbound")), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(0)),
                  new CodeStatement[] { new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodePrimitiveExpression(0)) },
                  new CodeStatement[] { new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("c")), "Magnitude"), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("lowbound")), CodeBinaryOperatorType.Divide, new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newxnorm"), CodeBinaryOperatorType.Add, new CodeVariableReferenceExpression("trinorm")), CodeBinaryOperatorType.Subtract, new CodeVariableReferenceExpression("lowbound")))) }
                ));
              //if ((newxnorm+trinorm-lowbound)==0) newd=0; else
              //  newd = ((Complex.Abs(p.c_x-c)-lowbound)/(newxnorm+trinorm-lowbound));
            }
            break;
          case SeqCheck.MPL_CHECK_ORBIT_TRAP:
            switch (p.c_orbittraptype)
            {
              case OrbitTrap.MPL_ORBIT_TRAP_POINT:
                ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Complex)), "Abs", new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), CodeBinaryOperatorType.Subtract, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_pointA")))));
                //newd = Complex.Abs(p.c_x - p.c_pointA);
                break;
              case OrbitTrap.MPL_ORBIT_TRAP_LINE:
                if ((p.c_pointA.Real) == 1)
                {
                  ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Abs", new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Real"))));
                  //newd = Math.Abs(p.c_x.Real);
                }
                else
                {
                  ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Abs", new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Imaginary"))));
                  //newd = Math.Abs(p.c_x.Imaginary);
                }
                break;
              case OrbitTrap.MPL_ORBIT_TRAP_GAUSS:
                {
                  ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("newd"), new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Complex)),"Abs", new CodeBinaryOperatorExpression(new CodeObjectCreateExpression(typeof(Complex),new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Round", new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Real")),new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Round", new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Imaginary"))), CodeBinaryOperatorType.Subtract,new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x")))));
                  //Complex gauss = new Complex(Math.Round(p.c_x.Real),Math.Round(p.c_x.Imaginary));
                  //newd = Complex.Abs(gauss - p.c_x);
                }
                break;
            }
            break;
        }
        switch (p.c_checkseqtype) {
          case SeqType.MPL_SEQ_NORMAL: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_calc"),new CodeVariableReferenceExpression("newd"))); break;
          case SeqType.MPL_SEQ_SUM: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_calc"), new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_calc"),CodeBinaryOperatorType.Add, new CodeVariableReferenceExpression("newd")))); break; // p.c_calc += newd; break;
          case SeqType.MPL_SEQ_MEAN: ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_calc"), new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_calc"), CodeBinaryOperatorType.Add, new CodeVariableReferenceExpression("newd")))); break; // p.c_calc += newd; break;
          case SeqType.MPL_SEQ_VARSX: {
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("delta"),new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newd"), CodeBinaryOperatorType.Subtract, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_cmean"))));
              //double delta = newd - p.c_cmean;
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_cmean"),new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_cmean"),CodeBinaryOperatorType.Add,new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("delta"),CodeBinaryOperatorType.Divide,new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_n")))));
              //p.c_cmean = p.c_cmean+delta/p.c_n;
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_calc"),new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_calc"),CodeBinaryOperatorType.Add,new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("delta"),CodeBinaryOperatorType.Multiply,new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newd"),CodeBinaryOperatorType.Subtract,new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_cmean"))))));
              //p.c_calc += delta*(newd-p.c_cmean);
            }
            break;
          case SeqType.MPL_SEQ_VARIANCE: {
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("delta"),new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newd"), CodeBinaryOperatorType.Subtract, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_cmean"))));
              //double delta = newd - p.c_cmean;
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_cmean"),new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_cmean"),CodeBinaryOperatorType.Add,new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("delta"),CodeBinaryOperatorType.Divide,new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_n")))));
              //p.c_cmean = p.c_cmean+delta/p.c_n;
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_cvarsx"),new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_cvarsx"),CodeBinaryOperatorType.Add,new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("delta"),CodeBinaryOperatorType.Multiply,new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newd"),CodeBinaryOperatorType.Subtract,new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_cmean"))))));
              //p.c_cvarsx = p.c_cvarsx + delta*(newd-p.c_cmean);
              ifst.TrueStatements.Add(new CodeConditionStatement(
                  new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_n"), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(1)),
                  new CodeStatement[] { },
                  new CodeStatement[] { new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_calc"),new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_cvarsx"),CodeBinaryOperatorType.Divide,new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_n"),CodeBinaryOperatorType.Subtract,new CodePrimitiveExpression(1.0))))
                  }));
              /*if (p.c_n!=1) {
                p.c_calc = p.c_cvarsx/(p.c_n-1.0);
              }*/
            }
             break;
          case SeqType.MPL_SEQ_STDDEV: {
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("delta"), new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newd"), CodeBinaryOperatorType.Subtract, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_cmean"))));
              //double delta = newd - p.c_cmean;
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_cmean"), new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_cmean"), CodeBinaryOperatorType.Add, new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("delta"), CodeBinaryOperatorType.Divide, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_n")))));
              //p.c_cmean = p.c_cmean+delta/p.c_n;
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_cvarsx"), new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_cvarsx"), CodeBinaryOperatorType.Add, new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("delta"), CodeBinaryOperatorType.Multiply, new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newd"), CodeBinaryOperatorType.Subtract, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_cmean"))))));
              //p.c_cvarsx = p.c_cvarsx + delta*(newd-p.c_cmean);
              ifst.TrueStatements.Add(new CodeConditionStatement(
                  new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_n"), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(1)),
                  new CodeStatement[] { },
                  new CodeStatement[] { new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_cvariance"),new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_cvarsx"),CodeBinaryOperatorType.Divide,new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_n"),CodeBinaryOperatorType.Subtract,new CodePrimitiveExpression(1.0))))
                  }));
              /*if (p.c_n!=1) {
                p.c_cvariance = p.c_cvarsx/(p.c_n-1.0);
              }*/
              ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_calc"),new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)), "Sqrt",new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_cvariance"))));
              //p.c_calc = Math.Sqrt(p.c_cvariance);
            }
            break;
          case SeqType.MPL_SEQ_MIN:
            ifst.TrueStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_n"), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(1)),
              new CodeStatement[] { new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_calc"), new CodeVariableReferenceExpression("newd")) },
              new CodeStatement[] { new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_calc"),CodeBinaryOperatorType.GreaterThan,new CodeVariableReferenceExpression("newd")),
                new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_calc"),new CodeVariableReferenceExpression("newd")),
                new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_resx"),new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x")),
                new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_resn"),new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_n")))
              }));
            //if (p.c_n==1) p.c_calc=newd; else if (p.c_calc>newd) { p.c_calc = newd; p.c_resx = p.c_x; p.c_resn = p.c_n; } 
          break;
          case SeqType.MPL_SEQ_MAX:
            ifst.TrueStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_n"), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(1)),
              new CodeStatement[] { new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_calc"), new CodeVariableReferenceExpression("newd")) },
              new CodeStatement[] { new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_calc"),CodeBinaryOperatorType.LessThan,new CodeVariableReferenceExpression("newd")),
                new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_calc"),new CodeVariableReferenceExpression("newd")),
                new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_resx"),new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x")),
                new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_resn"),new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_n")))
              }));
            // if (p.c_n==1) p.c_calc=newd; else if (p.c_calc<newd) { p.c_calc = newd; p.c_resx = p.c_x; p.c_resn = p.c_n; }
          break;
          case SeqType.MPL_SEQ_DELTA:
            ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_calc"),new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("newd"), CodeBinaryOperatorType.Subtract,new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_calc"))));
            //p.c_calc = newd-p.c_calc; 
          break;
          default:
            ifst.TrueStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_calc"), new CodeVariableReferenceExpression("newd")));
            //p.c_calc = newd; 
          break;
        }

        if (p.c_convchktype==ConvCheck.MPL_CONVCHK_REAL) {
            ifst.TrueStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x"),"Real"),CodeBinaryOperatorType.Multiply,new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x"),"Real")),fractdiv ? CodeBinaryOperatorType.GreaterThan : CodeBinaryOperatorType.LessThan,new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_bailout")),new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_active"),new CodePrimitiveExpression(false))));
           /*double ddd = p.c_x.Real*p.c_x.Real;
           if ((fractdiv) && ( ddd>p.c_bailout)) p.c_active = false;
           if (!(fractdiv) && ( ddd<p.c_bailout)) p.c_active = false;*/
        } else if (p.c_convchktype==ConvCheck.MPL_CONVCHK_IMAG) {
           ifst.TrueStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Imaginary"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Imaginary")), fractdiv ? CodeBinaryOperatorType.GreaterThan : CodeBinaryOperatorType.LessThan, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_bailout")), new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_active"), new CodePrimitiveExpression(false))));
           /*double ddd = p.c_x.Imaginary*p.c_x.Imaginary;
           if ((fractdiv) && ( ddd>p.c_bailout)) p.c_active = false;
           if (!(fractdiv) && ( ddd<p.c_bailout)) p.c_active = false;*/
        } else if (p.c_convchktype==ConvCheck.MPL_CONVCHK_OR) {
          ifst.TrueStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Imaginary"), CodeBinaryOperatorType.Multiply,new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Imaginary")),CodeBinaryOperatorType.BooleanOr,new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Real"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Real"))), fractdiv ? CodeBinaryOperatorType.GreaterThan : CodeBinaryOperatorType.LessThan, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_bailout")), new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_active"), new CodePrimitiveExpression(false))));
          /*if ((fractdiv) && ((p.c_x.Real*p.c_x.Real>p.c_bailout) || (p.c_x.Imaginary*p.c_x.Imaginary>p.c_bailout))) p.c_active = false;
          if (!(fractdiv) && ((p.c_x.Real*p.c_x.Real<p.c_bailout) || (p.c_x.Imaginary*p.c_x.Imaginary<p.c_bailout))) p.c_active = false;*/
        } else if (p.c_convchktype==ConvCheck.MPL_CONVCHK_AND) {
          ifst.TrueStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Imaginary"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Imaginary")), CodeBinaryOperatorType.BooleanAnd, new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Real"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Real"))), fractdiv ? CodeBinaryOperatorType.GreaterThan : CodeBinaryOperatorType.LessThan, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_bailout")), new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_active"), new CodePrimitiveExpression(false))));
          /*if ((fractdiv) && ((p.c_x.Real*p.c_x.Real>p.c_bailout) && (p.c_x.Imaginary*p.c_x.Imaginary>p.c_bailout))) p.c_active = false;
          if (!(fractdiv) && ((p.c_x.Real*p.c_x.Real<p.c_bailout) && (p.c_x.Imaginary*p.c_x.Imaginary<p.c_bailout))) p.c_active = false;*/
        } else if (p.c_convchktype==ConvCheck.MPL_CONVCHK_MANH) {
          ifst.TrueStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)),"Abs",new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x"),"Imaginary")),CodeBinaryOperatorType.Add,new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)),"Abs",new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x"),"Real"))),CodeBinaryOperatorType.Multiply,new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)),"Abs",new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x"),"Imaginary")),CodeBinaryOperatorType.Add,new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Math)),"Abs",new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x"),"Real")))),fractdiv ? CodeBinaryOperatorType.GreaterThan : CodeBinaryOperatorType.LessThan, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_bailout")), new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_active"), new CodePrimitiveExpression(false))));
          /*double ddd = (absval(p.c_x.Imaginary)+absval(p.c_x.Real))*(absval(p.c_x.Imaginary)+absval(p.c_x.Real));
           if ((fractdiv) && ( ddd>p.c_bailout)) p.c_active = false;
          if (!(fractdiv) && ( ddd<p.c_bailout)) p.c_active = false;*/
        } else if (p.c_convchktype==ConvCheck.MPL_CONVCHK_MANR) {
          ifst.TrueStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x"),"Real"),CodeBinaryOperatorType.Add,new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x"),"Imaginary")),CodeBinaryOperatorType.Multiply,new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x"),"Real"),CodeBinaryOperatorType.Add,new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_x"),"Imaginary"))),fractdiv ? CodeBinaryOperatorType.GreaterThan : CodeBinaryOperatorType.LessThan, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_bailout")), new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_active"), new CodePrimitiveExpression(false))));
          /*double ddd = (p.c_x.Real+p.c_x.Imaginary)*(p.c_x.Real+p.c_x.Imaginary);
           if ((fractdiv) && ( ddd>p.c_bailout)) p.c_active = false;
          if (!(fractdiv) && ( ddd<p.c_bailout)) p.c_active = false; */
        } else {
          ifst.TrueStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Real"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Real")), CodeBinaryOperatorType.Add, new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Imaginary"), CodeBinaryOperatorType.Multiply, new CodePropertyReferenceExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_x"), "Imaginary"))), fractdiv ? CodeBinaryOperatorType.GreaterThan : CodeBinaryOperatorType.LessThan, new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_bailout")), new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_active"), new CodePrimitiveExpression(false))));
          /*double ddd = p.c_x.Norm();
           if ((fractdiv) && ( ddd>p.c_bailout)) p.c_active = false;
          if (!(fractdiv) && ( ddd<p.c_bailout)) p.c_active = false;*/
        }
        ifst.TrueStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(
          new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_n"),CodeBinaryOperatorType.GreaterThan,new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_nlimit")),
          new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_active"),new CodePrimitiveExpression(false)),
          new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_isin"),new CodePrimitiveExpression(true))
        ));
        //if (p.c_n>p.c_nlimit) { p.c_active = false; p.c_isin = true; }
        if (p.c_checktype == SeqCheck.MPL_CHECK_TRIANGLE_SMOOTH) {
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
          ifst.TrueStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_active"),CodeBinaryOperatorType.ValueEquality,new CodePrimitiveExpression(false)),new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_calc"),new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_calc"),CodeBinaryOperatorType.Divide,new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"),"c_n"),CodeBinaryOperatorType.Add,new CodePrimitiveExpression(1))))));
          //if (p.c_active == false) p.c_calc /= p.c_n+1;
        }
        if (p == deflayer)
        {
          ifst.TrueStatements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("p"), "c_active"), CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(false)),new CodeAssignStatement(new CodeVariableReferenceExpression("end"), new CodePrimitiveExpression(true))));
          /*if (!deflayer.c_active) end = true; */
        }
        itr.Statements.Add(ifst);
      }
      itr.Statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("x"), new CodeVariableReferenceExpression("newx")));
      // x = newx;  

      CodeCompileUnit compileUnit = new CodeCompileUnit();
      compileUnit.Namespaces.Add(ns);
      compileUnit.ReferencedAssemblies.Add(typeof(ICalculator).Assembly.Location);
      compileUnit.ReferencedAssemblies.Add(typeof(Complex).Assembly.Location);

#if USE_CPP
      Microsoft.VisualC.CppCodeProvider10 cs = new Microsoft.VisualC.CppCodeProvider10();
      //CSharpCodeProvider cs = new CSharpCodeProvider(new Dictionary<string, string>(){ {"CompilerVersion", "v4.0"} });
      CompilerParameters opts = new CompilerParameters();
      opts.GenerateExecutable = false;
      opts.GenerateInMemory = true;
      opts.IncludeDebugInformation = false;
      using (Stream s = File.Open("test.cpp",FileMode.Create))
        using (StreamWriter sw = new StreamWriter(s)) 
          cs.GenerateCodeFromCompileUnit(compileUnit,sw,new CodeGeneratorOptions());

      var pr = System.Diagnostics.Process.Start(@"cmd", @"/c """"c:\Program Files (x86)\Microsoft Visual Studio 10.0\VC\bin\vcvars32.bat"" & ""c:\Program Files (x86)\Microsoft Visual Studio 10.0\VC\bin\cl.exe"""" test.cpp /CLR:PURE /O2 /LD /GS-");
      pr.WaitForExit();
      Assembly assembly = Assembly.Load(File.ReadAllBytes("test.dll"), null);
#else
      CSharpCodeProvider cs = new CSharpCodeProvider(new Dictionary<string, string>(){ {"CompilerVersion", "v4.0"} });
      CompilerParameters opts = new CompilerParameters();
      opts.GenerateExecutable = false;
      opts.GenerateInMemory = true;
      opts.IncludeDebugInformation = false;
      CompilerResults res =  cs.CompileAssemblyFromDom(opts, compileUnit);
      /*using (Stream s = File.Open("test.cs", FileMode.Create))
        using (StreamWriter sw = new StreamWriter(s))
          cs.GenerateCodeFromCompileUnit(compileUnit, sw, new CodeGeneratorOptions());*/
      foreach (CompilerError error in res.Errors)
      {
        if (!error.IsWarning) {
          throw new NotImplementedException(error.ErrorText + " " + error.Line.ToString());
        }
      }
      Assembly assembly = res.CompiledAssembly;
#endif
      
      return new PreCompiledCalculator((IPreCompiledCalculator)Activator.CreateInstance(assembly.GetType("FractRunner")));
    }
  }
}
