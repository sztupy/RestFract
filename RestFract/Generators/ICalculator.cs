using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;

namespace RestFract.Generators
{
  public interface ICalculator
  {
    void InitData(List<ProcessLayer> LayerData, double param, long count);
    void AddPoint(int px, int py, Complex x, Complex c);
    bool GetPoint(out int px, out int py, out List<ProcessLayer> LayerData);
    void EndSend();
    void EndGet(bool final);
  }
}
