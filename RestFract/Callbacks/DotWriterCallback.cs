using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RestFract.Callbacks
{
  public class DotWriterCallback : ICalculationCallback
  {
    public void SetPoint(int x, int y)
    {
    }

    public void SetLine(int y)
    {
      System.Console.Write(".");
    }
  }
}
