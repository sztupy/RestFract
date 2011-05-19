using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;

namespace RestFract
{
  [Flags]
  public enum ConvCheck
  {
    MPL_CONVCHK_NORMAL = 1, // x^2 + y^2 > limit
    MPL_CONVCHK_REAL = 2, // x^2 > limit
    MPL_CONVCHK_IMAG = 4, // y^2 > limit
    MPL_CONVCHK_OR = 8, // x^2 > limit vagy y^2 > limit
    MPL_CONVCHK_AND = 16, // x^2 > limit es y^2 > limit
    MPL_CONVCHK_MANH = 32, // (|x|+|y|)^2 > limit
    MPL_CONVCHK_MANR = 64, // (x+y)^2 > limit
    MPL_CONVCHK_EXPR = 128
  }

  [Flags]
  public enum SeqType
  {
    MPL_SEQ_NORMAL = 0, // x_n
    MPL_SEQ_SUM = 1, // \sum(x_n)
    MPL_SEQ_MEAN = 2, // x_n-ek atlaga
    MPL_SEQ_VARSX = 4, // x_n-ek szórásnégyzetosszege
    MPL_SEQ_VARIANCE = 8, // x_n-ek szórásnégyzete
    MPL_SEQ_STDDEV = 16, // x_n-ek szórása
    MPL_SEQ_MIN = 32, // min{x_n}
    MPL_SEQ_MAX = 64, // max{x_n}
    MPL_SEQ_DELTA = 128, // x_n - x_{n-1}
    MPL_SEQ_EXPR = 256 // egyeb kifejezes
  }

  [Flags]
  public enum SeqCheck
  {
    // mihez nezzuk az adatokat
    MPL_CHECK_NORMAL = 1, // csak az iteraciot es az eredmenyt nezzuk
    MPL_CHECK_SMOOTH = 2, // iteracio elmosva
    MPL_CHECK_TRIANGLE = 4, // haromszog egyenlotlenseg
    MPL_CHECK_TRIANGLE_SMOOTH = 8, // haromszog egyenlotlenseg elmosva
    MPL_CHECK_ORBIT_TRAP = 16, // orbit trap
    MPL_CHECK_REAL = 32,
    MPL_CHECK_IMAG = 64,
    MPL_CHECK_ARG = 128,
    MPL_CHECK_ABS = 256,
    MPL_CHECK_CURVATURE = 512
  }

  [Flags]
  public enum OrbitTrap
  {
    // orbit trap tipusok
    MPL_ORBIT_TRAP_POINT = 1,
    MPL_ORBIT_TRAP_LINE = 2,
    MPL_ORBIT_TRAP_GAUSS = 4
  }

  [Serializable]
  public class ProcessLayer
  {  
    public ProcessLayer()
    {
      c_bailout = 0;
      c_convchktype = ConvCheck.MPL_CONVCHK_NORMAL;
      c_nlimit = 0;
      c_active = false;
      c_default = false;
      c_isin = false;
      c_seqtype = SeqType.MPL_SEQ_NORMAL;
      c_x = 0;
      c_checktype = SeqCheck.MPL_CHECK_NORMAL;
      c_checkseqtype = SeqType.MPL_SEQ_NORMAL;
      c_n = 0;
      c_calc = 0;
      c_resx = 0;
      c_orbittraptype = OrbitTrap.MPL_ORBIT_TRAP_POINT;
      c_pointA = 0;
      c_pointB = 0;
    }


    public ProcessLayer(double bailout, ConvCheck convchktype, int nlimit, SeqType seqtype, SeqCheck checktype, SeqType checkseqtype, Complex pointA = default(Complex), Complex pointB = default(Complex), OrbitTrap orbittraptype = OrbitTrap.MPL_ORBIT_TRAP_POINT)
    {
      c_bailout = bailout;
      c_convchktype = convchktype;
      c_nlimit = nlimit;
      c_active = false;
      c_default = false;
      c_isin = false;
      c_seqtype = seqtype;
      c_x = 0;
      c_checktype = checktype;
      c_checkseqtype = checkseqtype;
      c_n = 0;
      c_calc = 0;
      c_resx = 0;
      c_orbittraptype = orbittraptype;
      c_pointA = pointA;
      c_pointB = pointB;
    }

