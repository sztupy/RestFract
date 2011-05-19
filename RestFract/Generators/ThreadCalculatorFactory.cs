using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Threading;
using System.Collections.Concurrent;

namespace RestFract.Generators
{
  public class ThreadCalculatorFactory : ICalculatorFactory
  {
    class ThreadCalculator : ICalculator
    {
      ICalculator[] icalc;
      Thread[] threads;
      bool[] done;
      ConcurrentQueue<Tuple<int, int, Complex, Complex>> inputlist;
      ConcurrentQueue<Tuple<int,int,List<ProcessLayer>>> outputlist;
      
      public ThreadCalculator(ICalculator[] innercalc)
      {
        icalc = innercalc;
      }

      public void InitData(List<ProcessLayer> LayerData, double param, long count)
      {
        foreach (var i in icalc)
        {
          List<ProcessLayer> p = new List<ProcessLayer>();
          foreach (var pp in LayerData)
          {
            p.Add(pp.Clone());
          }
          i.InitData(p, param, count);
        }
        done = new bool[icalc.Length];
        inputlist = new ConcurrentQueue<Tuple<int, int, Complex, Complex>>();
        outputlist = new ConcurrentQueue<Tuple<int, int, List<ProcessLayer>>>();
        threads = new Thread[icalc.Length];
        for (int i = 0; i < threads.Length; i++)
        {
          threads[i] = new Thread(prnum => {
            while (true)
            {
              int ii = (int)prnum;
              Tuple<int,int,Complex,Complex> output;
              if (inputlist.TryDequeue(out output))
              {
                icalc[ii].AddPoint(output.Item1, output.Item2, output.Item3, output.Item4);
                icalc[ii].EndSend();
                int px,py;
                List<ProcessLayer> pl;
                if (icalc[ii].GetPoint(out px, out py, out pl))
                {
                  List<ProcessLayer> p = new List<ProcessLayer>();
                  foreach (var pp in pl)
                  {
                    p.Add(pp.Clone());
                  }
                  outputlist.Enqueue(Tuple.Create(px, py, p));
                }
              }
              else
              {
                Thread.Sleep(0);
              }
              if (done[ii]) break;
            }
          });
          threads[i].Start(i);
        }
      }

      public void AddPoint(int px, int py, Complex x, Complex c)
      {
        inputlist.Enqueue(Tuple.Create(px, py, x, c));
      }

      public bool GetPoint(out int px, out int py, out List<ProcessLayer> LayerData)
      {
        while(true) {
          Tuple<int, int, List<ProcessLayer>> output;
          if (outputlist.TryDequeue(out output))
          {
            px = output.Item1;
            py = output.Item2;
            LayerData = output.Item3;
            return true;
          }
          else
          {
            Thread.Sleep(0);
          }
        }
      }

      public void EndSend()
      {
        for (int i = 0; i < icalc.Length; i++)
        {
          done[i] = false;
        }
      }

      public void EndGet(bool final)
      {
        for (int i = 0; i < icalc.Length; i++)
        {
          done[i] = final;
        }
      }
    }

    ICalculatorFactory _factory;
    int _processes;
    
    public ThreadCalculatorFactory(int processes, ICalculatorFactory fr)
    {
      _factory = fr;
      _processes = processes;
    }
    
    public ICalculator GenFractalCalc(List<ProcessLayer> LayerData, FractalType fractaltype, string code, ProcessLayer deflayer)
    {
      List<ICalculator> c = new List<ICalculator>();
      for (int i = 0; i < _processes; i++)
      {
        c.Add(_factory.GenFractalCalc(LayerData, fractaltype, code, deflayer));
      }
      return new ThreadCalculator(c.ToArray());
    }
  }
}
