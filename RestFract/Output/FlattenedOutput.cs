using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using RestFract.Color;

namespace RestFract.Output
{
  public abstract class FlattenedOutput : IMandelOutput
  {
    public virtual void InitDraw() { oldx = -1; oldy = -1; SetInitDraw(); }
    public virtual void EndDraw() { SetEndDraw(); }
    public virtual void NextLine(int flags, int y)
    {
      SetPoint(flags, oldx, oldy, old);
      oldx = -1;
      oldy = -1;
      SetNextLine(flags, y);
    }
    public virtual void PutPoint(int layernum, int flags, int x, int y, ColorValue c)
    {
      if (oldx == -1)
      {
        old = c;
        oldx = x;
        oldy = y;
      }
      else if ((oldx != x) || (oldy != y))
      {
        SetPoint(flags, oldx, oldy, old);
        old = c;
        oldx = x;
        oldy = y;
      }
      else
      {
        old.Blend(c);
      }
    }

    public abstract void SetNextLine(int flags, int y);
    public abstract void SetInitDraw();
    public abstract void SetEndDraw();
    public abstract void SetPoint(int flags, int x, int y, ColorValue c);

    private ColorValue old;
    private int oldx, oldy;
  }
}