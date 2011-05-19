using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RestFract.Color
{
  public enum GradientType
  {
    GRADIENT_MAP_RGB = 0, // RGB kodok szerinti gradiens
    GRADIENT_MAP_HSV = 1, // HSV kodok szerinti gradiens, szinek szerint novekvo sorrendben
    GRADIENT_MAP_HSVBACK = 2, // HSV kodok szerinti gradiens, szinek szerint csokeno sorrendben
    GRADIENT_MAP_HSVROT = 3 // HSV kodok szerinti gradiens, szinek szerint ciklikus modban
  }

  public interface IGradientMap
  {
    ColorValue getPoint(double pos, bool cyclic = false);
  }
}
