using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RestFract.Generators
{
  public interface ICalculatorFactory
  {
    ICalculator GenFractalCalc(List<ProcessLayer> LayerData, FractalType fractaltype, string code, ProcessLayer deflayer);
  }
}
