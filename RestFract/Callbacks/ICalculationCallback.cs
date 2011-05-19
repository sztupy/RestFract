using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;

namespace RestFract.Callbacks
{
  public interface ICalculationCallback
  {
    void SetPoint(int x, int y);
    void SetLine(int y);
  }
}
