using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using RestFract.Color;

namespace RestFract.Output
{
  public interface IMandelOutput
  {
    void InitDraw();
    void EndDraw();
    void NextLine(int flags, int y);
    void PutPoint(int layernum, int flags, int x, int y, ColorValue c);
  }
}