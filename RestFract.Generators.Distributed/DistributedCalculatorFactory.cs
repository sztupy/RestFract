using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Net.Sockets;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Runtime.InteropServices;

namespace RestFract.Generators.Distributed
{
  public class DistributedCalculatorFactory : ICalculatorFactory, IDisposable
  {


    class DistributedCalculator : ICalculator
    {
      List<TcpClient> _clients;
      List<ProcessLayer> _LayerData;
      int _num;
      BinaryFormatter _bin;
      MemoryStream[] ms;
      int[] _count;
      long ccount;

      public DistributedCalculator(List<TcpClient> clients)
      {
        _num = 0;
        _clients = clients;
        _bin = new BinaryFormatter();
      }

      public void InitData(List<ProcessLayer> LayerData, double param, long count)
      {
        _LayerData = LayerData;
        ms = new MemoryStream[_clients.Count];
        _count = new int[_clients.Count];
        ccount = count;
        for(int i=0; i< _clients.Count; i++)
        {
          var s = _clients[i].GetStream();
          BinaryWriter bw = new BinaryWriter(s);
          bw.Write((int)0);
          _bin.Serialize(s, LayerData);
          _bin.Serialize(s, param);
          _bin.Serialize(s, count);
          ms[i] = new MemoryStream(((int)count/_clients.Count+1)*40);
          _count[i] = 0;
        }
        
      }

      public void AddPoint(int px, int py, Complex x, Complex c)
      {
        var s = ms[_num % _clients.Count];
        var bw = new BinaryWriter(s);
        bw.Write(px);
        bw.Write(py);
        bw.Write(x.Real);
        bw.Write(x.Imaginary);
        bw.Write(c.Real);
        bw.Write(c.Imaginary);
        _count[_num % _clients.Count]++;
      }

      public bool GetPoint(out int px, out int py, out List<ProcessLayer> LayerData)
      {
        for (int i=0; i<_clients.Count; i++)
        {
          if (ms[i].Length != ms[i].Position)
          {
            BinaryReader br = new BinaryReader(ms[i]);
              px = br.ReadInt32();
              py = br.ReadInt32();
              List<ProcessLayer> pl = new List<ProcessLayer>();
              foreach (var p in _LayerData) {
                ProcessLayer np = p.Clone();
                double r,im;
                r = br.ReadDouble(); im = br.ReadDouble(); np.c_old2x = new Complex(r,im);
                r = br.ReadDouble(); im = br.ReadDouble(); np.c_oldx = new Complex(r,im);
                r = br.ReadDouble(); im = br.ReadDouble(); np.c_x = new Complex(r,im);
                r = br.ReadDouble(); im = br.ReadDouble(); np.c_resx = new Complex(r,im);
                np.c_calc = br.ReadDouble();
                np.c_cmean = br.ReadDouble();
                np.c_cvarsx = br.ReadDouble();
                np.c_cvariance = br.ReadDouble();
                np.c_active = br.ReadInt32() != 0;
                np.c_isin = br.ReadInt32() != 0;
                np.c_n = br.ReadInt32();
                np.c_resn = br.ReadInt32();
                pl.Add(np);
              }
              LayerData = pl;
            return true;
          }
        }
        px = 0;
        py = 0;
        LayerData = null;
        return false;
      }

      public void EndSend()
      {
        for (int i=0; i<_clients.Count; i++)
        {
          var s = _clients[i].GetStream();
          byte[] buf = ms[i].GetBuffer();
          BinaryWriter bw = new BinaryWriter(s);
          bw.Write(3);
          bw.Write(_count[i]);
          s.Write(buf, 0, _count[i]*40);
          buf = new byte[_count[i] * (2 * sizeof(int) + _LayerData.Count * (4 * sizeof(int) + 12 * sizeof(double)))];
          ms[i] = new MemoryStream(buf);
          int pos = 0;
          while (pos != buf.Length)
          {
            pos += s.Read(buf, pos, buf.Length-pos);
          }
          _count[i] = 0;
        }
      }

      public void EndGet(bool final)
      {
        for (int i=0; i<_clients.Count; i++)
        {
          var s = _clients[i].GetStream();
          BinaryWriter bw = new BinaryWriter(s);
          bw.Write(final?(int)4:(int)5);
          ms[i] = new MemoryStream(((int)ccount / _clients.Count + 1) * 40);
        }
      }
    }

    private List<TcpClient> clients;

    public DistributedCalculatorFactory()
    {
      clients = new List<TcpClient>();
    }

    public void AddClient(string host, int port)
    {
      clients.Add(new TcpClient(host,port));
    }

    public ICalculator GenFractalCalc(List<ProcessLayer> LayerData, FractalType fractaltype, string code, ProcessLayer deflayer)
    {
      BinaryFormatter bin = new BinaryFormatter();
      foreach (var client in clients) {
        var s = client.GetStream();
        BinaryWriter bw = new BinaryWriter(s);
        bw.Write(1);
        bin.Serialize(s, LayerData);
        bin.Serialize(s, fractaltype);
        bin.Serialize(s, code);
        for (int i = 0; i < LayerData.Count; i++)
        {
          if (LayerData[i]==deflayer) bin.Serialize(s, i);
        }        
      }
      return new DistributedCalculator(clients);
    }

    public void Dispose()
    {
      if (clients != null)
      {
        foreach (var c in clients)
        {
          BinaryWriter bw = new BinaryWriter(c.GetStream());
          bw.Write(-1);
          c.Close();
        }
      }
      clients = null;
    }

    ~DistributedCalculatorFactory()
    {
      Dispose();
    }
  }
}