    public void LoadFrom(ProcessLayer pl)
    {
      if (pl == this) return;
      this.c_active = pl.c_active;
      this.c_bailout = pl.c_bailout;
      this.c_calc = pl.c_calc;
      this.c_checkseqtype = pl.c_checkseqtype;
      this.c_checktype = pl.c_checktype;
      this.c_cmean = pl.c_cmean;
      this.c_convchktype = pl.c_convchktype;
      this.c_cvariance = pl.c_cvariance;
      this.c_cvarsx = pl.c_cvarsx;
      this.c_default = pl.c_default;
      this.c_isin = pl.c_isin;
      this.c_n = pl.c_n;
      this.c_nlimit = pl.c_nlimit;
      this.c_old2x = pl.c_old2x;
      this.c_oldx = pl.c_oldx;
      this.c_orbittraptype = pl.c_orbittraptype;
      this.c_pointA = pl.c_pointA;
      this.c_pointB = pl.c_pointB;
      this.c_resn = pl.c_resn;
      this.c_resx = pl.c_resx;
      this.c_seqtype = pl.c_seqtype;
      this.c_x = pl.c_x;
    }

    public ProcessLayer Clone()
    {
      return (ProcessLayer)MemberwiseClone();
    }

    public bool Similar(ProcessLayer p)
    {
      return (p.c_bailout == c_bailout) &&
             (p.c_convchktype == c_convchktype) &&
             (p.c_nlimit == c_nlimit) &&
             (p.c_seqtype == c_seqtype) &&
             (p.c_checktype == c_checktype) &&
             (p.c_checkseqtype == c_checkseqtype) &&
             (p.c_orbittraptype == c_orbittraptype) &&
             (p.c_pointA == c_pointA) &&
             (p.c_pointB == c_pointB);
    }

    // meddig nezzuk a sorozatot
    public double c_bailout; // hatarertek
    public ConvCheck c_convchktype; // konvergenciaellenorzes tipusa, lasd MPL_CONVCHK_* konstansok
    public int c_nlimit; // maximalis n ertek
    public bool c_active; // aktiv-e meg ez a szamolas
    public bool c_default; // ez-e a fractalmeghatarozo reteg
    public bool c_isin; // halmazban vagyunk-e

    // milyen sorozatot nezunk  
    public SeqType c_seqtype; // x_n tipusa, lasd MPL_SEQ_* konstansok
    public Complex c_old2x; // aktualis ertek
    public Complex c_oldx; // aktualis ertek
    public Complex c_x; // aktualis ertek

    // kiszamolando ertekek
    public SeqCheck c_checktype; // mit nezunk, lasd MPL_CHECK_* konstansok
    public SeqType c_checkseqtype; // mi erdekel minket az eredmeny sorozatbol, lasd MPL_SEQ_* konstansok
    public int c_n; // aktualis n erteke
    public double c_calc; // kiszamitott ertek (SMOOTH, TRIANGLE, TRIANGLE_SMOOTH eseteben a szamolt ertekek, ORBIT_TRAP eseteben a tavolsag a csapdatol)
    public double c_cmean, c_cvarsx, c_cvariance;
    public Complex c_resx; // hol ertuk el a megfelelo helyet
    public int c_resn; // hanyadik lepesben ertuk el a megfelelo helyets

    //orbit trap adatok
    public OrbitTrap c_orbittraptype; // milyen csapdat hasznalunk, lasd MPL_ORBIT_TRAP_* konstansok
    public Complex c_pointA; // orbit_trap A pont
    public Complex c_pointB; // orbit_trap B pont  
  }
}